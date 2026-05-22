import { useSyncExternalStore } from "react";

import type { LogEntry } from "@shared/api";
import { activityLog } from "@shared/api";

export function useActivityEntries(): readonly LogEntry[] {
  return useSyncExternalStore(activityLog.subscribe, activityLog.getSnapshot);
}
