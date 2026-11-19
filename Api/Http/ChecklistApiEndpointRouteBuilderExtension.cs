using Api.AuditLog;

namespace Api.Http;

public static class ChecklistApiEndpointRouteBuilderExtension
{
    public static WebApplication MapChecklistApi(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        RouteGroupBuilder checklistGroup = app.MapGroup("/api/list")
            .RequireAuthorization()
            .AddEndpointFilter<AuditEndpointFilter>();

        // Item Group
        GetItemGroupEndpoint.MapEndpoint(checklistGroup);
        GetItemGroupsEndpoint.MapEndpoint(checklistGroup);
        CreateItemGroupEndpoint.MapEndpoint(checklistGroup);
        UpdateItemGroupEndpoint.MapEndpoint(checklistGroup);
        UpdateItemGroupSettingsEndpoint.MapEndpoint(checklistGroup);
        DeleteItemGroupEndpoint.MapEndpoint(checklistGroup);

        // Item
        CreateItemEndpoint.MapEndpoint(checklistGroup);
        UpdateItemEndpoint.MapEndpoint(checklistGroup);
        DeleteItemEndpoint.MapEndpoint(checklistGroup);

        // Member
        GetMembersEndpoint.MapEndpoint(checklistGroup);
        AddMemberEndpoint.MapEndpoint(checklistGroup);
        RemoveMemberEndpoint.MapEndpoint(checklistGroup);

        return app;
    }
}
