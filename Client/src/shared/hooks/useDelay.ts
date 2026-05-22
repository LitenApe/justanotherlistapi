import { useSyncExternalStore } from "react";

import { delayStore } from "@shared/api";

export function useDelay(): number {
  return useSyncExternalStore(delayStore.subscribe, delayStore.getSnapshot);
}
