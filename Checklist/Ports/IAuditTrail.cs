namespace Checklist.Ports;

/// <summary>
/// Records the changes no request asked for. The HTTP adapter audits every request by itself, so
/// this is only for what the inside does on its own.
/// </summary>
public interface IAuditTrail
{
    /// <summary>Records that auto-delete deleted the list at <paramref name="at"/>.</summary>
    void ItemGroupAutoDeleted(Guid itemGroupId, DateTimeOffset at);
}
