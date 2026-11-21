using Microsoft.IdentityModel.Tokens;

namespace Api.Accounts;

/// <summary>
/// How the API signs the tokens it issues and checks the ones it receives: the configuration
/// section <c>Auth</c> (see specifications/authentication.md).
/// </summary>
internal sealed class AuthOptions
{
    public const string Section = "Auth";

    /// <summary>The smallest signing key that's safe for HMAC-SHA256: 256 bits.</summary>
    public const int MinKeyBytes = 32;

    /// <summary>The token's <c>iss</c> claim, which validation requires.</summary>
    public string Issuer { get; set; } = "JustAnotherList";

    /// <summary>The token's <c>aud</c> claim, which validation requires.</summary>
    public string Audience { get; set; } = "JustAnotherList";

    /// <summary>
    /// A secret of at least <see cref="MinKeyBytes"/> random bytes, base64-encoded, that signs
    /// and checks every token. Anyone who has it can sign in as anyone: keep it out of source
    /// control outside development.
    /// </summary>
    public string SigningKey { get; set; } = "";

    /// <summary>How long a token works after it's issued.</summary>
    public TimeSpan TokenLifetime { get; set; } = TimeSpan.FromHours(8);

    public bool HasUsableSigningKey()
    {
        try
        {
            return Convert.FromBase64String(SigningKey).Length >= MinKeyBytes;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public SymmetricSecurityKey CreateSigningKey() => new(Convert.FromBase64String(SigningKey));
}
