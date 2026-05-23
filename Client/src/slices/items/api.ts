import type { Item } from "@shared/types";
import { apiClient } from "@shared/api/client";
import { pendingService } from "@shared/api/pendingService";

export function createItem(
  groupId: string,
  body: { name: string; description?: string; isComplete?: boolean },
): Promise<Item> {
  return pendingService.track(
    "items/create",
    apiClient.post<Item>(`/api/list/${groupId}`, body),
  );
}

export function updateItem(
  groupId: string,
  itemId: string,
  body: { name: string; description?: string; isComplete?: boolean },
): Promise<void> {
  return pendingService.track(
    "items/update",
    apiClient.put<void>(`/api/list/${groupId}/${itemId}`, body),
  );
}

export function deleteItem(groupId: string, itemId: string): Promise<void> {
  return pendingService.track(
    "items/delete",
    apiClient.delete<void>(`/api/list/${groupId}/${itemId}`),
  );
}

export function toggleItem(item: Item): Promise<void> {
  return pendingService.track(
    "items/toggle",
    apiClient.put<void>(`/api/list/${item.itemGroupId}/${item.id}`, {
      name: item.name,
      ...(item.description !== null && { description: item.description }),
      isComplete: !item.isComplete,
    }),
  );
}
