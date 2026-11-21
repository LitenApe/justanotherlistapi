using System.ComponentModel;

namespace Checklist.Domain;

/// <summary>A member of a list, as the other members see them: never their email.</summary>
public sealed record Member
{
    [Description("The member's user ID")]
    public required Guid Id { get; init; }

    [Description(
        "What the member is called, or null for someone added before accounts existed who hasn't signed up since"
    )]
    public string? Name { get; init; }
}
