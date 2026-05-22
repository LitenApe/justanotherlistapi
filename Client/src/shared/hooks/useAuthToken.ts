import { useSyncExternalStore } from "react";

import { authStore } from "@shared/api/authStore";

export function useAuthToken(): string | null {
  return useSyncExternalStore(authStore.subscribe, authStore.getSnapshot);
}
