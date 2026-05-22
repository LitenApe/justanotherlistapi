import { useEffect, useRef, type FormEvent } from "react";
import { useNavigate, useParams } from "react-router";
import type { ItemGroup } from "@shared/types";
import { PendingBorder } from "@shared/components";
import { useChecklistsConcurrent, useChecklistsLegacy } from "./hooks";
import styles from "./ChecklistList.module.css";

interface ChecklistItemProps {
  group: ItemGroup;
  isActive: boolean;
  onSelect: (id: string) => void;
  onDelete: (id: string) => void;
}

function ChecklistItem({
  group,
  isActive,
  onSelect,
  onDelete,
}: ChecklistItemProps) {
  return (
    <div
      className={isActive ? styles.itemActive : styles.item}
      onClick={() => onSelect(group.id)}
      role="button"
      tabIndex={0}
      onKeyDown={(e) => e.key === "Enter" && onSelect(group.id)}
    >
      <div>
        <span className={styles.itemName}>{group.name}</span>
        <span className={styles.itemCount}> ({group.items.length} items)</span>
      </div>
      <button
        type="button"
        className={styles.deleteBtn}
        onClick={(e) => {
          e.stopPropagation();
          onDelete(group.id);
        }}
        aria-label={`Delete ${group.name}`}
      >
        ✕
      </button>
    </div>
  );
}

function AddForm({
  onAdd,
  disabled,
}: {
  onAdd: (name: string) => void;
  disabled: boolean;
}) {
  const inputRef = useRef<HTMLInputElement>(null);

  function handleSubmit(e: FormEvent) {
    e.preventDefault();
    const name = inputRef.current?.value.trim();
    if (!name) return;
    onAdd(name);
    if (inputRef.current) inputRef.current.value = "";
  }

  return (
    <form className={styles.addForm} onSubmit={handleSubmit}>
      <input
        ref={inputRef}
        className={styles.addInput}
        placeholder="New checklist…"
        aria-label="New checklist name"
      />
      <button type="submit" className={styles.addBtn} disabled={disabled}>
        Add
      </button>
    </form>
  );
}

export function ChecklistListConcurrent() {
  const { checklists, isPending, add, remove } = useChecklistsConcurrent();
  const navigate = useNavigate();
  const { groupId } = useParams();

  return (
    <PendingBorder pending={isPending}>
      <nav className={styles.list} aria-label="Checklists">
        {checklists.length === 0 && (
          <p className={styles.empty}>No checklists yet.</p>
        )}
        {checklists.map((g) => (
          <ChecklistItem
            key={g.id}
            group={g}
            isActive={g.id === groupId}
            onSelect={(id) => navigate(`/${id}`)}
            onDelete={remove}
          />
        ))}
      </nav>
      <AddForm onAdd={add} disabled={isPending} />
    </PendingBorder>
  );
}

export function ChecklistListLegacy() {
  const { checklists, isPending, refresh, add, remove } = useChecklistsLegacy();
  const navigate = useNavigate();
  const { groupId } = useParams();

  useEffect(() => {
    refresh();
  }, [refresh]);

  return (
    <PendingBorder pending={isPending}>
      <nav className={styles.list} aria-label="Checklists">
        {checklists.length === 0 && !isPending && (
          <p className={styles.empty}>No checklists yet.</p>
        )}
        {checklists.map((g) => (
          <ChecklistItem
            key={g.id}
            group={g}
            isActive={g.id === groupId}
            onSelect={(id) => navigate(`/${id}`)}
            onDelete={(id) => {
              remove(id);
            }}
          />
        ))}
      </nav>
      <AddForm onAdd={add} disabled={isPending} />
    </PendingBorder>
  );
}
