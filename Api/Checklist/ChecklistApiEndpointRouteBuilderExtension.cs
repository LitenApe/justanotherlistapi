using Api.AuditLog;
using Api.Checklist;
using Api.Http;

namespace Api;

public static class ChecklistApiEndpointRouteBuilderExtension
{
    public static WebApplication MapChecklistApi(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        RouteGroupBuilder checklistGroup = app.MapGroup("/api/list")
            .RequireAuthorization()
            .AddEndpointFilter<AuditEndpointFilter>();

        // Item Group
        GetItemGroup.MapEndpoint(checklistGroup);
        GetItemGroups.MapEndpoint(checklistGroup);
        CreateItemGroup.MapEndpoint(checklistGroup);
        UpdateItemGroup.MapEndpoint(checklistGroup);
        UpdateItemGroupSettings.MapEndpoint(checklistGroup);
        DeleteItemGroup.MapEndpoint(checklistGroup);

        // Item
        CreateItem.MapEndpoint(checklistGroup);
        UpdateItem.MapEndpoint(checklistGroup);
        DeleteItem.MapEndpoint(checklistGroup);

        // Member
        GetMembersEndpoint.MapEndpoint(checklistGroup);
        AddMemberEndpoint.MapEndpoint(checklistGroup);
        RemoveMemberEndpoint.MapEndpoint(checklistGroup);

        return app;
    }
}
