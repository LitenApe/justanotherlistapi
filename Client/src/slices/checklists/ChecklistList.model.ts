import { useChecklistsConcurrent, useChecklistsLegacy } from "./hooks";
import { useNavigate, useParams } from "react-router";

import type { ItemGroup } from "@shared/types";
import { routes } from "@shared/routes";
import { useEffect } from "react";
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
  refreshSignal: number,
  onCreated: (newId: string) => void,
): ChecklistListModel {
  const { flags } = useFeatures();
  const navigate = useNavigate();
  const { groupId } = useParams();

  const concurrent = useChecklistsConcurrent();
  const legacy = useChecklistsLegacy();

  const { checklists, isPending, refresh, add, remove } = flags.suspense
    ? { ...concurrent, refresh: concurrent.refresh }
    : legacy;

  // Re-fetch when refreshSignal changes (legacy path)
  useEffect(() => {
    refresh();
  }, [refreshSignal, refresh]);

  function select(id: string) {
    navigate(routes.checklist(id));
  }

  async function handleAdd(name: string) {
    const created = await add(name);
    if (created) {
      onCreated(created.id);
    }
  }

  return {
    checklists,
    isPending,
    activeId: groupId,
    select,
    add: handleAdd,
    remove,
  };
}
