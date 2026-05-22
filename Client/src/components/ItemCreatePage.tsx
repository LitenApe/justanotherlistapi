import { ItemCreate } from "../slices/items/ItemCreate";
import { useParams } from "react-router";

export function ItemCreatePage() {
  const { groupId } = useParams<{ groupId: string }>();
  if (!groupId) return null;
  return <ItemCreate groupId={groupId} />;
}
