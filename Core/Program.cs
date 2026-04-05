using System.Data;
using System.Diagnostics;
using Core;
using Core.AuditLog;
using Core.Utility;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection.Extensions;
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

// Audit Log
builder.Services.AddScoped<AuditContext>();
builder.Services.AddSingleton<ChannelAuditWriter>();
builder.Services.AddSingleton<IAuditWriter>(sp => sp.GetRequiredService<ChannelAuditWriter>());
builder.Services.TryAddEnumerable(
    ServiceDescriptor.Singleton<IHostedService, ChannelAuditWriter>(sp =>
        sp.GetRequiredService<ChannelAuditWriter>()
    )
);

// Authentication & Authorization
builder
    .Services.AddAuthentication()
    .AddJwtBearer(options =>
    {
        string? authority = builder.Configuration["OAuth:Authority"];
        if (!string.IsNullOrEmpty(authority))
        {
            options.Authority = authority;
            options.RequireHttpsMetadata = !authority.StartsWith(
                "http://",
                StringComparison.OrdinalIgnoreCase
            );
        }

        string? audience = builder.Configuration["OAuth:Audience"];
        if (!string.IsNullOrEmpty(audience))
        {
            options.Audience = audience;
        }
        else
        {
            // No audience configured — disable validation. Set OAuth:Audience in production.
            options.TokenValidationParameters.ValidateAudience = false;
        }

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                IAuditWriter writer =
                    context.HttpContext.RequestServices.GetRequiredService<IAuditWriter>();
                writer.Enqueue(
                    new AuditEntry(
                        Timestamp: DateTimeOffset.UtcNow,
                        TraceId: Activity.Current?.TraceId.ToString(),
                        UserId: null,
                        IpAddress: context.HttpContext.Connection.RemoteIpAddress?.ToString(),
                        ResourceType: null,
                        Operation: "AuthenticationFailed",
                        ResourceId: null,
                        SubResourceId: null,
                        TargetUserId: null,
                        Outcome: "AuthenticationFailed",
                        FailureReason: context.Exception.Message
                    )
                );
                return Task.CompletedTask;
            },
        };
    });
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

            All endpoints require a valid Bearer token.
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
app.UseAuthentication();
app.UseAuthorization();

app.MapChecklistApi();

app.MapOpenApi();
app.MapScalarApiReference(opt =>
{
    string? authority = app.Configuration["OAuth:Authority"];
    if (!string.IsNullOrEmpty(authority))
    {
        opt.AddPreferredSecuritySchemes("OAuth2")
            .AddClientCredentialsFlow(
                "OAuth2",
                flow =>
                {
                    flow.WithClientId("00000000-0000-0000-0000-000000000001")
                        .WithClientSecret("dev");
                }
            );
    }
    else
    {
        opt.AddPreferredSecuritySchemes("Bearer");
    }
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
