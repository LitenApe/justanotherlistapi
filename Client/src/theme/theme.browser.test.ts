import { afterEach, describe, expect, it } from "vitest";
import { createThemeStore, themeBootScript } from "./theme";

/** Runs the boot script the way the page does: as an inline <script>. */
function runBootScript() {
  const script = document.createElement("script");
  script.textContent = themeBootScript;
  document.head.append(script);
  script.remove();
}

const meta = (name: string) => document.querySelector<HTMLMetaElement>(`meta[name="${name}"]`);
const background = () => getComputedStyle(document.body).backgroundColor;

describe("theme", () => {
  afterEach(() => {
    for (const tag of document.querySelectorAll('meta[name="theme-color"]')) tag.remove();
    delete document.documentElement.dataset.theme;
  });

  it("starts dark and switches the whole page's colours", () => {
    const store = createThemeStore(localStorage);
    expect(store.getTheme()).toBe("dark");
    expect(document.documentElement.dataset.theme).toBe("dark");
    expect(background()).toBe("rgb(20, 22, 27)");

    store.setTheme("light");

    expect(document.documentElement.dataset.theme).toBe("light");
    expect(background()).toBe("rgb(245, 246, 248)");
    // color-scheme comes from the theme's CSS, so native controls match.
    expect(getComputedStyle(document.documentElement).colorScheme).toBe("light");
    expect(meta("theme-color")?.content).toBe("#ffffff");
    expect(document.querySelectorAll('meta[name="theme-color"]')).toHaveLength(1);
  });

  it("remembers the choice and ignores values it doesn't know", () => {
    createThemeStore(localStorage).setTheme("light");
    expect(createThemeStore(localStorage).getTheme()).toBe("light");

    localStorage.setItem("jal.theme", "sepia");
    expect(createThemeStore(localStorage).getTheme()).toBe("dark");
  });

  it("still switches when storage is unavailable", () => {
    const store = createThemeStore(undefined);
    store.setTheme("light");
    expect(store.getTheme()).toBe("light");
    expect(document.documentElement.dataset.theme).toBe("light");
  });

  it("boot script applies a saved light theme before the app starts", () => {
    localStorage.setItem("jal.theme", "light");

    runBootScript();

    expect(document.documentElement.dataset.theme).toBe("light");
    expect(meta("theme-color")?.content).toBe("#ffffff");
  });

  it("boot script uses the dark default otherwise", () => {
    runBootScript();
    expect(document.documentElement.dataset.theme).toBe("dark");
    expect(meta("theme-color")?.content).toBe("#1d2027");
  });
});
