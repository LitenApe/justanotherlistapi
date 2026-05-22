import type { ItemGroup } from "@shared/types";
import { routes } from "@shared/routes";
import { useChecklistDetailConcurrent } from "./hooks";
import { useNavigate } from "react-router";

export interface ChecklistDetailModel {
  groupId: string;
  checklist: ItemGroup;
  isPending: boolean;
  refresh: () => void;
  addItem: () => void;
}

export function useChecklistDetailModel(groupId: string): ChecklistDetailModel {
  const navigate = useNavigate();
  const { checklist, isPending, refresh } =
    useChecklistDetailConcurrent(groupId);

  function addItem() {
    navigate(routes.itemCreate(groupId));
  }

  return { groupId, checklist, isPending, refresh, addItem };
}
