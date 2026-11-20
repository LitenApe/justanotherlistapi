using System.ComponentModel;
using System.Diagnostics;
using System.Security.Claims;
using Api.AuditLog;
using Checklist.Domain;
using Checklist.UseCases;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Api.Http;

internal static class UnreserveItemEndpoint
{
    /// <summary>The endpoint's name, stored in <c>AuditLog.Operation</c>: never change it.</summary>
    internal const string Operation = "UnreserveItem";

    internal static void MapEndpoint(this IEndpointRouteBuilder builder)
    {
        builder
            .MapDelete("/{itemGroupId:guid}/{itemId:guid}/reservation", Handle)
            .WithSummary("Cancel an item's reservation")
            .WithDescription(
                "Cancels the reservation of an item, so that any member can reserve it. Returns 409 Conflict, changing nothing, when the item is now reserved for someone other than `memberId`, or when the group protects reservations and the caller is neither the member the item is reserved for nor whoever reserved it. The authenticated user must be a member of the item group."
            )
            .WithTags(nameof(Item))
            .WithName(Operation);
    }

    private static async Task<
        Results<NoContent, UnauthorizedHttpResult, ForbidHttpResult, Conflict>
    > Handle(
        Guid itemGroupId,
        Guid itemId,
        [Description("Who the item is reserved for as the caller last saw it")] Guid memberId,
        ClaimsPrincipal user,
        HttpRequest httpRequest,
        AuditContext auditContext,
        UnreserveItem useCase,
        CancellationToken ct
    )
    {
        // Set first, so refused requests are audited with their target too.
        auditContext.TargetUserId = memberId;
        if (user.ToCaller(httpRequest) is not { } caller)
        {
            return TypedResults.Unauthorized();
        }

        return await useCase.Execute(caller, itemGroupId, itemId, memberId, ct) switch
        {
            UnreserveItem.Outcome.Unreserved => TypedResults.NoContent(),
            UnreserveItem.Outcome.NotAMember => TypedResults.Forbid(),
            UnreserveItem.Outcome.Outdated or UnreserveItem.Outcome.Protected =>
                TypedResults.Conflict(),
            _ => throw new UnreachableException(),
        };
    }
}
