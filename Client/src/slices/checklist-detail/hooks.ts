import { use, useCallback } from "react";

import type { ItemGroup } from "@shared/types";
import { fetchChecklist } from "./api";
import { useTrackedTransition } from "@shared/hooks";

const detailCache = new Map<string, Promise<ItemGroup>>();

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

export function useChecklistDetail(groupId: string) {
  const checklist = use(getDetailPromise(groupId));
  const [isPending, startTransition] = useTrackedTransition("detail/refresh");

  const invalidateAndRefetch = useCallback(async () => {
    invalidateDetail(groupId);
    await getDetailPromise(groupId);
  }, [groupId]);

  const refresh = useCallback(() => {
    startTransition(() => invalidateAndRefetch());
  }, [startTransition, invalidateAndRefetch]);

  return { checklist, isPending, refresh, invalidateAndRefetch };
}
