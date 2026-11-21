using System.ComponentModel;
using Checklist.Domain;

namespace Api.Http;

/// <summary>What signing up or signing in answers: the token to send from now on, and who it's for.</summary>
public sealed record SessionResponse
{
    [Description("The bearer token to send with every request, as `Authorization: Bearer <token>`")]
    public required string AccessToken { get; init; }

    [Description("When the token stops working; sign in again after that")]
    public required DateTimeOffset ExpiresAt { get; init; }

    [Description("Who is signed in")]
    public required User User { get; init; }

    internal static SessionResponse From(Session session) =>
        new()
        {
            AccessToken = session.Token.Value,
            ExpiresAt = session.Token.ExpiresAt,
            User = session.User,
        };
}
