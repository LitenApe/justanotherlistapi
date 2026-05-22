import { useChecklistSearch } from "./hooks";

import type { ItemGroup } from "@shared/types";

export interface ChecklistSearchModel {
  query: string;
  setQuery: (q: string) => void;
  filtered: ItemGroup[];
  isStale: boolean;
}

export function useChecklistSearchModel(
  checklists: ItemGroup[],
): ChecklistSearchModel {
  return useChecklistSearch(checklists);
}
