import { use, useCallback, useEffect, useState, useTransition } from "react";

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

export function useChecklistDetailConcurrent(groupId: string) {
  const checklist = use(getDetailPromise(groupId));
  const [isPending, startTransition] = useTransition();

  const refresh = useCallback(() => {
    startTransition(async () => {
      invalidateDetail(groupId);
      await getDetailPromise(groupId);
    });
  }, [groupId, startTransition]);

  return { checklist, isPending, refresh };
}

export function useChecklistDetailLegacy(groupId: string) {
  const [checklist, setChecklist] = useState<ItemGroup | null>(null);
  const [isPending, setIsPending] = useState(true);
  const [error, setError] = useState<Error | null>(null);

  const refresh = useCallback(async () => {
    setIsPending(true);
    setError(null);
    try {
      const data = await fetchChecklist(groupId);
      setChecklist(data);
    } catch (e) {
      setError(e instanceof Error ? e : new Error(String(e)));
    } finally {
      setIsPending(false);
    }
  }, [groupId]);

  useEffect(() => {
    refresh();
  }, [refresh]);

  return { checklist, isPending, error, refresh };
}
