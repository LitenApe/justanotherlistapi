import { useId, useRef, useState, type SubmitEvent } from "react";
import type { Member } from "../api/types";
import { isEmailAddress } from "../auth/accountRules";
import { Icon } from "../components/Icon";
import styles from "./ListPage.module.css";
import { memberName, sortMembers } from "./members";

interface MembersPanelProps {
  members: Member[];
  currentUserId: string;
  /** Adds the person who signed up with the email; resolves to a problem to show, or null. */
  onAdd: (email: string) => Promise<string | null>;
  onRemove: (memberId: string) => void;
}

export function MembersPanel({ members, currentUserId, onAdd, onRemove }: MembersPanelProps) {
  const sorted = sortMembers(members, currentUserId);
  const onlyMember = members.length === 1;

  return (
    <section aria-labelledby="members-heading" className={`card ${styles.panel}`}>
      <h2 id="members-heading" className={styles.panelHeading} tabIndex={-1}>
        <Icon name="users" size={20} />
        Members <span className="muted">({members.length})</span>
      </h2>
      <p className="hint">Everyone here can view and change this list.</p>

      <ul className={styles.memberList}>
        {sorted.map(({ id: memberId }) => {
          const isYou = memberId === currentUserId;
          const name = memberName(memberId, members);
          return (
            <li key={memberId} className={styles.member}>
              <div className={styles.memberInfo}>
                <span className={styles.memberName}>{name}</span>
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
                  Remove <span className="visually-hidden">member {name}</span>
                </button>
              )}
            </li>
          );
        })}
      </ul>

      <AddMemberForm onAdd={onAdd} />
    </section>
  );
}

function AddMemberForm({ onAdd }: Pick<MembersPanelProps, "onAdd">) {
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

    const email = value.trim();
    if (!email) {
      fail("Enter their email address.");
      return;
    }
    if (!isEmailAddress(email)) {
      fail("Enter an email address, like name@example.com.");
      return;
    }

    setError(null);
    setPending(true);
    const failure = await onAdd(email);
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
      <label className="label" htmlFor="member-email">
        Add a member
      </label>
      <p className="hint" id={hintId}>
        The email they signed up with. Members see each other's names, never their emails.
      </p>
      <input
        ref={inputRef}
        id="member-email"
        type="email"
        className="input"
        value={value}
        onChange={(event) => {
          setValue(event.target.value);
        }}
        placeholder="name@example.com"
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
