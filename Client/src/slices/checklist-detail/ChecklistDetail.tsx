import { useNavigate, useParams } from "react-router";

import { ItemList } from "../items/ItemList";
import { Members } from "../members/Members";
import { PendingBorder } from "@shared/components";
import styles from "./ChecklistDetail.module.css";
import { useChecklistDetailConcurrent } from "./hooks";

export function ChecklistDetail() {
  const { groupId } = useParams<{ groupId: string }>();
  const navigate = useNavigate();

  if (!groupId) return null;

  const { checklist, isPending, refresh } =
    useChecklistDetailConcurrent(groupId);

  return (
    <PendingBorder pending={isPending}>
      <div className={styles.header}>
        <h2 className={styles.title}>{checklist.name}</h2>
        <button
          type="button"
          className={styles.addBtn}
          onClick={() => navigate(`/${groupId}/items/new`)}
        >
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
