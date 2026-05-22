import type { ApiClient } from "../client";
import type { Item } from "../../types";
import { apiClient } from "../client";

export interface CreateItemRequest {
  name: string;
  description?: string;
  isComplete?: boolean;
}

export interface UpdateItemRequest {
  name: string;
  description?: string;
  isComplete?: boolean;
}

export interface ItemsResource {
  create(groupId: string, body: CreateItemRequest): Promise<Item>;
  update(groupId: string, itemId: string, body: UpdateItemRequest): Promise<void>;
  remove(groupId: string, itemId: string): Promise<void>;
}

export function createItemsResource(client: ApiClient): ItemsResource {
  return {
    create(groupId, body) {
      return client.post<Item>(`/api/list/${groupId}`, body);
    },
    update(groupId, itemId, body) {
      return client.put<void>(`/api/list/${groupId}/${itemId}`, body);
    },
    remove(groupId, itemId) {
      return client.delete<void>(`/api/list/${groupId}/${itemId}`);
    },
  };
}

export const itemsResource: ItemsResource = createItemsResource(apiClient);
