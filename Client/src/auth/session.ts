export interface Session {
  accessToken: string;
  userId: string;
  /** Epoch milliseconds at which the access token expires. */
  expiresAt: number;
}

export type SignOutReason = "user" | "expired";

const SESSION_KEY = "jal.session";
// setTimeout overflows above ~24.8 days; tokens are far shorter-lived than that.
const MAX_TIMER_DELAY = 2_147_483_647;

/**
 * Holds the signed-in session. Stored in sessionStorage so every browser tab
 * keeps its own sign-in, which makes it easy to try sharing between users.
 */
export function createSessionStore(storage: Storage | undefined = safeSessionStorage()) {
  let session = read();
  let signOutReason: SignOutReason | null = null;
  let expiryTimer: ReturnType<typeof setTimeout> | undefined;
  const listeners = new Set<() => void>();

  function read(): Session | null {
    try {
      const raw = storage?.getItem(SESSION_KEY);
      if (!raw) return null;
      const parsed = JSON.parse(raw) as Session;
      return parsed.expiresAt > Date.now() ? parsed : null;
    } catch {
      return null;
    }
  }

  function write(next: Session | null): void {
    try {
      if (next) storage?.setItem(SESSION_KEY, JSON.stringify(next));
      else storage?.removeItem(SESSION_KEY);
    } catch {
      // Storage can be unavailable (privacy modes); the session still lives in memory.
    }
  }

  function scheduleExpiry(): void {
    clearTimeout(expiryTimer);
    if (!session) return;
    const delay = Math.min(Math.max(session.expiresAt - Date.now(), 0), MAX_TIMER_DELAY);
    expiryTimer = setTimeout(() => {
      signOut("expired");
    }, delay);
  }

  function emit(): void {
    for (const listener of listeners) listener();
  }

  function setSession(next: Session): void {
    session = next;
    signOutReason = null;
    write(next);
    scheduleExpiry();
    emit();
  }

  function signOut(reason: SignOutReason): void {
    if (!session) return;
    session = null;
    signOutReason = reason;
    write(null);
    scheduleExpiry();
    emit();
  }

  scheduleExpiry();

  return {
    getSession: () => session,
    getSignOutReason: () => signOutReason,
    setSession,
    signOut,
    subscribe: (listener: () => void): (() => void) => {
      listeners.add(listener);
      return () => listeners.delete(listener);
    },
  };
}

function safeSessionStorage(): Storage | undefined {
  try {
    return window.sessionStorage;
  } catch {
    return undefined;
  }
}

export type SessionStore = ReturnType<typeof createSessionStore>;

export const sessionStore = createSessionStore();
