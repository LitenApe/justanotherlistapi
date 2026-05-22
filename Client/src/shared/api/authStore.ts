const STORAGE_KEY = "auth_token";

export function createAuthStore() {
  let token: string | null = null;
  const listeners = new Set<() => void>();

  // Initialize from sessionStorage
  try {
    token = sessionStorage.getItem(STORAGE_KEY);
  } catch {
    // sessionStorage unavailable (e.g., private browsing in some browsers)
  }

  const notify = () => listeners.forEach((l) => l());

  return {
    getToken: () => token,
    setToken: (newToken: string) => {
      token = newToken;
      try {
        sessionStorage.setItem(STORAGE_KEY, newToken);
      } catch {
        // silent fail
      }
      notify();
    },
    clearToken: () => {
      token = null;
      try {
        sessionStorage.removeItem(STORAGE_KEY);
      } catch {
        // silent fail
      }
      notify();
    },
    subscribe: (listener: () => void) => {
      listeners.add(listener);
      return () => {
        listeners.delete(listener);
      };
    },
    getSnapshot: () => token,
  };
}

export type AuthStore = ReturnType<typeof createAuthStore>;
export const authStore = createAuthStore();
