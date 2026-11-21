using System.Diagnostics;
using System.Security.Claims;
using Api.AuditLog;
using Checklist.Domain;
using Checklist.UseCases;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Api.Http;

internal static class AddMemberEndpoint
{
    /// <summary>The endpoint's name, stored in <c>AuditLog.Operation</c>: never change it.</summary>
    internal const string Operation = "AddMember";

    internal static void MapEndpoint(this IEndpointRouteBuilder builder)
    {
        builder
            .MapPost("/{itemGroupId:guid}/member/{memberId:guid}", Handle)
            .WithSummary("Add a new member to an item group")
            .WithDescription(
                "Grants another user access to an item group by adding them as a member, and returns them with their name. The authenticated user must already be a member. Returns 409 Conflict if the user is already a member."
            )
            .WithTags("Member")
            .WithName(Operation);
    }

    private static async Task<
        Results<Ok<Member>, UnauthorizedHttpResult, ForbidHttpResult, Conflict>
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
            { Outcome: AddMember.Outcome.Added, Member: { } member } => TypedResults.Ok(member),
            { Outcome: AddMember.Outcome.AlreadyAMember } => TypedResults.Conflict(),
            { Outcome: AddMember.Outcome.NotAMember } => TypedResults.Forbid(),
            _ => throw new UnreachableException(),
        };
    }
}
