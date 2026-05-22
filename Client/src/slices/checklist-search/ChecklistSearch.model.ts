import {
  useChecklistSearchConcurrent,
  useChecklistSearchLegacy,
} from "./hooks";

import type { ItemGroup } from "@shared/types";
import { useFeatures } from "../dev-panel";

export interface ChecklistSearchModel {
  query: string;
  setQuery: (q: string) => void;
  filtered: ItemGroup[];
  isStale: boolean;
}

export function useChecklistSearchModel(
  checklists: ItemGroup[],
): ChecklistSearchModel {
  const { flags } = useFeatures();
  const concurrent = useChecklistSearchConcurrent(checklists);
  const legacy = useChecklistSearchLegacy(checklists);
  const { query, setQuery, filtered, isStale } = flags.useDeferredValue
    ? concurrent
    : legacy;

  return { query, setQuery, filtered, isStale };
}
