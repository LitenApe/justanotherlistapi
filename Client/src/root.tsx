// Global styles first so component CSS modules can override them.
import "./styles/global.css";
import type { ReactNode } from "react";
import { isRouteErrorResponse, Links, Meta, Scripts, ScrollRestoration } from "react-router";
import type { Route } from "./+types/root";
import { AppShell } from "./components/AppShell";
import { DocumentTitle } from "./components/DocumentTitle";
import { ToastProvider } from "./components/Toasts";
import { DEFAULT_THEME, THEME_COLORS, themeBootScript } from "./theme/theme";

/** The HTML document. In SPA mode it is prerendered at build time with HydrateFallback inside. */
export function Layout({ children }: { children: ReactNode }) {
  return (
    // The boot script may switch the theme before hydration; React leaves that alone.
    <html lang="en" data-theme={DEFAULT_THEME} suppressHydrationWarning>
      <head>
        <meta charSet="utf-8" />
        <meta name="viewport" content="width=device-width, initial-scale=1" />
        <meta name="color-scheme" content={DEFAULT_THEME} suppressHydrationWarning />
        <meta name="theme-color" content={THEME_COLORS[DEFAULT_THEME]} suppressHydrationWarning />
        <meta name="description" content="Shareable checklists for everything." />
        <link rel="icon" href="/favicon.svg" type="image/svg+xml" />
        <script dangerouslySetInnerHTML={{ __html: themeBootScript }} />
        <Meta />
        <Links />
      </head>
      <body>
        {children}
        <ScrollRestoration />
        <Scripts />
      </body>
    </html>
  );
}

export default function App() {
  return (
    <ToastProvider>
      <AppShell />
    </ToastProvider>
  );
}

/** Shown while the first page's data loads. */
export function HydrateFallback() {
  return (
    <div className="app-loading">
      <DocumentTitle />
      <p role="status">Loading JustAnotherList…</p>
      <noscript>JustAnotherList needs JavaScript to run.</noscript>
    </div>
  );
}

/** Last resort for errors no route handled. */
export function ErrorBoundary({ error }: Route.ErrorBoundaryProps) {
  const notFound = isRouteErrorResponse(error) && error.status === 404;
  const title = notFound ? "Page not found" : "Something went wrong";

  return (
    <main className="app-error">
      <DocumentTitle title={title} />
      <h1>{title}</h1>
      <p className="muted">
        {notFound
          ? "There's nothing at this address."
          : "The app ran into an unexpected problem. Reloading the page usually helps."}
      </p>
      {import.meta.env.DEV && error instanceof Error && error.stack && (
        <pre className="app-error-stack">{error.stack}</pre>
      )}
      {/* A full page load, since the app itself may be what's broken. */}
      <a href="/">Go to your lists</a>
    </main>
  );
}
