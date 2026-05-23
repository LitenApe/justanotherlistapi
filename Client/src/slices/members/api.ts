import { apiClient } from "@shared/api/client";
import { pendingService } from "@shared/api/pendingService";

export function fetchMembers(groupId: string): Promise<string[]> {
  return pendingService.track(
    "members/list",
    apiClient.get<string[]>(`/api/list/${groupId}/member`),
  );
}

export function addMember(groupId: string, memberId: string): Promise<void> {
  return pendingService.track(
    "members/add",
    apiClient.post<void>(`/api/list/${groupId}/member/${memberId}`),
  );
}

export function removeMember(groupId: string, memberId: string): Promise<void> {
  return pendingService.track(
    "members/remove",
    apiClient.delete<void>(`/api/list/${groupId}/member/${memberId}`),
  );
}
