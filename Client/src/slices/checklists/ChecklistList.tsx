import { ChecklistListView } from "./ChecklistList.view";
import { useChecklistListModel } from "./ChecklistList.model";

export interface ChecklistListProps {
  refreshSignal: number;
  onCreated: (newId: string) => void;
}

export function ChecklistList({
  refreshSignal,
  onCreated,
}: ChecklistListProps) {
  const model = useChecklistListModel(refreshSignal, onCreated);
  return <ChecklistListView {...model} />;
}
