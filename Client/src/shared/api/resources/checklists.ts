import type { ApiClient } from "@shared/api/client";
import type { ItemGroup } from "@shared/types";
import { apiClient } from "@shared/api/client";

export interface CreateItemGroupRequest {
  name: string;
}

export interface UpdateItemGroupRequest {
  name: string;
}

export interface ChecklistsResource {
  getAll(): Promise<ItemGroup[]>;
  getById(id: string): Promise<ItemGroup>;
  create(body: CreateItemGroupRequest): Promise<ItemGroup>;
  update(id: string, body: UpdateItemGroupRequest): Promise<void>;
  remove(id: string): Promise<void>;
}

export function createChecklistsResource(
  client: ApiClient,
): ChecklistsResource {
  return {
    getAll() {
      return client.get<ItemGroup[]>("/api/list");
    },
    getById(id) {
      return client.get<ItemGroup>(`/api/list/${id}`);
    },
    create(body) {
      return client.post<ItemGroup>("/api/list", body);
    },
    update(id, body) {
      return client.put<void>(`/api/list/${id}`, body);
    },
    remove(id) {
      return client.delete<void>(`/api/list/${id}`);
    },
  };
}

export const checklistsResource: ChecklistsResource =
  createChecklistsResource(apiClient);
