# Client Application Specification

## Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
  - [Vertical Slice Architecture](#vertical-slice-architecture)
  - [Three-Layer HTTP Stack](#three-layer-http-stack)
  - [Factory Pattern](#factory-pattern)
  - [MVC Component Pattern](#mvc-component-pattern)
  - [Route Registry](#route-registry)
- [Technology Stack](#technology-stack)
- [Routing](#routing)
  - [Route Table](#route-table)
  - [Navigation Patterns](#navigation-patterns)
  - [Protected Routes](#protected-routes)
- [React 19 Concurrent Features](#react-19-concurrent-features)
  - [Feature Flags](#feature-flags)
  - [use() and Suspense](#use-and-suspense)
  - [useTransition](#usetransition)
  - [useDeferredValue](#usedeferredvalue)
  - [useOptimistic](#useoptimistic)
  - [useActionState](#useactionstate)
- [Authentication](#authentication)
- [Data Flow](#data-flow)
  - [Fetching](#fetching)
  - [Mutations](#mutations)
  - [Data Freshness](#data-freshness)
  - [Optimistic Updates](#optimistic-updates)
- [Error Handling](#error-handling)
  - [Typed HTTP Errors](#typed-http-errors)
  - [Render Errors](#render-errors)
  - [Mutation Errors](#mutation-errors)
  - [401 Handling](#401-handling)
- [Inter-Slice Contracts](#inter-slice-contracts)
- [Visual Design](#visual-design)
  - [Design Tokens](#design-tokens)
  - [Typography](#typography)
  - [Component Aesthetics](#component-aesthetics)
  - [Loading States](#loading-states)
  - [Empty States](#empty-states)
  - [Accessibility](#accessibility)
- [File Structure](#file-structure)
- [Build and Tooling](#build-and-tooling)
  - [Vite Configuration](#vite-configuration)
  - [TypeScript Configuration](#typescript-configuration)
  - [ESLint Configuration](#eslint-configuration)
  - [Package Scripts](#package-scripts)
- [Aspire Integration](#aspire-integration)
- [Testing Strategy](#testing-strategy)

---

## Overview

The Client is a React 19 single-page application that serves as an interactive explorer for React's concurrent rendering capabilities. It connects to the JustAnotherListApi backend (checklist CRUD) and provides a side-by-side comparison of concurrent vs. legacy rendering patterns for each feature.

The application uses Vertical Slice Architecture to keep each UI feature self-contained. A development-only DevPanel provides chaos controls (artificial delay, compute overhead, error injection) and per-feature toggles that make concurrent rendering behaviour observable.

**Key constraints:**

- Direct use of React 19 primitives — no third-party data-fetching or state management libraries.
- Each concurrent feature has a Legacy counterpart for comparison.
- Production-grade patterns: typed errors, factory DI, pure functions, co-located tests.
- Dark mode only. Pleasing aesthetics via CSS custom properties.

---

## Architecture

### Vertical Slice Architecture

Each UI feature is a self-contained slice under `src/slices/`. Slices own their components, hooks, actions, validators, types, and tests. Slices never import from each other — communication happens through prop interfaces and callbacks wired in the Layout.

Infrastructure lives in `src/shared/` and is available to all slices.

```
src/
  shared/       ← infrastructure (api, hooks, components, styles, types, routes)
  slices/       ← feature slices (auth, checklists, items, members, dev-panel, etc.)
  components/   ← app-shell only (Layout)
```

### Three-Layer HTTP Stack

HTTP communication is separated into three layers with clear responsibilities:

| Layer     | Location                    | Responsibility                                            | Example                                                             |
| --------- | --------------------------- | --------------------------------------------------------- | ------------------------------------------------------------------- |
| Transport | `shared/api/client.ts`      | HOW — timeout, delay, error injection, auth header, fetch | `apiClient.get<ItemGroup[]>(url)`                                   |
| Resource  | `shared/api/resources/*.ts` | WHERE — URL paths, HTTP methods, request shaping          | `checklistsResource.list()`                                         |
| Slice API | `slices/*/api.ts`           | WHAT — business semantics, pending tracking               | `getItemGroups()` wraps `track('checklists/list', resource.list())` |

**Key rule:** Slices never see URL paths. URLs exist in exactly one place (the resource layer).

### Factory Pattern

Every service module exports a factory function, a TypeScript type, and a default singleton instance:

```typescript
export function createDelayStore() {
  /* ... */
}
export type DelayStore = ReturnType<typeof createDelayStore>;
export const delayStore = createDelayStore();
```

Dependencies between services are explicit:

```typescript
export function createPendingService(log: ActivityLog) {
  /* ... */
}
export const pendingService = createPendingService(activityLog);
```

Tests create fresh instances — no shared mutable state, no `__resetForTesting()`, safe for parallel execution.

### MVC Component Pattern

Every UI feature is split into three files that map to Model-View-Controller:

| File              | Role       | Contains                                                                 |
| ----------------- | ---------- | ------------------------------------------------------------------------ |
| `Xxx.model.ts`    | Model      | `useXxxModel()` hook — state, effects, API calls, navigation, handlers   |
| `Xxx.view.tsx`    | View       | `XxxView` — pure presentational JSX, receives all data via typed props   |
| `Xxx.tsx`         | Controller | Thin connector: calls model hook, spreads result into view               |

**Rules:**
- Views never import hooks, API modules, or `useNavigate`. They are pure functions of props.
- Models return a flat props-compatible object that spreads directly into the view.
- Controllers are 3–5 lines — no logic, no conditional rendering (guards go above the hook call).
- Views can contain sub-components (e.g. `ItemRow`) that are internal presentation helpers.

```typescript
// ItemCreate.tsx (Controller)
import { ItemCreateView } from "./ItemCreate.view";
import { useItemCreateModel } from "./ItemCreate.model";

export function ItemCreate({ groupId }: Props) {
  const model = useItemCreateModel(groupId);
  return <ItemCreateView {...model} />;
}
```

This separation enables:
- Unit testing models (hook logic) without rendering
- Unit testing views with mock props (no API dependencies)
- Swapping views without touching business logic

### Route Registry

All URL paths are defined in a single registry (`shared/routes.ts`). Slices never hardcode path strings — they reference route builders by intent:

```typescript
// shared/routes.ts
export const routes = {
  home: () => "/",
  checklist: (groupId: string) => `/${groupId}`,
  itemCreate: (groupId: string) => `/${groupId}/items/new`,
  itemEdit: (groupId: string, itemId: string) => `/${groupId}/items/${itemId}`,
  login: () => "/login",
};
```

Models call `navigate(routes.checklist(groupId))` instead of `navigate(`/${groupId}`)`. This decouples feature slices from the URL structure — if paths change, only `routes.ts` and `App.tsx` need updating.

---

## Technology Stack

| Category  | Choice                                                | Rationale                                                             |
| --------- | ----------------------------------------------------- | --------------------------------------------------------------------- |
| Framework | React 19                                              | Target: concurrent rendering primitives                               |
| Bundler   | Vite + @vitejs/plugin-react-swc                       | Fast HMR, SWC-based JSX transform                                     |
| Language  | TypeScript (strict)                                   | Type safety, `noUncheckedIndexedAccess`, `exactOptionalPropertyTypes` |
| Routing   | React Router v7                                       | Standard router; keeps routing logic out of demo code                 |
| Styling   | CSS Modules + CSS custom properties                   | Scoped styles, design token system                                    |
| Testing   | Vitest + React Testing Library + MSW                  | Vite-native, mock at network layer                                    |
| Linting   | ESLint + boundaries + react-hooks + typescript-eslint | Zone enforcement, hook rules, type imports                            |

---

## Routing

### Route Table

| Path                      | Component            | Purpose                              |
| ------------------------- | -------------------- | ------------------------------------ |
| `/login`                  | `Login` (React.lazy) | OAuth token exchange                 |
| `/`                       | Empty state          | No group selected                    |
| `/:groupId`               | `ChecklistDetail`    | Items list (inline toggle) + members |
| `/:groupId/items/new`     | `ItemCreate`         | Create item form                     |
| `/:groupId/items/:itemId` | `ItemEdit`           | Edit item form (name, description)   |

### Navigation Patterns

- **Sidebar group selection:** Navigates to `routes.checklist(groupId)` — wrapped in `startTransition` when concurrent mode active.
- **Item toggle (isComplete):** Inline checkbox action, no navigation. `useOptimistic` for instant feedback.
- **Item edit:** "Edit" link navigates to `routes.itemEdit(groupId, itemId)`. Only for name/description changes.
- **Item create:** Button links to `routes.itemCreate(groupId)`.
- **After create/edit:** `navigate(routes.checklist(groupId))` — route remount triggers fresh data fetch.

### Protected Routes

`ProtectedRoute` component checks `authStore.getSnapshot()`. If no token → `<Navigate to="/login" />`. Layout route wraps all authenticated routes.

### Layout Route Structure

```
BrowserRouter
  Routes
    Route path="/" element={<Layout />}
      Route index → empty state
      Route path="login" → Login (lazy)
      Route path=":groupId" → ChecklistDetail
      Route path=":groupId/items/new" → ItemCreate
      Route path=":groupId/items/:itemId" → ItemEdit
```

`useParams()` extracts IDs. Layout reads `useLocation()` for active sidebar highlight.

---

## React 19 Concurrent Features

### Feature Flags

Toggled at runtime via the DevPanel through `FeaturesContext`:

| Flag               | Controls                                                             |
| ------------------ | -------------------------------------------------------------------- |
| `useConcurrent`    | Master toggle: `use()` + Suspense + transitions vs. Legacy variants  |
| `showRenderCounts` | Render count badges on components                                    |

When `useConcurrent` is off, Legacy variants render for all features.

### `use()` and Suspense

**Concurrent variant:** Parent component creates a promise via `useMemo([id, retryKey])` and passes it as a prop. Child component calls `use(promise)` — suspends until resolved. `PendingBoundary` (Suspense + ErrorBoundary) shows skeleton fallback.

**Legacy variant:** `useEffect` fires fetch on mount/deps change. `AbortController` in cleanup cancels stale requests. Manual `loading`/`error`/`data` state via `useReducer`.

**Promise stability:** The promise reference must be stable across re-renders. Created in the parent's dispatcher via `useMemo` keyed on `[id, retryKey]`. Never created inside the suspending component.

### `useTransition`

Lives in **Layout only**. Wraps `navigate()` calls for group selection.

- **Concurrent:** `startTransition(() => navigate(...))` — React keeps old content visible while new route suspends. `isPending` drives the global pending border + `aria-busy`.
- **Legacy:** Direct `navigate()` — immediate Suspense fallback (skeleton) on every navigation.

### `useDeferredValue`

Used in search slices (`checklist-search`, `item-search`).

- **Concurrent:** `const deferredTerm = useDeferredValue(term)` — filter runs on deferred value; input stays responsive. Stale state indicated by `opacity: 0.6` + "Updating…" micro-label.
- **Legacy:** Filter runs synchronously on every keystroke — with compute overhead, the input visibly freezes.

`React.memo` on `ItemRow` components amplifies the benefit: unchanged rows skip re-render entirely, visible in render count badges.

### `useOptimistic`

Used for item `isComplete` toggle (the primary item interaction).

- **Concurrent:** `useOptimistic` immediately reflects the new state. On error: automatic rollback + `@keyframes flash-error` animation. Optimistic items shown with `opacity: 0.5`, italic, animated pulse dot.
- **Legacy:** Wait for server response before updating UI. Loading spinner on the toggled item.

Temp IDs for optimistic creates use `crypto.randomUUID()` for stable React keys.

### `useActionState`

Used in all form slices (`ChecklistForm`, `ItemCreate`, `ItemEdit`). No variant split — always concurrent.

Action functions are pure: `(prevState, formData) → Promise<State>`. They validate, call the API, and return typed state. `isPending` from `useActionState` drives form disable state and pending tracking.

---

## Authentication

### Flow

1. User lands on `/login` (or is redirected by `ProtectedRoute`).
2. Login form is pre-filled: `client_id: 00000000-0000-0000-0000-000000000001`, `client_secret: dev`.
3. Submit POSTs to `/default/token` (form-urlencoded, `grant_type: client_credentials`).
4. On success: `authStore.setToken(token)` — persists to `sessionStorage`.
5. App re-renders (via `useSyncExternalStore`) → navigates to `/`.

### Auth Store

Plain module (`shared/api/authStore.ts`) — not a React context.

- Factory: `createAuthStore()`
- State: in-memory token + `sessionStorage` persistence (survives refresh, cleared on tab close)
- Exports: `setToken`, `getToken`, `clearToken`, `subscribe`, `getSnapshot`
- Used via `useSyncExternalStore` in the `useAuth()` hook

### Token Lifecycle

- On `setToken`: writes to both memory and `sessionStorage`
- On app init: reads from `sessionStorage` (restores session after refresh)
- On 401 response: `clearToken()` removes from both → app re-renders → Login shown
- On `clearToken`: removes from both memory and `sessionStorage`

---

## Data Flow

### Fetching

**Concurrent path:**

1. Layout navigates (optionally in `startTransition`)
2. Route component's dispatcher creates promise via `useMemo([id, retryKey])`
3. Child component calls `use(promise)` → suspends
4. `PendingBoundary` shows skeleton fallback
5. Promise resolves → component renders data

**Legacy path:**

1. Navigation triggers route mount
2. `useEffect` fires with `AbortController`
3. Manual loading state shown
4. On success: `dispatch({ type: 'success', data })`
5. On cleanup (deps change): `controller.abort()` cancels stale request

### Mutations

| Operation         | Location                        | Approach                                                             |
| ----------------- | ------------------------------- | -------------------------------------------------------------------- |
| Toggle isComplete | Inline on ItemRow               | `useOptimistic` → instant → PUT full replacement → rollback on error |
| Create item       | `/:groupId/items/new` route     | `useActionState` → navigate back on success                          |
| Edit item         | `/:groupId/items/:itemId` route | `useActionState` → PUT all fields → navigate back                    |
| Delete item       | Inline on ItemRow               | Two-step confirmation → DELETE → refresh                             |
| Create checklist  | Sidebar form                    | `useActionState` → navigate to new `/:groupId`                       |
| Rename checklist  | Inline edit                     | `useActionState` → PUT → refresh sidebar                             |
| Delete checklist  | Sidebar action                  | Two-step confirmation → DELETE → navigate to `/`                     |
| Add member        | Members panel                   | Form → POST → refresh members                                        |
| Remove member     | Members panel                   | Button → DELETE → handle 409 (last member)                           |

### Data Freshness

- **After item create/edit:** Navigate back to `/:groupId` triggers route remount → fresh fetch.
- **After checklist create:** Navigate to `/:newId` + increment `refreshSignal` for sidebar.
- **After checklist delete:** Navigate to `/` + increment `refreshSignal`.
- **Sidebar refreshes on:** Checklist create, rename, delete. NOT on item mutations (sidebar shows names only).

### Optimistic Updates

For item toggle (`isComplete`):

1. `useOptimistic` immediately shows new checked/unchecked state
2. PUT fires with all current fields + flipped `isComplete`
3. On success: re-fetch confirms state (optimistic item replaced by server truth)
4. On error: automatic rollback + `flash-error` animation + inline error message

---

## Error Handling

### Typed HTTP Errors

```typescript
export class HttpError extends Error {
  constructor(
    public readonly status: number,
    message: string,
  ) {
    super(message);
  }
}
export class SimulatedNetworkError extends Error {}
export class UnauthorizedError extends Error {}
```

Action functions pattern-match on `status`:

- `409` → "Already a member" / "Cannot remove last member"
- `403` → "Access denied"
- `404` → "Not found"
- `400` → "Invalid input" (should not reach API if client validates)

### Render Errors

- **Concurrent:** `use(promise)` rejection caught by `ErrorBoundary` in `PendingBoundary`. Error panel with status message + "Retry" button (increments `retryKey` → remount → re-fetch).
- **Legacy:** `useReducer` error state; inline error display within the component.

### Mutation Errors

Action functions catch `HttpError`, pattern-match status, return typed error state. Displayed inline near the action that caused it. NOT caught by `ErrorBoundary`.

For `useOptimistic`: rollback is automatic (React reverts to server state). Visual: `@keyframes flash-error` (brief red background → fade).

### 401 Handling

`client.ts` detects 401 → `authStore.clearToken()` → `useSyncExternalStore` triggers re-render → `ProtectedRoute` redirects to `/login`.

### Unhandled Rejections

`main.tsx` registers `window.addEventListener('unhandledrejection', ...)` — logs to console in dev, would integrate with error monitoring in production.

---

## Inter-Slice Contracts

### Layer 1: Index Barrel

Each slice exports its public API from `index.ts`. No deep imports allowed.

### Layer 2: Prop Interfaces

Typed interfaces define the contract between Layout and slice components:

| Slice             | Props                                                           |
| ----------------- | --------------------------------------------------------------- |
| `ChecklistList`   | `{ refreshSignal: number; onCreated: (newId: string) => void }` |
| `ChecklistSearch` | `{ items: ItemGroup[] }`                                        |
| `ChecklistDetail` | Uses `useParams()` internally                                   |
| `ItemList`        | `{ groupId: string; items: Item[]; onMutate: () => void }`      |
| `ItemSearch`      | `{ items: Item[] }`                                             |
| `Members`         | `{ groupId: string }`                                           |
| `ChecklistForm`   | `{ onCreated: (newId: string) => void }`                        |

### Layer 3: ESLint Enforcement

`eslint-plugin-boundaries` enforces zone rules:

- `shared/` + `components/` → accessible from anywhere
- `slices/*` → only importable from `components/Layout.tsx` and `App.tsx`
- No cross-slice imports. No deep imports past the barrel.

---

## Visual Design

### Design Tokens

All tokens defined as CSS custom properties on `:root` in `shared/styles/tokens.css`:

**Colors:**
| Token | Value | Usage |
|-------|-------|-------|
| `--bg` | `#0f172a` | Page background |
| `--surface` | `#1e293b` | Card/panel background |
| `--surface-2` | `#334155` | Elevated elements, inputs |
| `--surface-3` | `#475569` | Highest elevation |
| `--text` | `#f1f5f9` | Primary text |
| `--text-muted` | `#94a3b8` | Secondary text |
| `--border` | `#334155` | Borders |
| `--accent` | `#22d3ee` | Interactive elements, links |
| `--accent-hover` | `#67e8f9` | Hover state |
| `--error` | `#f87171` | Error states |
| `--success` | `#4ade80` | Success states |
| `--warning` | `#fb923c` | Warning states |

**Spacing:** `--space-1` (4px) through `--space-12` (48px).

**Radius:** `--radius-sm` (4px), `--radius-md` (8px), `--radius-lg` (12px), `--radius-full` (9999px).

**Transitions:** `--transition: background-color 150ms ease, color 150ms ease, border-color 150ms ease, opacity 150ms ease`

### Typography

| Token         | Value                            |
| ------------- | -------------------------------- |
| `--font-sans` | `'Inter', system-ui, sans-serif` |
| `--font-mono` | `'JetBrains Mono', monospace`    |
| `--text-xs`   | `0.75rem`                        |
| `--text-sm`   | `0.875rem`                       |
| `--text-base` | `1rem`                           |
| `--text-lg`   | `1.125rem`                       |
| `--text-xl`   | `1.25rem`                        |

Fonts loaded via `<link>` in `index.html` from Google Fonts.

### Component Aesthetics

- **Elevation:** No box-shadows. Layered via background color progression: `--bg` → `--surface` → `--surface-2` → `--surface-3`.
- **Cards:** `border: 1px solid var(--border); border-radius: var(--radius-md)`.
- **Buttons:** Primary (`--accent` bg), Ghost (transparent, `--accent` border), Danger (`--error` bg). All with `--radius-md` and focus ring.
- **Inputs:** `--surface-2` background, `--border` border, `--accent` 2px focus border.
- **Sidebar selected:** 3px left border `--accent` + `--surface-2` background.
- **Completed items:** `line-through` + `color: var(--text-muted)`.
- **Optimistic items:** `opacity: 0.5`, `font-style: italic`, animated pulse dot (`--accent`).
- **Rollback animation:** `@keyframes flash-error` — brief `--error` background → fade out.
- **Delete:** Two-step inline confirmation (click → "Confirm delete" state, blur cancels).

### Loading States

Suspense fallbacks use animated skeleton screens:

- `@keyframes shimmer` — gradient sweep, 1.5s infinite
- Sidebar skeleton: 3–4 pill shapes
- Detail skeleton: title block + 5 item row placeholders

### Empty States

- No checklists: Centered message + "Create your first checklist" prompt
- No items: "No items yet" + inline create link visible

### Accessibility

- Semantic HTML: `<nav>` sidebar, `<main>` content, `h1`/`h2`/`h3` hierarchy
- `useId()` for form input `id`/`htmlFor` pairs
- `aria-busy="true"` on content area during `useTransition` pending
- Focus management: selecting a group moves focus to detail panel heading
- Keyboard navigation: Tab focus order, visible focus rings (`--accent` outline)
- `prefers-reduced-motion`: All animations/transitions reduced to `0.01ms`

---

## File Structure

```
Client/
  index.html
  vite.config.ts
  tsconfig.json
  package.json
  eslint.config.js
  src/
    main.tsx
    App.tsx
    shared/
      routes.ts
      api/
        client.ts
        authStore.ts
        delay.ts
        computeOverhead.ts
        errorRate.ts
        pendingService.ts
        activityLog.ts
        resources/
          auth.ts
          checklists.ts
          items.ts
          members.ts
          seed.ts
      hooks/
        usePendingReporter.ts
        useRenderCount.ts
      types.ts
      styles/
        tokens.css
        global.css
        skeletons.module.css
      components/
        ErrorBoundary.tsx
        PendingBoundary.tsx
        PendingBorder.tsx
    slices/
      auth/
        index.ts
        api.ts
        Login.tsx
        Login.model.ts
        Login.view.tsx
        Login.module.css
      checklists/
        index.ts
        api.ts
        hooks.ts
        ChecklistList.tsx
        ChecklistList.model.ts
        ChecklistList.view.tsx
        ChecklistList.module.css
      checklist-search/
        index.ts
        hooks.ts
        filter.ts
        ChecklistSearch.tsx
        ChecklistSearch.module.css
      checklist-detail/
        index.ts
        api.ts
        hooks.ts
        ChecklistDetail.tsx
        ChecklistDetail.model.ts
        ChecklistDetail.view.tsx
        ChecklistDetail.module.css
      items/
        index.ts
        api.ts
        hooks.ts
        ItemList.tsx
        ItemList.model.ts
        ItemList.view.tsx
        ItemList.module.css
        ItemCreate.tsx
        ItemCreate.model.ts
        ItemCreate.view.tsx
        ItemEdit.tsx
        ItemEdit.model.ts
        ItemEdit.view.tsx
        ItemForm.module.css
        ItemCreatePage.tsx
        ItemEditPage.tsx
      members/
        index.ts
        api.ts
        Members.tsx
        Members.model.ts
        Members.view.tsx
        Members.module.css
      dev-panel/
        index.ts
        DevPanel.tsx
        DevPanel.model.ts
        DevPanel.view.tsx
        DevPanel.module.css
        FeaturesContext.tsx
    components/
      Layout.tsx
      Layout.module.css
```

Each slice follows the MVC triple: `Component.tsx` (controller), `Component.model.ts` (model hook), `Component.view.tsx` (view). Page-level route wrappers (e.g., `ItemCreatePage.tsx`) live in their owning slice.

---

## Build and Tooling

### Vite Configuration

```typescript
// vite.config.ts
export default defineConfig({
  plugins: [react()],
  server: {
    port: parseInt(process.env.PORT ?? "5173"),
    proxy: {
      "/api": process.env.services__Core__http__0 ?? "http://localhost:55733",
      "/default":
        process.env.services__oauth__http__0 ?? "http://localhost:8080",
    },
  },
  resolve: {
    alias: {
      "@shared": "/src/shared",
      "@slices": "/src/slices",
      "@components": "/src/components",
    },
  },
  build: { sourcemap: true },
  esbuild: { drop: ["console", "debugger"] }, // production only
});
```

### TypeScript Configuration

- `strict: true`
- `noUncheckedIndexedAccess: true` — `items[0]` is `Item | undefined`
- `exactOptionalPropertyTypes: true` — `{ name?: string }` won't accept `{ name: undefined }`
- `paths` with relative `./` prefixes (no `baseUrl` — deprecated in TS 7.0)

### ESLint Configuration

- `eslint-plugin-boundaries` — zone enforcement (shared, slices, components)
- `eslint-plugin-react-hooks` — `exhaustive-deps` rule (catches stale closures)
- `@typescript-eslint/consistent-type-imports` — enforces `import type` for type-only imports

### Package Scripts

| Script      | Command                                             | Purpose                       |
| ----------- | --------------------------------------------------- | ----------------------------- |
| `dev`       | `vite`                                              | Development server            |
| `build`     | `tsc --noEmit && vite build`                        | Type-check + production build |
| `typecheck` | `tsc --noEmit`                                      | Type checking only            |
| `lint`      | `eslint .`                                          | Lint all files                |
| `test`      | `vitest`                                            | Run tests in watch mode       |
| `coverage`  | `vitest --coverage`                                 | Coverage report               |
| `check`     | `npm run typecheck && npm run lint && npm run test` | CI gate                       |

---

## Aspire Integration

The client is integrated into the .NET Aspire orchestrator:

**Changes required:**

- `Directory.Packages.props`: Add `Aspire.Hosting.NodeJs` package
- `Aspire/Aspire.csproj`: Add package reference
- `Aspire/Program.cs`: Register client app

```csharp
var client = builder.AddNpmApp("client", "../Client", "dev")
    .WithReference(core)
    .WithReference(oauth)
    .WaitFor(core)
    .WithHttpEndpoint(port: 5173, env: "PORT");
```

**Environment variables injected:**

- `services__Core__http__0` — Core API base URL
- `services__oauth__http__0` — OAuth server base URL
- `PORT` — Dev server port (5173)

**Fallback values** (running without Aspire): `http://localhost:55733` (Core), `http://localhost:8080` (OAuth).

---

## Testing Strategy

### Stack

- **Vitest** — Vite-native test runner (shares config, aliases, plugins)
- **React Testing Library** — Component testing (`@testing-library/react` v16+)
- **MSW (Mock Service Worker)** — Network-level mocking
- **Setup:** `src/test-setup.ts` — imports `@testing-library/jest-dom`, starts MSW server

### Test Organization

Tests are co-located within their slice:

```
slices/items/ItemList/
  ItemList.test.tsx
  useItemListConcurrent.test.ts
  useItemListLegacy.test.ts
  actions.test.ts
```

### Testing by Layer

| Layer                             | Tool                             | Asserts                                          |
| --------------------------------- | -------------------------------- | ------------------------------------------------ |
| `filter.ts`, validators, reducers | Vitest unit                      | Input → output, edge cases                       |
| `actions.ts`                      | Vitest + mock resource           | State transitions on success/error/validation    |
| Resource clients                  | Vitest + mock ApiClient          | Correct path, method, body                       |
| `createApiClient`                 | Vitest + MSW                     | Pipeline: delay, error injection, 401, timeout   |
| Slice `api.ts`                    | Vitest + mock resource           | `track()` called with correct ID                 |
| Custom hooks                      | `renderHook` + `act` + `waitFor` | State transitions, isPending, error states       |
| Components                        | RTL + MSW                        | Renders hook state, user interactions, callbacks |

### Testing Concurrent Features

- **useTransition:** `act(async () => {...})`, assert `isPending` true during transition, false after.
- **useOptimistic:** Assert optimistic state appears before await, server state after `waitFor`.
- **useDeferredValue:** `waitFor` for deferred value to settle; assert stale state shown during deferral.
