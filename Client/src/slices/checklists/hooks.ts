import { createChecklist, deleteChecklist } from "./api";
import { use, useCallback } from "react";

import type { ItemGroup } from "@shared/types";
import { checklistsResource } from "@shared/api";
import { useTrackedTransition } from "@shared/hooks";

let checklistsPromise: Promise<ItemGroup[]> | null = null;

function getChecklistsPromise(): Promise<ItemGroup[]> {
  if (!checklistsPromise) {
    checklistsPromise = checklistsResource.getAll();
  }
  return checklistsPromise;
}

export function invalidateChecklists(): void {
  checklistsPromise = null;
}

export function useChecklists() {
  const checklists = use(getChecklistsPromise());
  const [isPending, startTransition] = useTrackedTransition(
    "checklists/mutation",
  );

  const refresh = useCallback(() => {
    startTransition(async () => {
      invalidateChecklists();
      await getChecklistsPromise();
    });
  }, [startTransition]);

  const add = useCallback(
    (name: string): Promise<ItemGroup> => {
      return new Promise((resolve, reject) => {
        startTransition(async () => {
          try {
            const created = await createChecklist(name);
            invalidateChecklists();
            await getChecklistsPromise();
            resolve(created);
          } catch (e) {
            reject(e);
          }
        });
      });
    },
    [startTransition],
  );

  const remove = useCallback(
    (id: string) => {
      startTransition(async () => {
        await deleteChecklist(id);
        invalidateChecklists();
        await getChecklistsPromise();
      });
    },
    [startTransition],
  );

  return { checklists, isPending, refresh, add, remove };
}
