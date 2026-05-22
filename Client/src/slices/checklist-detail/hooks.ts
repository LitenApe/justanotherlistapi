import { use, useCallback, useTransition } from "react";

import type { ItemGroup } from "@shared/types";
import { fetchChecklist } from "./api";

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

export function useChecklistDetail(groupId: string) {
  const checklist = use(getDetailPromise(groupId));
  const [isPending, startTransition] = useTransition();

  const invalidateAndRefetch = useCallback(async () => {
    invalidateDetail(groupId);
    await getDetailPromise(groupId);
  }, [groupId]);

  const refresh = useCallback(() => {
    startTransition(() => invalidateAndRefetch());
  }, [startTransition, invalidateAndRefetch]);

  return { checklist, isPending, refresh, invalidateAndRefetch };
}
