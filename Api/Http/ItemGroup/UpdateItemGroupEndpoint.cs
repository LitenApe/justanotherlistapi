using System.ComponentModel;
using System.Diagnostics;
using System.Security.Claims;
using Checklist.Domain;
using Checklist.UseCases;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Api.Http;

internal static class UpdateItemGroupEndpoint
{
    /// <summary>The endpoint's name, stored in <c>AuditLog.Operation</c>: never change it.</summary>
    internal const string Operation = "UpdateItemGroup";

    internal static void MapEndpoint(this IEndpointRouteBuilder builder)
    {
        builder
            .MapPut("/{itemGroupId:guid}", Handle)
            .WithSummary("Update an item group")
            .WithDescription(
                "Updates the name of an existing item group. The authenticated user must be a member of the group."
            )
            .WithTags(nameof(ItemGroup))
            .WithName(Operation);
    }

    private static async Task<
        Results<NoContent, BadRequest, UnauthorizedHttpResult, ForbidHttpResult>
    > Handle(
        Guid itemGroupId,
        Request request,
        ClaimsPrincipal user,
        HttpRequest httpRequest,
        UpdateItemGroup useCase,
        CancellationToken ct
    )
    {
        if (!Name.TryCreate(request.Name, out Name? name))
        {
            return TypedResults.BadRequest();
        }
        if (user.ToCaller(httpRequest) is not { } caller)
        {
            return TypedResults.Unauthorized();
        }

        return await useCase.Execute(caller, itemGroupId, name, ct) switch
        {
            UpdateItemGroup.Outcome.Updated => TypedResults.NoContent(),
            UpdateItemGroup.Outcome.NotAMember => TypedResults.Forbid(),
            _ => throw new UnreachableException(),
        };
    }

    public sealed record Request
    {
        [Description("Name of the item group")]
        public required string Name { get; init; }
    }
}
