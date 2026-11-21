namespace Checklist.Domain;

/// <summary>The bearer token that signs someone in, and when it stops working.</summary>
/// <param name="Value">The token itself, a secret: never log it.</param>
/// <param name="ExpiresAt">When the token stops working.</param>
public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt)
{
    public override string ToString() => $"AccessToken (expires {ExpiresAt:O})";
}
