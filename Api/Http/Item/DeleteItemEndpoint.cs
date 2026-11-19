using System.Diagnostics;
using System.Security.Claims;
using Checklist.Domain;
using Checklist.UseCases;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Api.Http;

internal static class DeleteItemEndpoint
{
    /// <summary>The endpoint's name, stored in <c>AuditLog.Operation</c>: never change it.</summary>
    internal const string Operation = "DeleteItem";

    internal static void MapEndpoint(this IEndpointRouteBuilder builder)
    {
        builder
            .MapDelete("/{itemGroupId:guid}/{itemId:guid}", Handle)
            .WithSummary("Delete an item")
            .WithDescription(
                "Permanently deletes an item from the specified item group. The authenticated user must be a member of the group."
            )
            .WithTags(nameof(Item))
            .WithName(Operation);
    }

    private static async Task<Results<NoContent, UnauthorizedHttpResult, ForbidHttpResult>> Handle(
        Guid itemGroupId,
        Guid itemId,
        ClaimsPrincipal user,
        HttpRequest httpRequest,
        DeleteItem useCase,
        CancellationToken ct
    )
    {
        if (user.ToCaller(httpRequest) is not { } caller)
        {
            return TypedResults.Unauthorized();
        }

        return await useCase.Execute(caller, itemGroupId, itemId, ct) switch
        {
            DeleteItem.Outcome.Deleted => TypedResults.NoContent(),
            DeleteItem.Outcome.NotAMember => TypedResults.Forbid(),
            _ => throw new UnreachableException(),
        };
    }
}
