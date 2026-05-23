import type { ItemGroup } from "@shared/types";
import { apiClient } from "@shared/api/client";
import { pendingService } from "@shared/api/pendingService";

export function fetchChecklists(): Promise<ItemGroup[]> {
  return pendingService.track(
    "checklists/list",
    apiClient.get<ItemGroup[]>("/api/list"),
  );
}

export function createChecklist(name: string): Promise<ItemGroup> {
  return pendingService.track(
    "checklists/create",
    apiClient.post<ItemGroup>("/api/list", { name }),
  );
}

export function deleteChecklist(id: string): Promise<void> {
  return pendingService.track(
    "checklists/delete",
    apiClient.delete<void>(`/api/list/${id}`),
  );
}
