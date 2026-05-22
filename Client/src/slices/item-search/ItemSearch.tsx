import type { Item } from "@shared/types";
import { ItemSearchView } from "./ItemSearch.view";
import { useItemSearchModel } from "./ItemSearch.model";

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
