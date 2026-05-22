import { useCallback } from "react";
import { pendingService } from "../api/pendingService";

export function usePendingReporter() {
  const track = useCallback(
    <T>(operationId: string, promise: Promise<T>): Promise<T> => {
      return pendingService.track(operationId, promise);
    },
    [],
  );

  return { track };
}
