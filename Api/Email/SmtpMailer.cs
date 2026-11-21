using System.Net;
using System.Net.Mail;
using Checklist.Domain;
using Checklist.Ports;
using Microsoft.Extensions.Options;

namespace Api.Email;

/// <summary>
/// <see cref="IMailer"/> over SMTP: Mailpit in development and the end-to-end tests, a real mail
/// server in production. A failure to send is logged, not thrown, so the person asking for a reset
/// gets the same answer whether or not an account uses the email.
/// </summary>
internal sealed partial class SmtpMailer(IOptions<MailOptions> options, ILogger<SmtpMailer> logger)
    : IMailer
{
    public async Task SendPasswordReset(User user, string code, CancellationToken ct)
    {
        MailOptions settings = options.Value;
        try
        {
            using MailMessage message = PasswordResetMessage(settings, user, code);
            using var client = new SmtpClient(settings.Host, settings.Port)
            {
                EnableSsl = settings.EnableSsl,
            };
            if (!string.IsNullOrEmpty(settings.UserName))
            {
                client.Credentials = new NetworkCredential(settings.UserName, settings.Password);
            }
            await client.SendMailAsync(message, ct);
        }
        catch (Exception ex) when (ex is SmtpException or FormatException)
        {
            LogSendFailed(logger, user.Id, ex);
        }
    }

    /// <summary>The email with the link that opens the client's reset page with the code.</summary>
    internal static MailMessage PasswordResetMessage(MailOptions settings, User user, string code)
    {
        string link =
            $"{settings.ClientUrl.TrimEnd('/')}/reset-password?user={user.Id}&code={Uri.EscapeDataString(code)}";
        return new MailMessage(
            new MailAddress(settings.From),
            new MailAddress(user.Email, user.Name)
        )
        {
            Subject = "Choose a new password for JustAnotherList",
            Body = $"""
                Hi {user.Name},

                Someone asked to choose a new password for your JustAnotherList account. If it was you, open this link within an hour:

                {link}

                The link works once. If you didn't ask, ignore this email: your password stays as it is.
                """,
            IsBodyHtml = false,
        };
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Couldn't send the password reset email for user {UserId}."
    )]
    private static partial void LogSendFailed(ILogger logger, Guid userId, Exception ex);
}
