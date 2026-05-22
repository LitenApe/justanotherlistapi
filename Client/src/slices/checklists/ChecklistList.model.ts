import { useChecklistsConcurrent, useChecklistsLegacy } from "./hooks";
import { useNavigate, useParams } from "react-router";

import type { ItemGroup } from "@shared/types";
import { routes } from "@shared/routes";
import { useEffect } from "react";

export interface ChecklistListModel {
  checklists: ItemGroup[];
  isPending: boolean;
  activeId: string | undefined;
  select: (id: string) => void;
  add: (name: string) => void;
  remove: (id: string) => void;
}

export function useChecklistListConcurrentModel(): ChecklistListModel {
  const { checklists, isPending, add, remove } = useChecklistsConcurrent();
  const navigate = useNavigate();
  const { groupId } = useParams();

  function select(id: string) {
    navigate(routes.checklist(id));
  }

  return { checklists, isPending, activeId: groupId, select, add, remove };
}

export function useChecklistListLegacyModel(): ChecklistListModel {
  const { checklists, isPending, refresh, add, remove } = useChecklistsLegacy();
  const navigate = useNavigate();
  const { groupId } = useParams();

  useEffect(() => {
    refresh();
  }, [refresh]);

  function select(id: string) {
    navigate(routes.checklist(id));
  }

  return { checklists, isPending, activeId: groupId, select, add, remove };
}
