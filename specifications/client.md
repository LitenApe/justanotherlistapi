# Client Specification

## Table of Contents

- [Overview](#overview)
- [Tech Stack](#tech-stack)
- [Project Structure](#project-structure)
- [Running The Client](#running-the-client)
- [Pages](#pages)
- [Authentication](#authentication)
- [Data Flow](#data-flow)
- [Real-Time Updates](#real-time-updates)
- [Accessibility](#accessibility)
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
| Build/dev   | Vite 8 (dev server also proxies the API, hub and OAuth server)     |
| Routing     | React Router 8 (declarative `BrowserRouter`)                       |
| Real-time   | `@microsoft/signalr`                                               |
| Styling     | Plain CSS: global design tokens + CSS Modules per component        |
| State       | React state and reducers; no state management library              |
| Lint/format | ESLint 10 (typescript-eslint strict, jsx-a11y strict, react-hooks) |
| Tests       | Vitest 5, Testing Library, axe-core, jsdom                         |

There are no UI component or icon libraries. Interactive widgets use native elements (`<dialog>`, `popover`, `<fieldset>` radios, `<progress>`) so that keyboard handling and semantics come from the browser.

---

## Project Structure

```
Client/
  index.html                 - HTML shell (lang, viewport, color-scheme)
  vite.config.ts             - Dev proxy (/api, /hubs → Core; /default → mock OAuth) and Vitest config
  eslint.config.js           - Type-aware + accessibility lint rules
  src/
    main.tsx                 - Entry point; loads global CSS before component CSS
    App.tsx                  - Routes: /sign-in, / (lists), /lists/:listId, 404
    api/
      http.ts                - fetch wrapper: bearer token, X-SignalR-Connection-Id, ApiError, 401 → sign out
      checklists.ts          - One function per API endpoint
      types.ts               - Item, ItemGroup, ItemInput
    auth/
      session.ts             - Session store (sessionStorage, expiry timer, sign-out reason)
      signIn.ts              - Dev sign-in against the mock OAuth server
      SignInPage.tsx         - Sign-in form
      RequireAuth.tsx        - Redirects signed-out users to /sign-in
    realtime/
      hub.ts                 - Single SignalR connection; per-list subscriptions with ref-counted groups
      hooks.ts               - useRealtimeConnection, useGroupEvents, useConnectionStatus
    components/
      Layout.tsx             - Skip link, header, account popover, connection banner, focus on navigation
      Dialog.tsx             - Native <dialog> wrapper; ConfirmDialog.tsx builds on it
      Toasts.tsx             - Visible notifications + screen reader announcements (live regions)
      Icon.tsx, CopyButton.tsx, useDocumentTitle.ts
    lists/ListsPage.tsx      - Overview of all lists, create list, filter lists
    list/
      ListPage.tsx           - Single list: items, filter/search, dialogs, focus management
      listReducer.ts         - Pure, idempotent state transitions shared by local and realtime changes
      ItemRow.tsx, EditItemDialog.tsx, RenameListDialog.tsx, MembersPanel.tsx
    styles/global.css        - Design tokens (light + dark), base styles, buttons, forms, utilities
    test/                    - Test setup, fake API (fetch), fake SignalR connection, axe helper
```

---

## Running The Client

`dotnet run --project Aspire` starts the client together with SQL Server, the mock OAuth server and Core. Aspire runs `npm install` and `npm run dev`, then serves the client at **http://localhost:5173**. The port is fixed so that browser storage (which is per origin) survives restarts.

The Vite dev server proxies:

| Path       | Target                                          |
| ---------- | ----------------------------------------------- |
| `/api`     | Core (`services__Core__https__0` / `__http__0`) |
| `/hubs`    | Core, WebSocket upgrade enabled                 |
| `/default` | Mock OAuth server (`services__oauth__http__0`)  |

Without Aspire, start Core and the OAuth server yourself and run `npm run dev` in `Client/`. The proxy falls back to `http://localhost:5093` and `http://localhost:8080`.

| Script           | Purpose                                        |
| ---------------- | ---------------------------------------------- |
| `npm run dev`    | Dev server with hot reload                     |
| `npm run build`  | Typecheck, then production build into `dist/`  |
| `npm run check`  | Typecheck + lint + format check + tests (CI)   |
| `npm test`       | Run tests once (`npm run test:watch` to watch) |
| `npm run format` | Format with Prettier                           |

---

## Pages

| Route            | Page       | Notes                                                                                                 |
| ---------------- | ---------- | ----------------------------------------------------------------------------------------------------- |
| `/sign-in`       | Sign in    | Enter or generate a user ID. Shows a notice when the previous session expired                         |
| `/`              | Your lists | Cards sorted by name with open item count and a preview. Create a list. Filter appears above 6 lists  |
| `/lists/:listId` | List       | Progress, add item, search, All / To do / Done filter, edit/delete items, rename/delete list, members |
| `*`              | Not found  | Also shown for `/lists/<not-a-guid>`                                                                  |

The overview mirrors `GET /api/list`, which only returns incomplete items. The list page loads `GET /api/list/{id}` and shows every item. Destructive actions (deleting a list or item, removing a member, leaving) ask for confirmation in a dialog whose initial focus is **Cancel**.

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
- The list page keeps state in `listReducer`. Local changes and realtime events dispatch the **same idempotent actions**, so receiving an event for a change already applied locally is harmless.
- The API returns items in no particular order. The client keeps the order stable during a visit and puts new items at the top, next to the add form.

---

## Real-Time Updates

`realtime/hub.ts` owns one `HubConnection` while a user is signed in:

- Connects with `skipNegotiation` over WebSockets, which keeps the Vite proxy from splitting negotiate and connect across connections.
- Pages subscribe per list with `useGroupEvents(listIds, handlers)`. The hub calls `JoinGroup` on the first subscription for a list and `LeaveGroup` after the last one.
- Reconnects automatically with exponential backoff (capped at 30s). After reconnecting it rejoins all groups and calls each subscriber's `resync`, which silently reloads data to catch missed events.
- While reconnecting, a banner (`role="status"`) says that live updates are paused.

| Event                   | Overview page                      | List page                                                    |
| ----------------------- | ---------------------------------- | ------------------------------------------------------------ |
| `ItemCreated`/`Updated` | Update open-item count and preview | Upsert the item                                              |
| `ItemDeleted`           | Remove from preview                | Remove; close the edit dialog if it was open for that item   |
| `GroupRenamed`          | Rename the card                    | Update heading and document title                            |
| `GroupDeleted`          | Remove the card                    | Return to the overview with a message                        |
| `MemberAdded`           | —                                  | Add to members                                               |
| `MemberRemoved`         | Remove the card if it was you      | Remove; if it was you, return to the overview with a message |

The overview also refetches when the tab becomes visible again, to pick up lists that were shared with you while you were away.

---

## Accessibility

Target: WCAG 2.2 AA. The conventions below are enforced by code review, `eslint-plugin-jsx-a11y` (strict) and axe-core checks in the tests.

**Structure**

- Every page has a unique `document.title` (`useDocumentTitle`) and exactly one `<h1>`, with a logical heading hierarchy below it.
- Landmarks: `<header>`, `<main id="main">`, and labelled `<section>` regions for the items and members.
- A "Skip to main content" link is the first focusable element.
- On client-side navigation, focus moves to the new page's `<h1>` (`tabIndex={-1}`) and the window scrolls to the top.

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
- `prefers-reduced-motion` disables animations; `prefers-color-scheme` switches to the dark theme.
- Units are `rem`, so text scales with browser settings, and layouts reflow without horizontal scrolling at 320px CSS width (WCAG 1.4.10).

---

## Responsive Design

Mobile first, with a 16px page gutter growing to 24px from `40rem`.

| Width   | Layout                                                                                  |
| ------- | --------------------------------------------------------------------------------------- |
| < 30rem | Dialog buttons stack full width                                                         |
| any     | List cards use `repeat(auto-fill, minmax(min(100%, 16rem), 1fr))`; forms wrap as needed |
| ≥ 48rem | Overview header and "New list" form sit side by side                                    |
| < 60rem | Members panel sits below the items; a "Members (n)" button in the header jumps to it    |
| ≥ 60rem | Two columns: items and a sticky 20rem members panel                                     |

---

## Testing

Tests live next to the code as `*.test.ts(x)` and run in jsdom.

- **Unit:** `listReducer` (idempotency, ordering, filtering), the session store (persistence, expiry), and the hub (ref-counted groups, routing events, resync after reconnect).
- **Page:** each page renders the whole app at a URL (`renderApp`) against `installFakeApi()`, an in-memory implementation of the REST API installed as `fetch`. Realtime events are injected through `latestConnection().emit(...)`. The real hub logic runs over a fake connection, set up in `test/setup.ts`.
- **Accessibility:** `expectNoAxeViolations()` runs axe-core on every page and dialog state under test. Color contrast is excluded because jsdom does not compute styles; the palette was checked separately.
- Tests assert on roles, accessible names and descriptions, and focus, the way assistive technology sees the page.
- jsdom lacks `HTMLDialogElement.showModal()`, so `test/setup.ts` polyfills it.

---

## Known Limitations

- **Dev-only sign-in.** See [Authentication](#authentication).
- **Item order.** The API has no ordering column, so items reload in database order.
- **Members are GUIDs.** There is no user directory, so members are shown and added by user ID.
- **Overview payload.** `GET /api/list` returns every open item of every list, which can be large with seeded data.
