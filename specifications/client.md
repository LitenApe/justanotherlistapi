# Client Application Specification

## Table of Contents

- [Overview](#overview)
  - [User Flow](#user-flow)
- [Architecture](#architecture)
  - [Vertical Slice Architecture](#vertical-slice-architecture)
  - [Two-Layer HTTP Stack](#two-layer-http-stack)
  - [Factory Pattern](#factory-pattern)
  - [MVC Component Pattern](#mvc-component-pattern)
  - [Route Registry](#route-registry)
- [Technology Stack](#technology-stack)
- [Routing](#routing)
  - [Route Table](#route-table)
  - [Page Structure](#page-structure)
  - [Navigation Patterns](#navigation-patterns)
  - [Protected Routes](#protected-routes)
- [React 19 Concurrent Features](#react-19-concurrent-features)
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
- [Real-Time Updates (SignalR)](#real-time-updates-signalr)
  - [Connection Lifecycle](#connection-lifecycle)
  - [Overview Page Real-Time](#overview-page-real-time)
  - [Detail Page Real-Time](#detail-page-real-time)
  - [Caller Exclusion](#caller-exclusion)
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

---

## Overview

The Client is a React 19 single-page application that fully utilizes concurrent rendering to build a clean, responsive checklist UI. It connects to the JustAnotherListApi backend (checklist CRUD) and demonstrates that React 19 primitives alone (`use`, `useTransition`, `useDeferredValue`, `useOptimistic`, `useActionState`) are sufficient for a production-quality SPA — helping developers develop an intuition for when they should reach for a third-party library.

The application uses Vertical Slice Architecture to keep each UI feature self-contained. A development-only DevPanel provides chaos controls (artificial delay, compute overhead, error injection) that make concurrent rendering behaviour observable.

**Key constraints:**

- Direct use of React 19 primitives — no third-party data-fetching or state management libraries.
- Single rendering mode: always concurrent. No legacy fallback paths.
- Production-grade patterns: typed errors, factory DI, pure functions, co-located tests.
- Dark mode only. Pleasing aesthetics via CSS custom properties.

### User Flow

1. User logs in → lands on the **Overview page** (`/`)
2. Overview shows all checklists the user belongs to, each as an accordion panel
3. Expanding a panel reveals the checklist's incomplete items — user can toggle items complete directly
4. Items toggled complete become dimmed (strikethrough) but stay visible until next page load
5. Other users' changes to visible items are reflected in real-time via SignalR
6. Clicking a checklist name navigates to the **Detail page** (`/:groupId`) showing ALL items + members
7. Detail page supports full CRUD: create, edit, delete items; manage members; rename/delete checklist

---

## Architecture

### Vertical Slice Architecture

Each UI feature is a self-contained slice under `src/slices/`. Slices own their components, hooks, actions, validators, types, and tests. Cross-slice imports are allowed only through barrel exports (`index.ts`) and are enforced by ESLint boundaries rules.

Infrastructure lives in `src/shared/` and is available to all slices.

```
src/
  shared/       ← infrastructure (api, hooks, components, styles, types, routes)
  slices/       ← feature slices (auth, checklist-overview, checklist-detail, items, members, dev-panel)
  components/   ← app-shell only (Layout)
```

### Two-Layer HTTP Stack

HTTP communication is separated into two layers with clear responsibilities:

| Layer     | Location               | Responsibility                                            | Example                                                |
| --------- | ---------------------- | --------------------------------------------------------- | ------------------------------------------------------ |
| Transport | `shared/api/client.ts` | HOW — timeout, delay, error injection, auth header, fetch | `apiClient.get<ItemGroup[]>(url)`                      |
| Slice API | `slices/*/api.ts`      | WHAT — business semantics, URLs, pending tracking         | `fetchChecklists()` wraps `apiClient.get('/api/list')` |

**Key rule:** Each slice owns its own URL paths in its `api.ts` file. URL knowledge is co-located with the business operation.

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

Every UI feature uses the Model-View-Controller pattern within a single `.tsx` file, separated by section comments:

| Section         | Role       | Contains                                                               |
| --------------- | ---------- | ---------------------------------------------------------------------- |
| `// Model`      | Model      | `useXxxModel()` hook — state, effects, API calls, navigation, handlers |
| `// View`       | View       | `XxxView` — pure presentational JSX, receives all data via typed props |
| `// Controller` | Controller | Thin exported connector: calls model hook, spreads result into view    |

**Rules:**

- Views never import hooks, API modules, or `useNavigate`. They are pure functions of props.
- Models return a flat props-compatible object that spreads directly into the view.
- Controllers are 3–5 lines — no logic, no conditional rendering (guards go above the hook call).
- Views can contain sub-components (e.g. `ItemRow`) that are internal presentation helpers.

```typescript
// ItemCreate.tsx
import { createItem } from "./api";
import { routes } from "@shared/routes";
import styles from "./ItemForm.module.css";

// ─── Model ────────────────────────────────────────────────────────────────────

function useItemCreateModel(groupId: string) { /* ... */ }

// ─── View ─────────────────────────────────────────────────────────────────────

function ItemCreateView({ error, isPending, formAction, cancel }) { /* ... */ }

// ─── Controller ───────────────────────────────────────────────────────────────

export function ItemCreate({ groupId }: Props) {
  const model = useItemCreateModel(groupId);
  return <ItemCreateView {...model} />;
}
```

This separation enables:

- Unit testing models (hook logic) without rendering
- Unit testing views with mock props (no API dependencies)
- Swapping views without touching business logic

**Suspense Split Pattern:** When a controller needs to render content both inside and outside a Suspense boundary, the file contains an internal suspending component:

- `ChecklistDetail` (exported controller) — renders the header (with name from location state) and the `+ New Item` button immediately; wraps `ChecklistDetailContent` in `<PendingBoundary>`.
- `ChecklistDetailContent` (internal, same file) — calls the model hook (which uses `use()` and suspends), then renders the view.

This ensures the header displays instantly on navigation while only the items/members area shows the skeleton fallback.

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
| Testing   | Vitest + React Testing Library + MSW                  | Vite-native, mock at network layer (planned)                          |
| Linting   | ESLint + boundaries + react-hooks + typescript-eslint | Zone enforcement, hook rules, type imports                            |

---

## Routing

### Route Table

| Path                      | Component            | Purpose                                                 |
| ------------------------- | -------------------- | ------------------------------------------------------- |
| `/login`                  | `Login` (React.lazy) | OAuth token exchange                                    |
| `/`                       | `ChecklistOverview`  | All checklists with accordion of incomplete items       |
| `/:groupId`               | `ChecklistDetail`    | Full item list (all items) + members for a single group |
| `/:groupId/items/new`     | `ItemCreate`         | Create item form                                        |
| `/:groupId/items/:itemId` | `ItemEdit`           | Edit item form (name, description)                      |

### Page Structure

**Overview page (`/`)** — the primary view after login:

- Displays all checklists the user belongs to as a vertical list
- Each checklist is an **accordion panel** that expands/collapses to reveal its incomplete items
- Items can be toggled (checked off) directly from the overview without navigating away
- Completed items remain visible (dimmed with strikethrough) until the next fresh GET fetch — what determines presence in the list is whether the item was included in the GET response, not its current `isComplete` state
- Each checklist card has a link/button to navigate into the full detail view
- Real-time updates via SignalR: if another user toggles or updates an item that is currently displayed, the change is reflected immediately

**Detail page (`/:groupId`)** — the full view for a single checklist:

- Displays ALL items (complete and incomplete) with full CRUD capabilities
- Shows member list with add/remove functionality
- Inline item toggle, edit link, delete action
- Real-time updates for items, members, group rename, group delete

### Navigation Patterns

- **After login:** Navigate to `/` (overview page)
- **Checklist drill-down:** From overview, navigate to `routes.checklist(groupId)` to see all items + members
- **Item toggle on overview:** Inline checkbox action, no navigation. `useOptimistic` for instant feedback.
- **Item edit:** "Edit" link navigates to `routes.itemEdit(groupId, itemId)`.
- **Item create:** Button links to `routes.itemCreate(groupId)`.
- **After create/edit:** `navigate(routes.checklist(groupId))` — route remount triggers fresh data fetch.
- **Back to overview:** Navigate to `routes.home()` from detail view.

### Protected Routes

`ProtectedRoute` component checks `authStore.getSnapshot()`. If no token → `<Navigate to="/login" />`. Layout route wraps all authenticated routes.

### Layout Route Structure

```
BrowserRouter (useTransitions={false})
  Routes
    Route path="/" element={<Layout />}
      Route index → ChecklistOverview
      Route path="login" → Login (lazy)
      Route path=":groupId" → ChecklistDetail
      Route path=":groupId/items/new" → ItemCreate
      Route path=":groupId/items/:itemId" → ItemEdit
```

`useTransitions={false}` on `BrowserRouter` disables React Router's internal `startTransition` wrapping of navigation state updates. This ensures `useParams()` and `useLocation()` update synchronously on navigation, enabling immediate Suspense fallback rendering.

---

## React 19 Concurrent Features

### `use()` and Suspense

A module-level promise cache (`Map<string, Promise>` or singleton) stores in-flight/resolved promises keyed by ID. The hook calls `use(getPromise(id))` which suspends on first access and returns immediately from cache on subsequent renders. `PendingBoundary` (Suspense wrapper) shows skeleton fallback during suspension.

**Promise stability:** Promises are cached at module level (`detailCache` Map for checklist detail, `checklistsPromise` singleton for the list). Cache invalidation (`invalidateDetail(id)` / `invalidateChecklists()`) deletes the entry; the next `use()` call creates a fresh promise and re-suspends.

### `useTransition`

Used for **data mutations** — wraps refetch and mutation operations to keep the UI responsive. NOT used for navigation (navigation is always synchronous via `useTransitions={false}` on BrowserRouter).

**Used in:**

- `checklist-overview/hooks.ts` — wraps overview list refresh, create, and item toggle operations
- `checklist-detail/hooks.ts` — wraps detail refresh after item mutations
- `items/hooks.ts` — wraps optimistic item toggle (required for `useOptimistic` to work)

**Effect:** During a transition, the old data stays visible while the new data loads. `isPending` from `useTransition` drives `aria-busy` states. Transitions never suppress Suspense fallbacks for initial data loads — only for refetches where content is already showing.

### `useDeferredValue`

Used in search slices (`checklist-search`, `item-search`).

`const deferredTerm = useDeferredValue(term)` — filter runs on deferred value; input stays responsive. Stale state indicated by `opacity: 0.6` + "Updating…" micro-label.

`React.memo` on `ItemRow` components amplifies the benefit: unchanged rows skip re-render entirely, visible in render count badges.

### `useOptimistic`

Used for item `isComplete` toggle (the primary item interaction).

`useOptimistic` immediately reflects the new state. On error: automatic rollback + `@keyframes flash-error` animation. Optimistic items shown with `opacity: 0.5`, italic, animated pulse dot.

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
5. App re-renders (via `useAuthToken()` hook) → navigates to `/`.

### Auth Store

Plain module (`shared/api/authStore.ts`) — not a React context.

- Factory: `createAuthStore()`
- State: in-memory token + `sessionStorage` persistence (survives refresh, cleared on tab close)
- Exports: `setToken`, `getToken`, `clearToken`, `subscribe`, `getSnapshot`
- Consumed via `useAuthToken()` hook (`shared/hooks/useAuthToken.ts`) which wraps `useSyncExternalStore`

### Token Lifecycle

- On `setToken`: writes to both memory and `sessionStorage`
- On app init: reads from `sessionStorage` (restores session after refresh)
- On 401 response: `clearToken()` removes from both → app re-renders → Login shown
- On `clearToken`: removes from both memory and `sessionStorage`

---

## Data Flow

### Fetching

**Overview page:**

1. `ChecklistOverview` renders, suspending child calls `use(getOverviewPromise())`
2. Fetches `GET /api/list` — returns all groups with incomplete items only
3. `PendingBoundary` shows skeleton fallback during initial load
4. Accordion panels render with checklist names; expanding reveals items

**Detail page:**

1. Navigation updates URL synchronously (`useTransitions={false}`)
2. Route component reads `groupId` from `useParams()`
3. Suspending child calls `use(getDetailPromise(groupId))` → suspends if not cached
4. `PendingBoundary` shows skeleton fallback
5. Promise resolves → component renders ALL items (complete + incomplete) + members

### Mutations

| Operation                    | Location                        | Approach                                                             |
| ---------------------------- | ------------------------------- | -------------------------------------------------------------------- |
| Toggle isComplete (overview) | Inline on accordion ItemRow     | `useOptimistic` → instant → PUT full replacement → rollback on error |
| Toggle isComplete (detail)   | Inline on detail ItemRow        | `useOptimistic` → instant → PUT full replacement → rollback on error |
| Create item                  | `/:groupId/items/new` route     | `useActionState` → navigate back on success                          |
| Edit item                    | `/:groupId/items/:itemId` route | `useActionState` → PUT all fields → navigate back                    |
| Delete item                  | Inline on detail ItemRow        | Two-step confirmation → DELETE → refresh                             |
| Create checklist             | Overview page form              | `useActionState` → refresh overview list                             |
| Rename checklist             | Detail page header              | `useActionState` → PUT → refresh                                     |
| Delete checklist             | Detail page action              | Two-step confirmation → DELETE → navigate to `/`                     |
| Add member                   | Detail page members panel       | Form → POST → refresh members                                        |
| Remove member                | Detail page members panel       | Button → DELETE → handle 409 (last member)                           |

### Data Freshness

- **After item toggle on overview:** Synchronous cache mutation via `updateOverviewItems()` — mutates the cached promise in-place with a React-tagged resolved promise. Component re-renders via `useSyncExternalStore` subscription. No refetch needed. Completed items stay visible (dimmed/strikethrough) until next full fetch.
- **After item toggle/delete on detail:** Synchronous cache mutation via `updateDetailItems()` — same pattern. Also triggers `invalidateOverview()` to refresh overview data on next visit.
- **After item create/edit:** Cache invalidation (`invalidateDetail(groupId)`) + navigate back to detail route (remount triggers fresh fetch). Also invalidates overview cache.
- **After checklist create:** Refresh overview list via `startTransition`.
- **After checklist delete:** Refresh overview list via `startTransition`. Navigate to `/`.
- **Overview list:** Uses `use()` with module-level singleton promise — auto-fetches on first render, subsequent refreshes via `startTransition`.

### Optimistic Updates

For item toggle (`isComplete`) and delete:

1. `useOptimistic` immediately shows new checked/unchecked state (or removes item)
2. API call fires (PUT with flipped `isComplete`, or DELETE)
3. On success: `updateDetailItems()` mutates cache with the same transformation — when the transition commits, `useOptimistic` reveals the base state which now matches the optimistic state
4. On error: automatic rollback (transition ends without cache mutation, base state unchanged)

---

## Real-Time Updates (SignalR)

### Connection Lifecycle

- SignalR auto-connects when auth token is set (`signalRStore`)
- Hub URL: `/hubs/checklist`
- Auto-reconnect with pending group flush (re-joins any previously joined groups)

### Overview Page Real-Time

The overview page joins **all groups** the user belongs to on mount. When another user modifies an item that appears in the overview (i.e., was included in the initial GET response), the change is reflected immediately:

| Event          | Effect on Overview                                               |
| -------------- | ---------------------------------------------------------------- |
| `ItemUpdated`  | Update item in-place (toggle state, name, description)           |
| `ItemCreated`  | Add item to the matching group's accordion (if group is visible) |
| `ItemDeleted`  | Remove item from the matching group's accordion                  |
| `GroupRenamed` | Update checklist name in the accordion header                    |
| `GroupDeleted` | Remove checklist from overview entirely                          |

On unmount (navigating away from overview): leave all groups.

### Detail Page Real-Time

The detail page joins a **single group** on mount:

| Event           | Effect on Detail                  |
| --------------- | --------------------------------- |
| `ItemCreated`   | Add item to list                  |
| `ItemUpdated`   | Update item in-place              |
| `ItemDeleted`   | Remove item from list             |
| `GroupRenamed`  | Update header name                |
| `GroupDeleted`  | Navigate to `/` with notification |
| `MemberAdded`   | Add member to members panel       |
| `MemberRemoved` | Remove member from members panel  |

### Caller Exclusion

All mutations send `X-SignalR-Connection-Id` header. The backend excludes the calling connection from broadcasts — prevents the originator from receiving their own mutation back (which would conflict with optimistic state).

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

`use(promise)` rejection caught by `ErrorBoundary` in `PendingBoundary`. Error panel with status message + "Retry" button (increments `retryKey` → remount → re-fetch).

### Mutation Errors

Action functions catch `HttpError`, pattern-match status, return typed error state. Displayed inline near the action that caused it. NOT caught by `ErrorBoundary`.

For `useOptimistic`: rollback is automatic (React reverts to server state). Visual: `@keyframes flash-error` (brief red background → fade).

### 401 Handling

`client.ts` detects 401 → `authStore.clearToken()` → `useAuthToken()` triggers re-render → `ProtectedRoute` redirects to `/login`.

### Unhandled Rejections

`main.tsx` registers `window.addEventListener('unhandledrejection', ...)` — logs to console in dev, would integrate with error monitoring in production.

---

## Inter-Slice Contracts

### Layer 1: Index Barrel

Each slice exports its public API from `index.ts`. No deep imports allowed.

### Layer 2: Prop Interfaces

Typed interfaces define the contract between Layout/pages and slice components:

| Slice               | Props                                                      |
| ------------------- | ---------------------------------------------------------- |
| `ChecklistOverview` | None (fetches own data)                                    |
| `ChecklistDetail`   | Uses `useParams()` + `useLocation().state.name` internally |
| `ItemList`          | `{ groupId: string; items: Item[] }`                       |
| `ItemSearch`        | `{ items: Item[] }`                                        |
| `Members`           | `{ groupId: string }`                                      |

### Layer 3: ESLint Enforcement

`eslint-plugin-boundaries` enforces zone rules:

- `shared/` + `components/` → accessible from anywhere
- `slices/*` → importable from other slices, `components/Layout.tsx`, and `App.tsx` (barrel-only via `internalPath: "index.ts"`)
- No deep imports past the barrel (`@slices/foo/internal` forbidden)
- No parent-relative imports (`../` forbidden via `no-restricted-imports`)

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
- **Accordion panel:** Checklist name as header (clickable to expand/collapse). Chevron icon indicates state. Smooth height transition on expand/collapse.
- **Completed items (overview):** `text-decoration: line-through` + `color: var(--text-muted)` + `opacity: 0.6`. Remain in the list until next fresh fetch; toggling marks them done visually but does not remove them.
- **Completed items (detail):** `line-through` + `color: var(--text-muted)`.
- **Optimistic items:** `opacity: 0.5`, `font-style: italic`, animated pulse dot (`--accent`).
- **Rollback animation:** `@keyframes flash-error` — brief `--error` background → fade out.
- **Delete:** Two-step inline confirmation (click → "Confirm delete" state, blur cancels).

### Loading States

Suspense fallbacks use animated skeleton screens:

- `@keyframes shimmer` — gradient sweep, 1.5s infinite
- Overview skeleton: 3–4 checklist card placeholders with collapsed accordion
- Detail skeleton: title block + 5 item row placeholders

### Empty States

- No checklists (overview): Centered message + "Create your first checklist" prompt
- No incomplete items in accordion: "All done!" message or collapsed by default
- No items (detail): "No items yet" + inline create link visible

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
      features.tsx
      types.ts
      api/
        client.ts
        authStore.ts
        signalrStore.ts
        delay.ts
        computeOverhead.ts
        errorRate.ts
        pendingService.ts
        activityLog.ts
      hooks/
        useAuthToken.ts
        usePending.ts
        useSignalRStatus.ts
        useRenderCount.ts
        useTrackedActionState.ts
        useTrackedTransition.ts
      styles/
        tokens.css
        global.css
        skeletons.module.css
      components/
        ErrorBoundary.tsx
        PendingBoundary.tsx
        PendingBorder.tsx
        ProtectedRoute.tsx
        RenderCount.tsx
    slices/
      auth/
        index.ts
        api.ts
        Login.tsx
        Login.module.css
      checklist-overview/
        index.ts
        api.ts
        hooks.ts
        ChecklistOverview.tsx
        ChecklistOverview.module.css
      checklist-detail/
        index.ts
        api.ts
        hooks.ts
        ChecklistDetail.tsx
        ChecklistDetail.module.css
      items/
        index.ts
        api.ts
        hooks.ts
        ItemList.tsx
        ItemList.module.css
        ItemCreate.tsx
        ItemEdit.tsx
        ItemForm.module.css
        ItemCreatePage.tsx
        ItemEditPage.tsx
      members/
        index.ts
        api.ts
        hooks.ts
        Members.tsx
        Members.module.css
      dev-panel/
        index.ts
        hooks.ts
        DevPanel.tsx
        DevPanel.module.css
        sessionPersistence.ts
    components/
      Layout.tsx
      Layout.module.css
```

Each slice contains a single `.tsx` file with Model, View, and Controller sections. Page-level route wrappers (e.g., `ItemCreatePage.tsx`) live in their owning slice.

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
