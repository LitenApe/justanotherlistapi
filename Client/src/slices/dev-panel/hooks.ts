import { useSyncExternalStore } from "react";

import type { LogEntry } from "@shared/api";
import {
  activityLog,
  computeOverheadStore,
  delayStore,
  errorRateStore,
} from "@shared/api";

export function useDelay(): number {
  return useSyncExternalStore(delayStore.subscribe, delayStore.getSnapshot);
}

export function useErrorRate(): number {
  return useSyncExternalStore(
    errorRateStore.subscribe,
    errorRateStore.getSnapshot,
  );
}

export function useOverhead(): number {
  return useSyncExternalStore(
    computeOverheadStore.subscribe,
    computeOverheadStore.getSnapshot,
  );
}

export function useActivityEntries(): readonly LogEntry[] {
  return useSyncExternalStore(activityLog.subscribe, activityLog.getSnapshot);
}
