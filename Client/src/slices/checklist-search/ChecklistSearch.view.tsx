import type { ItemGroup } from "@shared/types";
import styles from "./ChecklistSearch.module.css";

interface ChecklistSearchViewProps {
  query: string;
  onQueryChange: (q: string) => void;
  filtered: ItemGroup[];
  isStale: boolean;
  children: (filtered: ItemGroup[], isStale: boolean) => React.ReactNode;
}

export function ChecklistSearchView({
  query,
  onQueryChange,
  filtered,
  isStale,
  children,
}: ChecklistSearchViewProps) {
  return (
    <div>
      <input
        type="search"
        className={styles.searchInput}
        placeholder="Search checklists…"
        value={query}
        onChange={(e) => onQueryChange(e.target.value)}
        aria-label="Search checklists"
      />
      <div className={isStale ? styles.stale : undefined}>
        {children(filtered, isStale)}
      </div>
    </div>
  );
}
