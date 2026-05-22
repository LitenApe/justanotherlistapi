import type { Item } from "@shared/types";
import { itemsResource, pendingService } from "@shared/api";

export function createItem(
  groupId: string,
  body: { name: string; description?: string; isComplete?: boolean },
): Promise<Item> {
  return pendingService.track(
    "items/create",
    itemsResource.create(groupId, body),
  );
}

export function updateItem(
  groupId: string,
  itemId: string,
  body: { name: string; description?: string; isComplete?: boolean },
): Promise<void> {
  return pendingService.track(
    "items/update",
    itemsResource.update(groupId, itemId, body),
  );
}

export function deleteItem(groupId: string, itemId: string): Promise<void> {
  return pendingService.track(
    "items/delete",
    itemsResource.remove(groupId, itemId),
  );
}

export function toggleItem(item: Item): Promise<void> {
  return pendingService.track(
    "items/toggle",
    itemsResource.update(item.itemGroupId, item.id, {
      name: item.name,
      ...(item.description !== null && { description: item.description }),
      isComplete: !item.isComplete,
    }),
  );
}
