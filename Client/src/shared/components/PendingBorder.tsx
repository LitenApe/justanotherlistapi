import { usePending } from "@shared/hooks";
import styles from "./PendingBorder.module.css";

export function PendingBorder() {
  const isPending = usePending();

  return (
    <div
      className={`${styles.pendingBorder} ${isPending ? styles.active : ""}`}
    />
  );
}
