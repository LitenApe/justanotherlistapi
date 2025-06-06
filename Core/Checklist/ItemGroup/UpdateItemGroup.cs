﻿using System.ComponentModel;
using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Core.Checklist;

public static class UpdateItemGroup
{
    public static void MapEndpoint(this RouteGroupBuilder builder)
    {
        builder.MapPut("/{itemGroupId:guid}", Execute)
            .WithSummary("Update a item group")
            .WithTags(nameof(ItemGroup))
            .WithName(nameof(UpdateItemGroup));
    }

    public static async Task<Results<NoContent, BadRequest, UnauthorizedHttpResult, ForbidHttpResult>> Execute(
        Guid itemGroupId,
        Request request,
        ClaimsPrincipal claimsPrincipal,
        DatabaseContext db,
        CancellationToken ct)
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

        var isMember = await db.IsMember(itemGroupId, userId, ct);
        if (!isMember)
        {
            return TypedResults.Forbid();
        }

        await UpdateData(itemGroupId, request, db, ct);
        return TypedResults.NoContent();
    }

    internal static async Task UpdateData(Guid itemGroupId, Request request, DatabaseContext db, CancellationToken ct)
    {
        var itemGroup = await db.ItemGroups.FindAsync([itemGroupId], cancellationToken: ct);
        if (itemGroup is null)
        {
            return;
        }

        itemGroup.Name = request.Name;

        db.ItemGroups.Update(itemGroup);
        await db.SaveChangesAsync(ct);
    }

    public class Request
    {
        [Description("Name of the item group")]
        public required string Name { get; set; }
    }
}
