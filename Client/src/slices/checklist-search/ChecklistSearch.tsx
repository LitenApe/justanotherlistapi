import {
  useChecklistSearchConcurrent,
  useChecklistSearchLegacy,
} from "./hooks";

import type { ItemGroup } from "@shared/types";
import styles from "./ChecklistSearch.module.css";
import { useFeatures } from "../dev-panel";

interface Props {
  checklists: ItemGroup[];
  children: (filtered: ItemGroup[], isStale: boolean) => React.ReactNode;
}

export function ChecklistSearch({ checklists, children }: Props) {
  const { flags } = useFeatures();
  const concurrent = useChecklistSearchConcurrent(checklists);
  const legacy = useChecklistSearchLegacy(checklists);
  const { query, setQuery, filtered, isStale } = flags.useDeferredValue
    ? concurrent
    : legacy;

  return (
    <div>
      <input
        type="search"
        className={styles.searchInput}
        placeholder="Search checklists…"
        value={query}
        onChange={(e) => setQuery(e.target.value)}
        aria-label="Search checklists"
      />
      <div className={isStale ? styles.stale : undefined}>
        {children(filtered, isStale)}
      </div>
    </div>
  );
}
