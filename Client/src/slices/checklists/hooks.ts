import { createChecklist, deleteChecklist, fetchChecklists } from "./api";
import { use, useCallback, useState, useTransition } from "react";

import type { ItemGroup } from "@shared/types";

let checklistsPromise: Promise<ItemGroup[]> | null = null;

function getChecklistsPromise(): Promise<ItemGroup[]> {
  if (!checklistsPromise) {
    checklistsPromise = fetchChecklists();
  }
  return checklistsPromise;
}

export function invalidateChecklists(): void {
  checklistsPromise = null;
}

export function useChecklistsConcurrent() {
  const checklists = use(getChecklistsPromise());
  const [isPending, startTransition] = useTransition();

  const refresh = useCallback(() => {
    startTransition(() => {
      invalidateChecklists();
      getChecklistsPromise();
    });
  }, [startTransition]);

  const add = useCallback(
    (name: string) => {
      startTransition(async () => {
        await createChecklist(name);
        invalidateChecklists();
        getChecklistsPromise();
      });
    },
    [startTransition],
  );

  const remove = useCallback(
    (id: string) => {
      startTransition(async () => {
        await deleteChecklist(id);
        invalidateChecklists();
        getChecklistsPromise();
      });
    },
    [startTransition],
  );

  return { checklists, isPending, refresh, add, remove };
}

export function useChecklistsLegacy() {
  const [checklists, setChecklists] = useState<ItemGroup[]>([]);
  const [isPending, setIsPending] = useState(false);
  const [error, setError] = useState<Error | null>(null);

  const refresh = useCallback(async () => {
    setIsPending(true);
    setError(null);
    try {
      const data = await fetchChecklists();
      setChecklists(data);
    } catch (e) {
      setError(e instanceof Error ? e : new Error(String(e)));
    } finally {
      setIsPending(false);
    }
  }, []);

  const add = useCallback(async (name: string) => {
    setIsPending(true);
    try {
      await createChecklist(name);
      const data = await fetchChecklists();
      setChecklists(data);
    } catch (e) {
      setError(e instanceof Error ? e : new Error(String(e)));
    } finally {
      setIsPending(false);
    }
  }, []);

  const remove = useCallback(async (id: string) => {
    setIsPending(true);
    try {
      await deleteChecklist(id);
      const data = await fetchChecklists();
      setChecklists(data);
    } catch (e) {
      setError(e instanceof Error ? e : new Error(String(e)));
    } finally {
      setIsPending(false);
    }
  }, []);

  return { checklists, isPending, error, refresh, add, remove };
}
