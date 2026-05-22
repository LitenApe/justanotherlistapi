import { ItemEdit } from "../slices/items/ItemEdit";
import { useParams } from "react-router";

export function ItemEditPage() {
  const { groupId, itemId } = useParams<{ groupId: string; itemId: string }>();
  if (!groupId || !itemId) return null;
  return <ItemEdit groupId={groupId} itemId={itemId} />;
}
