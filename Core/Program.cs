using System.Data;
using Core;
using Core.Utility;
using Microsoft.Data.SqlClient;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// --- Service Registrations ---

// Network
builder.Services.AddCors();
builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
});
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation();
        tracing.AddHttpClientInstrumentation();
    })
    .UseOtlpExporter();

// Database
builder.AddSqlServerClient(connectionName: "database");
builder.Services.AddScoped<IDbConnection>(sp => sp.GetRequiredService<SqlConnection>());

// Authentication & Authorization
builder.Services.AddAuthentication()
    .AddJwtBearer(options =>
    {
        var authority = builder.Configuration["OAuth:Authority"];
        if (!string.IsNullOrEmpty(authority))
        {
            options.Authority = authority;
            options.RequireHttpsMetadata = !authority.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
        }

        var audience = builder.Configuration["OAuth:Audience"];
        if (!string.IsNullOrEmpty(audience))
        {
            options.Audience = audience;
        }
        else
        {
            // No audience configured — disable validation. Set OAuth:Audience in production.
            options.TokenValidationParameters.ValidateAudience = false;
        }
    });
builder.Services.AddAuthorization();

// API Documentation
builder.Services.AddOpenApi(opt =>
    opt.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

var app = builder.Build();

// --- Middleware Pipeline ---

app.UseCors(policyBuilder => policyBuilder
    .AllowAnyHeader()
    .AllowAnyMethod()
    .SetIsOriginAllowed(origin => true)
    .AllowCredentials());
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapChecklistApi();

app.MapOpenApi();
app.MapScalarApiReference(opt =>
{
    var authority = app.Configuration["OAuth:Authority"];
    if (!string.IsNullOrEmpty(authority))
    {
        opt.AddPreferredSecuritySchemes("OAuth2")
            .AddClientCredentialsFlow("OAuth2", flow =>
            {
                flow.WithClientId("00000000-0000-0000-0000-000000000001").WithClientSecret("dev");
            });
    }
    else
    {
        opt.AddPreferredSecuritySchemes("Bearer");
    }
    opt.WithDocumentDownloadType(DocumentDownloadType.Both)
       .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.Curl);
});

// --- Database Initialization ---

using (var scope = app.Services.CreateScope())
{
    var connection = scope.ServiceProvider.GetRequiredService<SqlConnection>();
    await DatabaseInitializer.InitializeAsync(connection);
}

await app.RunAsync();
