using System.ComponentModel;

namespace Checklist.Domain;

/// <summary>Someone with an account. Their password never leaves the accounts adapter.</summary>
public sealed record User
{
    [Description("The user's ID: lists name their members, and items their reservations, by it")]
    public required Guid Id { get; init; }

    [Description("What the user is called")]
    public required string Name { get; init; }

    [Description("The email address the user signs in with")]
    public required string Email { get; init; }
}
