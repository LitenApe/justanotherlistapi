import { memo, useRef, useCallback, type FormEvent } from "react";
import { Link, useParams } from "react-router";

import type { ItemGroup } from "@shared/types";
import { RenderCount } from "@shared/components";
import { ChecklistSearch } from "../checklist-search";
import { preloadDetail } from "../checklist-detail";
import { routes } from "@shared/routes";
import { useChecklists } from "./hooks";
import styles from "./ChecklistList.module.css";

// ─── Model ────────────────────────────────────────────────────────────────────

interface ChecklistListModel {
  checklists: ItemGroup[];
  isPending: boolean;
  activeId: string | undefined;
  add: (name: string) => void;
  remove: (id: string) => void;
}

function useChecklistListModel(
  onCreated: (newId: string) => void,
): ChecklistListModel {
  const { groupId } = useParams();
  const { checklists, isPending, add, remove } = useChecklists();

  const handleAdd = useCallback(
    async (name: string) => {
      const created = await add(name);
      if (created) {
        onCreated(created.id);
      }
    },
    [add, onCreated],
  );

  return {
    checklists,
    isPending,
    activeId: groupId,
    add: handleAdd,
    remove,
  };
}

// ─── View ─────────────────────────────────────────────────────────────────────

interface ChecklistItemProps {
  group: ItemGroup;
  isActive: boolean;
  onDelete: (id: string) => void;
}

const ChecklistItem = memo(function ChecklistItem({
  group,
  isActive,
  onDelete,
}: ChecklistItemProps) {
  return (
    <div className={isActive ? styles.itemActive : styles.item}>
      <Link
        to={routes.checklist(group.id)}
        state={{ name: group.name }}
        className={styles.itemLink}
        onMouseEnter={() => preloadDetail(group.id)}
        onFocus={() => preloadDetail(group.id)}
      >
        <span className={styles.itemName}>{group.name}</span>
        <span className={styles.itemCount}> ({group.items.length} items)</span>
      </Link>
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
});

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

interface ChecklistListViewProps {
  checklists: ItemGroup[];
  isPending: boolean;
  activeId: string | undefined;
  add: (name: string) => void;
  remove: (id: string) => void;
}

function ChecklistListView({
  checklists,
  isPending,
  activeId,
  add,
  remove,
}: ChecklistListViewProps) {
  return (
    <div style={{ position: "relative" }}>
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
                onDelete={remove}
              />
            ))}
          </nav>
        )}
      </ChecklistSearch>
      <AddForm onAdd={add} disabled={isPending} />
    </div>
  );
}

// ─── Controller ───────────────────────────────────────────────────────────────

export interface ChecklistListProps {
  onCreated: (newId: string) => void;
}

export function ChecklistList({ onCreated }: ChecklistListProps) {
  const model = useChecklistListModel(onCreated);
  return <ChecklistListView {...model} />;
}
