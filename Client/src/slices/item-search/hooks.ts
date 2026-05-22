import { useDeferredValue, useMemo, useState } from "react";

import type { Item } from "@shared/types";
import { filterItems } from "./filter";

export function useItemSearchConcurrent(items: Item[]) {
  const [query, setQuery] = useState("");
  const deferredQuery = useDeferredValue(query);
  const isStale = query !== deferredQuery;

  const filtered = useMemo(
    () => filterItems(items, deferredQuery),
    [items, deferredQuery],
  );

  return { query, setQuery, filtered, isStale };
}

export function useItemSearchLegacy(items: Item[]) {
  const [query, setQuery] = useState("");

  const filtered = useMemo(() => filterItems(items, query), [items, query]);

  return { query, setQuery, filtered, isStale: false };
}
