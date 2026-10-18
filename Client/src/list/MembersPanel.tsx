import { useId, useRef, useState, type SubmitEvent } from "react";
import { CopyButton } from "../components/CopyButton";
import { Icon } from "../components/Icon";
import { isGuid, normalizeGuid } from "../lib/guid";
import styles from "./ListPage.module.css";

interface MembersPanelProps {
  members: string[];
  currentUserId: string;
  onAdd: (memberId: string) => Promise<string | null>;
  onRemove: (memberId: string) => void;
}

export function MembersPanel({ members, currentUserId, onAdd, onRemove }: MembersPanelProps) {
  // Show yourself first.
  const sorted = members.toSorted((a, b) =>
    a === currentUserId ? -1 : b === currentUserId ? 1 : a.localeCompare(b),
  );
  const onlyMember = members.length === 1;

  return (
    <section aria-labelledby="members-heading" className={`card ${styles.panel}`}>
      <h2 id="members-heading" className={styles.panelHeading} tabIndex={-1}>
        <Icon name="users" size={20} />
        Members <span className="muted">({members.length})</span>
      </h2>
      <p className="hint">Everyone here can view and change this list.</p>

      <ul className={styles.memberList}>
        {sorted.map((memberId) => {
          const isYou = memberId === currentUserId;
          return (
            <li key={memberId} className={styles.member}>
              <div className={styles.memberInfo}>
                <code className={styles.memberId}>{memberId}</code>
                {isYou && <span className="badge">You</span>}
              </div>
              {isYou ? (
                !onlyMember && (
                  <button
                    type="button"
                    className="button button-small button-quiet button-danger-text"
                    onClick={() => {
                      onRemove(memberId);
                    }}
                  >
                    Leave list
                  </button>
                )
              ) : (
                <button
                  type="button"
                  className="button button-small button-quiet button-danger-text"
                  onClick={() => {
                    onRemove(memberId);
                  }}
                >
                  Remove <span className="visually-hidden">member {memberId}</span>
                </button>
              )}
            </li>
          );
        })}
      </ul>

      <div className={styles.shareId}>
        <p className="hint">Share your user ID so others can add you to their lists.</p>
        <CopyButton value={currentUserId} label="Copy your user ID" />
      </div>

      <AddMemberForm members={members} onAdd={onAdd} />
    </section>
  );
}

function AddMemberForm({ members, onAdd }: Pick<MembersPanelProps, "members" | "onAdd">) {
  const [value, setValue] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);
  const hintId = useId();
  const errorId = useId();

  async function handleSubmit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault();
    if (pending) return;

    const fail = (message: string) => {
      setError(message);
      inputRef.current?.focus();
    };

    if (!isGuid(value)) {
      fail("Enter a user ID in GUID format.");
      return;
    }
    const memberId = normalizeGuid(value);
    if (members.includes(memberId)) {
      fail("That person is already a member.");
      return;
    }

    setError(null);
    setPending(true);
    const failure = await onAdd(memberId);
    setPending(false);
    if (failure) {
      fail(failure);
    } else {
      setValue("");
    }
  }

  return (
    <form
      className={styles.addMember}
      noValidate
      onSubmit={(event) => {
        void handleSubmit(event);
      }}
    >
      <label className="label" htmlFor="member-id">
        Add a member
      </label>
      <p className="hint" id={hintId}>
        Paste their user ID. They can find it under Account.
      </p>
      <input
        ref={inputRef}
        id="member-id"
        className={`input ${styles.mono}`}
        value={value}
        onChange={(event) => {
          setValue(event.target.value);
        }}
        placeholder="00000000-0000-0000-0000-000000000000"
        autoComplete="off"
        autoCapitalize="none"
        spellCheck={false}
        aria-invalid={error ? true : undefined}
        aria-describedby={error ? `${hintId} ${errorId}` : hintId}
      />
      {error && (
        <p className="field-error" id={errorId}>
          {error}
        </p>
      )}
      <button type="submit" className="button" aria-disabled={pending}>
        <Icon name="plus" size={18} />
        {pending ? "Adding…" : "Add member"}
      </button>
    </form>
  );
}
