using System.ComponentModel;
using Api.AuditLog;
using Checklist.Domain;
using Checklist.UseCases;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Api.Http;

internal static class SignUpEndpoint
{
    /// <summary>The endpoint's name, stored in <c>AuditLog.Operation</c>: never change it.</summary>
    internal const string Operation = "SignUp";

    internal static void MapEndpoint(this IEndpointRouteBuilder builder)
    {
        builder
            .MapPost("/sign-up", Handle)
            .WithSummary("Create an account")
            .WithDescription(
                "Creates an account with a name, an email address and a password, and signs its owner in. Returns 400 with the problem for each field that breaks its rule, and 409 Conflict when another account already uses the email address, ignoring case. Needs no token."
            )
            .WithTags("Account")
            .WithName(Operation);
    }

    private static async Task<
        Results<Created<SessionResponse>, ValidationProblem, Conflict>
    > Handle(Request request, AuditContext auditContext, SignUp useCase, CancellationToken ct)
    {
        Dictionary<string, string[]> problems = new(StringComparer.Ordinal);
        if (!DisplayName.TryCreate(request.Name, out DisplayName? name))
        {
            problems["name"] = [$"Enter a name of 1 to {DisplayName.MaxLength} characters."];
        }
        if (!EmailAddress.TryCreate(request.Email, out EmailAddress? email))
        {
            problems["email"] = ["Enter an email address, like name@example.com."];
        }
        if (!Password.TryCreate(request.Password, out Password? password))
        {
            problems["password"] =
            [
                $"Use {Password.MinLength} to {Password.MaxLength} characters.",
            ];
        }
        if (name is null || email is null || password is null)
        {
            return TypedResults.ValidationProblem(problems);
        }

        if (await useCase.Execute(name, email, password, ct) is not { } session)
        {
            return TypedResults.Conflict();
        }

        auditContext.TargetUserId = session.User.Id;
        return TypedResults.Created((string?)null, SessionResponse.From(session));
    }

    public sealed record Request
    {
        [Description("What to call the new user: 1 to 50 characters once trimmed")]
        public string? Name { get; init; }

        [Description("The email address to sign in with; another account mustn't use it")]
        public string? Email { get; init; }

        [Description("The password to sign in with: 12 to 128 characters")]
        public string? Password { get; init; }
    }
}
