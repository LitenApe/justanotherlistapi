import {
  ErrorBoundary,
  PendingBorder,
  PendingBoundary,
} from "@shared/components";
import { Outlet, useNavigate } from "react-router";

import { ChecklistListConcurrent } from "../slices/checklists";
import { logout } from "../slices/auth";
import styles from "./Layout.module.css";
import { useTransition } from "react";

export function Layout() {
  const navigate = useNavigate();
  const [isPending, startTransition] = useTransition();

  function handleLogout() {
    logout();
    navigate("/login", { replace: true });
  }

  return (
    <div className={styles.layout}>
      <aside className={styles.sidebar}>
        <div className={styles.sidebarHeader}>
          <h1 className={styles.sidebarTitle}>Checklists</h1>
        </div>

        <ErrorBoundary
          fallback={(err, reset) => (
            <p>
              Error: {err.message} <button onClick={reset}>Retry</button>
            </p>
          )}
        >
          <PendingBoundary>
            <ChecklistListConcurrent />
          </PendingBoundary>
        </ErrorBoundary>

        <button
          type="button"
          className={styles.logoutBtn}
          onClick={handleLogout}
        >
          Sign Out
        </button>
      </aside>

      <main className={styles.main}>
        <PendingBorder pending={isPending}>
          <Outlet context={{ startTransition }} />
        </PendingBorder>
      </main>
    </div>
  );
}
