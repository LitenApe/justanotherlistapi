using System.Diagnostics.CodeAnalysis;

namespace Checklist.Domain;

/// <summary>
/// An email address, trimmed: at most <see cref="MaxLength"/> characters, exactly one <c>@</c>
/// with text on both sides, and no whitespace. Whether the mailbox exists isn't checked. Accounts
/// are looked up by email ignoring case.
/// </summary>
public sealed record EmailAddress
{
    public const int MaxLength = 254;

    private EmailAddress(string value) => Value = value;

    public string Value { get; }

    /// <summary>Trims the address, and accepts it when it has the shape described above.</summary>
    public static bool TryCreate(string? value, [NotNullWhen(true)] out EmailAddress? email)
    {
        string trimmed = value?.Trim() ?? "";
        int at = trimmed.IndexOf('@', StringComparison.Ordinal);
        bool valid =
            trimmed.Length <= MaxLength
            && at > 0
            && at < trimmed.Length - 1
            && trimmed.Count(c => c == '@') == 1
            && !trimmed.Any(char.IsWhiteSpace);
        email = valid ? new EmailAddress(trimmed) : null;
        return email is not null;
    }

    public override string ToString() => Value;
}
