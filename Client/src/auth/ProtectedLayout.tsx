import { Navigate, Outlet, redirect, useLocation } from "react-router";
import type { Route } from "./+types/ProtectedLayout";
import { pathOf, signInPath } from "./redirects";
import { sessionStore } from "./session";
import { useSession } from "./useSession";

/**
 * Pages below this layout need a session. Middleware runs before any of their
 * loaders (which run in parallel), so signed-out visitors never call the API.
 */
export const clientMiddleware: Route.ClientMiddlewareFunction[] = [
  ({ request }) => {
    if (!sessionStore.getSession()) throw redirect(signInPath(pathOf(request)));
  },
];

export default function ProtectedLayout() {
  const session = useSession();
  const location = useLocation();

  // The session can also end while a page is open (sign-out, expiry or a 401).
  if (!session) {
    return <Navigate to={signInPath(location.pathname + location.search)} replace />;
  }

  return <Outlet />;
}
