import { Outlet, useNavigate } from "react-router";

import { logout } from "@slices/auth";
import { routes } from "@shared/routes";
import styles from "./Layout.module.css";
import { useAuthToken } from "@shared/hooks";

export function Layout() {
  const navigate = useNavigate();
  const token = useAuthToken();

  function handleLogout() {
    logout();
    navigate(routes.login(), { replace: true });
  }

  return (
    <div className={styles.layout}>
      <a href="#main-content" className={styles.skipLink}>
        Skip to content
      </a>
      {token && (
        <header className={styles.topBar}>
          <button
            type="button"
            className={styles.logoutBtn}
            onClick={handleLogout}
          >
            Sign Out
          </button>
        </header>
      )}

      <main id="main-content" className={styles.main}>
        <Outlet />
      </main>
    </div>
  );
}
