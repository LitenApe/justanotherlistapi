import { useItemSearch } from "./hooks";

import type { Item } from "@shared/types";

export interface ItemSearchModel {
  query: string;
  setQuery: (q: string) => void;
  filtered: Item[];
  isStale: boolean;
}

export function useItemSearchModel(items: Item[]): ItemSearchModel {
  return useItemSearch(items);
}
