import type { ItemGroup } from "@shared/types";
import { ItemList } from "../items/ItemList";
import { ItemSearch } from "../item-search";
import { Members } from "../members/Members";
import styles from "./ChecklistDetail.module.css";

export interface ChecklistDetailViewProps {
  groupId: string;
  checklist: ItemGroup;
  onItemChanged: () => Promise<void>;
  addItem: () => void;
}

export function ChecklistDetailView({
  groupId,
  checklist,
  onItemChanged,
  addItem,
}: ChecklistDetailViewProps) {
  return (
    <>
      <div className={styles.header}>
        <h2 className={styles.title}>{checklist.name}</h2>
        <button type="button" className={styles.addBtn} onClick={addItem}>
          + New Item
        </button>
      </div>

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
