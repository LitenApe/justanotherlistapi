import type { ItemGroup } from "@shared/types";
import styles from "./ChecklistSearch.module.css";
import { useChecklistSearch } from "./hooks";

interface Props {
  checklists: ItemGroup[];
  children: (filtered: ItemGroup[], isStale: boolean) => React.ReactNode;
}

export function ChecklistSearch({ checklists, children }: Props) {
  const { query, setQuery, filtered, isStale } = useChecklistSearch(checklists);

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
