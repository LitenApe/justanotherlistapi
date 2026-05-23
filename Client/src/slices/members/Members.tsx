import { useRef, type FormEvent } from "react";

import styles from "./Members.module.css";
import { useMembers } from "./hooks";

// ─── Model ────────────────────────────────────────────────────────────────────

interface MembersModel {
  members: string[];
  isPending: boolean;
  handleAdd: (memberId: string) => void;
  handleRemove: (memberId: string) => void;
}

function useMembersModel(groupId: string): MembersModel {
  const { members, isPending, add, remove } = useMembers(groupId);

  return { members, isPending, handleAdd: add, handleRemove: remove };
}

// ─── View ─────────────────────────────────────────────────────────────────────

function truncateId(id: string): string {
  return id.length > 8 ? `${id.slice(0, 8)}…` : id;
}

interface MembersViewProps {
  members: string[];
  isPending: boolean;
  handleAdd: (memberId: string) => void;
  handleRemove: (memberId: string) => void;
}

function MembersView({
  members,
  isPending,
  handleAdd,
  handleRemove,
}: MembersViewProps) {
  const inputRef = useRef<HTMLInputElement>(null);

  function onSubmit(e: FormEvent) {
    e.preventDefault();
    const memberId = inputRef.current?.value.trim();
    if (!memberId) return;
    handleAdd(memberId);
    if (inputRef.current) inputRef.current.value = "";
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
      <form className={styles.addForm} onSubmit={onSubmit}>
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
    </section>
  );
}

// ─── Controller ───────────────────────────────────────────────────────────────

interface Props {
  groupId: string;
}

export function Members({ groupId }: Props) {
  const model = useMembersModel(groupId);
  return <MembersView {...model} />;
}
