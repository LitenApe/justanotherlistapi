import {
  useChecklistDetailConcurrent,
  useChecklistDetailLegacy,
} from "./hooks";

import type { ItemGroup } from "@shared/types";
import { routes } from "@shared/routes";
import { useFeatures } from "../dev-panel";
import { useNavigate } from "react-router";

export interface ChecklistDetailModel {
  groupId: string;
  checklist: ItemGroup | null;
  refresh: () => void;
  addItem: () => void;
}

export function useChecklistDetailModel(groupId: string): ChecklistDetailModel {
  const navigate = useNavigate();
  const { flags } = useFeatures();

  const concurrent = useChecklistDetailConcurrent(groupId);
  const legacy = useChecklistDetailLegacy(groupId);
  const { checklist, refresh } = flags.suspense ? concurrent : legacy;

  function addItem() {
    navigate(routes.itemCreate(groupId));
  }

  return { groupId, checklist, refresh, addItem };
}
