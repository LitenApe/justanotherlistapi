using System.Security.Claims;
using Checklist.UseCases;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Api.Http;

internal static class GetMembersEndpoint
{
    /// <summary>The endpoint's name, stored in <c>AuditLog.Operation</c>: never change it.</summary>
    internal const string Operation = "GetMembers";

    internal static void MapEndpoint(this IEndpointRouteBuilder builder)
    {
        builder
            .MapGet("/{itemGroupId:guid}/member", Handle)
            .WithSummary("Get members of an item group")
            .WithDescription(
                "Returns the list of user IDs that are members of the specified item group. The authenticated user must already be a member."
            )
            .WithTags("Member")
            .WithName(Operation);
    }

    private static async Task<
        Results<Ok<IReadOnlyList<Guid>>, UnauthorizedHttpResult, ForbidHttpResult>
    > Handle(
        Guid itemGroupId,
        ClaimsPrincipal user,
        HttpRequest httpRequest,
        GetMembers useCase,
        CancellationToken ct
    )
    {
        if (user.ToCaller(httpRequest) is not { } caller)
        {
            return TypedResults.Unauthorized();
        }

        return await useCase.Execute(caller, itemGroupId, ct) is { } members
            ? TypedResults.Ok(members)
            : TypedResults.Forbid();
    }
}
