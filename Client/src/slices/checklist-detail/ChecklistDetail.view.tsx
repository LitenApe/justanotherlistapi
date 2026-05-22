import type { ItemGroup } from "@shared/types";
import { ItemList } from "../items/ItemList";
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
      <div className={styles.header}>
        <h2 className={styles.title}>{checklist.name}</h2>
        <button type="button" className={styles.addBtn} onClick={addItem}>
          + New Item
        </button>
      </div>

      <ItemList
        items={checklist.items}
        groupId={groupId}
        onRefresh={onItemChanged}
      />
      <Members groupId={groupId} />
    </>
  );
}
