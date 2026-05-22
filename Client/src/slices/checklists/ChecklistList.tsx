import {
  useChecklistListConcurrentModel,
  useChecklistListLegacyModel,
} from "./ChecklistList.model";

import { ChecklistListView } from "./ChecklistList.view";

export function ChecklistListConcurrent() {
  const model = useChecklistListConcurrentModel();
  return <ChecklistListView {...model} />;
}

export function ChecklistListLegacy() {
  const model = useChecklistListLegacyModel();
  return <ChecklistListView {...model} />;
}
