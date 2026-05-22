import { useChecklistDetail } from "./hooks";

import type { ItemGroup } from "@shared/types";
import { routes } from "@shared/routes";
import { useNavigate } from "react-router";

export interface ChecklistDetailModel {
  groupId: string;
  checklist: ItemGroup;
  refresh: () => void;
  onItemChanged: () => Promise<void>;
  addItem: () => void;
}

export function useChecklistDetailModel(groupId: string): ChecklistDetailModel {
  const navigate = useNavigate();
  const { checklist, refresh, invalidateAndRefetch } =
    useChecklistDetail(groupId);

  function addItem() {
    navigate(routes.itemCreate(groupId));
  }

  return { groupId, checklist, refresh, onItemChanged: invalidateAndRefetch, addItem };
}
