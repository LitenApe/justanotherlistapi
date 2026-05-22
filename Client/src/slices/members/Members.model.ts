import { useState } from "react";
import { HttpError } from "@shared/api";
import { addMember, removeMember } from "./api";

export interface MembersModel {
  members: string[];
  error: string | null;
  isPending: boolean;
  handleAdd: (memberId: string) => void;
  handleRemove: (memberId: string) => void;
}

export function useMembersModel(
  groupId: string,
  members: string[],
  onRefresh: () => void,
): MembersModel {
  const [error, setError] = useState<string | null>(null);
  const [isPending, setIsPending] = useState(false);

  async function handleAdd(memberId: string) {
    setIsPending(true);
    setError(null);
    try {
      await addMember(groupId, memberId);
      onRefresh();
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
      onRefresh();
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
