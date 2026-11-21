using System.ComponentModel;
using Checklist.Domain;
using Checklist.UseCases;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Api.Http;

internal static class ForgotPasswordEndpoint
{
    /// <summary>The endpoint's name, stored in <c>AuditLog.Operation</c>: never change it.</summary>
    internal const string Operation = "RequestPasswordReset";

    internal static void MapEndpoint(this IEndpointRouteBuilder builder)
    {
        builder
            .MapPost("/forgot-password", Handle)
            .WithSummary("Ask for a link to choose a new password")
            .WithDescription(
                "Emails a link to choose a new password, which works for an hour and once, when an account uses the email address. Answers 202 Accepted either way, so nobody learns whether the account exists. Needs no token."
            )
            .WithTags("Account")
            .WithName(Operation);
    }

    private static async Task<Results<Accepted, BadRequest>> Handle(
        Request request,
        RequestPasswordReset useCase,
        CancellationToken ct
    )
    {
        if (!EmailAddress.TryCreate(request.Email, out EmailAddress? email))
        {
            return TypedResults.BadRequest();
        }

        await useCase.Execute(email, ct);
        return TypedResults.Accepted((string?)null);
    }

    public sealed record Request
    {
        [Description("The email address of the account whose password was forgotten")]
        public string? Email { get; init; }
    }
}
