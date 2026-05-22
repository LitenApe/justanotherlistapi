import { useDeferredValue, useMemo, useState } from "react";

import type { ItemGroup } from "@shared/types";
import { filterChecklists } from "./filter";

export function useChecklistSearch(checklists: ItemGroup[]) {
  const [query, setQuery] = useState("");
  const deferredQuery = useDeferredValue(query);
  const isStale = query !== deferredQuery;

  const filtered = useMemo(
    () => filterChecklists(checklists, deferredQuery),
    [checklists, deferredQuery],
  );

  return { query, setQuery, filtered, isStale };
}
