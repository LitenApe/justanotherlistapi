import { screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { expectNoAxeViolations, renderApp, signIn } from "../test/render";
import { createThemeStore } from "./theme";

describe("theme", () => {
  it("defaults to dark mode", () => {
    expect(createThemeStore(localStorage).getTheme()).toBe("dark");
    expect(document.documentElement.dataset.theme).toBe("dark");
  });

  it("restores a saved choice and ignores unknown values", () => {
    localStorage.setItem("jal.theme", "light");
    expect(createThemeStore(localStorage).getTheme()).toBe("light");

    localStorage.setItem("jal.theme", "sepia");
    expect(createThemeStore(localStorage).getTheme()).toBe("dark");
  });

  it("works without storage", () => {
    const store = createThemeStore(undefined);
    store.setTheme("light");
    expect(store.getTheme()).toBe("light");
  });

  it("toggles between dark and light from the header and remembers the choice", async () => {
    const { user } = renderApp("/sign-in");
    const toggle = await screen.findByRole("button", { name: "Dark mode", pressed: true });

    await user.click(toggle);

    expect(toggle).toHaveAttribute("aria-pressed", "false");
    expect(document.documentElement.dataset.theme).toBe("light");
    expect(localStorage.getItem("jal.theme")).toBe("light");
    await expectNoAxeViolations();

    await user.click(toggle);
    expect(toggle).toHaveAttribute("aria-pressed", "true");
    expect(document.documentElement.dataset.theme).toBe("dark");
  });

  it("is available alongside the account menu when signed in", async () => {
    signIn();
    renderApp("/lists/not-a-guid");

    expect(await screen.findByRole("button", { name: "Dark mode" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Account" })).toBeInTheDocument();
  });
});
