using System.ComponentModel;
using System.Diagnostics;
using Api.AuditLog;
using Checklist.Domain;
using Checklist.UseCases;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Api.Http;

internal static class ResetPasswordEndpoint
{
    /// <summary>The endpoint's name, stored in <c>AuditLog.Operation</c>: never change it.</summary>
    internal const string Operation = "ResetPassword";

    internal static void MapEndpoint(this IEndpointRouteBuilder builder)
    {
        builder
            .MapPost("/reset-password", Handle)
            .WithSummary("Choose a new password")
            .WithDescription(
                "Sets a new password with the user ID and code from a reset email, and ends any lockout. Returns 400 with the problem by field: `code` when the code isn't the account's, has expired or was already used, `password` when the new one breaks its rule. Needs no token."
            )
            .WithTags("Account")
            .WithName(Operation);
    }

    private static async Task<Results<NoContent, ValidationProblem>> Handle(
        Request request,
        AuditContext auditContext,
        ResetPassword useCase,
        CancellationToken ct
    )
    {
        // Set first, so refused attempts are audited with the account they were for.
        auditContext.TargetUserId = request.UserId;

        Dictionary<string, string[]> problems = new(StringComparer.Ordinal);
        if (request.UserId is null || string.IsNullOrEmpty(request.Code))
        {
            problems["code"] = ["The link is incomplete. Ask for a new one."];
        }
        if (!Password.TryCreate(request.Password, out Password? password))
        {
            problems["password"] =
            [
                $"Use {Password.MinLength} to {Password.MaxLength} characters.",
            ];
        }
        if (request.UserId is not { } userId || request.Code is not { } code || password is null)
        {
            return TypedResults.ValidationProblem(problems);
        }

        return await useCase.Execute(userId, code, password, ct) switch
        {
            ResetPassword.Outcome.Reset => TypedResults.NoContent(),
            ResetPassword.Outcome.InvalidCode => TypedResults.ValidationProblem(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["code"] = ["This link has expired or was already used. Ask for a new one."],
                }
            ),
            _ => throw new UnreachableException(),
        };
    }

    public sealed record Request
    {
        [Description("The user ID from the link in the reset email")]
        public Guid? UserId { get; init; }

        [Description("The code from the link in the reset email")]
        public string? Code { get; init; }

        [Description("The new password: 12 to 128 characters")]
        public string? Password { get; init; }
    }
}
