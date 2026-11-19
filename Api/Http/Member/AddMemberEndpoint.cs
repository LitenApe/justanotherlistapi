using System.Diagnostics;
using System.Security.Claims;
using Api.AuditLog;
using Checklist.UseCases;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Api.Http;

internal static class AddMemberEndpoint
{
    /// <summary>The endpoint's name, stored in <c>AuditLog.Operation</c>: never change it.</summary>
    internal const string Name = "AddMember";

    internal static void MapEndpoint(this IEndpointRouteBuilder builder)
    {
        builder
            .MapPost("/{itemGroupId:guid}/member/{memberId:guid}", Handle)
            .WithSummary("Add a new member to an item group")
            .WithDescription(
                "Grants another user access to an item group by adding them as a member. The authenticated user must already be a member. Returns 409 Conflict if the user is already a member."
            )
            .WithTags("Member")
            .WithName(Name);
    }

    private static async Task<
        Results<NoContent, UnauthorizedHttpResult, ForbidHttpResult, Conflict>
    > Handle(
        Guid itemGroupId,
        Guid memberId,
        ClaimsPrincipal user,
        HttpRequest httpRequest,
        AuditContext auditContext,
        AddMember useCase,
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
            AddMember.Outcome.Added => TypedResults.NoContent(),
            AddMember.Outcome.AlreadyAMember => TypedResults.Conflict(),
            AddMember.Outcome.NotAMember => TypedResults.Forbid(),
            _ => throw new UnreachableException(),
        };
    }
}
