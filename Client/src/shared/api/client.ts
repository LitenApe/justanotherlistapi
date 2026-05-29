import { type DelayStore, delayStore } from "./delay";
import { type ErrorRateStore, errorRateStore } from "./errorRate";

export class HttpError extends Error {
  constructor(
    public readonly status: number,
    message: string,
  ) {
    super(message);
    this.name = "HttpError";
  }
}

export class SimulatedNetworkError extends Error {
  constructor() {
    super("Simulated network error");
    this.name = "SimulatedNetworkError";
  }
}

export class UnauthorizedError extends Error {
  constructor() {
    super("Unauthorized");
    this.name = "UnauthorizedError";
  }
}

export interface RequestOptions {
  skipAuth?: boolean;
  signal?: AbortSignal;
}

export interface ApiClientStores {
  delay: DelayStore;
  errorRate: ErrorRateStore;
  getToken: () => string | null;
  clearToken: () => void;
  getConnectionId?: () => string | null;
  timeout?: number;
}

export interface ApiClient {
  get<T>(url: string, options?: RequestOptions): Promise<T>;
  post<T>(url: string, body?: unknown, options?: RequestOptions): Promise<T>;
  put<T>(url: string, body?: unknown, options?: RequestOptions): Promise<T>;
  delete<T>(url: string, options?: RequestOptions): Promise<T>;
}

function sleep(ms: number, signal?: AbortSignal): Promise<void> {
  if (ms <= 0) return Promise.resolve();
  return new Promise((resolve, reject) => {
    const timer = setTimeout(resolve, ms);
    signal?.addEventListener("abort", () => {
      clearTimeout(timer);
      reject(signal.reason);
    });
  });
}

export function createApiClient(stores: ApiClientStores): ApiClient {
  const timeoutMs = stores.timeout ?? 30_000;

  async function request<T>(
    method: string,
    url: string,
    body?: unknown,
    options?: RequestOptions,
  ): Promise<T> {
    const controller = new AbortController();
    const externalSignal = options?.signal;

    if (externalSignal?.aborted) {
      throw externalSignal.reason;
    }

    externalSignal?.addEventListener("abort", () =>
      controller.abort(externalSignal.reason),
    );

    const timeoutId =
      timeoutMs > 0 && timeoutMs < Infinity
        ? setTimeout(
            () => controller.abort(new Error("Request timeout")),
            timeoutMs,
          )
        : undefined;

    try {
      await sleep(stores.delay.getDelay(), controller.signal);

      if (Math.random() * 100 < stores.errorRate.getRate()) {
        throw new SimulatedNetworkError();
      }

      const headers: Record<string, string> = {
        "Content-Type": "application/json",
      };

      if (!options?.skipAuth) {
        const token = stores.getToken();
        if (token) {
          headers["Authorization"] = `Bearer ${token}`;
        }
        const connectionId = stores.getConnectionId?.();
        if (connectionId) {
          headers["X-SignalR-Connection-Id"] = connectionId;
        }
      }

      const response = await fetch(url, {
        method,
        headers,
        body: body !== undefined ? JSON.stringify(body) : null,
        signal: controller.signal,
      });

      if (response.status === 401) {
        stores.clearToken();
        throw new UnauthorizedError();
      }

      if (!response.ok) {
        const text = await response.text().catch(() => response.statusText);
        throw new HttpError(response.status, text || response.statusText);
      }

      if (response.status === 204) {
        return undefined as T;
      }

      return (await response.json()) as T;
    } finally {
      if (timeoutId !== undefined) {
        clearTimeout(timeoutId);
      }
    }
  }

  return {
    get: <T>(url: string, options?: RequestOptions) =>
      request<T>("GET", url, undefined, options),
    post: <T>(url: string, body?: unknown, options?: RequestOptions) =>
      request<T>("POST", url, body, options),
    put: <T>(url: string, body?: unknown, options?: RequestOptions) =>
      request<T>("PUT", url, body, options),
    delete: <T>(url: string, options?: RequestOptions) =>
      request<T>("DELETE", url, undefined, options),
  };
}

// Default instance — wired up after authStore is created (see authStore.ts)
// Temporarily uses placeholder getToken/clearToken; real wiring happens in the barrel export
const wiring = {
  getToken: (): string | null => null,
  clearToken: (): void => {},
  getConnectionId: (): string | null => null,
};

export function wireAuth(
  getToken: () => string | null,
  clearToken: () => void,
) {
  wiring.getToken = getToken;
  wiring.clearToken = clearToken;
}

export function wireSignalR(getConnectionId: () => string | null) {
  wiring.getConnectionId = getConnectionId;
}

export const apiClient: ApiClient = createApiClient({
  delay: delayStore,
  errorRate: errorRateStore,
  getToken: () => wiring.getToken(),
  clearToken: () => wiring.clearToken(),
  getConnectionId: () => wiring.getConnectionId(),
});
