import { useSyncExternalStore } from "react";

import { pendingService } from "@shared/api";

export function usePending(): boolean {
  return useSyncExternalStore(
    pendingService.subscribe,
    pendingService.getSnapshot,
  );
}
