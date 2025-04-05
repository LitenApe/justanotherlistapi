namespace JustAnotherListApi.Checklist;

public class EndpointConfiguration
{
    public static void MapEndpoints(WebApplication app)
    {
        // Item
        CreateItem.MapEndpoint(app);
        UpdateItem.MapEndpoint(app);
        DeleteItem.MapEndpoint(app);

        // Item Group
        GetItemGroup.MapEndpoint(app);
        GetItemGroups.MapEndpoint(app);
        CreateItemGroup.MapEndpoint(app);
        UpdateItemGroup.MapEndpoint(app);
        DeleteItemGroup.MapEndpoint(app);

        // Member
        GetMembers.MapEndpoint(app);
        AddMember.MapEndpoint(app);
        RemoveMember.MapEndpoint(app);
    }
}
