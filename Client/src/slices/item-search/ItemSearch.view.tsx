import type { Item } from "@shared/types";
import styles from "./ItemSearch.module.css";

interface ItemSearchViewProps {
  query: string;
  onQueryChange: (q: string) => void;
  filtered: Item[];
  isStale: boolean;
  children: (filtered: Item[], isStale: boolean) => React.ReactNode;
}

export function ItemSearchView({
  query,
  onQueryChange,
  filtered,
  isStale,
  children,
}: ItemSearchViewProps) {
  return (
    <div>
      <input
        type="search"
        className={styles.searchInput}
        placeholder="Search items…"
        value={query}
        onChange={(e) => onQueryChange(e.target.value)}
        aria-label="Search items"
      />
      <div className={isStale ? styles.stale : undefined}>
        {children(filtered, isStale)}
      </div>
    </div>
  );
}
