using System.Security.Claims;
using Checklist.Domain;
using Checklist.UseCases;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Api.Http;

internal static class GetItemGroupsEndpoint
{
    /// <summary>The endpoint's name, stored in <c>AuditLog.Operation</c>: never change it.</summary>
    internal const string Operation = "GetItemGroups";

    internal static void MapEndpoint(this IEndpointRouteBuilder builder)
    {
        builder
            .MapGet("/", Handle)
            .WithSummary("Get all item groups related to authenticated user with uncompleted items")
            .WithDescription(
                "Returns all item groups where the authenticated user is a member, each populated with their incomplete items only."
            )
            .WithTags(nameof(ItemGroup))
            .WithName(Operation);
    }

    private static async Task<Results<Ok<IReadOnlyList<ItemGroup>>, UnauthorizedHttpResult>> Handle(
        ClaimsPrincipal user,
        HttpRequest httpRequest,
        GetItemGroups useCase,
        CancellationToken ct
    )
    {
        if (user.ToCaller(httpRequest) is not { } caller)
        {
            return TypedResults.Unauthorized();
        }

        return TypedResults.Ok(await useCase.Execute(caller, ct));
    }
}
