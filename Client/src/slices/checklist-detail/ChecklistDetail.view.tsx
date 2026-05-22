import type { ItemGroup } from "@shared/types";
import { ItemList } from "../items/ItemList";
import { ItemSearch } from "../item-search";
import { Members } from "../members/Members";

export interface ChecklistDetailViewProps {
  groupId: string;
  checklist: ItemGroup;
  onItemChanged: () => Promise<void>;
}

export function ChecklistDetailView({
  groupId,
  checklist,
  onItemChanged,
}: ChecklistDetailViewProps) {
  return (
    <>
      <ItemSearch items={checklist.items}>
        {(filtered) => (
          <ItemList
            items={filtered}
            groupId={groupId}
            onRefresh={onItemChanged}
          />
        )}
      </ItemSearch>
      <Members groupId={groupId} />
    </>
  );
}
