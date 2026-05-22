import { MembersView } from "./Members.view";
import { useMembersModel } from "./Members.model";

interface Props {
  groupId: string;
  members: string[];
  onRefresh: () => void;
}

export function Members({ groupId, members, onRefresh }: Props) {
  const model = useMembersModel(groupId, members, onRefresh);
  return <MembersView {...model} />;
}
