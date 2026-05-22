import type { ItemGroup } from "@shared/types";
import { ItemList } from "../items/ItemList";
import { Members } from "../members/Members";
import { PendingBorder } from "@shared/components";
import styles from "./ChecklistDetail.module.css";

export interface ChecklistDetailViewProps {
  groupId: string;
  checklist: ItemGroup;
  isPending: boolean;
  refresh: () => void;
  addItem: () => void;
}

export function ChecklistDetailView({
  groupId,
  checklist,
  isPending,
  refresh,
  addItem,
}: ChecklistDetailViewProps) {
  return (
    <PendingBorder pending={isPending}>
      <div className={styles.header}>
        <h2 className={styles.title}>{checklist.name}</h2>
        <button type="button" className={styles.addBtn} onClick={addItem}>
          + New Item
        </button>
      </div>

      <ItemList items={checklist.items} groupId={groupId} onRefresh={refresh} />
      <Members
        groupId={groupId}
        members={checklist.members}
        onRefresh={refresh}
      />
    </PendingBorder>
  );
}
