import { useCallback, useEffect, useState } from "react";
import { HttpError } from "@shared/api";
import { addMember, fetchMembers, removeMember } from "./api";

export interface MembersModel {
  members: string[];
  error: string | null;
  isPending: boolean;
  handleAdd: (memberId: string) => void;
  handleRemove: (memberId: string) => void;
}

export function useMembersModel(groupId: string): MembersModel {
  const [members, setMembers] = useState<string[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [isPending, setIsPending] = useState(false);

  const refresh = useCallback(async () => {
    const data = await fetchMembers(groupId);
    setMembers(data);
  }, [groupId]);

  useEffect(() => {
    refresh();
  }, [refresh]);

  async function handleAdd(memberId: string) {
    setIsPending(true);
    setError(null);
    try {
      await addMember(groupId, memberId);
      await refresh();
    } catch (err) {
      if (err instanceof HttpError && err.status === 409) {
        setError("Member already exists");
      } else {
        setError(err instanceof Error ? err.message : "Failed to add member");
      }
    } finally {
      setIsPending(false);
    }
  }

  async function handleRemove(memberId: string) {
    setIsPending(true);
    setError(null);
    try {
      await removeMember(groupId, memberId);
      await refresh();
    } catch (err) {
      if (err instanceof HttpError && err.status === 409) {
        setError("Cannot remove the last member");
      } else {
        setError(
          err instanceof Error ? err.message : "Failed to remove member",
        );
      }
    } finally {
      setIsPending(false);
    }
  }

  return { members, error, isPending, handleAdd, handleRemove };
}
