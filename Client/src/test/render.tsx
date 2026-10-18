import { render } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import axe from "axe-core";
import { MemoryRouter } from "react-router";
import { expect } from "vitest";
import { AppRoutes } from "../App";
import { sessionStore } from "../auth/session";
import { ToastProvider } from "../components/Toasts";
import { ME } from "./fakeApi";

export function signIn(userId = ME) {
  sessionStore.setSession({
    accessToken: "test-token",
    userId,
    expiresAt: Date.now() + 60 * 60 * 1000,
  });
}

/** Renders the whole app at the given address. */
export function renderApp(path: string, state?: unknown) {
  const user = userEvent.setup();
  const result = render(
    <MemoryRouter initialEntries={[{ pathname: path, state }]}>
      <ToastProvider>
        <AppRoutes />
      </ToastProvider>
    </MemoryRouter>,
  );
  return { user, ...result };
}

/**
 * Fails with a readable list of axe-core violations. Colour contrast is
 * skipped because jsdom doesn't compute styles; the palette is checked by hand.
 */
export async function expectNoAxeViolations(context: Element = document.body) {
  const results = await axe.run(context, {
    rules: { "color-contrast": { enabled: false } },
  });
  const summary = results.violations.map(
    (violation) =>
      `${violation.id}: ${violation.help} (${violation.nodes
        .map((node) => node.target.join(" "))
        .join(", ")})`,
  );
  expect(summary).toEqual([]);
}
