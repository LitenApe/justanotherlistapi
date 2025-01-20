using JustAnotherListAPI.Checklist.Controller;

namespace JustAnotherListAPI.Checklist
{
    public static class ChecklistWebExtension
    {
        public static WebApplication MapEndpoints(this WebApplication app)
        {
            var builder = app.MapGroup("/api/list");

            builder.MapGet("/", ItemGroupController.GetAllItemGroups).WithTags("Checklist - Item Group");
            builder.MapPost("/", ItemGroupController.CreateItemGroup).WithTags("Checklist - Item Group");
            builder.MapGet("/{itemGroupId}", ItemGroupController.GetItemGroup).WithTags("Checklist - Item Group");
            builder.MapPut("/{itemGroupId}", ItemGroupController.UpdateItemGroup).WithTags("Checklist - Item Group");
            builder.MapDelete("/{itemGroupId}", ItemGroupController.DeleteItemGroup).WithTags("Checklist - Item Group");

            builder.MapPost("/{itemGroupId}", ItemController.CreateItem).WithTags("Checklist - Item");
            builder.MapPut("/{itemGroupId}/{itemId}", ItemController.UpdateItem).WithTags("Checklist - Item");
            builder.MapDelete("/{itemGroupId}/{itemId}", ItemController.DeleteItem).WithTags("Checklist - Item");

            builder.MapGet("/{itemGroupId}/member", MemberController.GetAllMembers).WithTags("Checklist - Item Group Member");
            builder.MapPost("/{itemGroupId}/member/{memberId}", MemberController.AddMember).WithTags("Checklist - Item Group Member");
            builder.MapDelete("/{itemGroupId}/member/{memberId}", MemberController.RemoveMember).WithTags("Checklist - Item Group Member");

            return app;
        }
    }

}