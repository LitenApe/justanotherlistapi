using System.ComponentModel;
using Api.AuditLog;
using Checklist.Domain;
using Checklist.UseCases;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Api.Http;

internal static class SignInEndpoint
{
    /// <summary>The endpoint's name, stored in <c>AuditLog.Operation</c>: never change it.</summary>
    internal const string Operation = "SignIn";

    internal static void MapEndpoint(this IEndpointRouteBuilder builder)
    {
        builder
            .MapPost("/sign-in", Handle)
            .WithSummary("Sign in")
            .WithDescription(
                "Signs someone in with their email address and password. Returns 401 Unauthorized when no account uses the email or the password is wrong, without saying which, and 423 Locked after five wrong passwords in a row, for five minutes. Needs no token."
            )
            .WithTags("Account")
            .WithName(Operation);
    }

    private static async Task<
        Results<Ok<SessionResponse>, BadRequest, UnauthorizedHttpResult, StatusCodeHttpResult>
    > Handle(Request request, AuditContext auditContext, SignIn useCase, CancellationToken ct)
    {
        if (
            !EmailAddress.TryCreate(request.Email, out EmailAddress? email)
            || string.IsNullOrEmpty(request.Password)
        )
        {
            return TypedResults.BadRequest();
        }

        SignIn.Result result = await useCase.Execute(email, request.Password, ct);
        auditContext.TargetUserId = result.Session?.User.Id;
        return result switch
        {
            { Outcome: SignIn.Outcome.SignedIn, Session: { } session } => TypedResults.Ok(
                SessionResponse.From(session)
            ),
            { Outcome: SignIn.Outcome.LockedOut } => TypedResults.StatusCode(
                StatusCodes.Status423Locked
            ),
            _ => TypedResults.Unauthorized(),
        };
    }

    public sealed record Request
    {
        [Description("The email address of the account")]
        public string? Email { get; init; }

        [Description("The account's password")]
        public string? Password { get; init; }
    }
}
