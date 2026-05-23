import type { Item, ItemGroup } from "@shared/types";
import { use, useCallback, useSyncExternalStore } from "react";

import { fetchChecklist } from "./api";
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
  useSyncExternalStore(
    (cb) => subscribeDetail(groupId, cb),
    () => getDetailVersion(groupId),
  );
  const checklist = use(getDetailPromise(groupId));
  const [isPending, startTransition] = useTrackedTransition("detail/refresh");

  const invalidateAndRefetch = useCallback(async () => {
    invalidateDetail(groupId);
    await getDetailPromise(groupId);
    detailVersions.set(groupId, (detailVersions.get(groupId) ?? 0) + 1);
    notifyDetailListeners(groupId);
  }, [groupId]);

  const refresh = useCallback(() => {
    startTransition(() => invalidateAndRefetch());
  }, [startTransition, invalidateAndRefetch]);

  return { checklist, isPending, refresh, invalidateAndRefetch };
}
