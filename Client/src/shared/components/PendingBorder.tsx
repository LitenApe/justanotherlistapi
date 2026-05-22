import { useSyncExternalStore } from "react";

import { pendingService } from "@shared/api";
import styles from "./PendingBorder.module.css";

export function PendingBorder() {
  const isPending = useSyncExternalStore(
    pendingService.subscribe,
    pendingService.getSnapshot,
  );

  return (
    <div
      className={`${styles.pendingBorder} ${isPending ? styles.active : ""}`}
    />
  );
}
