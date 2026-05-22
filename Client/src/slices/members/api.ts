import { membersResource } from "@shared/api";

export function fetchMembers(groupId: string): Promise<string[]> {
  return membersResource.getAll(groupId);
}

export function addMember(groupId: string, memberId: string): Promise<void> {
  return membersResource.add(groupId, memberId);
}

export function removeMember(groupId: string, memberId: string): Promise<void> {
  return membersResource.remove(groupId, memberId);
}
