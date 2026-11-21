using System.Threading.RateLimiting;
using Api.AuditLog;

namespace Api.Http;

/// <summary>
/// How often one client may call the account endpoints: a fixed window per IP address, so
/// nobody can guess passwords, or fill someone's inbox with reset emails, without end. Requests
/// over the limit get 429 Too Many Requests, and the audit log records them.
/// </summary>
internal static class AuthRateLimit
{
    internal const string Policy = "auth";

    /// <summary>Requests per client per window, unless <c>Auth:RateLimit:PermitLimit</c> says otherwise.</summary>
    internal const int DefaultPermitLimit = 20;

    internal static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    internal static IServiceCollection AddAuthRateLimit(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        int permitLimit = configuration.GetValue("Auth:RateLimit:PermitLimit", DefaultPermitLimit);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = RateLimitAudit.OnRejected;
            options.AddPolicy(Policy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = Window,
                        QueueLimit = 0,
                    }
                )
            );
        });
        return services;
    }
}
