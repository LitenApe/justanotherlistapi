using System.Diagnostics.CodeAnalysis;

namespace Checklist.Domain;

/// <summary>A list or item name. The only rule: it can't be blank.</summary>
public sealed record Name
{
    private Name(string value) => Value = value;

    public string Value { get; }

    /// <summary>Accepts any name that isn't blank, exactly as given.</summary>
    public static bool TryCreate(string? value, [NotNullWhen(true)] out Name? name)
    {
        name = string.IsNullOrWhiteSpace(value) ? null : new Name(value);
        return name is not null;
    }

    public override string ToString() => Value;
}
