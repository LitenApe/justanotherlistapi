export { type ItemGroup, type Item } from "@shared/types";
export {
  type ApiClient,
  type RequestOptions,
  HttpError,
  SimulatedNetworkError,
  UnauthorizedError,
  createApiClient,
  apiClient,
} from "./client";
export { type AuthStore, createAuthStore, authStore } from "./authStore";
export { type DelayStore, createDelayStore, delayStore } from "./delay";
export {
  type ComputeOverheadStore,
  createComputeOverheadStore,
  computeOverheadStore,
} from "./computeOverhead";
export {
  type ErrorRateStore,
  createErrorRateStore,
  errorRateStore,
} from "./errorRate";
export {
  type PendingService,
  createPendingService,
  pendingService,
} from "./pendingService";
export {
  type ActivityLog,
  type LogEntry,
  type LogSource,
  createActivityLog,
  activityLog,
} from "./activityLog";
export {
  type ConnectionState,
  type SignalRStore,
  type SignalRStoreDeps,
  createSignalRStore,
  signalRStore,
} from "./signalrStore";

// Wire auth store into the API client
import { wireAuth, wireSignalR } from "./client";
import { authStore } from "./authStore";
import { signalRStore } from "./signalrStore";
wireAuth(authStore.getToken, authStore.clearToken);
wireSignalR(signalRStore.getConnectionId);
