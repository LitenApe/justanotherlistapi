using System.Security.Claims;
using Checklist.Domain;
using Checklist.UseCases;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Api.Http;

internal static class GetItemGroupEndpoint
{
    /// <summary>The endpoint's name, stored in <c>AuditLog.Operation</c>: never change it.</summary>
    internal const string Operation = "GetItemGroup";

    internal static void MapEndpoint(this IEndpointRouteBuilder builder)
    {
        builder
            .MapGet("/{itemGroupId:guid}", Handle)
            .WithSummary("Get an item group")
            .WithDescription(
                "Returns a single item group by ID, including all its items and members. The authenticated user must be a member of the group."
            )
            .WithTags(nameof(ItemGroup))
            .WithName(Operation);
    }

    private static async Task<
        Results<Ok<ItemGroup>, UnauthorizedHttpResult, ForbidHttpResult>
    > Handle(
        Guid itemGroupId,
        ClaimsPrincipal user,
        HttpRequest httpRequest,
        GetItemGroup useCase,
        CancellationToken ct
    )
    {
        if (user.ToCaller(httpRequest) is not { } caller)
        {
            return TypedResults.Unauthorized();
        }

        if (await useCase.Execute(caller, itemGroupId, ct) is not { } itemGroup)
        {
            return TypedResults.Forbid();
        }

        return TypedResults.Ok(itemGroup);
    }
}
