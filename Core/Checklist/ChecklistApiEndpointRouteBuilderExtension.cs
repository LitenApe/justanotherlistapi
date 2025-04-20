using Core.Checklist;

namespace Core
{
    public static class ChecklistApiEndpointRouteBuilderExtension
    {
        public static WebApplication MapChecklistApi(this WebApplication app)
        {
            ArgumentNullException.ThrowIfNull(app);

            var checklistGroup = app.MapGroup("/api/list")
                .RequireAuthorization();

            // Item Group
            GetItemGroup.MapEndpoint(checklistGroup);
            GetItemGroups.MapEndpoint(checklistGroup);
            CreateItemGroup.MapEndpoint(checklistGroup);
            UpdateItemGroup.MapEndpoint(checklistGroup);
            DeleteItemGroup.MapEndpoint(checklistGroup);

            // Item
            CreateItem.MapEndpoint(checklistGroup);
            UpdateItem.MapEndpoint(checklistGroup);
            DeleteItem.MapEndpoint(checklistGroup);

            // Member
            GetMembers.MapEndpoint(checklistGroup);
            AddMember.MapEndpoint(checklistGroup);
            RemoveMember.MapEndpoint(checklistGroup);

            return app;
        }
    }
}
