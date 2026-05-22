import type { ReactNode } from "react";
import styles from "./PendingBorder.module.css";

interface Props {
  pending: boolean;
  children: ReactNode;
}

export function PendingBorder({ pending, children }: Props) {
  return (
    <div className={`${styles.border} ${pending ? styles.pending : ""}`}>
      {children}
    </div>
  );
}
