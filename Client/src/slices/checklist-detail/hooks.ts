import type { Item, ItemGroup } from "@shared/types";
import {
  startTransition,
  use,
  useCallback,
  useEffect,
  useSyncExternalStore,
} from "react";

import { activityLog } from "@shared/api";
import { fetchChecklist } from "./api";
import { invalidateChecklists } from "@slices/checklists";
import { signalRStore } from "@shared/api/signalrStore";
import { useTrackedTransition } from "@shared/hooks";

const detailCache = new Map<string, Promise<ItemGroup>>();
const detailListeners = new Map<string, Set<() => void>>();
const detailVersions = new Map<string, number>();

function notifyDetailListeners(id: string): void {
  detailListeners.get(id)?.forEach((l) => l());
}

function subscribeDetail(id: string, listener: () => void): () => void {
  let set = detailListeners.get(id);
  if (!set) {
    set = new Set();
    detailListeners.set(id, set);
  }
  set.add(listener);
  return () => {
    set.delete(listener);
    if (set.size === 0) detailListeners.delete(id);
  };
}

function getDetailVersion(id: string): number {
  return detailVersions.get(id) ?? 0;
}

function getDetailPromise(id: string): Promise<ItemGroup> {
  let promise = detailCache.get(id);
  if (!promise) {
    promise = fetchChecklist(id);
    detailCache.set(id, promise);
  }
  return promise;
}

export function invalidateDetail(id: string): void {
  detailCache.delete(id);
}

export function preloadDetail(id: string): void {
  getDetailPromise(id);
}

/**
 * Mutate the cached detail data without refetching.
 * Creates a pre-resolved promise tagged for React's use() to read synchronously.
 * Notifies subscribed components to re-render within the current transition.
 */
export function updateDetailItems(
  id: string,
  updater: (items: Item[]) => Item[],
): void {
  const existing = detailCache.get(id) as
    | (Promise<ItemGroup> & { status?: string; value?: ItemGroup })
    | undefined;
  if (!existing || existing.status !== "fulfilled" || !existing.value) return;

  const updated: ItemGroup = {
    ...existing.value,
    items: updater(existing.value.items),
  };
  const resolved = Promise.resolve(updated) as Promise<ItemGroup> & {
    status: string;
    value: ItemGroup;
  };
  resolved.status = "fulfilled";
  resolved.value = updated;
  detailCache.set(id, resolved);
  detailVersions.set(id, (detailVersions.get(id) ?? 0) + 1);
  notifyDetailListeners(id);
}

export function useChecklistDetail(groupId: string) {
  useRealtimeSync(groupId);

  useSyncExternalStore(
    (cb) => subscribeDetail(groupId, cb),
    () => getDetailVersion(groupId),
  );
  const checklist = use(getDetailPromise(groupId));
  const [isPending, startTrackedTransition] =
    useTrackedTransition("detail/refresh");

  const invalidateAndRefetch = useCallback(async () => {
    invalidateDetail(groupId);
    await getDetailPromise(groupId);
    detailVersions.set(groupId, (detailVersions.get(groupId) ?? 0) + 1);
    notifyDetailListeners(groupId);
  }, [groupId]);

  const refresh = useCallback(() => {
    startTrackedTransition(() => invalidateAndRefetch());
  }, [startTrackedTransition, invalidateAndRefetch]);

  return { checklist, isPending, refresh, invalidateAndRefetch };
}

function useRealtimeSync(groupId: string): void {
  useEffect(() => {
    signalRStore.joinGroup(groupId);

    function logSignalREvent(eventName: string): void {
      activityLog.append({
        id: crypto.randomUUID(),
        operationId: eventName,
        event: "complete",
        source: "signalr",
        timestamp: Date.now(),
      });
    }

    const handleItemCreated = (_gId: string, item: Item) => {
      logSignalREvent("ItemCreated");
      startTransition(() => {
        updateDetailItems(groupId, (items) => items.concat(item));
        invalidateChecklists();
      });
    };

    const handleItemUpdated = (_gId: string, item: Item) => {
      logSignalREvent("ItemUpdated");
      startTransition(() => {
        updateDetailItems(groupId, (items) =>
          items.map((i) => (i.id === item.id ? item : i)),
        );
        invalidateChecklists();
      });
    };

    const handleItemDeleted = (_gId: string, itemId: string) => {
      logSignalREvent("ItemDeleted");
      startTransition(() => {
        updateDetailItems(groupId, (items) =>
          items.filter((i) => i.id !== itemId),
        );
        invalidateChecklists();
      });
    };

    const handleGroupRenamed = () => {
      logSignalREvent("GroupRenamed");
      startTransition(() => {
        invalidateDetail(groupId);
        invalidateChecklists();
      });
    };

    const handleGroupDeleted = () => {
      logSignalREvent("GroupDeleted");
      startTransition(() => {
        invalidateChecklists();
      });
    };

    signalRStore.on("ItemCreated", handleItemCreated as never);
    signalRStore.on("ItemUpdated", handleItemUpdated as never);
    signalRStore.on("ItemDeleted", handleItemDeleted as never);
    signalRStore.on("GroupRenamed", handleGroupRenamed as never);
    signalRStore.on("GroupDeleted", handleGroupDeleted as never);

    const unsubReconnect = signalRStore.onReconnected(() => {
      signalRStore.joinGroup(groupId);
      invalidateDetail(groupId);
      getDetailPromise(groupId);
      detailVersions.set(groupId, (detailVersions.get(groupId) ?? 0) + 1);
      notifyDetailListeners(groupId);
    });

    return () => {
      signalRStore.leaveGroup(groupId);
      signalRStore.off("ItemCreated", handleItemCreated as never);
      signalRStore.off("ItemUpdated", handleItemUpdated as never);
      signalRStore.off("ItemDeleted", handleItemDeleted as never);
      signalRStore.off("GroupRenamed", handleGroupRenamed as never);
      signalRStore.off("GroupDeleted", handleGroupDeleted as never);
      unsubReconnect();
    };
  }, [groupId]);
}
