import { Outlet, useNavigate } from "react-router";

import { PendingBorder } from "@shared/components";
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

        {/* Checklist navigation will be rendered here by Phase 4 */}

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
