namespace Core.Checklist;

/// <summary>
/// Flat ItemGroups row for Dapper, which can't populate the nested <see cref="ItemGroupSettings"/>.
/// </summary>
internal sealed record ItemGroupRow
{
    public Guid Id { get; init; }

    public required string Name { get; init; }

    public int? AutoDeleteAfterMinutes { get; init; }

    public int? HideCompletedAfterMinutes { get; init; }

    public DateTimeOffset? AutoDeleteUpdatedAt { get; init; }

    public ItemGroup ToItemGroup() =>
        new()
        {
            Id = Id,
            Name = Name,
            Settings = new ItemGroupSettings
            {
                AutoDeleteAfterMinutes = AutoDeleteAfterMinutes,
                HideCompletedAfterMinutes = HideCompletedAfterMinutes,
                AutoDeleteUpdatedAt = AutoDeleteUpdatedAt,
            },
        };
}
