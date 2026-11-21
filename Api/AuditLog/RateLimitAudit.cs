using System.Diagnostics;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.AuditLog;

/// <summary>
/// Audits requests the rate limiter turns away. It answers them before the endpoint runs, so
/// <see cref="AuditEndpointFilter"/> never sees them.
/// </summary>
internal static class RateLimitAudit
{
    /// <summary>Handler for <see cref="RateLimiterOptions.OnRejected"/>.</summary>
    internal static ValueTask OnRejected(OnRejectedContext context, CancellationToken ct)
    {
        HttpContext httpContext = context.HttpContext;
        string? operation = httpContext
            .GetEndpoint()
            ?.Metadata.GetMetadata<IEndpointNameMetadata>()
            ?.EndpointName;
        if (operation is null)
        {
            return ValueTask.CompletedTask;
        }

        IServiceProvider services = httpContext.RequestServices;
        services
            .GetRequiredService<IAuditWriter>()
            .Enqueue(
                new AuditEntry(
                    Timestamp: services.GetRequiredService<TimeProvider>().GetUtcNow(),
                    TraceId: Activity.Current?.TraceId.ToString(),
                    UserId: httpContext.User.GetUserId(),
                    IpAddress: httpContext.Connection.RemoteIpAddress?.ToString(),
                    ResourceType: AuditEndpointFilter.ResourceTypeOf(operation),
                    Operation: operation,
                    ResourceId: null,
                    SubResourceId: null,
                    TargetUserId: null,
                    Outcome: "TooManyRequests",
                    FailureReason: null
                )
            );
        return ValueTask.CompletedTask;
    }
}
