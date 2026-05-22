import { useCallback } from "react";
import { useChecklists } from "./hooks";
import { useNavigate, useParams } from "react-router";

import type { ItemGroup } from "@shared/types";
import { routes } from "@shared/routes";

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
  const navigate = useNavigate();
  const { groupId } = useParams();
  const { checklists, isPending, add, remove } = useChecklists();

  const select = useCallback(
    (id: string) => {
      const group = checklists.find((g) => g.id === id);
      const state = group ? { name: group.name } : undefined;
      navigate(routes.checklist(id), { state });
    },
    [navigate, checklists],
  );

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
    isPending,
    activeId: groupId,
    select,
    add: handleAdd,
    remove,
  };
}
