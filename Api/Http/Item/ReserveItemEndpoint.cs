using System.ComponentModel;
using System.Diagnostics;
using System.Security.Claims;
using Api.AuditLog;
using Checklist.Domain;
using Checklist.UseCases;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Api.Http;

internal static class ReserveItemEndpoint
{
    /// <summary>The endpoint's name, stored in <c>AuditLog.Operation</c>: never change it.</summary>
    internal const string Operation = "ReserveItem";

    internal static void MapEndpoint(this IEndpointRouteBuilder builder)
    {
        builder
            .MapPut("/{itemGroupId:guid}/{itemId:guid}/reservation", Handle)
            .WithSummary("Reserve an item")
            .WithDescription(
                "Reserves an item for a member of the item group, the caller or someone else, taking it over from whoever had it. Returns 409 Conflict, changing nothing, when the item's reservation isn't the one in `replacing` any more, when the group protects reservations and the caller is neither the member the item is reserved for nor whoever reserved it, or when `memberId` isn't a member. The authenticated user must be a member of the item group."
            )
            .WithTags(nameof(Item))
            .WithName(Operation);
    }

    private static async Task<
        Results<NoContent, UnauthorizedHttpResult, ForbidHttpResult, Conflict>
    > Handle(
        Guid itemGroupId,
        Guid itemId,
        Request request,
        ClaimsPrincipal user,
        HttpRequest httpRequest,
        AuditContext auditContext,
        ReserveItem useCase,
        CancellationToken ct
    )
    {
        // Set first, so refused requests are audited with their target too.
        auditContext.TargetUserId = request.MemberId;
        if (user.ToCaller(httpRequest) is not { } caller)
        {
            return TypedResults.Unauthorized();
        }

        return await useCase.Execute(
            caller,
            itemGroupId,
            itemId,
            request.MemberId,
            request.Replacing,
            ct
        ) switch
        {
            ReserveItem.Outcome.Reserved => TypedResults.NoContent(),
            ReserveItem.Outcome.NotAMember => TypedResults.Forbid(),
            ReserveItem.Outcome.Outdated
            or ReserveItem.Outcome.Protected
            or ReserveItem.Outcome.ForNonMember => TypedResults.Conflict(),
            _ => throw new UnreachableException(),
        };
    }

    public sealed record Request
    {
        [Description("The member to reserve the item for: the caller or another member")]
        public required Guid MemberId { get; init; }

        [Description(
            "Who the item is reserved for as the caller last saw it, or null if they saw it free. Required, so that a reservation is never replaced unseen"
        )]
        public required Guid? Replacing { get; init; }
    }
}
