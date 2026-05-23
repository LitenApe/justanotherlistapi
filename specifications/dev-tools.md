# Dev Tools Specification

## Table of Contents

- [Overview](#overview)
- [DevPanel](#devpanel)
  - [Display Flags](#display-flags)
  - [Chaos Controls](#chaos-controls)
  - [Presets](#presets)
  - [Activity Log](#activity-log)
  - [Render Count Badges](#render-count-badges)
  - [Seed Data Trigger](#seed-data-trigger)
  - [Visual Design](#visual-design)
  - [Keyboard Shortcut](#keyboard-shortcut)
  - [Session Persistence](#session-persistence)
- [Seed Endpoint](#seed-endpoint)
  - [Endpoint Definition](#endpoint-definition)
  - [Data Generation](#data-generation)
  - [Registration](#registration)
  - [Dependencies](#dependencies)
- [Dev Guards](#dev-guards)
  - [import.meta.env.DEV Gating](#importmetaenvdev-gating)
  - [React StrictMode](#react-strictmode)
  - [Unhandled Rejection Handler](#unhandled-rejection-handler)
- [Global Pending Border](#global-pending-border)
- [Pending Service](#pending-service)
- [Activity Log Module](#activity-log-module)

---

## Overview

Dev tools are development-only features that make React's concurrent rendering behaviour observable under stress. They are completely excluded from production builds via `import.meta.env.DEV` guards (Vite dead-code elimination).

The system consists of:

1. **DevPanel** — UI control panel for chaos injection, render count display, and activity monitoring
2. **Seed endpoint** — Server-side bulk data generator for meaningful test scenarios
3. **Dev guards** — Build-time feature gating, StrictMode, and error monitoring
4. **Pending infrastructure** — Global pending border + pending service + activity log

---

## DevPanel

The DevPanel is a collapsible panel rendered only in development builds. It provides chaos controls that stress the application's concurrent rendering and an activity log to observe the results.

**Location:** `src/slices/dev-panel/DevPanel.tsx` — single file with MVC sections:

- Model: `useDevPanelModel()` hook (store subscriptions, keyboard shortcut, preset logic, seed handler)
- View: `DevPanelView` (pure rendering of sliders, buttons, log entries)
- Controller: exported `DevPanel` connecting model → view

### Display Flags

Managed via `FeaturesContext` — the only React context in the application (used for display flags, not data).

| Flag               | Type      | Default | Effect                                     |
| ------------------ | --------- | ------- | ------------------------------------------ |
| `showRenderCounts` | `boolean` | `false` | Displays render count badges on components |

This is an observation tool — it does not change application behaviour, only reveals how React re-renders components under different conditions.

### Chaos Controls

Three independent controls that simulate real-world conditions:

#### API Delay (`shared/api/delay.ts`)

- **Type:** Async (does not block main thread)
- **Range:** 0–5000ms (slider)
- **Effect:** `await sleep(getDelay())` before every fetch in `createApiClient`
- **Purpose:** Makes Suspense fallbacks, pending borders, and transitions visible

#### Compute Overhead (`shared/api/computeOverhead.ts`)

- **Type:** Synchronous (blocks main thread intentionally)
- **Range:** 0–500ms (slider)
- **Effect:** Busy-wait loop during filter functions
- **Purpose:** Makes `useDeferredValue` benefit observable — without it, heavy filtering would freeze the input; with it, input stays responsive while results update in the background
- **Key teaching moment:** The pending border will NOT appear during sync overhead because the thread is blocked (React cannot update DOM). This is intentional.

#### Error Rate (`shared/api/errorRate.ts`)

- **Type:** Random injection
- **Range:** 0–100% (slider)
- **Effect:** Before each fetch, if `Math.random() < rate`, throws `SimulatedNetworkError`
- **Purpose:** Demonstrates ErrorBoundary recovery, retry mechanisms, optimistic rollback

All three use the same `useSyncExternalStore`-compatible factory pattern. Each store is consumed via a dedicated custom hook in `slices/dev-panel/hooks.ts` (`useDelay`, `useErrorRate`, `useOverhead`, `useActivityEntries`) that encapsulates the `useSyncExternalStore` wiring:

```typescript
export function createDelayStore() {
  let delay = 0;
  const listeners = new Set<() => void>();
  return {
    getDelay: () => delay,
    setDelay: (ms: number) => {
      delay = ms;
      listeners.forEach((l) => l());
    },
    subscribe: (listener: () => void) => {
      listeners.add(listener);
      return () => listeners.delete(listener);
    },
    getSnapshot: () => delay,
  };
}
```

### Presets

One-click scenario configurations that set chaos controls to demonstrate concurrent rendering under specific real-world conditions:

| Preset       | Delay  | Overhead | Error Rate | Purpose                                                       |
| ------------ | ------ | -------- | ---------- | ------------------------------------------------------------- |
| Slow Network | 3000ms | 0ms      | 0%         | Makes Suspense fallbacks and transitions visible              |
| Laggy Device | 0ms    | 300ms    | 0%         | Makes useDeferredValue benefit observable (input stays fluid) |
| Unreliable   | 1500ms | 0ms      | 50%        | Demonstrates ErrorBoundary recovery, optimistic rollback      |
| Worst Case   | 3000ms | 300ms    | 30%        | All chaos combined                                            |
| Reset        | 0ms    | 0ms      | 0%         | Clears all chaos controls                                     |

### Activity Log

Displays a time-ordered list of all tracked operations:

- **Format:** `[timestamp] operation-id — event (duration?)`
- **Color coding:** `--accent` = start, `--success` = complete, `--error` = error
- **Cap:** 200 entries (oldest evicted)
- **Font:** `--font-mono` (JetBrains Mono)
- **Clear button** to reset

Each log entry:

```typescript
interface LogEntry {
  id: string;
  operationId: string;
  event: "start" | "complete" | "error";
  timestamp: number;
  duration?: number; // ms, present on 'complete' and 'error'
}
```

### Render Count Badges

When `showRenderCounts` is enabled, components display a small badge showing their render count.

- Badge: `--surface-2` background, `--radius-full`, `--font-mono`, `--text-xs`
- Tooltip notes that StrictMode doubles render counts in development
- `React.memo` on `ItemRow` makes the count difference between deferred and non-deferred visible

Implementation: `useRenderCount()` hook returns a `ref.current` counter incremented on every render.

### Seed Data Trigger

A button in the DevPanel that calls `POST /api/dev/seed`:

1. Acquires auth token (if not already authenticated)
2. Fires single POST request
3. Shows spinner during request
4. On success: displays "Seeded!" feedback + triggers sidebar refresh
5. On error: displays error message

### Visual Design

- Denser, more compact layout than the main application
- Developer tool aesthetic with `--font-mono` throughout
- Section dividers: `CHAOS CONTROLS` / `DISPLAY` / `SCENARIOS` / `ACTIVITY` (uppercase, `--text-muted`, `--text-xs`)
- Custom-styled range inputs (themed to dark mode)
- Collapsible to a single toggle button (floating, bottom-right)

### Keyboard Shortcut

- **Windows/Linux:** `Ctrl+Shift+D`
- **macOS:** `⌘+Shift+D`
- Toggles the DevPanel open/closed state

### Session Persistence

All DevPanel state persists to `sessionStorage`:

- Open/collapsed state
- Chaos control values (delay, overhead, error rate)
- Display flag state (showRenderCounts)
- Survives page refresh within the same tab
- Fresh state on new tab/window

Implementation: `sessionPersistence.ts` module with `save(state)` / `load(): State | null`.

---

## Seed Endpoint

### Endpoint Definition

```
POST /api/dev/seed
```

- **Auth:** Requires valid Bearer token (same as all API endpoints)
- **Guard:** Only registered when `app.Environment.IsDevelopment()` is true
- **Behaviour:** Clears all existing data, then creates 10 checklists with 20–30 items each (~250 items total)
- **Response:** `204 No Content` on success

### Data Generation

Uses the **Bogus** library with curated word lists for realistic task-like data:

**Checklist names** — combinatorial: `{context} {type}`

Contexts (~15): Kitchen, Bathroom, Garden, Office, Garage, Bedroom, Living Room, Apartment, Wedding, Birthday, Holiday, Q3, Q4, Sprint, Project

Types (~10): Renovation, Cleanup, Shopping, Planning, Tasks, Maintenance, Preparation, Checklist, To-Do, Setup

**Item names** — combinatorial: `{verb} {object} {qualifier?}`

Verbs (~30): Order, Buy, Call, Schedule, Review, Compare, Fix, Replace, Clean, Organize, Sort, Pack, Ship, Research, Book, Cancel, Renew, Update, Check, Measure, Paint, Install, Remove, Assemble, Return, Send, Print, File, Submit, Prepare

Objects (~40): cabinet handles, quarterly budget, paint samples, plumber, electrician, insurance policy, flight tickets, hotel room, rental car, moving boxes, cleaning supplies, light fixtures, door handles, curtain rods, storage bins, power tools, garden hose, lawn mower, vacuum filter, air filter, smoke detector, battery backup, drawer organizers, shelf brackets, wall anchors, picture frames, extension cords, surge protector, water filter, dryer vent, gutters, fence panels, deck boards, window screens, door weatherstrip, thermostat, outlet covers, dimmer switch, ceiling fan, towel rack

Qualifiers (~20): for hallway, for kitchen, for bathroom, about leak, about wiring, before Friday, by end of week, from hardware store, from online store, for master bedroom, for guest room, for backyard, for front porch, for garage, with warranty, with receipt, for inspection, for estimate, before move-in, after delivery

**Data variety:**

- ~40% of items marked `isComplete`
- ~60% of items have descriptions (generated from Bogus `Lorem.Sentence()`)
- ~40% of items include a qualifier suffix
- Random each time (no fixed Bogus seed) — different data on every call

**Uniqueness:** Combinatorial explosion (30 × 40 × 20 = 24,000 possible item names) ensures practical uniqueness at 250 items.

### Registration

```csharp
// Core/Program.cs
if (app.Environment.IsDevelopment())
{
    app.MapDevSeedEndpoints();
}
```

**File:** `Core/DevSeed/DevSeedEndpoint.cs`

The endpoint:

1. Opens a database connection
2. Deletes all existing data (`DELETE FROM Items; DELETE FROM Members; DELETE FROM ItemGroups;`)
3. Generates 10 checklists with their items and member records (the authenticated user as sole member)
4. Inserts all records in a single transaction
5. Returns `204 No Content`

### Dependencies

- **NuGet package:** `Bogus` — added to `Directory.Packages.props`
- **Project reference:** Used directly in `Core.csproj`
- **Runtime guard:** Code only executes in Development environment

---

## Dev Guards

### `import.meta.env.DEV` Gating

Vite replaces `import.meta.env.DEV` with `true` (dev) or `false` (prod) at build time. When `false`, the bundler eliminates the dead code branch entirely.

**Guarded features:**

- `DevPanel` component rendering
- `PendingBorder` component rendering
- `React.StrictMode` wrapper
- Seed data button/trigger
- Render count badges infrastructure
- `console.log` / `debugger` statements (via Vite `esbuild.drop`)

**Pattern:**

```tsx
// App.tsx
{
  import.meta.env.DEV && <PendingBorder />;
}
{
  import.meta.env.DEV && <DevPanel />;
}
```

### React StrictMode

```tsx
// main.tsx
const app = <App />;
root.render(
  import.meta.env.DEV ? <React.StrictMode>{app}</React.StrictMode> : app,
);
```

StrictMode double-invokes:

- Component render functions
- `useState` initializers
- `useReducer` reducers
- `useEffect` setup/cleanup (mount → unmount → mount)

This catches bugs specific to concurrent rendering (effects that assume single execution, stale closures). Render count badges show inflated numbers — DevPanel tooltip explains this is expected.

### Unhandled Rejection Handler

```tsx
// main.tsx
window.addEventListener("unhandledrejection", (event) => {
  console.error("Unhandled promise rejection:", event.reason);
});
```

Catches promises that reject without a `.catch()` handler. In production, this would integrate with error monitoring.

---

## Global Pending Border

**Location:** `src/shared/components/PendingBorder.tsx`

A fixed overlay that provides visual feedback when any async operation is in progress:

```css
.pendingBorder {
  position: fixed;
  inset: 0;
  pointer-events: none;
  z-index: 9999;
  border: 2px solid var(--accent);
  box-shadow: inset 0 0 12px rgba(34, 211, 238, 0.1);
  opacity: 0;
  transition: opacity 150ms ease;
}

.pendingBorder.active {
  opacity: 1;
}
```

**Behaviour:**

- Visible when `pendingService.getSnapshot()` returns `true` (any operation active)
- Consumed via `usePending()` hook (`shared/hooks/usePending.ts`)
- Does NOT appear during sync compute overhead (thread is blocked — React cannot paint)
- This is an intentional teaching moment: sync blocking prevents all DOM updates

---

## Pending Service

**Location:** `src/shared/api/pendingService.ts`

Tracks all in-flight async operations. Drives the global pending border and activity log.

```typescript
export function createPendingService(log: ActivityLog) {
  const active = new Set<string>();
  const listeners = new Set<() => void>();

  return {
    begin(id: string): () => void {
      /* adds to set, logs start, notifies, returns end fn */
    },
    end(id: string): void {
      /* removes from set, logs complete with duration, notifies */
    },
    track<T>(id: string, promise: Promise<T>): Promise<T> {
      /* begin → await → end/error */
    },
    subscribe(listener: () => void) {
      /* ... */
    },
    getSnapshot(): boolean {
      return active.size > 0;
    },
    getActiveOperations(): readonly string[] {
      return [...active];
    },
  };
}
```

**Usage in slice API:**

```typescript
export function getItemGroups(resource = checklistsResource) {
  return pendingService.track("checklists/list", resource.list());
}
```

---

## Activity Log Module

**Location:** `src/shared/api/activityLog.ts`

Maintains a capped list of operation events for the DevPanel activity display.

```typescript
export function createActivityLog() {
  let entries: LogEntry[] = [];
  const MAX_ENTRIES = 200;
  const listeners = new Set<() => void>();

  return {
    append(entry: LogEntry): void {
      /* push + cap + notify */
    },
    getSnapshot(): readonly LogEntry[] {
      return entries;
    },
    clear(): void {
      entries = [];
      notify();
    },
    subscribe(listener: () => void) {
      /* ... */
    },
  };
}
```

Written to exclusively by `pendingService`. Read by DevPanel's activity log display via `useActivityEntries()` hook.
