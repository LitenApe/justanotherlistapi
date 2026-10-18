import { useEffect, useLayoutEffect, useRef, useSyncExternalStore } from "react";
import { checklistHub, type ConnectionStatus, type GroupEventHandlers } from "./hub";

export function useConnectionStatus(): ConnectionStatus {
  return useSyncExternalStore(checklistHub.subscribeStatus, checklistHub.getStatus);
}

/** Keeps the realtime connection open while a user is signed in. */
export function useRealtimeConnection(accessTokenFactory: (() => string) | null): void {
  const enabled = accessTokenFactory !== null;
  const factoryRef = useRef(accessTokenFactory);
  useLayoutEffect(() => {
    factoryRef.current = accessTokenFactory;
  });

  useEffect(() => {
    if (!enabled) return;
    checklistHub.start(() => factoryRef.current?.() ?? "");
    return () => {
      checklistHub.stop();
    };
  }, [enabled]);
}

/**
 * Subscribes to realtime events for the given lists. Handlers may change on
 * every render; subscriptions only change when the set of list IDs does.
 */
export function useGroupEvents(groupIds: readonly string[], handlers: GroupEventHandlers): void {
  const handlersRef = useRef(handlers);
  useLayoutEffect(() => {
    handlersRef.current = handlers;
  });

  const subscriptions = useRef(new Map<string, () => void>());
  const key = [...groupIds].sort().join(",");

  useEffect(() => {
    const active = subscriptions.current;
    const wanted = new Set(key ? key.split(",") : []);

    for (const [groupId, unsubscribe] of active) {
      if (!wanted.has(groupId)) {
        unsubscribe();
        active.delete(groupId);
      }
    }
    for (const groupId of wanted) {
      if (!active.has(groupId)) {
        active.set(groupId, checklistHub.subscribe(groupId, forward(handlersRef)));
      }
    }
  }, [key]);

  useEffect(() => {
    const active = subscriptions.current;
    return () => {
      for (const unsubscribe of active.values()) unsubscribe();
      active.clear();
    };
  }, []);
}

function forward(ref: { current: GroupEventHandlers }): GroupEventHandlers {
  return {
    itemCreated: (...args) => ref.current.itemCreated?.(...args),
    itemUpdated: (...args) => ref.current.itemUpdated?.(...args),
    itemDeleted: (...args) => ref.current.itemDeleted?.(...args),
    memberAdded: (...args) => ref.current.memberAdded?.(...args),
    memberRemoved: (...args) => ref.current.memberRemoved?.(...args),
    groupRenamed: (...args) => ref.current.groupRenamed?.(...args),
    groupDeleted: (...args) => ref.current.groupDeleted?.(...args),
    resync: (...args) => ref.current.resync?.(...args),
  };
}
