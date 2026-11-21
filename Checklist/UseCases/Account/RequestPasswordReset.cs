using Checklist.Domain;
using Checklist.Ports;

namespace Checklist.UseCases;

/// <summary>
/// Emails a link to choose a new password, when an account uses the email. Anyone may ask,
/// without a token, and the answer is the same either way, so nobody learns whether the account
/// exists.
/// </summary>
public sealed class RequestPasswordReset(IAccounts accounts, IMailer mailer)
{
    public async Task Execute(EmailAddress email, CancellationToken ct)
    {
        if (await accounts.CreateResetCode(email, ct) is { } reset)
        {
            await mailer.SendPasswordReset(reset.User, reset.Code, ct);
        }
    }
}
