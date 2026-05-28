import { useSyncExternalStore } from "react";
import { type ConnectionState, signalRStore } from "@shared/api/signalrStore";

export function useSignalRStatus(): ConnectionState {
  return useSyncExternalStore(signalRStore.subscribe, signalRStore.getSnapshot);
}
