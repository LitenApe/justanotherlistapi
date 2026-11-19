namespace Checklist.Domain;

/// <summary>What a member sets on an item when adding or editing it.</summary>
public sealed record ItemInput(Name Name, string? Description, bool IsComplete);
