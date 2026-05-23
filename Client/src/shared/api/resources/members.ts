import type { ApiClient } from "@shared/api/client";
import { apiClient } from "@shared/api/client";

export interface MembersResource {
  getAll(groupId: string): Promise<string[]>;
  add(groupId: string, memberId: string): Promise<void>;
  remove(groupId: string, memberId: string): Promise<void>;
}

export function createMembersResource(client: ApiClient): MembersResource {
  return {
    getAll(groupId) {
      return client.get<string[]>(`/api/list/${groupId}/member`);
    },
    add(groupId, memberId) {
      return client.post<void>(`/api/list/${groupId}/member/${memberId}`);
    },
    remove(groupId, memberId) {
      return client.delete<void>(`/api/list/${groupId}/member/${memberId}`);
    },
  };
}

export const membersResource: MembersResource =
  createMembersResource(apiClient);
