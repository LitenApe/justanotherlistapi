using System.Diagnostics.CodeAnalysis;

namespace Checklist.Domain;

/// <summary>
/// A new password: <see cref="MinLength"/> to <see cref="MaxLength"/> characters, kept exactly as
/// typed. Length is the only rule, as NIST SP 800-63B recommends: rules about character kinds
/// make passwords harder to remember, not harder to guess.
/// </summary>
/// <remarks>
/// A class rather than a record, so it has no generated <c>ToString</c> or equality that could
/// put the password in a log.
/// </remarks>
public sealed class Password
{
    public const int MinLength = 12;
    public const int MaxLength = 128;

    private Password(string value) => Value = value;

    public string Value { get; }

    /// <summary>Accepts a password of <see cref="MinLength"/> to <see cref="MaxLength"/> characters.</summary>
    public static bool TryCreate(string? value, [NotNullWhen(true)] out Password? password)
    {
        password = value is { Length: >= MinLength and <= MaxLength } ? new Password(value) : null;
        return password is not null;
    }

    public override string ToString() => "********";
}
