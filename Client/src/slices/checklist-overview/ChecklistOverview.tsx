import { type FormEvent, memo, useCallback, useRef, useState } from "react";
import { Link } from "react-router";

import type { Item, ItemGroup } from "@shared/types";
import { RenderCount } from "@shared/components";
import { toggleItem } from "@slices/items";
import { routes } from "@shared/routes";
import { useOverview, updateOverviewItem } from "./hooks";
import { useTrackedTransition } from "@shared/hooks";
import styles from "./ChecklistOverview.module.css";

// ─── Model ────────────────────────────────────────────────────────────────────

interface OverviewModel {
  checklists: ItemGroup[];
  isPending: boolean;
  add: (name: string) => void;
  removeGroup: (id: string) => void;
  toggleItemAction: (groupId: string, item: Item) => void;
}

function useOverviewModel(): OverviewModel {
  const {
    checklists,
    isPending,
    add,
    removeGroup,
    toggleItem: optimisticToggle,
  } = useOverview();

  const [, startToggleTransition] = useTrackedTransition("overview/toggle");

  const toggleItemAction = useCallback(
    (groupId: string, item: Item) => {
      startToggleTransition(async () => {
        optimisticToggle(groupId, item);
        await toggleItem(item);
        updateOverviewItem(groupId, (items) =>
          items.map((i) =>
            i.id === item.id ? { ...i, isComplete: !item.isComplete } : i,
          ),
        );
      });
    },
    [startToggleTransition, optimisticToggle],
  );

  return { checklists, isPending, add, removeGroup, toggleItemAction };
}

// ─── View ─────────────────────────────────────────────────────────────────────

interface AccordionItemProps {
  item: Item;
  groupId: string;
  onToggle: (groupId: string, item: Item) => void;
}

const AccordionItem = memo(function AccordionItem({
  item,
  groupId,
  onToggle,
}: AccordionItemProps) {
  return (
    <div
      className={`${styles.item} ${item.isComplete ? styles.itemComplete : ""}`}
    >
      <input
        type="checkbox"
        className={styles.checkbox}
        checked={item.isComplete}
        onChange={() => onToggle(groupId, item)}
        aria-label={`Mark ${item.name} as ${item.isComplete ? "incomplete" : "complete"}`}
      />
      <span
        className={item.isComplete ? styles.itemNameComplete : styles.itemName}
      >
        {item.name}
      </span>
    </div>
  );
});

interface AccordionPanelProps {
  group: ItemGroup;
  onToggleItem: (groupId: string, item: Item) => void;
  onDelete: (id: string) => void;
}

function AccordionPanel({
  group,
  onToggleItem,
  onDelete,
}: AccordionPanelProps) {
  const [expanded, setExpanded] = useState(false);

  return (
    <div className={styles.panel}>
      <div className={styles.panelHeader}>
        <button
          type="button"
          className={styles.expandBtn}
          onClick={() => setExpanded((e) => !e)}
          aria-expanded={expanded}
          aria-controls={`panel-${group.id}`}
        >
          <span className={expanded ? styles.chevronDown : styles.chevronRight}>
            ▶
          </span>
          <span className={styles.groupName}>{group.name}</span>
          <span className={styles.itemCount}>
            ({group.items.filter((i) => !i.isComplete).length} remaining)
          </span>
        </button>
        <div className={styles.panelActions}>
          <Link
            to={routes.checklist(group.id)}
            className={styles.detailLink}
            aria-label={`View details for ${group.name}`}
          >
            View →
          </Link>
          <button
            type="button"
            className={styles.deleteBtn}
            onClick={() => onDelete(group.id)}
            aria-label={`Delete ${group.name}`}
          >
            ✕
          </button>
        </div>
      </div>
      {expanded && (
        <div id={`panel-${group.id}`} className={styles.panelContent}>
          {group.items.length === 0 ? (
            <p className={styles.empty}>All done!</p>
          ) : (
            group.items.map((item) => (
              <AccordionItem
                key={item.id}
                item={item}
                groupId={group.id}
                onToggle={onToggleItem}
              />
            ))
          )}
        </div>
      )}
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

interface OverviewViewProps {
  checklists: ItemGroup[];
  isPending: boolean;
  add: (name: string) => void;
  removeGroup: (id: string) => void;
  toggleItemAction: (groupId: string, item: Item) => void;
}

function OverviewView({
  checklists,
  isPending,
  add,
  removeGroup,
  toggleItemAction,
}: OverviewViewProps) {
  return (
    <div className={styles.container}>
      <RenderCount label="ChecklistOverview" />
      <div className={styles.header}>
        <h1 className={styles.title}>My Checklists</h1>
      </div>

      {checklists.length === 0 && !isPending && (
        <p className={styles.emptyState}>
          No checklists yet. Create your first one below!
        </p>
      )}

      <div className={styles.list} aria-busy={isPending}>
        {checklists.map((group) => (
          <AccordionPanel
            key={group.id}
            group={group}
            onToggleItem={toggleItemAction}
            onDelete={removeGroup}
          />
        ))}
      </div>

      <AddForm onAdd={add} disabled={isPending} />
    </div>
  );
}

// ─── Controller ───────────────────────────────────────────────────────────────

export function ChecklistOverview() {
  const model = useOverviewModel();
  return <OverviewView {...model} />;
}
