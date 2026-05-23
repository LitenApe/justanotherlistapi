import { PendingBoundary } from "@shared/components";
import { RenderCount } from "@shared/components";
import { Link, useLocation, useParams } from "react-router";

import type { Item, ItemGroup } from "@shared/types";
import { ItemList } from "../items/ItemList";
import { ItemSearch } from "../item-search";
import { Members } from "../members/Members";
import { routes } from "@shared/routes";
import { useChecklistDetail } from "./hooks";
import styles from "./ChecklistDetail.module.css";

// ─── Model ────────────────────────────────────────────────────────────────────

interface ChecklistDetailModel {
  groupId: string;
  checklist: ItemGroup;
  onItemChanged: () => Promise<void>;
}

function useChecklistDetailModel(groupId: string): ChecklistDetailModel {
  const { checklist, invalidateAndRefetch } = useChecklistDetail(groupId);

  return {
    groupId,
    checklist,
    onItemChanged: invalidateAndRefetch,
  };
}

// ─── View ─────────────────────────────────────────────────────────────────────

interface ChecklistDetailViewProps {
  groupId: string;
  checklist: ItemGroup;
  onItemChanged: () => Promise<void>;
}

function ChecklistDetailView({
  groupId,
  checklist,
  onItemChanged,
}: ChecklistDetailViewProps) {
  return (
    <>
      <ItemSearch items={checklist.items}>
        {(filtered: Item[]) => (
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

// ─── Controller ───────────────────────────────────────────────────────────────

function ChecklistDetailContent({ groupId }: { groupId: string }) {
  const { checklist, onItemChanged } = useChecklistDetailModel(groupId);

  return (
    <ChecklistDetailView
      groupId={groupId}
      checklist={checklist}
      onItemChanged={onItemChanged}
    />
  );
}

export function ChecklistDetail() {
  const { groupId } = useParams<{ groupId: string }>();
  const location = useLocation();
  const name = (location.state as { name?: string } | null)?.name;
  if (!groupId) return null;

  return (
    <div style={{ position: "relative" }}>
      <RenderCount label="ChecklistDetail" />
      <div className={styles.header}>
        <h2 className={styles.title}>{name ?? "\u00A0"}</h2>
        <Link to={routes.itemCreate(groupId)} className={styles.addBtn}>
          + New Item
        </Link>
      </div>
      <PendingBoundary>
        <ChecklistDetailContent groupId={groupId} />
      </PendingBoundary>
    </div>
  );
}
