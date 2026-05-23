import { useActionState, useEffect, useRef } from "react";

import { pendingService } from "@shared/api";

export function useTrackedActionState<State, Payload>(
  operationId: string,
  action: (state: Awaited<State>, payload: Payload) => State | Promise<State>,
  initialState: Awaited<State>,
): [
  state: Awaited<State>,
  dispatch: (payload: Payload) => void,
  isPending: boolean,
] {
  const [state, dispatch, isPending] = useActionState(action, initialState);
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

  return [state, dispatch, isPending];
}
