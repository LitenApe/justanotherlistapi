namespace Core.AuditLog;

/// <summary>Immutable snapshot of a single audited event, written to the AuditLog table.</summary>
public sealed record AuditEntry(
    /// <summary>UTC timestamp of the event.</summary>
    DateTimeOffset Timestamp,
    /// <summary>OpenTelemetry trace ID for correlation with distributed traces. NULL when no active trace exists.</summary>
    string? TraceId,
    /// <summary>ID of the authenticated user who triggered the event. NULL when the request was unauthenticated.</summary>
    Guid? UserId,
    /// <summary>Real client IP address after ForwardedHeaders middleware resolves X-Forwarded-For. NULL when not available.</summary>
    string? IpAddress,
    /// <summary>Discriminator for the resource type being acted upon (e.g. "ItemGroup", "Item"). NULL for AuthenticationFailed events.</summary>
    string? ResourceType,
    /// <summary>Name of the operation, matching the .WithName() value on the endpoint, or "AuthenticationFailed" for JWT failures.</summary>
    string Operation,
    /// <summary>Primary resource identifier. Always the itemGroupId for Checklist operations. NULL for GetItemGroups and auth events.</summary>
    Guid? ResourceId,
    /// <summary>Secondary resource identifier. The itemId for Item operations. NULL for ItemGroup and Member operations.</summary>
    Guid? SubResourceId,
    /// <summary>The user being affected by the operation. Populated only for AddMember and RemoveMember.</summary>
    Guid? TargetUserId,
    /// <summary>Result of the operation: Success, BadRequest, NotFound, Conflict, Forbidden, MissingClaim, or AuthenticationFailed.</summary>
    string Outcome,
    /// <summary>Human-readable reason for failure. Populated for MissingClaim and AuthenticationFailed outcomes.</summary>
    string? FailureReason
);
