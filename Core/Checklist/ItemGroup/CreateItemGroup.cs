﻿using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Core.Checklist;

public static class CreateItemGroup
{
    public static void MapEndpoint(this RouteGroupBuilder builder)
    {
        builder.MapPost("/", Execute)
            .WithSummary("Create a new item group")
            .WithTags(nameof(ItemGroup))
            .WithName(nameof(CreateItemGroup));
    }

    public static async Task<Results<Created<ItemGroup>, BadRequest, UnauthorizedHttpResult>> Execute(
        Request request,
        ClaimsPrincipal claimsPrincipal,
        DatabaseContext db,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(request.Name.Trim()))
        {
            return TypedResults.BadRequest();
        }

        var userId = claimsPrincipal.GetUserId();
        if (userId is null)
        {
            return TypedResults.Unauthorized();
        }

        var itemGroup = await CreateData((Guid)userId, request, db, ct);
        return TypedResults.Created($"/list/{itemGroup.Id}", itemGroup);
    }

    internal static async Task<ItemGroup> CreateData(Guid userId, Request request, DatabaseContext db, CancellationToken ct)
    {
        var itemGroup = new ItemGroup { Name = request.Name };
        await db.ItemGroups.AddAsync(itemGroup, ct);

        var member = new Member { ItemGroupId = itemGroup.Id, MemberId = userId };
        await db.Members.AddAsync(member, ct);
        await db.SaveChangesAsync(ct);
        return itemGroup;
    }

    public class Request
    {
        public required string Name { get; set; }
    }
}
