import type { ItemGroup } from "@shared/types";
import { RenderCount } from "@shared/components";
import { ItemList } from "../items/ItemList";
import { ItemSearch } from "../item-search";
import { Members } from "../members/Members";
import styles from "./ChecklistDetail.module.css";

export interface ChecklistDetailViewProps {
  groupId: string;
  checklist: ItemGroup | null;
  refresh: () => void;
  onItemChanged: () => Promise<void>;
  addItem: () => void;
}

export function ChecklistDetailView({
  groupId,
  checklist,
  refresh,
  onItemChanged,
  addItem,
}: ChecklistDetailViewProps) {
  if (!checklist) {
    return <p>Loading…</p>;
  }

  return (
    <>
      <RenderCount label="ChecklistDetail" />
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
