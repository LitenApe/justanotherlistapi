using System.Diagnostics;
using System.Security.Claims;
using Checklist.Domain;
using Checklist.UseCases;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Api.Http;

internal static class DeleteItemGroupEndpoint
{
    /// <summary>The endpoint's name, stored in <c>AuditLog.Operation</c>: never change it.</summary>
    internal const string Operation = "DeleteItemGroup";

    internal static void MapEndpoint(this IEndpointRouteBuilder builder)
    {
        builder
            .MapDelete("/{itemGroupId:guid}", Handle)
            .WithSummary("Delete an item group")
            .WithDescription(
                "Permanently deletes an item group and all its associated items and members. The authenticated user must be a member of the group."
            )
            .WithTags(nameof(ItemGroup))
            .WithName(Operation);
    }

    private static async Task<Results<NoContent, UnauthorizedHttpResult, ForbidHttpResult>> Handle(
        Guid itemGroupId,
        ClaimsPrincipal user,
        HttpRequest httpRequest,
        DeleteItemGroup useCase,
        CancellationToken ct
    )
    {
        if (user.ToCaller(httpRequest) is not { } caller)
        {
            return TypedResults.Unauthorized();
        }

        return await useCase.Execute(caller, itemGroupId, ct) switch
        {
            DeleteItemGroup.Outcome.Deleted => TypedResults.NoContent(),
            DeleteItemGroup.Outcome.NotAMember => TypedResults.Forbid(),
            _ => throw new UnreachableException(),
        };
    }
}
