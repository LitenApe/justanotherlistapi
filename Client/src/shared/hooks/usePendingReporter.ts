import { pendingService } from "@shared/api/pendingService";
import { useCallback } from "react";

export function usePendingReporter() {
  const track = useCallback(
    <T>(operationId: string, promise: Promise<T>): Promise<T> => {
      return pendingService.track(operationId, promise);
    },
    [],
  );

  return { track };
}
