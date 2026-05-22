import { Suspense, type ReactNode } from "react";
import styles from "../styles/skeletons.module.css";

interface Props {
  fallback?: ReactNode;
  children: ReactNode;
}

function DefaultFallback() {
  return (
    <div aria-busy="true" aria-label="Loading">
      <div className={styles.skeletonCard} />
      <div
        className={styles.skeletonCard}
        style={{ marginTop: "var(--space-sm)" }}
      />
      <div
        className={styles.skeletonCard}
        style={{ marginTop: "var(--space-sm)" }}
      />
    </div>
  );
}

export function PendingBoundary({ fallback, children }: Props) {
  return (
    <Suspense fallback={fallback ?? <DefaultFallback />}>{children}</Suspense>
  );
}
