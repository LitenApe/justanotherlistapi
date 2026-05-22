import { ChecklistListView } from "./ChecklistList.view";
import { useChecklistListModel } from "./ChecklistList.model";

export interface ChecklistListProps {
  onCreated: (newId: string) => void;
}

export function ChecklistList({ onCreated }: ChecklistListProps) {
  const model = useChecklistListModel(onCreated);
  return <ChecklistListView {...model} />;
}
