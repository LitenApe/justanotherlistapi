import { ChecklistDetailView } from "./ChecklistDetail.view";
import { useChecklistDetailModel } from "./ChecklistDetail.model";

interface Props {
  groupId: string;
}

export function ChecklistDetailContent({ groupId }: Props) {
  const { checklist, onItemChanged } = useChecklistDetailModel(groupId);

  return (
    <ChecklistDetailView
      groupId={groupId}
      checklist={checklist}
      onItemChanged={onItemChanged}
    />
  );
}
