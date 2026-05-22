import { authStore } from "@shared/api/authStore";
import { useSyncExternalStore } from "react";

export function useAuth() {
  const token = useSyncExternalStore(
    authStore.subscribe,
    authStore.getSnapshot,
  );

  return {
    isAuthenticated: token !== null,
    token,
  };
}
