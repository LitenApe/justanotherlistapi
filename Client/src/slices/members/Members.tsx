import { useState, useRef, type FormEvent } from "react";
import { HttpError } from "@shared/api";
import { addMember, removeMember } from "./api";
import styles from "./Members.module.css";

interface Props {
  groupId: string;
  members: string[];
  onRefresh: () => void;
}

function truncateId(id: string): string {
  return id.length > 8 ? `${id.slice(0, 8)}…` : id;
}

export function Members({ groupId, members, onRefresh }: Props) {
  const [error, setError] = useState<string | null>(null);
  const [isPending, setIsPending] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);

  async function handleAdd(e: FormEvent) {
    e.preventDefault();
    const memberId = inputRef.current?.value.trim();
    if (!memberId) return;

    setIsPending(true);
    setError(null);
    try {
      await addMember(groupId, memberId);
      if (inputRef.current) inputRef.current.value = "";
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

  return (
    <section className={styles.container}>
      <h3 className={styles.title}>Members ({members.length})</h3>
      <div className={styles.list}>
        {members.map((id) => (
          <div key={id} className={styles.member}>
            <span className={styles.memberId} title={id}>
              {truncateId(id)}
            </span>
            <button
              type="button"
              className={styles.removeBtn}
              onClick={() => handleRemove(id)}
              disabled={isPending}
              aria-label={`Remove member ${truncateId(id)}`}
            >
              ✕
            </button>
          </div>
        ))}
      </div>
      <form className={styles.addForm} onSubmit={handleAdd}>
        <input
          ref={inputRef}
          className={styles.addInput}
          placeholder="Member GUID…"
          aria-label="New member ID"
        />
        <button type="submit" className={styles.addBtn} disabled={isPending}>
          Add
        </button>
      </form>
      {error && <p className={styles.error}>{error}</p>}
    </section>
  );
}
