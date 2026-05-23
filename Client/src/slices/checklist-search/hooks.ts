import { useDeferredValue, useMemo, useState } from "react";

import type { ItemGroup } from "@shared/types";
import { filterChecklists } from "./filter";

export function useChecklistSearch(checklists: ItemGroup[]) {
  const [query, setQuery] = useState("");
  const deferredQuery = useDeferredValue(query);
  const deferredChecklists = useDeferredValue(checklists);
  const isStale = query !== deferredQuery || checklists !== deferredChecklists;

  const filtered = useMemo(
    () => filterChecklists(deferredChecklists, deferredQuery),
    [deferredChecklists, deferredQuery],
  );

  return { query, setQuery, filtered, isStale };
}
