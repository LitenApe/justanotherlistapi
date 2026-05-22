import { ChecklistDetailView } from "./ChecklistDetail.view";
import { useChecklistDetailModel } from "./ChecklistDetail.model";
import { useParams } from "react-router";

export function ChecklistDetail() {
  const { groupId } = useParams<{ groupId: string }>();
  if (!groupId) return null;
  return <ChecklistDetailInner groupId={groupId} />;
}

function ChecklistDetailInner({ groupId }: { groupId: string }) {
  const model = useChecklistDetailModel(groupId);
  return <ChecklistDetailView {...model} />;
}
