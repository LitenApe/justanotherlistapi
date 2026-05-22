import { ErrorBoundary, PendingBoundary } from "@shared/components";
import { Outlet, useNavigate } from "react-router";

import { ChecklistList } from "../slices/checklists";
import { logout } from "../slices/auth";
import { routes } from "@shared/routes";
import styles from "./Layout.module.css";
import { useAuthToken } from "@shared/hooks";
import { useCallback } from "react";

export function Layout() {
  const navigate = useNavigate();
  const token = useAuthToken();

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
      {token && (
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
              <ChecklistList onCreated={handleCreated} />
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
      )}

      <main className={styles.main}>
        <Outlet />
      </main>
    </div>
  );
}
