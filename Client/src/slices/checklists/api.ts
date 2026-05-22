import type { ItemGroup } from "@shared/types";
import { checklistsResource, pendingService } from "@shared/api";

export function fetchChecklists(): Promise<ItemGroup[]> {
  return pendingService.track("checklists/list", checklistsResource.getAll());
}

export function createChecklist(name: string): Promise<ItemGroup> {
  return pendingService.track(
    "checklists/create",
    checklistsResource.create({ name }),
  );
}

export function deleteChecklist(id: string): Promise<void> {
  return pendingService.track(
    "checklists/delete",
    checklistsResource.remove(id),
  );
}
