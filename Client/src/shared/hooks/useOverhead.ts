import { useSyncExternalStore } from "react";

import { computeOverheadStore } from "@shared/api";

export function useOverhead(): number {
  return useSyncExternalStore(
    computeOverheadStore.subscribe,
    computeOverheadStore.getSnapshot,
  );
}
