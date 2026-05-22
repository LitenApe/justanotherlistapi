import { ItemCreateView } from "./ItemCreate.view";
import { useItemCreateModel } from "./ItemCreate.model";

interface Props {
  groupId: string;
}

export function ItemCreate({ groupId }: Props) {
  const model = useItemCreateModel(groupId);
  return <ItemCreateView {...model} />;
}
