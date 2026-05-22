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
    (name: string): Promise<ItemGroup | undefined> => {
      let created: ItemGroup | undefined;
      startTransition(async () => {
        created = await createChecklist(name);
        invalidateChecklists();
        getChecklistsPromise();
      });
      return Promise.resolve(created);
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

  const refresh = useCallback(async () => {
    setIsPending(true);
    try {
      const data = await fetchChecklists();
      setChecklists(data);
    } catch (e) {
      throw e instanceof Error ? e : new Error(String(e));
    } finally {
      setIsPending(false);
    }
  }, []);

  const add = useCallback(
    async (name: string): Promise<ItemGroup | undefined> => {
      setIsPending(true);
      try {
        const created = await createChecklist(name);
        const data = await fetchChecklists();
        setChecklists(data);
        return created;
      } catch (e) {
        throw e instanceof Error ? e : new Error(String(e));
      } finally {
        setIsPending(false);
      }
    },
    [],
  );

  const remove = useCallback(async (id: string) => {
    setIsPending(true);
    try {
      await deleteChecklist(id);
      const data = await fetchChecklists();
      setChecklists(data);
    } catch (e) {
      throw e instanceof Error ? e : new Error(String(e));
    } finally {
      setIsPending(false);
    }
  }, []);

  return { checklists, isPending, refresh, add, remove };
}
