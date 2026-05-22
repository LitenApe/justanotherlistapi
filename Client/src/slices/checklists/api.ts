import type { ItemGroup } from "@shared/types";
import { checklistsResource } from "@shared/api";

export function fetchChecklists(): Promise<ItemGroup[]> {
  return checklistsResource.getAll();
}

export function createChecklist(name: string): Promise<ItemGroup> {
  return checklistsResource.create({ name });
}

export function deleteChecklist(id: string): Promise<void> {
  return checklistsResource.remove(id);
}
