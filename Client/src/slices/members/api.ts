import { membersResource, pendingService } from "@shared/api";

export function fetchMembers(groupId: string): Promise<string[]> {
  return pendingService.track("members/list", membersResource.getAll(groupId));
}

export function addMember(groupId: string, memberId: string): Promise<void> {
  return pendingService.track(
    "members/add",
    membersResource.add(groupId, memberId),
  );
}

export function removeMember(groupId: string, memberId: string): Promise<void> {
  return pendingService.track(
    "members/remove",
    membersResource.remove(groupId, memberId),
  );
}
