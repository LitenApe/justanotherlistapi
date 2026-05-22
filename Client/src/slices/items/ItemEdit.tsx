import { ItemEditView } from "./ItemEdit.view";
import { useItemEditModel } from "./ItemEdit.model";

interface Props {
  groupId: string;
  itemId: string;
}

export function ItemEdit({ groupId, itemId }: Props) {
  const model = useItemEditModel(groupId, itemId);
  return <ItemEditView {...model} />;
}
