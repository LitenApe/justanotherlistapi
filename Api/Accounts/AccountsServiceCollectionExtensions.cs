using Checklist.Domain;
using Checklist.Ports;
using Microsoft.AspNetCore.Identity;

namespace Api.Accounts;

internal static class AccountsServiceCollectionExtensions
{
    /// <summary>
    /// Plugs ASP.NET Core Identity in behind <see cref="IAccounts"/>, with its rules set to the
    /// domain's (specifications/accounts.md), and <see cref="JwtTokenIssuer"/> behind
    /// <see cref="ITokenIssuer"/>.
    /// </summary>
    internal static IServiceCollection AddAccounts(this IServiceCollection services)
    {
        services
            .AddIdentityCore<Account>(options =>
            {
                // The email is the user name: unique, and any character an email can hold.
                options.User.RequireUniqueEmail = true;
                options.User.AllowedUserNameCharacters = "";

                // The domain's Password checks the length; Identity's other rules stay off.
                options.Password.RequiredLength = Password.MinLength;
                options.Password.RequiredUniqueChars = 1;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            })
            .AddUserStore<SqlAccountStore>()
            .AddSignInManager();

        services.AddScoped<IAccounts, IdentityAccounts>();
        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();
        return services;
    }
}
