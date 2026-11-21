using Api.AuditLog;

namespace Api.Http;

public static class AccountApiEndpointRouteBuilderExtension
{
    /// <summary>
    /// The account endpoints under <c>/api/auth</c>. They need no token, since they're how people
    /// get one, so each client may only call them so often (<see cref="AuthRateLimit"/>).
    /// </summary>
    public static WebApplication MapAccountApi(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        RouteGroupBuilder accountGroup = app.MapGroup("/api/auth")
            .AllowAnonymous()
            .RequireRateLimiting(AuthRateLimit.Policy)
            .AddEndpointFilter<AuditEndpointFilter>();

        SignUpEndpoint.MapEndpoint(accountGroup);
        SignInEndpoint.MapEndpoint(accountGroup);
        ForgotPasswordEndpoint.MapEndpoint(accountGroup);
        ResetPasswordEndpoint.MapEndpoint(accountGroup);

        return app;
    }
}
