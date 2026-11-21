using System.Diagnostics.CodeAnalysis;

namespace Checklist.Domain;

/// <summary>
/// What a person is called in the app, as other members see it: trimmed, and 1 to
/// <see cref="MaxLength"/> characters long. Names don't have to be unique.
/// </summary>
public sealed record DisplayName
{
    public const int MaxLength = 50;

    private DisplayName(string value) => Value = value;

    public string Value { get; }

    /// <summary>Trims the name, and accepts it when 1 to <see cref="MaxLength"/> characters are left.</summary>
    public static bool TryCreate(string? value, [NotNullWhen(true)] out DisplayName? name)
    {
        string trimmed = value?.Trim() ?? "";
        name = trimmed.Length is > 0 and <= MaxLength ? new DisplayName(trimmed) : null;
        return name is not null;
    }

    public override string ToString() => Value;
}
