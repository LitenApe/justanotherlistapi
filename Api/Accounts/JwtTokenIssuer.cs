using Checklist.Domain;
using Checklist.Ports;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Api.Accounts;

/// <summary>
/// <see cref="ITokenIssuer"/> as JWTs signed with HMAC-SHA256. The user's ID goes in the
/// <c>sub</c> claim, which is where the API, the hub and the audit log read who is calling.
/// </summary>
internal sealed class JwtTokenIssuer(IOptions<AuthOptions> options, TimeProvider clock)
    : ITokenIssuer
{
    private readonly JsonWebTokenHandler handler = new();

    public AccessToken Issue(User user)
    {
        AuthOptions settings = options.Value;
        DateTimeOffset now = clock.GetUtcNow();
        // A JWT counts time in whole seconds; the expiry the client is told matches the token's.
        DateTimeOffset expiresAt = DateTimeOffset.FromUnixTimeSeconds(
            (now + settings.TokenLifetime).ToUnixTimeSeconds()
        );

        string token = handler.CreateToken(
            new SecurityTokenDescriptor
            {
                Issuer = settings.Issuer,
                Audience = settings.Audience,
                Claims = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
                },
                IssuedAt = now.UtcDateTime,
                NotBefore = now.UtcDateTime,
                Expires = expiresAt.UtcDateTime,
                SigningCredentials = new SigningCredentials(
                    settings.CreateSigningKey(),
                    SecurityAlgorithms.HmacSha256
                ),
            }
        );
        return new AccessToken(token, expiresAt);
    }
}
