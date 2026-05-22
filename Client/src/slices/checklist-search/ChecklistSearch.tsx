import type { ItemGroup } from "@shared/types";
import { ChecklistSearchView } from "./ChecklistSearch.view";
import { useChecklistSearchModel } from "./ChecklistSearch.model";

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
