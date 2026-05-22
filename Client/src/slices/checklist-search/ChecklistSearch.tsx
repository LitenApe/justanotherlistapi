import type { ItemGroup } from "@shared/types";
import styles from "./ChecklistSearch.module.css";
import { useChecklistSearch } from "./hooks";

// ─── Model ────────────────────────────────────────────────────────────────────

interface ChecklistSearchModel {
  query: string;
  setQuery: (q: string) => void;
  filtered: ItemGroup[];
  isStale: boolean;
}

function useChecklistSearchModel(
  checklists: ItemGroup[],
): ChecklistSearchModel {
  return useChecklistSearch(checklists);
}

// ─── View ─────────────────────────────────────────────────────────────────────

interface ChecklistSearchViewProps {
  query: string;
  onQueryChange: (q: string) => void;
  filtered: ItemGroup[];
  isStale: boolean;
  children: (filtered: ItemGroup[], isStale: boolean) => React.ReactNode;
}

function ChecklistSearchView({
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

// ─── Controller ───────────────────────────────────────────────────────────────

interface Props {
  checklists: ItemGroup[];
  children: (filtered: ItemGroup[], isStale: boolean) => React.ReactNode;
}

export function ChecklistSearch({ checklists, children }: Props) {
  const { query, setQuery, filtered, isStale } =
    useChecklistSearchModel(checklists);

  return (
    <ChecklistSearchView
      query={query}
      onQueryChange={setQuery}
      filtered={filtered}
      isStale={isStale}
    >
      {children}
    </ChecklistSearchView>
  );
}
