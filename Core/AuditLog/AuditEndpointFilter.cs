using System.Diagnostics;
using Core.Checklist;

namespace Core.AuditLog;

internal sealed partial class AuditEndpointFilter(
    IAuditWriter auditWriter,
    ILogger<AuditEndpointFilter> logger
) : IEndpointFilter
{
    private static readonly Dictionary<string, string> resourceTypes = new(StringComparer.Ordinal)
    {
        [nameof(GetItemGroups)] = "ItemGroup",
        [nameof(GetItemGroup)] = "ItemGroup",
        [nameof(CreateItemGroup)] = "ItemGroup",
        [nameof(UpdateItemGroup)] = "ItemGroup",
        [nameof(DeleteItemGroup)] = "ItemGroup",
        [nameof(CreateItem)] = "Item",
        [nameof(UpdateItem)] = "Item",
        [nameof(DeleteItem)] = "Item",
        [nameof(GetMembers)] = "ItemGroup",
        [nameof(AddMember)] = "ItemGroup",
        [nameof(RemoveMember)] = "ItemGroup",
    };

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next
    )
    {
        object? result = null;
        try
        {
            result = await next(context);
        }
        finally
        {
            try
            {
                RegisterAuditOnStarting(context);
            }
            catch (Exception ex)
            {
                LogAuditEntryFailed(logger, ex);
            }
        }
        return result;
    }

    // Registers a Response.OnStarting callback so the audit entry is enqueued just before
    // response headers are sent. At that point Response.StatusCode reflects the final HTTP
    // outcome, including 403 set by ForbidHttpResult via the auth middleware.
    private void RegisterAuditOnStarting(EndpointFilterInvocationContext context)
    {
        HttpContext httpContext = context.HttpContext;
        AuditContext auditContext = httpContext.RequestServices.GetRequiredService<AuditContext>();

        string? operation = httpContext
            .GetEndpoint()
            ?.Metadata.GetMetadata<IEndpointNameMetadata>()
            ?.EndpointName;

        if (operation is null)
        {
            LogOperationNameMissing(logger);
            return;
        }

        resourceTypes.TryGetValue(operation, out string? resourceType);
        Guid? resourceId = ResolveResourceId(httpContext, auditContext);
        Guid? subResourceId = ResolveSubResourceId(httpContext, auditContext);
        Guid? userId = httpContext.User.GetUserId();
        string? ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
        string? traceId = Activity.Current?.TraceId.ToString();
        Guid? targetUserId = auditContext.TargetUserId;

        httpContext.Response.OnStarting(() =>
        {
            EnqueueAuditEntry(
                httpContext.Response.StatusCode,
                operation,
                resourceType,
                resourceId,
                subResourceId,
                userId,
                ipAddress,
                traceId,
                targetUserId
            );
            return Task.CompletedTask;
        });
    }

    private void EnqueueAuditEntry(
        int statusCode,
        string operation,
        string? resourceType,
        Guid? resourceId,
        Guid? subResourceId,
        Guid? userId,
        string? ipAddress,
        string? traceId,
        Guid? targetUserId
    )
    {
        try
        {
            string outcome = MapOutcome(statusCode);
            string? failureReason = string.Equals(outcome, "MissingClaim", StringComparison.Ordinal)
                ? "Required user identifier claim is missing."
                : null;

            auditWriter.Enqueue(
                new AuditEntry(
                    Timestamp: DateTimeOffset.UtcNow,
                    TraceId: traceId,
                    UserId: userId,
                    IpAddress: ipAddress,
                    ResourceType: resourceType,
                    Operation: operation,
                    ResourceId: resourceId,
                    SubResourceId: subResourceId,
                    TargetUserId: targetUserId,
                    Outcome: outcome,
                    FailureReason: failureReason
                )
            );
        }
        catch (Exception ex)
        {
            LogAuditEntryFailed(logger, ex);
        }
    }

    private static Guid? ResolveResourceId(HttpContext httpContext, AuditContext auditContext)
    {
        if (auditContext.ResourceId.HasValue)
        {
            return auditContext.ResourceId;
        }

        return
            httpContext.Request.RouteValues.TryGetValue("itemGroupId", out object? raw)
            && Guid.TryParse(raw?.ToString(), out Guid id)
            ? id
            : null;
    }

    private static Guid? ResolveSubResourceId(HttpContext httpContext, AuditContext auditContext)
    {
        if (auditContext.SubResourceId.HasValue)
        {
            return auditContext.SubResourceId;
        }

        return
            httpContext.Request.RouteValues.TryGetValue("itemId", out object? raw)
            && Guid.TryParse(raw?.ToString(), out Guid id)
            ? id
            : null;
    }

    private static string MapOutcome(int statusCode) =>
        statusCode switch
        {
            >= 200 and <= 299 => "Success",
            400 => "BadRequest",
            401 => "MissingClaim",
            403 => "Forbidden",
            404 => "NotFound",
            409 => "Conflict",
            _ => "Unknown",
        };

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Endpoint has no operation name; audit entry skipped."
    )]
    private static partial void LogOperationNameMissing(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to build or enqueue audit entry.")]
    private static partial void LogAuditEntryFailed(ILogger logger, Exception ex);
}
