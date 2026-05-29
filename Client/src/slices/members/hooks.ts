import { addMember, fetchMembers, removeMember } from "./api";
import {
  startTransition,
  use,
  useCallback,
  useEffect,
  useOptimistic,
} from "react";

import { signalRStore } from "@shared/api/signalrStore";
import { useTrackedTransition } from "@shared/hooks";

const membersCache = new Map<string, Promise<string[]>>();

function getMembersPromise(groupId: string): Promise<string[]> {
  const existing = membersCache.get(groupId);
  if (existing) return existing;
  const promise = fetchMembers(groupId);
  membersCache.set(groupId, promise);
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
      return members.concat(action.memberId);
    case "remove":
      return members.filter((id) => id !== action.memberId);
  }
}

export function useMembers(groupId: string) {
  useMemberRealtimeSync(groupId);

  const members = use(getMembersPromise(groupId));
  const [optimisticMembers, addOptimistic] = useOptimistic(
    members,
    memberReducer,
  );
  const [isPending, startTrackedTransition] =
    useTrackedTransition("members/mutation");

  const add = useCallback(
    (memberId: string) => {
      startTrackedTransition(async () => {
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
    [groupId, startTrackedTransition, addOptimistic],
  );

  const remove = useCallback(
    (memberId: string) => {
      startTrackedTransition(async () => {
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
    [groupId, startTrackedTransition, addOptimistic],
  );

  return { members: optimisticMembers, isPending, add, remove };
}

function useMemberRealtimeSync(groupId: string): void {
  useEffect(() => {
    const handleMemberAdded = () => {
      startTransition(() => {
        invalidateMembers(groupId);
        getMembersPromise(groupId);
      });
    };

    const handleMemberRemoved = () => {
      startTransition(() => {
        invalidateMembers(groupId);
        getMembersPromise(groupId);
      });
    };

    signalRStore.on("MemberAdded", handleMemberAdded as never);
    signalRStore.on("MemberRemoved", handleMemberRemoved as never);

    return () => {
      signalRStore.off("MemberAdded", handleMemberAdded as never);
      signalRStore.off("MemberRemoved", handleMemberRemoved as never);
    };
  }, [groupId]);
}
