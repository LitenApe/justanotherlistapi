import { useSyncExternalStore } from "react";

export type Theme = "dark" | "light";

export const DEFAULT_THEME: Theme = "dark";

const STORAGE_KEY = "jal.theme";
/** Browser UI color on mobile, matching --color-surface. */
export const THEME_COLORS: Record<Theme, string> = { dark: "#1d2027", light: "#ffffff" };

/**
 * Inline <head> script (see root.tsx): applies a saved light theme before first
 * paint, so there is no flash of the dark default.
 */
export const themeBootScript = `try {
  if (localStorage.getItem(${JSON.stringify(STORAGE_KEY)}) === "light") {
    document.documentElement.dataset.theme = "light";
    document.querySelector('meta[name="color-scheme"]').content = "light";
    document.querySelector('meta[name="theme-color"]').content = ${JSON.stringify(THEME_COLORS.light)};
  }
} catch (e) {}`;

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
    // The build prerenders the app shell in Node, where there is no document.
    if (typeof document === "undefined") return;
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
  return useSyncExternalStore(themeStore.subscribe, themeStore.getTheme, () => DEFAULT_THEME);
}
