using System.Data;
using Api;
using Api.Accounts;
using Api.AuditLog;
using Api.Background;
using Api.DevSeed;
using Api.Email;
using Api.Http;
using Api.Realtime;
using Api.Sql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// --- Service Registrations ---

// Network
builder.Services.AddCors();
builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
});
builder
    .Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation();
        tracing.AddHttpClientInstrumentation();
    })
    .UseOtlpExporter();

// Database
builder.AddSqlServerClient(connectionName: "database");
builder.Services.AddScoped<IDbConnection>(sp => sp.GetRequiredService<SqlConnection>());

// Checklist: the use cases, and the adapter behind each of their ports
builder.Services.AddChecklist();

// Audit Log
builder.Services.AddScoped<AuditContext>();
builder.Services.AddSingleton<ChannelAuditWriter>();
builder.Services.AddSingleton<IAuditWriter>(sp => sp.GetRequiredService<ChannelAuditWriter>());
builder.Services.TryAddEnumerable(
    ServiceDescriptor.Singleton<IHostedService, ChannelAuditWriter>(sp =>
        sp.GetRequiredService<ChannelAuditWriter>()
    )
);

// Real-time notifications
builder
    .Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.PropertyNamingPolicy = System
            .Text
            .Json
            .JsonNamingPolicy
            .CamelCase;
    });

// The clock, for the use cases, the audit log and the cleanup job
builder.Services.AddSingleton(TimeProvider.System);

// Background job: deletes the lists whose auto-delete countdown has run out, once a minute
builder.Services.AddHostedService<ChecklistCleanupService>();

// Accounts: the tokens the API issues, and how often anyone may try to get one
builder
    .Services.AddOptions<AuthOptions>()
    .BindConfiguration(AuthOptions.Section)
    .Validate(
        options => options.HasUsableSigningKey(),
        $"Auth:SigningKey must be a base64-encoded secret of at least {AuthOptions.MinKeyBytes} bytes."
    )
    .ValidateOnStart();
builder.Services.AddAuthRateLimit(builder.Configuration);

// Email: password reset links, over SMTP (Mailpit in development)
builder
    .Services.AddOptions<MailOptions>()
    .BindConfiguration(MailOptions.Section)
    .Configure<IConfiguration>(
        (options, configuration) =>
            options.ClientUrl = configuration["App:ClientUrl"] ?? options.ClientUrl
    );

// Authentication & Authorization: the API accepts the tokens it issued itself, and no others
builder.Services.AddAuthentication().AddJwtBearer();
builder
    .Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<AuthOptions>>(
        (options, auth) =>
        {
            AuthOptions settings = auth.Value;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = settings.Issuer,
                ValidAudience = settings.Audience,
                IssuerSigningKey = settings.CreateSigningKey(),
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                // The API checks its own tokens, so clocks can't disagree by much.
                ClockSkew = TimeSpan.FromSeconds(30),
            };

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    if (
                        context.Request.Path.StartsWithSegments(
                            "/hubs",
                            StringComparison.OrdinalIgnoreCase
                        )
                        && context.Request.Query.TryGetValue(
                            "access_token",
                            out Microsoft.Extensions.Primitives.StringValues token
                        )
                    )
                    {
                        context.Token = token;
                    }
                    return Task.CompletedTask;
                },
                OnAuthenticationFailed = AuthenticationAudit.OnAuthenticationFailed,
            };
        }
    );
builder.Services.AddAuthorization();

// API Documentation
builder.Services.AddOpenApi(opt =>
{
    opt.AddDocumentTransformer(
        (document, context, ct) =>
        {
            document.Info.Title = "JustAnotherList API";
            document.Info.Version = "v1";
            document.Info.Description = """
                A collaborative checklist API.

                **Item groups** are shared lists that multiple users can collaborate on.
                **Items** are tasks within a group that can be marked as complete.
                **Members** are users who have access to an item group.
                **Accounts** are how people sign up and sign in, under `/api/auth`.

                All endpoints except the account ones require a valid Bearer token.
                """;
            return Task.CompletedTask;
        }
    );
    opt.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
});

WebApplication app = builder.Build();

// --- Middleware Pipeline ---

app.UseForwardedHeaders(
    new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    }
);
app.UseCors(policyBuilder =>
    policyBuilder
        .AllowAnyHeader()
        .AllowAnyMethod()
        .SetIsOriginAllowed(origin => true)
        .AllowCredentials()
);
if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection();
}
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapAccountApi();
app.MapChecklistApi();
app.MapHub<ChecklistHub>("/hubs/checklist");

if (app.Environment.IsDevelopment())
{
    app.MapDevSeedEndpoints();
}

app.MapOpenApi();
app.MapScalarApiReference(opt =>
{
    // Paste the accessToken that POST /api/auth/sign-in answers with.
    opt.AddPreferredSecuritySchemes("Bearer");
    opt.WithDocumentDownloadType(DocumentDownloadType.Both)
        .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.Curl);
});

// --- Database Initialization ---

if (!app.Environment.IsEnvironment("Testing"))
{
    await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
    SqlConnection connection = scope.ServiceProvider.GetRequiredService<SqlConnection>();
    await DatabaseInitializer.InitializeAsync(connection);
}

await app.RunAsync();
