import { use, useCallback, useOptimistic } from "react";

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

type MemberAction =
  | { type: "add"; memberId: string }
  | { type: "remove"; memberId: string };

function memberReducer(members: string[], action: MemberAction): string[] {
  switch (action.type) {
    case "add":
      return [...members, action.memberId];
    case "remove":
      return members.filter((id) => id !== action.memberId);
  }
}

export function useMembers(groupId: string) {
  const members = use(getMembersPromise(groupId));
  const [optimisticMembers, addOptimistic] = useOptimistic(
    members,
    memberReducer,
  );
  const [isPending, startTransition] = useTrackedTransition("members/mutation");

  const add = useCallback(
    (memberId: string) => {
      startTransition(async () => {
        addOptimistic({ type: "add", memberId });
        try {
          await addMember(groupId, memberId);
        } catch {
          // Optimistic update auto-reverts on next render with fresh members
        }
        invalidateMembers(groupId);
        await getMembersPromise(groupId);
      });
    },
    [groupId, startTransition, addOptimistic],
  );

  const remove = useCallback(
    (memberId: string) => {
      startTransition(async () => {
        addOptimistic({ type: "remove", memberId });
        try {
          await removeMember(groupId, memberId);
        } catch {
          // Optimistic update auto-reverts on next render with fresh members
        }
        invalidateMembers(groupId);
        await getMembersPromise(groupId);
      });
    },
    [groupId, startTransition, addOptimistic],
  );

  return { members: optimisticMembers, isPending, add, remove };
}
