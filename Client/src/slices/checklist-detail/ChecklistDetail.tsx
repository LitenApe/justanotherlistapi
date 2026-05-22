import { ChecklistDetailView } from "./ChecklistDetail.view";
import { useChecklistDetailModel } from "./ChecklistDetail.model";
import { useLocation, useParams } from "react-router";

export function ChecklistDetail() {
  const { groupId } = useParams<{ groupId: string }>();
  const location = useLocation();
  const name = (location.state as { name?: string } | null)?.name;
  if (!groupId) return null;
  return <ChecklistDetailInner groupId={groupId} previewName={name} />;
}

function ChecklistDetailInner({
  groupId,
  previewName,
}: {
  groupId: string;
  previewName: string | undefined;
}) {
  const model = useChecklistDetailModel(groupId);
  return <ChecklistDetailView {...model} previewName={previewName} />;
}
