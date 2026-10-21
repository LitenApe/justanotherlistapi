/** The sign-in URL that returns to `returnTo` once signed in. */
export function signInPath(returnTo: string): string {
  return returnTo === "/" ? "/sign-in" : `/sign-in?redirectTo=${encodeURIComponent(returnTo)}`;
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
