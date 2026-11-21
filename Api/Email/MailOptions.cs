namespace Api.Email;

/// <summary>
/// Where the app's emails go out and what their links point at: the configuration section
/// <c>Smtp</c>, plus <c>App:ClientUrl</c> (see specifications/accounts.md).
/// </summary>
internal sealed class MailOptions
{
    public const string Section = "Smtp";

    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 25;

    public bool EnableSsl { get; set; }

    /// <summary>Signs in to the SMTP server when set; local servers like Mailpit need none.</summary>
    public string? UserName { get; set; }

    public string? Password { get; set; }

    /// <summary>Who the emails come from.</summary>
    public string From { get; set; } = "JustAnotherList <no-reply@justanotherlist.invalid>";

    /// <summary>The web client's address, which the links in emails start with: <c>App:ClientUrl</c>.</summary>
    public string ClientUrl { get; set; } = "http://localhost:5173";
}
