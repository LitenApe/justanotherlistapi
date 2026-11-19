using System.Diagnostics;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Api.AuditLog;

/// <summary>
/// Audits requests whose bearer token fails validation. The JWT middleware rejects them before
/// routing, so <see cref="AuditEndpointFilter"/> never sees them.
/// </summary>
internal static class AuthenticationAudit
{
    /// <summary>Stored in <c>AuditLog.Operation</c>: never change it.</summary>
    internal const string Operation = "AuthenticationFailed";

    /// <summary>Handler for <see cref="JwtBearerEvents.OnAuthenticationFailed"/>.</summary>
    internal static Task OnAuthenticationFailed(AuthenticationFailedContext context)
    {
        IServiceProvider services = context.HttpContext.RequestServices;
        services
            .GetRequiredService<IAuditWriter>()
            .Enqueue(
                new AuditEntry(
                    Timestamp: services.GetRequiredService<TimeProvider>().GetUtcNow(),
                    TraceId: Activity.Current?.TraceId.ToString(),
                    UserId: null,
                    IpAddress: context.HttpContext.Connection.RemoteIpAddress?.ToString(),
                    ResourceType: null,
                    Operation: Operation,
                    ResourceId: null,
                    SubResourceId: null,
                    TargetUserId: null,
                    Outcome: "AuthenticationFailed",
                    FailureReason: context.Exception.Message
                )
            );
        return Task.CompletedTask;
    }
}
