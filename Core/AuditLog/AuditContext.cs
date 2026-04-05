namespace Core.AuditLog;

/// <summary>
/// Scoped per-request bag that handlers populate with semantic context the endpoint filter
/// cannot derive from route values alone. The filter reads this after the handler returns.
/// </summary>
public sealed class AuditContext
{
    /// <summary>
    /// Primary resource ID generated inside the handler. Set by CreateItemGroup after a
    /// successful insert so the filter can record the new group's ID as ResourceId.
    /// Leave null for all other handlers — the filter falls back to the itemGroupId route value.
    /// </summary>
    public Guid? ResourceId { get; set; }

    /// <summary>
    /// Secondary resource ID generated inside the handler. Set by CreateItem after a
    /// successful insert so the filter can record the new item's ID as SubResourceId.
    /// Leave null for other handlers — the filter falls back to the itemId route value when present.
    /// </summary>
    public Guid? SubResourceId { get; set; }

    /// <summary>
    /// The user being affected by the operation. Set by AddMember and RemoveMember using
    /// the memberId route value.
    /// </summary>
    public Guid? TargetUserId { get; set; }
}
