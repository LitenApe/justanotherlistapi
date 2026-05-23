import { use, useCallback } from "react";

import { addMember, fetchMembers, removeMember } from "./api";
import { useTrackedTransition } from "@shared/hooks";

const membersCache = new Map<string, Promise<string[]>>();

function getMembersPromise(groupId: string): Promise<string[]> {
  let promise = membersCache.get(groupId);
  if (!promise) {
    promise = fetchMembers(groupId);
    membersCache.set(groupId, promise);
  }
  return promise;
}

export function invalidateMembers(groupId: string): void {
  membersCache.delete(groupId);
}

export function useMembers(groupId: string) {
  const members = use(getMembersPromise(groupId));
  const [isPending, startTransition] = useTrackedTransition("members/mutation");

  const add = useCallback(
    (memberId: string) => {
      startTransition(async () => {
        await addMember(groupId, memberId);
        invalidateMembers(groupId);
        await getMembersPromise(groupId);
      });
    },
    [groupId, startTransition],
  );

  const remove = useCallback(
    (memberId: string) => {
      startTransition(async () => {
        await removeMember(groupId, memberId);
        invalidateMembers(groupId);
        await getMembersPromise(groupId);
      });
    },
    [groupId, startTransition],
  );

  return { members, isPending, add, remove };
}
