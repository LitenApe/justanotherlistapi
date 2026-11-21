using System.ComponentModel;
using System.Security.Claims;
using Checklist.Domain;
using Checklist.UseCases;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Api.Http;

internal static class ChangeNameEndpoint
{
    /// <summary>The endpoint's name, stored in <c>AuditLog.Operation</c>: never change it.</summary>
    internal const string Operation = "ChangeName";

    internal static void MapEndpoint(this IEndpointRouteBuilder builder)
    {
        builder
            .MapPut("", Handle)
            .WithSummary("Change your name")
            .WithDescription(
                "Changes what the signed-in user is called, and returns them with the new name. Other members see it the next time they load a list. Returns 400 when the name breaks its rule (1 to 50 characters once trimmed), and 404 when the signed-in user has no account."
            )
            .WithTags("Account")
            .WithName(Operation);
    }

    private static async Task<
        Results<Ok<User>, ValidationProblem, UnauthorizedHttpResult, NotFound>
    > Handle(
        Request request,
        ClaimsPrincipal user,
        HttpRequest httpRequest,
        ChangeName useCase,
        CancellationToken ct
    )
    {
        if (!DisplayName.TryCreate(request.Name, out DisplayName? name))
        {
            return TypedResults.ValidationProblem(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["name"] = [$"Enter a name of 1 to {DisplayName.MaxLength} characters."],
                }
            );
        }
        if (user.ToCaller(httpRequest) is not { } caller)
        {
            return TypedResults.Unauthorized();
        }

        return await useCase.Execute(caller, name, ct) is { } renamed
            ? TypedResults.Ok(renamed)
            : TypedResults.NotFound();
    }

    public sealed record Request
    {
        [Description("The new name: 1 to 50 characters once trimmed")]
        public string? Name { get; init; }
    }
}
