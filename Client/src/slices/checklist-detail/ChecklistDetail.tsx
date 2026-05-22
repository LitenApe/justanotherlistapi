import { ChecklistDetailContent } from "./ChecklistDetailContent";
import { PendingBoundary } from "@shared/components";
import { RenderCount } from "@shared/components";
import { useLocation, useNavigate, useParams } from "react-router";

import { routes } from "@shared/routes";
import styles from "./ChecklistDetail.module.css";

export function ChecklistDetail() {
  const { groupId } = useParams<{ groupId: string }>();
  const location = useLocation();
  const navigate = useNavigate();
  const name = (location.state as { name?: string } | null)?.name;
  if (!groupId) return null;

  return (
    <div style={{ position: "relative" }}>
      <RenderCount label="ChecklistDetail" />
      <div className={styles.header}>
        <h2 className={styles.title}>{name ?? "\u00A0"}</h2>
        <button
          type="button"
          className={styles.addBtn}
          onClick={() => navigate(routes.itemCreate(groupId))}
        >
          + New Item
        </button>
      </div>
      <PendingBoundary>
        <ChecklistDetailContent groupId={groupId} />
      </PendingBoundary>
    </div>
  );
}
