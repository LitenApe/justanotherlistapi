import { useSyncExternalStore } from "react";

import { errorRateStore } from "@shared/api";

export function useErrorRate(): number {
  return useSyncExternalStore(
    errorRateStore.subscribe,
    errorRateStore.getSnapshot,
  );
}
