import { useEffect, useRef, useState, type MouseEvent, type RefObject } from "react";
import { Link, Outlet, useLocation } from "react-router";
import { sessionStore, type Session } from "../auth/session";
import { useSession } from "../auth/useSession";
import { useConnectionStatus, useRealtimeConnection } from "../realtime/hooks";
import { themeStore, useTheme } from "../theme/theme";
import { CopyButton } from "./CopyButton";
import { Icon } from "./Icon";
import styles from "./Layout.module.css";

const getAccessToken = () => sessionStore.getSession()?.accessToken ?? "";

export function Layout() {
  const session = useSession();
  const mainRef = useRef<HTMLElement>(null);
  useRealtimeConnection(session ? getAccessToken : null);
  useFocusOnNavigation(mainRef);

  function skipToMain(event: MouseEvent<HTMLAnchorElement>) {
    event.preventDefault();
    mainRef.current?.focus();
    mainRef.current?.scrollIntoView();
  }

  return (
    <div className={styles.shell}>
      <a className="skip-link" href="#main" onClick={skipToMain}>
        Skip to main content
      </a>
      <header className={styles.header}>
        <div className={styles.headerInner}>
          <Link to="/" className={styles.brand}>
            <span className={styles.logo} aria-hidden="true">
              <Icon name="check" size={18} />
            </span>
            JustAnotherList
          </Link>
          <div className={styles.headerActions}>
            <ThemeToggle />
            {session && <AccountMenu session={session} />}
          </div>
        </div>
      </header>
      {session && <ConnectionBanner />}
      <main id="main" ref={mainRef} tabIndex={-1} className={styles.main}>
        <Outlet />
      </main>
    </div>
  );
}

/**
 * Client-side navigation doesn't reset focus like a page load does. Move focus
 * to the new page's <h1> so keyboard and screen reader users start at the top.
 */
function useFocusOnNavigation(mainRef: RefObject<HTMLElement | null>) {
  const { pathname } = useLocation();
  const previous = useRef(pathname);

  useEffect(() => {
    if (previous.current === pathname) return;
    previous.current = pathname;
    const heading = mainRef.current?.querySelector<HTMLElement>("h1");
    (heading ?? mainRef.current)?.focus();
    window.scrollTo(0, 0);
  }, [pathname, mainRef]);
}

/**
 * A toggle button: the name stays "Dark mode" and aria-pressed carries the
 * state, so screen readers announce "Dark mode, toggle button, pressed".
 */
function ThemeToggle() {
  const dark = useTheme() === "dark";

  return (
    <button
      type="button"
      className={`button button-small ${styles.themeToggle} ${styles.compactButton}`}
      aria-pressed={dark}
      onClick={() => {
        themeStore.setTheme(dark ? "light" : "dark");
      }}
    >
      <Icon name={dark ? "moon" : "sun"} size={18} />
      <span className={styles.compactLabel}>Dark mode</span>
    </button>
  );
}

/**
 * Uses the native popover API for light dismiss, Escape handling and focus
 * return. aria-expanded is set explicitly for older assistive technology.
 */
function AccountMenu({ session }: { session: Session }) {
  const [open, setOpen] = useState(false);

  return (
    <div className={styles.account}>
      <button
        type="button"
        className={`button button-small ${styles.compactButton}`}
        popoverTarget="account-panel"
        aria-expanded={open}
      >
        <Icon name="user" size={18} />
        <span className={styles.compactLabel}>Account</span>
      </button>
      <div
        id="account-panel"
        popover="auto"
        className={styles.accountPanel}
        onToggle={(event) => {
          setOpen(event.newState === "open");
        }}
      >
        <h2 className={styles.accountHeading}>Your account</h2>
        <p className="hint">Your user ID. Share it so others can add you to their lists.</p>
        <code className={styles.userId}>{session.userId}</code>
        <div className={styles.accountActions}>
          <CopyButton value={session.userId} label="Copy user ID" />
          <button
            type="button"
            className="button button-small"
            onClick={() => {
              sessionStore.signOut("user");
            }}
          >
            <Icon name="logout" size={16} />
            Sign out
          </button>
        </div>
      </div>
    </div>
  );
}

function ConnectionBanner() {
  const status = useConnectionStatus();
  const offline = status === "reconnecting" || status === "disconnected";

  return (
    <div role="status" className={styles.bannerRegion}>
      {offline && (
        <p className={styles.banner}>
          <Icon name="wifiOff" size={18} />
          Live updates paused. Reconnecting… Changes from other members will appear once
          reconnected.
        </p>
      )}
    </div>
  );
}
