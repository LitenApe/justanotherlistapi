import type { Item } from "@shared/types";
import styles from "./ItemSearch.module.css";
import { useItemSearch } from "./hooks";

// ─── Model ────────────────────────────────────────────────────────────────────

interface ItemSearchModel {
  query: string;
  setQuery: (q: string) => void;
  filtered: Item[];
  isStale: boolean;
}

function useItemSearchModel(items: Item[]): ItemSearchModel {
  return useItemSearch(items);
}

// ─── View ─────────────────────────────────────────────────────────────────────

interface ItemSearchViewProps {
  query: string;
  onQueryChange: (q: string) => void;
  filtered: Item[];
  isStale: boolean;
  children: (filtered: Item[], isStale: boolean) => React.ReactNode;
}

function ItemSearchView({
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

// ─── Controller ───────────────────────────────────────────────────────────────

interface Props {
  items: Item[];
  children: (filtered: Item[], isStale: boolean) => React.ReactNode;
}

export function ItemSearch({ items, children }: Props) {
  const { query, setQuery, filtered, isStale } = useItemSearchModel(items);

  return (
    <ItemSearchView
      query={query}
      onQueryChange={setQuery}
      filtered={filtered}
      isStale={isStale}
    >
      {children}
    </ItemSearchView>
  );
}
