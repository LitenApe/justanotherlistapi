import { MembersView } from "./Members.view";
import { useMembersModel } from "./Members.model";

interface Props {
  groupId: string;
}

export function Members({ groupId }: Props) {
  const model = useMembersModel(groupId);
  return <MembersView {...model} />;
}
