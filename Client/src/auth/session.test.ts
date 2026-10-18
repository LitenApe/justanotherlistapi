import { afterEach, describe, expect, it, vi } from "vitest";
import { createSessionStore } from "./session";

const session = (expiresInMs: number) => ({
  accessToken: "token",
  userId: "00000000-0000-0000-0000-000000000001",
  expiresAt: Date.now() + expiresInMs,
});

describe("sessionStore", () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it("persists the session per tab and restores it", () => {
    const store = createSessionStore(sessionStorage);
    store.setSession(session(60_000));

    const restored = createSessionStore(sessionStorage);
    expect(restored.getSession()?.userId).toBe("00000000-0000-0000-0000-000000000001");
  });

  it("drops stored sessions that have already expired", () => {
    sessionStorage.setItem("jal.session", JSON.stringify(session(-1)));
    expect(createSessionStore(sessionStorage).getSession()).toBeNull();
  });

  it("signs out with reason 'expired' when the token runs out", () => {
    vi.useFakeTimers();
    const store = createSessionStore(sessionStorage);
    const listener = vi.fn();
    store.subscribe(listener);
    store.setSession(session(1000));

    vi.advanceTimersByTime(1000);

    expect(store.getSession()).toBeNull();
    expect(store.getSignOutReason()).toBe("expired");
    expect(sessionStorage.getItem("jal.session")).toBeNull();
    expect(listener).toHaveBeenCalledTimes(2);
  });

  it("keeps working when storage is unavailable", () => {
    const store = createSessionStore(undefined);
    store.setSession(session(60_000));
    expect(store.getSession()).not.toBeNull();
    store.signOut("user");
    expect(store.getSignOutReason()).toBe("user");
  });
});
