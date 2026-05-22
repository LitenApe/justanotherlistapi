import { ChecklistDetailContent } from "./ChecklistDetailContent";
import { PendingBoundary } from "@shared/components";
import { RenderCount } from "@shared/components";
import { useLocation, useParams } from "react-router";
import styles from "./ChecklistDetail.module.css";

export function ChecklistDetail() {
  const { groupId } = useParams<{ groupId: string }>();
  const location = useLocation();
  const name = (location.state as { name?: string } | null)?.name;
  if (!groupId) return null;

  return (
    <div style={{ position: "relative" }}>
      <RenderCount label="ChecklistDetail" />
      {name && (
        <div className={styles.header}>
          <h2 className={styles.title}>{name}</h2>
        </div>
      )}
      <PendingBoundary>
        <ChecklistDetailContent groupId={groupId} />
      </PendingBoundary>
    </div>
  );
}
