import type { ItemGroup } from "@shared/types";
import { checklistsResource } from "@shared/api";

export function fetchChecklist(id: string): Promise<ItemGroup> {
  return checklistsResource.getById(id);
}

export function renameChecklist(id: string, name: string): Promise<void> {
  return checklistsResource.update(id, { name });
}
