import { useSyncExternalStore } from "react";

export type Theme = "dark" | "light";

export const DEFAULT_THEME: Theme = "dark";

// index.html applies a saved theme before first paint; keep the key and colors in sync.
const STORAGE_KEY = "jal.theme";
/** Browser UI color on mobile, matching --color-surface. */
const THEME_COLORS: Record<Theme, string> = { dark: "#1d2027", light: "#ffffff" };

/** The viewer's light/dark choice, remembered per browser. Dark is the default. */
export function createThemeStore(storage: Storage | undefined = safeLocalStorage()) {
  let theme = read();
  const listeners = new Set<() => void>();

  function read(): Theme {
    try {
      const stored = storage?.getItem(STORAGE_KEY);
      return stored === "light" || stored === "dark" ? stored : DEFAULT_THEME;
    } catch {
      return DEFAULT_THEME;
    }
  }

  function apply(): void {
    document.documentElement.dataset.theme = theme;
    document.querySelector('meta[name="color-scheme"]')?.setAttribute("content", theme);
    document
      .querySelector('meta[name="theme-color"]')
      ?.setAttribute("content", THEME_COLORS[theme]);
  }

  apply();

  return {
    getTheme: () => theme,
    setTheme: (next: Theme): void => {
      theme = next;
      apply();
      try {
        storage?.setItem(STORAGE_KEY, next);
      } catch {
        // Storage can be unavailable (privacy modes); the choice still applies to this visit.
      }
      for (const listener of listeners) listener();
    },
    subscribe: (listener: () => void): (() => void) => {
      listeners.add(listener);
      return () => listeners.delete(listener);
    },
  };
}

function safeLocalStorage(): Storage | undefined {
  try {
    return window.localStorage;
  } catch {
    return undefined;
  }
}

export const themeStore = createThemeStore();

export function useTheme(): Theme {
  return useSyncExternalStore(themeStore.subscribe, themeStore.getTheme);
}
