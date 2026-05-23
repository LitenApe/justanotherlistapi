import { useDeferredValue, useMemo, useState } from "react";

import type { Item } from "@shared/types";
import { filterItems } from "./filter";

export function useItemSearch(items: Item[]) {
  const [query, setQuery] = useState("");
  const deferredQuery = useDeferredValue(query);
  const deferredItems = useDeferredValue(items);
  const isStale = query !== deferredQuery || items !== deferredItems;

  const filtered = useMemo(
    () => filterItems(deferredItems, deferredQuery),
    [deferredItems, deferredQuery],
  );

  return { query, setQuery, filtered, isStale };
}
