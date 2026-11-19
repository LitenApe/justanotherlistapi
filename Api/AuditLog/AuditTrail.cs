using Checklist.Ports;

namespace Api.AuditLog;

/// <summary>
/// <see cref="IAuditTrail"/> on the audit log, through the same <see cref="IAuditWriter"/> as
/// <see cref="AuditEndpointFilter"/>. No request is behind these entries, so they have no user,
/// IP address or trace.
/// </summary>
internal sealed class AuditTrail(IAuditWriter writer) : IAuditTrail
{
    /// <summary>Stored in <c>AuditLog.Operation</c>: never change it.</summary>
    internal const string AutoDeleteItemGroupOperation = "AutoDeleteItemGroup";

    public void ItemGroupAutoDeleted(Guid itemGroupId, DateTimeOffset at) =>
        writer.Enqueue(
            new AuditEntry(
                Timestamp: at,
                TraceId: null,
                UserId: null,
                IpAddress: null,
                ResourceType: "ItemGroup",
                Operation: AutoDeleteItemGroupOperation,
                ResourceId: itemGroupId,
                SubResourceId: null,
                TargetUserId: null,
                Outcome: "Success",
                FailureReason: null
            )
        );
}
