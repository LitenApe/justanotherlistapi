export { type ItemGroup, type Item } from "../types";
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
  createActivityLog,
  activityLog,
} from "./activityLog";

export {
  type AuthResource,
  type TokenResponse,
  createAuthResource,
  authResource,
  type ChecklistsResource,
  type CreateItemGroupRequest,
  type UpdateItemGroupRequest,
  createChecklistsResource,
  checklistsResource,
  type ItemsResource,
  type CreateItemRequest,
  type UpdateItemRequest,
  createItemsResource,
  itemsResource,
  type MembersResource,
  createMembersResource,
  membersResource,
  type SeedResource,
  createSeedResource,
  seedResource,
} from "./resources";

// Wire auth store into the API client
import { wireAuth } from "./client";
import { authStore } from "./authStore";
wireAuth(authStore.getToken, authStore.clearToken);
