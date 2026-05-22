import { createChecklist, deleteChecklist } from "./api";
import { use, useCallback, useTransition } from "react";

import type { ItemGroup } from "@shared/types";
import { checklistsResource } from "@shared/api";

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
  const [isPending, startTransition] = useTransition();

  const refresh = useCallback(() => {
    startTransition(async () => {
      invalidateChecklists();
      await getChecklistsPromise();
    });
  }, [startTransition]);

  const add = useCallback(
    (name: string): Promise<ItemGroup | undefined> => {
      let created: ItemGroup | undefined;
      startTransition(async () => {
        created = await createChecklist(name);
        invalidateChecklists();
        await getChecklistsPromise();
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
        await getChecklistsPromise();
      });
    },
    [startTransition],
  );

  return { checklists, isPending, refresh, add, remove };
}
