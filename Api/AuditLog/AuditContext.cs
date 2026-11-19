namespace Api.AuditLog;

/// <summary>
/// Scoped per-request bag that endpoint adapters fill in with what the audit filter can't read
/// from the route. The filter reads it after the endpoint returns.
/// </summary>
public sealed class AuditContext
{
    /// <summary>
    /// The ID of a resource the request created. <c>CreateItemGroupEndpoint</c> sets it to the new
    /// list's ID, on success only. Leave it null elsewhere: the filter falls back to the
    /// <c>itemGroupId</c> route value.
    /// </summary>
    public Guid? ResourceId { get; set; }

    /// <summary>
    /// The ID of a sub-resource the request created. <c>CreateItemEndpoint</c> sets it to the new
    /// item's ID, on success only. Leave it null elsewhere: the filter falls back to the
    /// <c>itemId</c> route value when there is one.
    /// </summary>
    public Guid? SubResourceId { get; set; }

    /// <summary>
    /// The user the operation affects. <c>AddMemberEndpoint</c> and <c>RemoveMemberEndpoint</c>
    /// set it from the <c>memberId</c> route value before anything can refuse the request.
    /// </summary>
    public Guid? TargetUserId { get; set; }
}
