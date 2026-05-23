import type { ItemGroup } from "@shared/types";
import { apiClient } from "@shared/api/client";

export function fetchChecklist(id: string): Promise<ItemGroup> {
  return apiClient.get<ItemGroup>(`/api/list/${id}`);
}

export function renameChecklist(id: string, name: string): Promise<void> {
  return apiClient.put<void>(`/api/list/${id}`, { name });
}
