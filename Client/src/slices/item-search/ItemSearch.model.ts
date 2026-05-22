import { useItemSearchConcurrent, useItemSearchLegacy } from "./hooks";

import type { Item } from "@shared/types";
import { useFeatures } from "../dev-panel";

export interface ItemSearchModel {
  query: string;
  setQuery: (q: string) => void;
  filtered: Item[];
  isStale: boolean;
}

export function useItemSearchModel(items: Item[]): ItemSearchModel {
  const { flags } = useFeatures();
  const concurrent = useItemSearchConcurrent(items);
  const legacy = useItemSearchLegacy(items);
  const { query, setQuery, filtered, isStale } = flags.useDeferredValue
    ? concurrent
    : legacy;

  return { query, setQuery, filtered, isStale };
}
