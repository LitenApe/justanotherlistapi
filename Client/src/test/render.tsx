import type { RouteConfigEntry } from "@react-router/dev/routes";
import { render } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import axe from "axe-core";
import { createRoutesStub } from "react-router";
import { expect } from "vitest";
import { sessionStore } from "../auth/session";
import * as root from "../root";
import routes from "../routes";
import { ME } from "./fakeApi";

type StubRoute = Parameters<typeof createRoutesStub>[0][number];

/** The exports of a route module that matter in SPA mode. */
interface RouteModule {
  default?: StubRoute["Component"];
  ErrorBoundary?: StubRoute["ErrorBoundary"];
  HydrateFallback?: StubRoute["HydrateFallback"];
  clientLoader?: unknown;
  clientAction?: unknown;
  clientMiddleware?: StubRoute["middleware"];
}

// Route modules keyed like the paths in routes.ts (relative to src/).
const modules = import.meta.glob<RouteModule>(["../**/*.tsx", "!../**/*.test.tsx", "!./**"], {
  eager: true,
});

/** Mirrors routes.ts, so tests run the real middleware, loaders, actions and error boundaries. */
function toStubRoute(entry: RouteConfigEntry): StubRoute {
  const module = modules[`../${entry.file}`];
  if (!module) throw new Error(`routes.ts points at src/${entry.file}, which wasn't found.`);
  const route = {
    id: entry.id ?? entry.file,
    path: entry.path,
    index: entry.index,
    Component: module.default,
    ErrorBoundary: module.ErrorBoundary,
    HydrateFallback: module.HydrateFallback,
    middleware: module.clientMiddleware,
    loader: module.clientLoader,
    action: module.clientAction,
    children: entry.children?.map(toStubRoute),
  };
  // exactOptionalPropertyTypes: leave missing exports out rather than passing undefined.
  return Object.fromEntries(Object.entries(route).filter(([, value]) => value !== undefined));
}

const AppStub = createRoutesStub([
  {
    id: "root",
    path: "/",
    Component: root.default,
    ErrorBoundary: root.ErrorBoundary,
    HydrateFallback: root.HydrateFallback,
    children: routes.map(toStubRoute),
  },
]);

export function signIn(userId = ME) {
  sessionStore.setSession({
    accessToken: "test-token",
    userId,
    expiresAt: Date.now() + 60 * 60 * 1000,
  });
}

/** Renders the whole app at the given address. */
export function renderApp(path: string) {
  const user = userEvent.setup();
  const result = render(<AppStub initialEntries={[path]} />);
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
