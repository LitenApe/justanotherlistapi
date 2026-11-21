using Checklist.Domain;
using Checklist.Ports;

namespace Api.Tests;

/// <summary>Issues tokens that name their user, so use-case tests can tell who a session is for.</summary>
internal sealed class FakeTokenIssuer : ITokenIssuer
{
    public static readonly DateTimeOffset ExpiresAt = new(2026, 11, 21, 18, 0, 0, TimeSpan.Zero);

    public AccessToken Issue(User user) => new($"token-for-{user.Id}", ExpiresAt);
}
