using System.ComponentModel;
using System.Security.Claims;
using Api.AuditLog;
using Checklist.Domain;
using Checklist.UseCases;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Api.Http;

internal static class CreateItemGroupEndpoint
{
    /// <summary>The endpoint's name, stored in <c>AuditLog.Operation</c>: never change it.</summary>
    internal const string Operation = "CreateItemGroup";

    internal static void MapEndpoint(this IEndpointRouteBuilder builder)
    {
        builder
            .MapPost("/", Handle)
            .WithSummary("Create a new item group")
            .WithDescription(
                "Creates a new item group and automatically adds the authenticated user as its first member."
            )
            .WithTags(nameof(ItemGroup))
            .WithName(Operation);
    }

    private static async Task<
        Results<Created<ItemGroup>, BadRequest, UnauthorizedHttpResult>
    > Handle(
        Request request,
        ClaimsPrincipal user,
        HttpRequest httpRequest,
        AuditContext auditContext,
        CreateItemGroup useCase,
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

        ItemGroup itemGroup = await useCase.Execute(caller, name, ct);

        // POST /api/list has no itemGroupId route value for the audit filter to read.
        auditContext.ResourceId = itemGroup.Id;
        return TypedResults.Created($"/list/{itemGroup.Id}", itemGroup);
    }

    public sealed record Request
    {
        [Description("Name of the item group")]
        public required string Name { get; init; }
    }
}
