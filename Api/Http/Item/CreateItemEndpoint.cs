using System.ComponentModel;
using System.Security.Claims;
using Api.AuditLog;
using Checklist.Domain;
using Checklist.UseCases;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Api.Http;

internal static class CreateItemEndpoint
{
    /// <summary>The endpoint's name, stored in <c>AuditLog.Operation</c>: never change it.</summary>
    internal const string Operation = "CreateItem";

    internal static void MapEndpoint(this IEndpointRouteBuilder builder)
    {
        builder
            .MapPost("/{itemGroupId:guid}", Handle)
            .WithSummary("Create an item")
            .WithDescription(
                "Creates a new item within the specified item group. The authenticated user must be a member of the group."
            )
            .WithTags(nameof(Item))
            .WithName(Operation);
    }

    private static async Task<
        Results<Created<Item>, BadRequest, UnauthorizedHttpResult, ForbidHttpResult>
    > Handle(
        Guid itemGroupId,
        Request request,
        ClaimsPrincipal user,
        HttpRequest httpRequest,
        AuditContext auditContext,
        CreateItem useCase,
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
        if (await useCase.Execute(caller, itemGroupId, input, ct) is not { } item)
        {
            return TypedResults.Forbid();
        }

        auditContext.SubResourceId = item.Id;
        return TypedResults.Created($"/list/{itemGroupId}/{item.Id}", item);
    }

    public sealed record Request
    {
        [Description("Name of the item")]
        public required string Name { get; init; }

        [Description("Description of the item")]
        public string? Description { get; init; }

        [DefaultValue(false)]
        [Description("Indicates whether the item is complete")]
        public bool IsComplete { get; init; }
    }
}
