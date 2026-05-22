import type { Item } from "@shared/types";
import { itemsResource } from "@shared/api";

export function createItem(
  groupId: string,
  body: { name: string; description?: string; isComplete?: boolean },
): Promise<Item> {
  return itemsResource.create(groupId, body);
}

export function updateItem(
  groupId: string,
  itemId: string,
  body: { name: string; description?: string; isComplete?: boolean },
): Promise<void> {
  return itemsResource.update(groupId, itemId, body);
}

export function deleteItem(groupId: string, itemId: string): Promise<void> {
  return itemsResource.remove(groupId, itemId);
}

export function toggleItem(item: Item): Promise<void> {
  return itemsResource.update(item.itemGroupId, item.id, {
    name: item.name,
    ...(item.description !== null && { description: item.description }),
    isComplete: !item.isComplete,
  });
}
