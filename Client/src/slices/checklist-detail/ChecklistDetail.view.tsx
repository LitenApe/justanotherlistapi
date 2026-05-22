import type { ItemGroup } from "@shared/types";
import { ItemList } from "../items/ItemList";
import { ItemSearch } from "../item-search";
import { Members } from "../members/Members";
import { RenderCount } from "@shared/components";
import styles from "./ChecklistDetail.module.css";

export interface ChecklistDetailViewProps {
  groupId: string;
  checklist: ItemGroup | null;
  previewName: string | undefined;
  refresh: () => void;
  onItemChanged: () => Promise<void>;
  addItem: () => void;
}

export function ChecklistDetailView({
  groupId,
  checklist,
  previewName,
  refresh,
  onItemChanged,
  addItem,
}: ChecklistDetailViewProps) {
  if (!checklist) {
    return previewName ? (
      <div style={{ position: "relative" }}>
        <RenderCount label="ChecklistDetail" />
        <div className={styles.header}>
          <h2 className={styles.title}>{previewName}</h2>
        </div>
        <p>Loading items…</p>
      </div>
    ) : (
      <p>Loading…</p>
    );
  }

  return (
    <div style={{ position: "relative" }}>
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
    </div>
  );
}
