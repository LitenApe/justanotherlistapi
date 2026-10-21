# Client Specification

## Table of Contents

- [Overview](#overview)
- [Tech Stack](#tech-stack)
- [Project Structure](#project-structure)
- [Running The Client](#running-the-client)
- [Pages](#pages)
- [Routing And Data Loading](#routing-and-data-loading)
- [Authentication](#authentication)
- [Data Flow](#data-flow)
- [Real-Time Updates](#real-time-updates)
- [List Settings](#list-settings)
- [Accessibility](#accessibility)
- [Themes](#themes)
- [Responsive Design](#responsive-design)
- [Testing](#testing)
- [Known Limitations](#known-limitations)

---

## Overview

`Client/` is a single-page web app for the checklist API. Users sign in, see their lists, open a list to check off, add, edit and delete items, rename or delete the list, and manage its members. Changes made by other members appear live through SignalR.

The client targets **WCAG 2.2 level AA** and works from 320px wide phones up to desktop screens.

---

## Tech Stack

| Concern     | Choice                                                             |
| ----------- | ------------------------------------------------------------------ |
| UI          | React 19, TypeScript 6 (strict)                                    |
| Framework   | React Router 8 framework mode, SPA (`ssr: false`), on Vite 8       |
| Data        | Route `clientLoader`/`clientAction`/`clientMiddleware`, `<Form>`   |
| Real-time   | `@microsoft/signalr`                                               |
| Styling     | Plain CSS: global design tokens + CSS Modules per component        |
| State       | React state and reducers; no state management library              |
| Lint/format | ESLint 10 (typescript-eslint strict, jsx-a11y strict, react-hooks) |
| Tests       | Vitest 5 (unit, browser mode), Playwright Test, axe-core           |

There are no UI component or icon libraries. Interactive widgets use native elements (`<dialog>`, `popover`, `<fieldset>` radios, `<progress>`) so that keyboard handling and semantics come from the browser.

---

## Project Structure

```
Client/
  react-router.config.ts     - Framework config: appDirectory "src", SPA mode (ssr: false)
  vite.config.ts             - React Router plugin, dev proxy (/api, /hubs → Core; /default → mock OAuth)
  vitest.config.ts           - Unit (Node) and browser (Chromium) test projects
  playwright.config.ts       - Integration tests against the production build
  integration/               - Integration specs; support/ has the fake backend, fixtures and static server
  eslint.config.js           - Type-aware + accessibility lint rules
  src/
    root.tsx                 - HTML document (Layout), theme boot script, app shell, HydrateFallback, root ErrorBoundary
    routes.ts                - Route config (see Routing And Data Loading)
    NotFoundPage.tsx         - Route module for unknown addresses
    api/
      http.ts                - fetch wrapper: bearer token, X-SignalR-Connection-Id, ApiError, 401 → sign out
      checklists.ts          - One function per API endpoint
      routeData.ts           - loadRouteData(): maps API errors to redirects and route error responses
      types.ts               - Item, ItemGroup, ItemGroupSettings, request inputs
    auth/
      session.ts             - Session store (sessionStorage, expiry timer, sign-out reason)
      signIn.ts              - Dev sign-in against the mock OAuth server
      SignInPage.tsx         - Route module: sign-in form (clientLoader + clientAction)
      ProtectedLayout.tsx    - Route module: clientMiddleware that requires a session
      redirects.ts           - signInPath(), safeRedirect() for ?redirectTo=
    realtime/
      hub.ts                 - Single SignalR connection; per-list subscriptions with ref-counted groups
      hooks.ts               - useRealtimeConnection, useGroupEvents, useConnectionStatus
    components/
      AppShell.tsx           - Skip link, header, theme toggle, account popover, connection banner,
                               loading bar, focus on navigation
      Dialog.tsx             - Native <dialog> wrapper; ConfirmDialog.tsx builds on it
      Toasts.tsx             - Visible notifications + screen reader announcements (live regions)
      DocumentTitle.tsx      - Renders <title> (React hoists it into <head>)
      Icon.tsx, CopyButton.tsx
    lib/                     - GUID helpers, formText(), useLoaderState()
    lists/ListsPage.tsx      - Route module: overview of all lists, create list, filter lists
    list/
      ListPage.tsx           - Route module: single list (items, filter/search, dialogs, focus management)
      listReducer.ts         - Pure, idempotent state transitions shared by local and realtime changes,
                               plus filtering, auto-hide and auto-delete time helpers
      ListSettingsDialog.tsx - Auto-hide and auto-delete settings; duration.ts converts minutes ↔ amount + unit
      ItemRow.tsx, EditItemDialog.tsx, RenameListDialog.tsx, MembersPanel.tsx
    theme/theme.ts           - Light/dark theme store (dark default, saved in localStorage)
    styles/global.css        - Design tokens per theme, base styles, buttons, forms, utilities
    test/                    - Test data builders, browser test setup, axe helper
```

---

## Running The Client

`dotnet run --project Aspire` starts the client together with SQL Server, the mock OAuth server and Core. Aspire runs `npm install` and `npm run dev` (`react-router dev`), then serves the client at **http://localhost:5173**. The port is fixed so that browser storage (which is per origin) survives restarts.

The dev server proxies:

| Path       | Target                                          |
| ---------- | ----------------------------------------------- |
| `/api`     | Core (`services__Core__https__0` / `__http__0`) |
| `/hubs`    | Core, WebSocket upgrade enabled                 |
| `/default` | Mock OAuth server (`services__oauth__http__0`)  |

Without Aspire, start Core and the OAuth server yourself and run `npm run dev` in `Client/`. The proxy falls back to `http://localhost:5093` and `http://localhost:8080`.

| Script              | Purpose                                                                  |
| ------------------- | ------------------------------------------------------------------------ |
| `npm run dev`       | Dev server with hot reload                                               |
| `npm run build`     | Typecheck, then a static build into `build/client/` (serve it as an SPA) |
| `npm run typecheck` | Generate route types (`react-router typegen`), then `tsc`                |
| `npm run check`     | Typecheck + lint + format check + tests (CI)                             |
| `npm test`          | Unit, browser and integration tests (see [Testing](#testing))            |
| `npm run format`    | Format with Prettier                                                     |

The first visit after dependencies change can fail with "504 Outdated Optimize Dep" while Vite re-bundles them; reload the page.

---

## Pages

| Route            | Page       | Notes                                                                                                                             |
| ---------------- | ---------- | --------------------------------------------------------------------------------------------------------------------------------- |
| `/sign-in`       | Sign in    | Enter or generate a user ID. Shows a notice when the previous session expired                                                     |
| `/`              | Your lists | Cards sorted by name with open item count and a preview. Create a list. Filter appears above 6 lists                              |
| `/lists/:listId` | List       | Progress, add item, search, All / To do / Done filter, edit/delete items, rename/delete list, [settings](#list-settings), members |
| `*`              | Not found  | `/lists/<not-a-guid>` shows the list route's own "List not found"                                                                 |

The overview mirrors `GET /api/list`, which only returns incomplete items. The list page loads `GET /api/list/{id}` and shows every item. Destructive actions (deleting a list or item, removing a member, leaving) ask for confirmation in a dialog whose initial focus is **Cancel**.

---

## Routing And Data Loading

The client uses React Router's **framework mode** in **SPA mode** (`ssr: false` in `react-router.config.ts`). The API authenticates with bearer tokens that live in the browser, so there is nothing to render on a server. The build prerenders only the document shell (`root.tsx` `Layout` + `HydrateFallback`) into `build/client/index.html`. Anything imported by route modules must therefore be safe to import in Node: browser globals are only touched inside functions, never at module load.

`src/routes.ts`:

```ts
route("sign-in", "auth/SignInPage.tsx"),
layout("auth/ProtectedLayout.tsx", [
  index("lists/ListsPage.tsx"),
  route("lists/:listId", "list/ListPage.tsx"),
]),
route("*", "NotFoundPage.tsx"),
```

| Route module          | Exports                                                                                               |
| --------------------- | ----------------------------------------------------------------------------------------------------- |
| `root.tsx`            | `Layout` (HTML document), `default` (toasts + app shell), `HydrateFallback`, `ErrorBoundary`          |
| `ProtectedLayout.tsx` | `clientMiddleware` (redirect to `/sign-in?redirectTo=…` without a session), `default`                 |
| `SignInPage.tsx`      | `clientLoader` (skip if signed in), `clientAction` (token request), `default`                         |
| `ListsPage.tsx`       | `clientLoader` (`GET /api/list`), `clientAction` (create list → redirect), `default`, `ErrorBoundary` |
| `ListPage.tsx`        | `clientLoader` (`GET /api/list/{id}`; 404 for non-GUIDs), `default`, `ErrorBoundary`                  |

- **Auth gating** is middleware, not a loader, because React Router runs a navigation's loaders in parallel. Middleware runs first, so signed-out visitors never call the API. The layout component also redirects if the session ends while a page is open.
- **`redirectTo`** goes through `safeRedirect()`, which only accepts same-origin paths.
- **Loaders** call the API through `loadRouteData()`. A 401 redirects to sign-in (the session has already been cleared). Other API errors become error responses with the HTTP status (503 when the server can't be reached), which the route's `ErrorBoundary` shows: 403 "You can't open this list", 404 "List not found", anything else an alert with **Try again** (`useRevalidator()`).
- **Forms that navigate** use React Router forms and `clientAction`s: sign-in (`<Form>`) and create list (`useFetcher`, so a validation error doesn't change the URL or add a history entry). Validation errors come back as action data and move focus to the field.
- **Mutations on the list page** (toggle, edit, delete, members, settings) stay as direct API calls with an idempotent reducer. They need optimistic updates, focus management and realtime merging, and they shouldn't trigger a full reload of a list that can hold thousands of items.
- **Loader data vs. live state:** pages keep an editable copy of loader data for realtime events (`useLoaderState()` on the overview, `listReducer` on the list page). The copy resets whenever the loader returns new data. After a SignalR reconnect, or when a hidden overview tab becomes visible, pages call `revalidate()` instead of fetching by hand.
- **Pending UI:** while the next page loads, the current page stays visible, `<main>` gets `aria-busy`, and a thin bar appears under the header after 150ms (`AppShell`). The first load shows the root `HydrateFallback` ("Loading JustAnotherList…" in `role="status"`).
- **Titles** are React 19 `<title>` elements (`DocumentTitle`), so they update with the data, for example when another member renames the list.
- **Route types** come from `react-router typegen` (`.react-router/types`, git-ignored). Route modules import them as `./+types/<File>`.

---

## Authentication

The API accepts JWT bearer tokens and uses the `sub` claim as the user ID, which must be a GUID (see [authentication.md](authentication.md)).

For local development the client signs in against the mock OAuth server using the **client credentials** grant. The client ID entered on the sign-in page becomes the token's `sub`, so the user ID is whatever GUID you type. Use the same ID to get back to your lists, or generate a new one to act as a different person.

- The token is kept in **sessionStorage**, so each browser tab has its own sign-in. Two tabs with different user IDs are an easy way to try sharing.
- The last used user ID is remembered in localStorage to prefill the form.
- A timer signs the user out when the token's `exp` passes. Any `401` from the API also signs out. Both send the user to `/sign-in` with a "session expired" notice and return them to the page they were on afterwards.
- SignalR receives the token through `accessTokenFactory` (`?access_token=` on the WebSocket URL).

> **Production:** the client credentials grant with a shared secret is only suitable for the mock server. A real deployment needs an OIDC authorization code + PKCE flow. Only `auth/signIn.ts` and the sign-in page need to change; everything else reads the session store.

---

## Data Flow

- `api/http.ts` adds `Authorization` and, for mutations, `X-SignalR-Connection-Id` so the server doesn't echo the change back to the sender.
- Toggling an item is **optimistic**: the UI updates immediately and reverts with an error toast if the request fails. Edits, renames and deletes wait for the server before closing their dialog, so input is never lost.
- `UpdateItem` is a full replacement, so the client always sends `name`, `description` and `isComplete` together, taken from the latest local state.
- `completedAt` is set by the server and never sent. An optimistic toggle fills it in locally with the current time so auto-hide works straight away; other members receive the server's value through `ItemUpdated`.
- The list page keeps state in `listReducer`. Local changes and realtime events dispatch the **same idempotent actions**, so receiving an event for a change already applied locally is harmless.
- The API returns items in no particular order. The client keeps the order stable during a visit and puts new items at the top, next to the add form.

---

## Real-Time Updates

`realtime/hub.ts` owns one `HubConnection` while a user is signed in:

- Connects with `skipNegotiation` over WebSockets, which keeps the Vite proxy from splitting negotiate and connect across connections.
- Pages subscribe per list with `useGroupEvents(listIds, handlers)`. The hub calls `JoinGroup` on the first subscription for a list and `LeaveGroup` after the last one.
- Reconnects automatically with exponential backoff (capped at 30s). After reconnecting it rejoins all groups and calls each subscriber's `resync`, which revalidates the route's loader to catch missed events.
- While reconnecting, a banner (`role="status"`) says that live updates are paused.

| Event                   | Overview page                      | List page                                                               |
| ----------------------- | ---------------------------------- | ----------------------------------------------------------------------- |
| `ItemCreated`/`Updated` | Update open-item count and preview | Upsert the item                                                         |
| `ItemDeleted`           | Remove from preview                | Remove; close the edit dialog if it was open for that item              |
| `GroupRenamed`          | Rename the card                    | Update heading and document title                                       |
| `GroupSettingsChanged`  | —                                  | Apply the settings and show "Another member changed…"                   |
| `GroupDeleted`          | Remove the card                    | Return to the overview; the message says whether auto-delete removed it |
| `MemberAdded`           | —                                  | Add to members                                                          |
| `MemberRemoved`         | Remove the card if it was you      | Remove; if it was you, return to the overview with a message            |

The overview also revalidates when the tab becomes visible again, to pick up lists that were shared with you while you were away.

---

## List Settings

The **Settings** button on the list page opens `ListSettingsDialog`, which edits the two settings described in [checklist.md](checklist.md#list-settings). They apply to every member, and both are off by default.

**Dialog**

- Each setting is a checkbox with a hint (`aria-describedby`). Checking it reveals a "Hide after" / "Delete after" fieldset with an **Amount** text field (`inputmode="numeric"`) and a **Unit** select (minutes, hours, days). The fields are shown rather than disabled, so nothing focusable is ever disabled.
- Turning a setting on starts from 1 day (hide) or 7 days (delete). Stored minutes come back in the largest unit that divides them evenly.
- Validation mirrors the server: a whole number, hiding from 0 up to 365 days, deleting from 1 day up to 365 days. Errors appear under the fieldset, are linked to the amount field, and move focus to it.
- Saving sends `PUT /api/list/{id}/settings` and applies the returned settings, including `autoDeleteUpdatedAt`.

**On the list page**

- A hint under the heading summarises what's on, for example "Done items are hidden after 1 hour. The list is deleted 7 days after every item is done."
- **Auto-hide**: done items whose hide time has passed drop out of "All" (and its count). "Done" still lists them. A line under the result count says how many are hidden and offers **Show done items**, which switches to "Done" and moves focus to that radio, since the button itself disappears. Progress still counts hidden items.
- The page doesn't tick every second: a single timer wakes it when the next done item reaches its hide time (capped at a day, since browsers fire longer timers at once). With "hide right away" the toggled item disappears immediately.
- When a hidden item holds focus, focus moves to the nearest item that stays visible, the same as for filtering (`planFocusAfterRemoval`, which takes several IDs when items disappear together).
- **Auto-delete**: when every item is done, a notice gives the date and time the list will be deleted, computed the same way as the server (`autoDeleteTime`). It sits in a persistent `role="status"` region, so checking the last item announces it. Unchecking an item removes it.
- If the list is deleted while open and the deletion time has passed (allowing a minute for clock differences), the overview message says it was deleted automatically rather than by another member.

---

## Accessibility

Target: WCAG 2.2 AA. The conventions below are enforced by code review, `eslint-plugin-jsx-a11y` (strict) and axe-core checks in the tests.

**Structure**

- Every page, including error boundaries, has a unique title (`DocumentTitle`) and exactly one `<h1>`, with a logical heading hierarchy below it.
- Landmarks: `<header>`, `<main id="main">`, and labelled `<section>` regions for the items and members.
- A "Skip to main content" link is the first focusable element.
- On client-side navigation, focus moves to the new page's `<h1>` (`tabIndex={-1}`) once its data has loaded. `<ScrollRestoration>` scrolls to the top on new pages and restores the position on back/forward.
- While a page loads, `<main>` has `aria-busy`; the first load announces "Loading JustAnotherList…".

**Forms**

- Every input has a visible `<label>`. Hints and errors are linked with `aria-describedby`; invalid fields get `aria-invalid`.
- Validation errors are shown next to the field, and focus moves back to that field.
- Forms use `noValidate` so the app's own error messages are used consistently instead of the browser's.
- Buttons stay enabled while a request is in flight (`aria-disabled` + guard), because disabling the focused button would drop keyboard focus.

**Focus management**

- Dialogs use native `<dialog>.showModal()`, which provides the focus trap, Escape to close and an inert background. Initial focus goes to the element marked `data-autofocus`, and focus returns to the opener on close.
- When the focused item disappears (deleted, or filtered out after toggling), focus moves to the next item, then the previous one, then the "Items" heading. It never falls back to `<body>`.
- After removing a member, focus moves to the "Members" heading.

**Announcements**

- `ToastProvider` renders persistent live regions: `role="status"` for information and `role="alert"` for errors. Info toasts dismiss after 6 seconds unless hovered or focused; errors stay until dismissed.
- `announce()` gives screen reader–only feedback where the visual change is self-evident (for example "Added “Milk”").
- Filtered result counts ("Showing 3 of 12 items") are in a `role="status"` element.

**Visual**

- All text meets 4.5:1 contrast and control borders and focus rings meet 3:1, in both light and dark themes (tokens in `styles/global.css`).
- The focus indicator is a 3px outline with an offset (`:focus-visible`) and stays visible in Windows High Contrast (`forced-colors`).
- Pointer targets are at least 24×24px; most are 44px tall.
- State never relies on color alone: done items are struck through, the active filter shows a check mark, and invalid fields have a thicker border plus a text error.
- Icons are decorative (`aria-hidden`) and always paired with visible or visually hidden text.
- `prefers-reduced-motion` disables animations.
- Units are `rem`, so text scales with browser settings, and layouts reflow without horizontal scrolling at 320px CSS width (WCAG 1.4.10).

---

## Themes

The client has a dark theme (the default) and a light theme.

- **Toggle:** a "Dark mode" toggle button in the header (`aria-pressed`, moon or sun icon). It's available on every page, including sign-in.
- **Persistence:** the choice is saved in localStorage (`jal.theme`) per browser. The operating system's `prefers-color-scheme` is deliberately not used, so new visitors always start in dark mode.
- **Tokens:** `global.css` defines the colors twice: under `:root, :root[data-theme="dark"]` and under `:root[data-theme="light"]`. Each block also sets `color-scheme`, so native controls and scrollbars match. Components only use the `--color-*` tokens.
- **No flash:** the document from `root.tsx` starts with `data-theme="dark"`. An inline script in `<head>` (`themeBootScript`, built in `theme/theme.ts` from the same storage key and colors) switches to light before first paint when that is the saved choice, and adds `<meta name="theme-color">` (mobile browser UI). `<html>` uses `suppressHydrationWarning`. React doesn't render the `theme-color` tag itself: one the script had changed wouldn't match during hydration, and React would add a second tag. `themeStore` keeps `data-theme` and that tag in sync afterwards. `color-scheme` comes from each theme's CSS.

---

## Responsive Design

Mobile first, with a 16px page gutter growing to 24px from `40rem`.

| Width   | Layout                                                                                  |
| ------- | --------------------------------------------------------------------------------------- |
| < 30rem | Header buttons become icon-only; dialog buttons stack full width                        |
| any     | List cards use `repeat(auto-fill, minmax(min(100%, 16rem), 1fr))`; forms wrap as needed |
| ≥ 48rem | Overview header and "New list" form sit side by side                                    |
| < 60rem | Members panel sits below the items; a "Members (n)" button in the header jumps to it    |
| ≥ 60rem | Two columns: items and a sticky 20rem members panel                                     |

---

## Testing

Tests come in three kinds. Each kind has its own tool, file naming and rule about what may be faked. The fewer fakes, the closer the test is to what users get.

| Kind        | Runs in                                         | Files                         | May fake                                      |
| ----------- | ----------------------------------------------- | ----------------------------- | --------------------------------------------- |
| Unit        | Vitest, Node (no DOM)                           | `src/**/*.unit.test.ts`       | Nothing                                       |
| Browser     | Vitest browser mode, real Chromium (Playwright) | `src/**/*.browser.test.ts(x)` | Callbacks passed as props, timers when needed |
| Integration | Playwright Test against the production build    | `integration/*.spec.ts`       | The network (REST, OAuth, SignalR) and timers |

```bash
npm run test:unit         # Vitest "unit" project
npm run test:browser      # Vitest "browser" project
npm run test:integration  # builds the SPA, serves it, runs Playwright
npm test                  # all three
```

The browser and integration tests need Playwright's Chromium, installed once with `npx playwright install chromium`. Nothing uses `vi.mock`, jsdom or polyfills.

### Unit tests

Pure functions with no browser APIs: the list reducer and its auto-hide/auto-delete maths, durations, GUID helpers, redirect safety (`safeRedirect`), form parsing and the loader error mapping (`loadRouteData`). They run in Node, so anything that reaches for `window` or `document` fails instead of passing by accident.

### Browser tests

Components and browser-API modules rendered on their own in a real browser (`vitest-browser-react`), driven with real input through Playwright (`page`, `userEvent` from `vitest/browser`):

- `Dialog`/`ConfirmDialog`: native `showModal()`, `:modal`, Escape, focus return and inert background.
- Toasts: live regions, auto-dismiss, pausing on hover.
- `CopyButton`: the real clipboard (permission granted in `vitest.config.ts`).
- Theme store and boot script: real `localStorage`, computed colours.
- Session store: real `sessionStorage`.
- `EditItemDialog`, `ListSettingsDialog`, `MembersPanel`: validation, focus and accessible errors.

`src/test/browserSetup.ts` loads `global.css`, so layout and colours are real. `expectNoAxeViolations()` (`src/test/axe.ts`) runs the WCAG 2.2 A/AA rules, **colour contrast included**, after waiting for running animations to finish.

- Only fake the timers a test is about (`vi.useFakeTimers({ toFake: ["setTimeout", "clearTimeout"] })`). axe-core itself uses `setTimeout`, so run axe with real timers.
- Playwright won't click `aria-disabled` buttons. Use `click({ force: true })` when the point is that a click is ignored.
- Give test harness markup the app's classes (`button`, `input`, `label`), or scope axe to the component, so unstyled harness elements don't fail contrast.

### Integration tests

The production SPA (`react-router build`) is served the way a static host serves it (`integration/support/serveSpa.mjs`: files, else `index.html`; `/api`, `/hubs` and `/default` return 404 so unfaked requests fail loudly). Everything in the browser is the real app: entry point, routing, middleware, loaders, session handling, the SignalR client and the CSS.

Only the network is replaced, by `FakeBackend` (`integration/support/fakeBackend.ts`), installed through Playwright's routing for every test:

- **REST API:** an in-memory implementation of [checklist.md](checklist.md), including membership checks (403), conflicts (409), `completedAt` and settings. It authenticates the bearer token on every request (401 when missing or expired).
- **OAuth:** the token endpoint returns an unsigned JWT whose `sub` is the client ID, like the mock OAuth server.
- **SignalR:** `page.routeWebSocket` speaks the hub's JSON protocol: handshake, `JoinGroup` (members only) and `LeaveGroup`, pings and server-pushed events. Like the real server, every change is broadcast to the list's group except the connection named in `X-SignalR-Connection-Id`.
- `backend.as(OTHER)` makes changes as another member through the same code paths, so the page receives real events. `deleteListAutomatically()` stands in for the cleanup service.
- `failNext()` fails a request with a status; `holdNext()` holds one to look at loading states; `dropHubConnections()` simulates a lost connection. `backend.calls` records every request.

Fixtures (`integration/support/test.ts`):

- `app.signIn()` goes through the real sign-in page.
- `app.openList(list)` seeds, signs in, opens the list and waits until the page has joined its SignalR group. Wait the same way (`app.subscribed(id)`) before another member changes something, or the event is (correctly) missed.
- Any uncaught error in the page fails the test.
- `expectAccessible(page, selector?)` runs every axe rule, colour contrast and best practices included.

Time-based behaviour uses the real clock where waits are short: an item due to hide in a few seconds, or a 3-second token lifetime. `page.clock` is available when that isn't practical.

Projects: `desktop` runs everything except `@mobile` tests; `mobile` (Pixel 7) runs only tests tagged `@mobile`. The accessibility spec covers both themes, keyboard use, 320px reflow and tap target sizes.

### Known issue in the tests

`realtime.spec.ts` has a "your own changes (known issue: echoed back)" group marked `test.fail()`. Because the client skips SignalR negotiation, it never learns its connection ID, never sends `X-SignalR-Connection-Id`, and the server echoes your own changes back. For example, saving settings shows "Another member changed the list settings.". These tests describe the intended behaviour. Playwright reports them as soon as they start passing; then remove the `test.fail()` lines.

---

## Known Limitations

- **Dev-only sign-in.** See [Authentication](#authentication).
- **Item order.** The API has no ordering column, so items reload in database order.
- **Members are GUIDs.** There is no user directory, so members are shown and added by user ID.
- **Overview payload.** `GET /api/list` returns every open item of every list, which can be large with seeded data.
- **Clock differences.** Auto-hide compares the server's `completedAt` with the browser's clock, so a device whose clock is off hides items correspondingly early or late.
