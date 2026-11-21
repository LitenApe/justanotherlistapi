/** The sign-in URL that returns to `returnTo` once signed in. */
export function signInPath(returnTo: string): string {
  return withReturnTo("/sign-in", returnTo);
}

/** An account page's URL that still returns to `returnTo` once signed in. */
export function withReturnTo(page: string, returnTo: string): string {
  return returnTo === "/" ? page : `${page}?redirectTo=${encodeURIComponent(returnTo)}`;
}

/** Path and query of a loader/action request. */
export function pathOf(request: Request): string {
  const url = new URL(request.url);
  return url.pathname + url.search;
}

/** Only same-origin paths are allowed, so the parameter can't send people to another site. */
export function safeRedirect(target: string | null): string {
  if (!target?.startsWith("/") || target.startsWith("//") || target.startsWith("/\\")) return "/";
  return target;
}

/** Where a request to an account page should send people once they're signed in. */
export function returnPathOf(request: Request): string {
  return safeRedirect(new URL(request.url).searchParams.get("redirectTo"));
}
