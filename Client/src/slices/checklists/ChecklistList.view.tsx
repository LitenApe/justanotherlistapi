import { useRef, type FormEvent } from "react";

import type { ItemGroup } from "@shared/types";
import { RenderCount } from "@shared/components";
import { ChecklistSearch } from "../checklist-search";
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

export interface ChecklistListViewProps {
  checklists: ItemGroup[];
  isPending: boolean;
  activeId: string | undefined;
  select: (id: string) => void;
  add: (name: string) => void;
  remove: (id: string) => void;
}

export function ChecklistListView({
  checklists,
  isPending,
  activeId,
  select,
  add,
  remove,
}: ChecklistListViewProps) {
  return (
    <>
      <RenderCount label="ChecklistList" />
      <ChecklistSearch checklists={checklists}>
        {(filtered) => (
          <nav className={styles.list} aria-label="Checklists">
            {filtered.length === 0 && !isPending && (
              <p className={styles.empty}>No checklists yet.</p>
            )}
            {filtered.map((g) => (
              <ChecklistItem
                key={g.id}
                group={g}
                isActive={g.id === activeId}
                onSelect={select}
                onDelete={remove}
              />
            ))}
          </nav>
        )}
      </ChecklistSearch>
      <AddForm onAdd={add} disabled={isPending} />
    </>
  );
}
