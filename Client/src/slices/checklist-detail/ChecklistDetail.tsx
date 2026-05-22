import { ChecklistDetailView } from "./ChecklistDetail.view";
import { useChecklistDetailModel } from "./ChecklistDetail.model";

export function ChecklistDetail() {
  const model = useChecklistDetailModel();
  if (!model) return null;
  return <ChecklistDetailView {...model} />;
}
