using System.Diagnostics;
using System.Security.Claims;
using Api.AuditLog;
using Checklist.UseCases;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Api.Http;

internal static class RemoveMemberEndpoint
{
    /// <summary>The endpoint's name, stored in <c>AuditLog.Operation</c>: never change it.</summary>
    internal const string Operation = "RemoveMember";

    internal static void MapEndpoint(this IEndpointRouteBuilder builder)
    {
        builder
            .MapDelete("/{itemGroupId:guid}/member/{memberId:guid}", Handle)
            .WithSummary("Remove member from item group")
            .WithDescription(
                "Revokes a user's access to an item group by removing them as a member. The authenticated user must be a member of the group. Returns 409 Conflict if the target member is the last member of the group, as removing them would leave the group permanently unreachable."
            )
            .WithTags("Member")
            .WithName(Operation);
    }

    private static async Task<
        Results<NoContent, UnauthorizedHttpResult, ForbidHttpResult, Conflict>
    > Handle(
        Guid itemGroupId,
        Guid memberId,
        ClaimsPrincipal user,
        HttpRequest httpRequest,
        AuditContext auditContext,
        RemoveMember useCase,
        CancellationToken ct
    )
    {
        // Set first, so refused requests are audited with their target too.
        auditContext.TargetUserId = memberId;
        if (user.ToCaller(httpRequest) is not { } caller)
        {
            return TypedResults.Unauthorized();
        }

        return await useCase.Execute(caller, itemGroupId, memberId, ct) switch
        {
            RemoveMember.Outcome.Removed => TypedResults.NoContent(),
            RemoveMember.Outcome.LastMember => TypedResults.Conflict(),
            RemoveMember.Outcome.NotAMember => TypedResults.Forbid(),
            _ => throw new UnreachableException(),
        };
    }
}
