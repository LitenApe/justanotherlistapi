using System.ComponentModel;
using System.Data;
using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Core.Checklist;

public static class UpdateItemGroupSettings
{
    public static void MapEndpoint(this IEndpointRouteBuilder builder)
    {
        builder
            .MapPut("/{itemGroupId:guid}/settings", Execute)
            .WithSummary("Update item group settings")
            .WithDescription(
                "Replaces the auto-delete and auto-hide settings of an item group. A null or omitted value turns that setting off. The authenticated user must be a member of the group."
            )
            .WithTags(nameof(ItemGroup))
            .WithName(nameof(UpdateItemGroupSettings));
    }

    public static async Task<
        Results<Ok<ItemGroupSettings>, BadRequest, UnauthorizedHttpResult, ForbidHttpResult>
    > Execute(
        Guid itemGroupId,
        Request request,
        ClaimsPrincipal claimsPrincipal,
        IDbConnection db,
        IChecklistNotifier notifier,
        HttpRequest httpRequest,
        TimeProvider timeProvider,
        CancellationToken ct = default
    )
    {
        if (
            !IsInRange(request.AutoDeleteAfterMinutes, ItemGroupSettings.MinAutoDeleteMinutes)
            || !IsInRange(request.HideCompletedAfterMinutes, 0)
        )
        {
            return TypedResults.BadRequest();
        }

        return await db.ExecuteAsItemGroupMember<
            Results<Ok<ItemGroupSettings>, BadRequest, UnauthorizedHttpResult, ForbidHttpResult>
        >(
            itemGroupId,
            claimsPrincipal,
            async _ =>
            {
                ItemGroupSettings settings = await UpdateData(
                    itemGroupId,
                    request,
                    timeProvider.GetUtcNow(),
                    db,
                    ct
                );
                string? connectionId = httpRequest.Headers["X-SignalR-Connection-Id"];
                await notifier.NotifyGroupSettingsChanged(itemGroupId, settings, connectionId);
                return TypedResults.Ok(settings);
            },
            TypedResults.Unauthorized(),
            TypedResults.Forbid(),
            ct
        );
    }

    /// <summary>
    /// Saves the settings. <see cref="ItemGroupSettings.AutoDeleteUpdatedAt"/> moves to
    /// <paramref name="now"/> only when the auto-delete value changes, which restarts its countdown.
    /// </summary>
    internal static async Task<ItemGroupSettings> UpdateData(
        Guid itemGroupId,
        Request request,
        DateTimeOffset now,
        IDbConnection db,
        CancellationToken ct
    )
    {
        ItemGroupSettings? updated = await db.QuerySingleOrDefaultAsync<ItemGroupSettings>(
            new CommandDefinition(
                """
                UPDATE ItemGroups
                SET AutoDeleteUpdatedAt = CASE
                        WHEN AutoDeleteAfterMinutes IS DISTINCT FROM @AutoDeleteAfterMinutes THEN @Now
                        ELSE AutoDeleteUpdatedAt
                    END,
                    AutoDeleteAfterMinutes = @AutoDeleteAfterMinutes,
                    HideCompletedAfterMinutes = @HideCompletedAfterMinutes
                OUTPUT INSERTED.AutoDeleteAfterMinutes, INSERTED.HideCompletedAfterMinutes, INSERTED.AutoDeleteUpdatedAt
                WHERE Id = @Id
                """,
                new
                {
                    Id = itemGroupId,
                    request.AutoDeleteAfterMinutes,
                    request.HideCompletedAfterMinutes,
                    Now = now,
                },
                cancellationToken: ct
            )
        );

        // Only null if the group was deleted between the membership check and the update.
        return updated
            ?? new ItemGroupSettings
            {
                AutoDeleteAfterMinutes = request.AutoDeleteAfterMinutes,
                HideCompletedAfterMinutes = request.HideCompletedAfterMinutes,
            };
    }

    private static bool IsInRange(int? minutes, int min) =>
        minutes is null || (minutes >= min && minutes <= ItemGroupSettings.MaxMinutes);

    public record Request
    {
        [Description(
            "Minutes after the last item is completed before the list is deleted, from 1440 (1 day) to 525600 (365 days). Null turns auto-delete off"
        )]
        public int? AutoDeleteAfterMinutes { get; init; }

        [Description(
            "Minutes after an item is completed before it is hidden, from 0 to 525600 (365 days). Null turns auto-hide off"
        )]
        public int? HideCompletedAfterMinutes { get; init; }
    }
}
