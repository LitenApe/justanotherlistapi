using System.ComponentModel;
using System.Security.Claims;
using Checklist.Domain;
using Checklist.UseCases;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Api.Http;

internal static class UpdateItemGroupSettingsEndpoint
{
    /// <summary>The endpoint's name, stored in <c>AuditLog.Operation</c>: never change it.</summary>
    internal const string Operation = "UpdateItemGroupSettings";

    internal static void MapEndpoint(this IEndpointRouteBuilder builder)
    {
        builder
            .MapPut("/{itemGroupId:guid}/settings", Handle)
            .WithSummary("Update item group settings")
            .WithDescription(
                "Replaces the auto-delete and auto-hide settings of an item group. A null or omitted value turns that setting off. The authenticated user must be a member of the group."
            )
            .WithTags(nameof(ItemGroup))
            .WithName(Operation);
    }

    private static async Task<
        Results<Ok<ItemGroupSettings>, BadRequest, UnauthorizedHttpResult, ForbidHttpResult>
    > Handle(
        Guid itemGroupId,
        Request request,
        ClaimsPrincipal user,
        HttpRequest httpRequest,
        UpdateItemGroupSettings useCase,
        CancellationToken ct
    )
    {
        if (
            !ItemGroupSettingsInput.TryCreate(
                request.AutoDeleteAfterMinutes,
                request.HideCompletedAfterMinutes,
                out ItemGroupSettingsInput? settings
            )
        )
        {
            return TypedResults.BadRequest();
        }
        if (user.ToCaller(httpRequest) is not { } caller)
        {
            return TypedResults.Unauthorized();
        }

        if (await useCase.Execute(caller, itemGroupId, settings, ct) is not { } stored)
        {
            return TypedResults.Forbid();
        }

        return TypedResults.Ok(stored);
    }

    public sealed record Request
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
