import { useCallback, useEffect, useState, useTransition } from "react";
import { useChecklistsConcurrent, useChecklistsLegacy } from "./hooks";
import { useNavigate, useParams } from "react-router";

import type { ItemGroup } from "@shared/types";
import { routes } from "@shared/routes";
import { useFeatures } from "../dev-panel";

export interface ChecklistListModel {
  checklists: ItemGroup[];
  isPending: boolean;
  activeId: string | undefined;
  select: (id: string) => void;
  add: (name: string) => void;
  remove: (id: string) => void;
}

export function useChecklistListModel(
  onCreated: (newId: string) => void,
): ChecklistListModel {
  const { flags } = useFeatures();
  const navigate = useNavigate();
  const { groupId } = useParams();
  const [isTransitioning, startTransition] = useTransition();

  const concurrent = useChecklistsConcurrent();
  const legacy = useChecklistsLegacy();

  const usingSuspense = flags.suspense;

  const {
    checklists,
    isPending: hookPending,
    refresh,
    add,
    remove,
  } = usingSuspense ? { ...concurrent, refresh: concurrent.refresh } : legacy;

  // Fetch on mount (legacy path only — concurrent path uses use() which auto-fetches)
  useEffect(() => {
    if (!usingSuspense) {
      refresh();
    }
  }, [usingSuspense, refresh]);

  const [pendingId, setPendingId] = useState<string | undefined>();

  const select = useCallback(
    (id: string) => {
      setPendingId(id);
      if (flags.useTransition) {
        startTransition(() => {
          navigate(routes.checklist(id));
        });
      } else {
        navigate(routes.checklist(id));
      }
    },
    [flags.useTransition, startTransition, navigate],
  );

  const activeId = isTransitioning ? pendingId ?? groupId : groupId;

  const handleAdd = useCallback(
    async (name: string) => {
      const created = await add(name);
      if (created) {
        onCreated(created.id);
      }
    },
    [add, onCreated],
  );

  return {
    checklists,
    isPending: hookPending || isTransitioning,
    activeId,
    select,
    add: handleAdd,
    remove,
  };
}
