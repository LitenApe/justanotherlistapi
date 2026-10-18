import { sessionStore } from "../auth/session";
import { checklistHub } from "../realtime/hub";

export class ApiError extends Error {
  constructor(
    readonly status: number,
    message: string,
  ) {
    super(message);
    this.name = "ApiError";
  }
}

type Method = "GET" | "POST" | "PUT" | "DELETE";

export async function apiRequest<T>(
  method: Method,
  path: string,
  options: { body?: unknown; signal?: AbortSignal } = {},
): Promise<T> {
  const headers = new Headers({ Accept: "application/json" });
  const session = sessionStore.getSession();
  if (session) headers.set("Authorization", `Bearer ${session.accessToken}`);

  const connectionId = checklistHub.getConnectionId();
  if (connectionId && method !== "GET") headers.set("X-SignalR-Connection-Id", connectionId);

  const init: RequestInit = { method, headers };
  if (options.body !== undefined) {
    headers.set("Content-Type", "application/json");
    init.body = JSON.stringify(options.body);
  }
  if (options.signal) init.signal = options.signal;

  let response: Response;
  try {
    response = await fetch(path, init);
  } catch (error) {
    if (options.signal?.aborted) throw error;
    throw new ApiError(0, "Can't reach the server. Check your connection and try again.");
  }

  if (response.status === 401) {
    sessionStore.signOut("expired");
    throw new ApiError(401, "Your session has expired. Sign in again to continue.");
  }

  if (!response.ok) {
    throw new ApiError(response.status, messageFor(response.status));
  }

  const text = await response.text();
  return (text ? JSON.parse(text) : undefined) as T;
}

function messageFor(status: number): string {
  switch (status) {
    case 400:
      return "The request was invalid. Check the form and try again.";
    case 403:
      return "You don't have access to this list.";
    case 409:
      return "That change conflicts with the current state of the list.";
    default:
      return status >= 500
        ? "The server ran into a problem. Try again in a moment."
        : `Something went wrong (HTTP ${status}).`;
  }
}

export function errorMessage(error: unknown): string {
  return error instanceof Error ? error.message : "Something went wrong.";
}

export function isAbortError(error: unknown): boolean {
  return error instanceof DOMException && error.name === "AbortError";
}
