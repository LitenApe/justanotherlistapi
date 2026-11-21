using Api.AuditLog;

namespace Api.Http;

public static class AccountApiEndpointRouteBuilderExtension
{
    /// <summary>
    /// The account endpoints: under <c>/api/auth</c>, the ones that need no token, since they're
    /// how people get one, so each client may only call them so often (<see cref="AuthRateLimit"/>);
    /// under <c>/api/me</c>, the signed-in user's own account.
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

        // The signed-in user's own account.
        RouteGroupBuilder meGroup = app.MapGroup("/api/me")
            .RequireAuthorization()
            .AddEndpointFilter<AuditEndpointFilter>();

        ChangeNameEndpoint.MapEndpoint(meGroup);

        return app;
    }
}
