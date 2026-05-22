import { ErrorBoundary, PendingBoundary } from "@shared/components";
import { Outlet, useNavigate } from "react-router";
import { useCallback, useState, useTransition } from "react";

import { ChecklistList } from "../slices/checklists";
import { logout } from "../slices/auth";
import { routes } from "@shared/routes";
import styles from "./Layout.module.css";
import { useFeatures } from "../slices/dev-panel";

export function Layout() {
  const navigate = useNavigate();
  const [, startTransition] = useTransition();
  const { flags } = useFeatures();
  const [refreshSignal, setRefreshSignal] = useState(0);

  const wrappedStartTransition = flags.useTransition
    ? startTransition
    : (fn: () => void) => fn();

  function handleLogout() {
    logout();
    navigate(routes.login(), { replace: true });
  }

  const handleCreated = useCallback(
    (newId: string) => {
      navigate(routes.checklist(newId));
    },
    [navigate],
  );

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
            <ChecklistList
              refreshSignal={refreshSignal}
              onCreated={handleCreated}
            />
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
        <Outlet
          context={{
            startTransition: wrappedStartTransition,
            flags,
            refresh: () => setRefreshSignal((s) => s + 1),
          }}
        />
      </main>
    </div>
  );
}
