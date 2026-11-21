using System.ComponentModel;
using System.Diagnostics;
using System.Security.Claims;
using Api.AuditLog;
using Checklist.Domain;
using Checklist.UseCases;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Api.Http;

internal static class AddMemberEndpoint
{
    /// <summary>The endpoint's name, stored in <c>AuditLog.Operation</c>: never change it.</summary>
    internal const string Operation = "AddMember";

    internal static void MapEndpoint(this IEndpointRouteBuilder builder)
    {
        builder
            .MapPost("/{itemGroupId:guid}/member", Handle)
            .WithSummary("Add a new member to an item group")
            .WithDescription(
                "Grants someone access to an item group by the email address they signed up with, and returns them as a member with their name; the other members never see the email. The authenticated user must already be a member. Returns 404 Not Found when no account uses the email address, and 409 Conflict if that person is already a member."
            )
            .WithTags("Member")
            .WithName(Operation);
    }

    private static async Task<
        Results<
            Ok<Member>,
            BadRequest,
            UnauthorizedHttpResult,
            ForbidHttpResult,
            NotFound,
            Conflict
        >
    > Handle(
        Guid itemGroupId,
        Request request,
        ClaimsPrincipal user,
        HttpRequest httpRequest,
        AuditContext auditContext,
        AddMember useCase,
        CancellationToken ct
    )
    {
        if (!EmailAddress.TryCreate(request.Email, out EmailAddress? email))
        {
            return TypedResults.BadRequest();
        }
        if (user.ToCaller(httpRequest) is not { } caller)
        {
            return TypedResults.Unauthorized();
        }

        AddMember.Result result = await useCase.Execute(caller, itemGroupId, email, ct);
        // The target is known once the email led to an account.
        auditContext.TargetUserId = result.Member?.Id;
        return result switch
        {
            { Outcome: AddMember.Outcome.Added, Member: { } member } => TypedResults.Ok(member),
            { Outcome: AddMember.Outcome.AlreadyAMember } => TypedResults.Conflict(),
            { Outcome: AddMember.Outcome.NoAccount } => TypedResults.NotFound(),
            { Outcome: AddMember.Outcome.NotAMember } => TypedResults.Forbid(),
            _ => throw new UnreachableException(),
        };
    }

    public sealed record Request
    {
        [Description("The email address the person to add signed up with, in any case")]
        public string? Email { get; init; }
    }
}
