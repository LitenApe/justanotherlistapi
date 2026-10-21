import { data, isRouteErrorResponse, redirect } from "react-router";
import { pathOf, signInPath } from "../auth/redirects";
import { ApiError } from "./http";

interface RouteErrorData {
  message: string;
}

/**
 * Runs an API call from a clientLoader and turns failures into what route
 * ErrorBoundaries understand: a 401 (already signed out by apiRequest) redirects
 * to sign-in, other API errors become error responses with the HTTP status.
 */
export async function loadRouteData<T>(
  request: Request,
  load: (signal: AbortSignal) => Promise<T>,
): Promise<T> {
  try {
    return await load(request.signal);
  } catch (error) {
    if (!(error instanceof ApiError)) throw error;
    if (error.status === 401) throw redirect(signInPath(pathOf(request)));
    // Status 0 means the server couldn't be reached.
    throw data<RouteErrorData>(
      { message: error.message },
      { status: error.status === 0 ? 503 : error.status },
    );
  }
}

export function routeErrorMessage(error: unknown): string {
  if (isRouteErrorResponse(error)) {
    const body = error.data as Partial<RouteErrorData> | undefined;
    return body?.message ?? `Something went wrong (HTTP ${error.status}).`;
  }
  return error instanceof Error ? error.message : "Something went wrong.";
}
