using System.Data;
using Api.Accounts;
using Checklist.Domain;
using Checklist.Ports;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

/// <summary>
/// The accounts adapter as the app registers it, ASP.NET Core Identity on a test database, for
/// use-case and adapter tests. Dispose the provider when the test ends.
/// </summary>
internal static class TestAccounts
{
    public const string KnownPassword = "correct horse battery";

    public static ServiceProvider CreateServices(SqlConnection db, TimeProvider? clock = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        // Identity's SignInManager asks the authentication services which schemes exist.
        services.AddAuthentication();
        services.AddSingleton<IDbConnection>(db);
        services.AddSingleton(clock ?? TimeProvider.System);
        services
            .AddOptions<AuthOptions>()
            .Configure(options => options.SigningKey = ApiFactory.SigningKey);
        services.AddAccounts();
        return services.BuildServiceProvider();
    }

    public static IAccounts Accounts(this ServiceProvider services) =>
        services.GetRequiredService<IAccounts>();

    /// <summary>Creates an account with <see cref="KnownPassword"/>, and returns its user.</summary>
    public static async Task<User> SignUp(
        this IAccounts accounts,
        string email,
        string name = "Kim"
    ) =>
        await accounts.Create(
            ValidDisplayName(name),
            ValidEmail(email),
            ValidPassword(KnownPassword),
            TestContext.Current.CancellationToken
        ) ?? throw new InvalidOperationException($"{email} already has an account.");

    public static DisplayName ValidDisplayName(string value) =>
        DisplayName.TryCreate(value, out DisplayName? name)
            ? name
            : throw new ArgumentException("Not a valid name.", nameof(value));

    public static EmailAddress ValidEmail(string value) =>
        EmailAddress.TryCreate(value, out EmailAddress? email)
            ? email
            : throw new ArgumentException("Not a valid email address.", nameof(value));

    public static Password ValidPassword(string value) =>
        Password.TryCreate(value, out Password? password)
            ? password
            : throw new ArgumentException("Not a valid password.", nameof(value));

    /// <summary>An email no other test uses, so tests can share a database.</summary>
    public static string UniqueEmail(string name = "kim") =>
        $"{name}.{Guid.NewGuid():N}@example.com";
}
