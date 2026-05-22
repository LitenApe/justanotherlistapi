import { ChecklistDetailView } from "./ChecklistDetail.view";
import { useChecklistDetailModel } from "./ChecklistDetail.model";

interface Props {
  groupId: string;
}

export function ChecklistDetailContent({ groupId }: Props) {
  const { checklist, onItemChanged } = useChecklistDetailModel(groupId);

  if (!checklist) return null;

  return (
    <ChecklistDetailView
      groupId={groupId}
      checklist={checklist}
      onItemChanged={onItemChanged}
    />
  );
}
