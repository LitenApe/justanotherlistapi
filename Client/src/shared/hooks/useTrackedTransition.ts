import { useEffect, useRef, useTransition } from "react";

import { pendingService } from "@shared/api";

export function useTrackedTransition(operationId: string) {
  const [isPending, startTransition] = useTransition();
  const activeRef = useRef(false);

  useEffect(() => {
    if (isPending && !activeRef.current) {
      activeRef.current = true;
      pendingService.begin(operationId);
    } else if (!isPending && activeRef.current) {
      activeRef.current = false;
      pendingService.end(operationId);
    }
  }, [isPending, operationId]);

  useEffect(() => {
    return () => {
      if (activeRef.current) {
        pendingService.end(operationId);
      }
    };
  }, [operationId]);

  return [isPending, startTransition] as const;
}
