import type { ItemGroup } from "@shared/types";
import { useNavigate, useParams } from "react-router";
import { useChecklistDetailConcurrent } from "./hooks";

export interface ChecklistDetailModel {
  groupId: string;
  checklist: ItemGroup;
  isPending: boolean;
  refresh: () => void;
  addItem: () => void;
}

export function useChecklistDetailModel(): ChecklistDetailModel | null {
  const { groupId } = useParams<{ groupId: string }>();
  const navigate = useNavigate();

  if (!groupId) return null;

  const { checklist, isPending, refresh } =
    useChecklistDetailConcurrent(groupId);

  function addItem() {
    navigate(`/${groupId}/items/new`);
  }

  return { groupId, checklist, isPending, refresh, addItem };
}
