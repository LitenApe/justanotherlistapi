using System.ComponentModel;
using System.Diagnostics;
using System.Security.Claims;
using Checklist.Domain;
using Checklist.UseCases;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Api.Http;

internal static class UpdateItemEndpoint
{
    /// <summary>The endpoint's name, stored in <c>AuditLog.Operation</c>: never change it.</summary>
    internal const string Operation = "UpdateItem";

    internal static void MapEndpoint(this IEndpointRouteBuilder builder)
    {
        builder
            .MapPut("/{itemGroupId:guid}/{itemId:guid}", Handle)
            .WithSummary("Update an item")
            .WithDescription(
                "Updates the name, description, or completion status of an existing item. The authenticated user must be a member of the item group."
            )
            .WithTags(nameof(Item))
            .WithName(Operation);
    }

    private static async Task<
        Results<NoContent, BadRequest, UnauthorizedHttpResult, ForbidHttpResult>
    > Handle(
        Guid itemGroupId,
        Guid itemId,
        Request request,
        ClaimsPrincipal user,
        HttpRequest httpRequest,
        UpdateItem useCase,
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

        var input = new ItemInput(name, request.Description, request.IsComplete);
        return await useCase.Execute(caller, itemGroupId, itemId, input, ct) switch
        {
            UpdateItem.Outcome.Updated => TypedResults.NoContent(),
            UpdateItem.Outcome.NotAMember => TypedResults.Forbid(),
            _ => throw new UnreachableException(),
        };
    }

    public sealed record Request
    {
        [Description("Name of the item")]
        public required string Name { get; init; }

        [Description("Description of the item")]
        public string? Description { get; init; }

        [DefaultValue(false)]
        [Description("Indicates if the item is complete")]
        public bool IsComplete { get; init; }
    }
}
