import { useItemSearchConcurrent, useItemSearchLegacy } from "./hooks";

import type { Item } from "@shared/types";
import styles from "./ItemSearch.module.css";
import { useFeatures } from "../dev-panel";

interface Props {
  items: Item[];
  children: (filtered: Item[], isStale: boolean) => React.ReactNode;
}

export function ItemSearch({ items, children }: Props) {
  const { flags } = useFeatures();
  const concurrent = useItemSearchConcurrent(items);
  const legacy = useItemSearchLegacy(items);
  const { query, setQuery, filtered, isStale } = flags.useDeferredValue
    ? concurrent
    : legacy;

  return (
    <div>
      <input
        type="search"
        className={styles.searchInput}
        placeholder="Search items…"
        value={query}
        onChange={(e) => setQuery(e.target.value)}
        aria-label="Search items"
      />
      <div className={isStale ? styles.stale : undefined}>
        {children(filtered, isStale)}
      </div>
    </div>
  );
}
