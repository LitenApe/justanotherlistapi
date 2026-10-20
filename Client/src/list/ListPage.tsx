import {
  useCallback,
  useEffect,
  useLayoutEffect,
  useMemo,
  useReducer,
  useRef,
  useState,
  type SubmitEvent,
} from "react";
import { Link, useLocation, useNavigate, useParams } from "react-router";
import { checklistApi, toItemInput } from "../api/checklists";
import { ApiError, errorMessage, isAbortError } from "../api/http";
import type { Item, ItemGroupSettings, ItemGroupSettingsInput, ItemInput } from "../api/types";
import { useSession } from "../auth/useSession";
import { ConfirmDialog } from "../components/ConfirmDialog";
import { Icon } from "../components/Icon";
import { useToast } from "../components/Toasts";
import { useDocumentTitle } from "../components/useDocumentTitle";
import { isGuid } from "../lib/guid";
import { useGroupEvents } from "../realtime/hooks";
import { EditItemDialog } from "./EditItemDialog";
import { formatDuration } from "./duration";
import { ItemRow, checkboxId } from "./ItemRow";
import {
  autoDeleteTime,
  filterItems,
  initialListState,
  isAutoHidden,
  listReducer,
  nextHideTime,
  type ItemFilter,
  type ListState,
} from "./listReducer";
import { ListSettingsDialog } from "./ListSettingsDialog";
import { MembersPanel } from "./MembersPanel";
import { RenameListDialog } from "./RenameListDialog";
import styles from "./ListPage.module.css";

type DialogState =
  | null
  | { type: "rename" }
  | { type: "settings" }
  | { type: "deleteList" }
  | { type: "deleteItem"; item: Item }
  | { type: "removeMember"; memberId: string };

type FocusIntent = { kind: "item"; id: string } | { kind: "itemsHeading" } | { kind: "members" };

const FILTERS: { value: ItemFilter; label: string }[] = [
  { value: "all", label: "All" },
  { value: "open", label: "To do" },
  { value: "done", label: "Done" },
];

const NO_ITEMS: Item[] = [];

/** Timers longer than about 24.8 days fire at once, so long waits re-check daily. */
const MAX_TIMER_DELAY = 24 * 60 * 60 * 1000;

const dateTimeFormat = new Intl.DateTimeFormat(undefined, {
  dateStyle: "medium",
  timeStyle: "short",
});

/** Remounts the page per list so state never leaks between lists. */
export function ListRoute() {
  const { listId = "" } = useParams();
  return isGuid(listId) ? <ListPage key={listId} listId={listId} /> : <ListNotFound />;
}

function ListPage({ listId }: { listId: string }) {
  const location = useLocation();
  const navigate = useNavigate();
  const userId = useSession()?.userId ?? "";
  const { notify, announce } = useToast();

  const initialName = (location.state as { name?: string } | null)?.name ?? null;
  const [state, dispatch] = useReducer(listReducer, initialName, initialListState);
  const [filter, setFilter] = useState<ItemFilter>("all");
  const [query, setQuery] = useState("");
  const [editingId, setEditingId] = useState<string | null>(null);
  const [dialog, setDialog] = useState<DialogState>(null);
  const [dialogPending, setDialogPending] = useState(false);
  // Only moves forward when a done item reaches its auto-hide time (see below).
  const [now, setNow] = useState(() => Date.now());

  useDocumentTitle(state.name ?? "List");

  const items = state.status === "ready" ? state.items : NO_ITEMS;
  const settings = state.status === "ready" ? state.settings : null;
  const hideAfter = settings?.hideCompletedAfterMinutes ?? null;
  const isHidden = useCallback(
    (item: Item) => isAutoHidden(item, hideAfter, now),
    [hideAfter, now],
  );
  const visibleItems = useMemo(
    () => filterItems(items, filter, query, isHidden),
    [items, filter, query, isHidden],
  );
  const doneCount = useMemo(() => items.filter((item) => item.isComplete).length, [items]);
  const hiddenCount = useMemo(() => items.filter(isHidden).length, [items, isHidden]);
  const deleteAt = useMemo(
    () => (settings ? autoDeleteTime(items, settings) : null),
    [items, settings],
  );
  const editingItem = items.find((item) => item.id === editingId) ?? null;
  const listName = state.name ?? "this list";

  // ─── Data loading ──────────────────────────────────────────────────────────

  const load = useCallback(
    async ({ signal, silent = false }: { signal?: AbortSignal; silent?: boolean } = {}) => {
      try {
        dispatch({ type: "loaded", list: await checklistApi.getList(listId, signal) });
      } catch (error) {
        if (isAbortError(error) || silent) return;
        dispatch({
          type: "failed",
          message: errorMessage(error),
          forbidden: error instanceof ApiError && error.status === 403,
        });
      }
    },
    [listId],
  );

  useEffect(() => {
    const controller = new AbortController();
    void load({ signal: controller.signal });
    return () => {
      controller.abort();
    };
  }, [load]);

  const retry = () => {
    dispatch({ type: "loading" });
    void load();
  };

  // ─── Focus management ──────────────────────────────────────────────────────
  // When the focused item disappears (deleted, filtered out after a change, or
  // auto-hidden), move focus to its neighbour instead of letting it fall back to <body>.

  const latest = useRef({ visibleItems, filter, query, state, hideAfter });
  useLayoutEffect(() => {
    latest.current = { visibleItems, filter, query, state, hideAfter };
  });
  const focusIntent = useRef<FocusIntent | null>(null);
  const itemsHeadingRef = useRef<HTMLHeadingElement>(null);

  /** Pass several IDs when items disappear together, so focus skips all of them. */
  const planFocusAfterRemoval = useCallback(
    (removed: string | readonly string[], force = false) => {
      const ids = typeof removed === "string" ? [removed] : removed;
      const focusedId = ids.find(
        (id) =>
          force ||
          document.getElementById(checkboxId(id))?.closest("li")?.contains(document.activeElement),
      );
      if (focusedId === undefined) return;
      const visible = latest.current.visibleItems;
      const index = visible.findIndex((item) => item.id === focusedId);
      if (index === -1) return;
      const staying = (item: Item) => !ids.includes(item.id);
      const neighbour =
        visible.slice(index + 1).find(staying) ?? visible.slice(0, index).findLast(staying);
      focusIntent.current = neighbour
        ? { kind: "item", id: neighbour.id }
        : { kind: "itemsHeading" };
    },
    [],
  );

  useEffect(() => {
    const intent = focusIntent.current;
    if (!intent) return;
    focusIntent.current = null;
    if (intent.kind === "item") document.getElementById(checkboxId(intent.id))?.focus();
    else if (intent.kind === "itemsHeading") itemsHeadingRef.current?.focus();
    else document.getElementById("members-heading")?.focus();
  });

  /** Whether the item shows under the current filter, search and auto-hide at time `at`. */
  const isVisible = useCallback(
    (item: Item, at = Date.now()) =>
      filterItems([item], latest.current.filter, latest.current.query, (candidate) =>
        isAutoHidden(candidate, latest.current.hideAfter, at),
      ).length > 0,
    [],
  );

  // ─── Auto-hide ─────────────────────────────────────────────────────────────
  // Wake up when the next done item reaches its hide time, rather than ticking.

  useEffect(() => {
    const next = nextHideTime(items, hideAfter, now);
    if (next === null) return;
    const timer = setTimeout(
      () => {
        const at = Date.now();
        const leaving = latest.current.visibleItems.filter((item) => !isVisible(item, at));
        planFocusAfterRemoval(leaving.map((item) => item.id));
        setNow(at);
      },
      Math.min(Math.max(next - Date.now(), 0), MAX_TIMER_DELAY),
    );
    return () => {
      clearTimeout(timer);
    };
  }, [items, hideAfter, now, isVisible, planFocusAfterRemoval]);

  // ─── Items ─────────────────────────────────────────────────────────────────

  const toggle = useCallback(
    (item: Item) => {
      const updated = {
        ...item,
        isComplete: !item.isComplete,
        // The server sets the real time; this keeps auto-hide working until it arrives.
        completedAt: item.isComplete ? null : new Date().toISOString(),
      };
      const at = Date.now();
      if (!isVisible(updated, at)) {
        planFocusAfterRemoval(item.id);
        announce(`Marked “${item.name}” as ${updated.isComplete ? "done" : "to do"}.`);
        setNow(at);
      }
      dispatch({ type: "itemUpserted", item: updated });
      checklistApi.updateItem(listId, item.id, toItemInput(updated)).catch((error: unknown) => {
        dispatch({ type: "itemUpserted", item });
        notify(`Couldn't update “${item.name}”. ${errorMessage(error)}`, "error");
      });
    },
    [listId, announce, notify, isVisible, planFocusAfterRemoval],
  );

  const openEditor = useCallback((item: Item) => {
    setEditingId(item.id);
  }, []);

  async function addItem(name: string): Promise<string | null> {
    try {
      const item = await checklistApi.createItem(listId, {
        name,
        description: null,
        isComplete: false,
      });
      dispatch({ type: "itemUpserted", item });
      const hidden = !isVisible(item);
      announce(`Added “${item.name}”.${hidden ? " It's hidden by the current filter." : ""}`);
      return null;
    } catch (error) {
      return `Couldn't add the item. ${errorMessage(error)}`;
    }
  }

  async function saveItem(item: Item, changes: Pick<ItemInput, "name" | "description">) {
    const current = items.find((existing) => existing.id === item.id) ?? item;
    const updated = { ...current, ...changes };
    try {
      await checklistApi.updateItem(listId, item.id, toItemInput(updated));
    } catch (error) {
      notify(`Couldn't save “${current.name}”. ${errorMessage(error)}`, "error");
      throw error;
    }
    dispatch({ type: "itemUpserted", item: updated });
    setEditingId(null);
    announce(`Saved “${updated.name}”.`);
  }

  async function deleteItem(item: Item) {
    setDialogPending(true);
    try {
      await checklistApi.deleteItem(listId, item.id);
      planFocusAfterRemoval(item.id, true);
      dispatch({ type: "itemRemoved", itemId: item.id });
      setDialog(null);
      announce(`Deleted “${item.name}”.`);
    } catch (error) {
      notify(`Couldn't delete “${item.name}”. ${errorMessage(error)}`, "error");
    } finally {
      setDialogPending(false);
    }
  }

  // ─── List & members ────────────────────────────────────────────────────────

  async function renameList(name: string) {
    try {
      await checklistApi.renameList(listId, name);
    } catch (error) {
      notify(`Couldn't rename the list. ${errorMessage(error)}`, "error");
      throw error;
    }
    dispatch({ type: "renamed", name });
    setDialog(null);
    announce(`Renamed the list to “${name}”.`);
  }

  async function saveSettings(input: ItemGroupSettingsInput) {
    let saved: ItemGroupSettings;
    try {
      saved = await checklistApi.updateSettings(listId, input);
    } catch (error) {
      notify(`Couldn't save the settings. ${errorMessage(error)}`, "error");
      throw error;
    }
    dispatch({ type: "settingsChanged", settings: saved });
    setDialog(null);
    announce("Saved the list settings.");
  }

  async function deleteList() {
    setDialogPending(true);
    try {
      await checklistApi.deleteList(listId);
      notify(`Deleted “${listName}”.`);
      void navigate("/", { replace: true });
    } catch (error) {
      notify(`Couldn't delete the list. ${errorMessage(error)}`, "error");
      setDialogPending(false);
    }
  }

  async function addMember(memberId: string): Promise<string | null> {
    try {
      await checklistApi.addMember(listId, memberId);
      dispatch({ type: "memberAdded", memberId });
      announce("Member added.");
      return null;
    } catch (error) {
      if (error instanceof ApiError && error.status === 409) {
        return "That person is already a member.";
      }
      return `Couldn't add the member. ${errorMessage(error)}`;
    }
  }

  async function removeMember(memberId: string) {
    const leaving = memberId === userId;
    setDialogPending(true);
    try {
      await checklistApi.removeMember(listId, memberId);
    } catch (error) {
      notify(
        error instanceof ApiError && error.status === 409
          ? "A list needs at least one member. Delete the list instead."
          : `Couldn't remove the member. ${errorMessage(error)}`,
        "error",
      );
      setDialogPending(false);
      return;
    }
    if (leaving) {
      notify(`You left “${listName}”.`);
      void navigate("/", { replace: true });
      return;
    }
    focusIntent.current = { kind: "members" };
    dispatch({ type: "memberRemoved", memberId });
    setDialog(null);
    setDialogPending(false);
    announce("Member removed.");
  }

  // ─── Realtime ──────────────────────────────────────────────────────────────

  const leaveRemovedList = (message: string) => {
    notify(message);
    void navigate("/", { replace: true });
  };

  useGroupEvents([listId], {
    itemCreated: (_, item) => {
      dispatch({ type: "itemUpserted", item });
    },
    itemUpdated: (_, item) => {
      if (!isVisible(item)) planFocusAfterRemoval(item.id);
      dispatch({ type: "itemUpserted", item });
    },
    itemDeleted: (_, itemId) => {
      planFocusAfterRemoval(itemId);
      if (itemId === editingId) {
        setEditingId(null);
        notify("The item you were editing was deleted by another member.", "error");
      }
      dispatch({ type: "itemRemoved", itemId });
    },
    groupRenamed: (_, name) => {
      dispatch({ type: "renamed", name });
    },
    groupSettingsChanged: (_, changed) => {
      dispatch({ type: "settingsChanged", settings: changed });
      notify("Another member changed the list settings.");
    },
    groupDeleted: () => {
      // Allow a minute either way for clocks that are slightly apart.
      const automatic = deleteAt !== null && Date.now() >= deleteAt - 60_000;
      leaveRemovedList(
        automatic
          ? `“${listName}” was deleted automatically because every item was done.`
          : `“${listName}” was deleted by another member.`,
      );
    },
    memberAdded: (_, memberId) => {
      dispatch({ type: "memberAdded", memberId });
    },
    memberRemoved: (_, memberId) => {
      if (memberId === userId) leaveRemovedList(`You were removed from “${listName}”.`);
      else dispatch({ type: "memberRemoved", memberId });
    },
    resync: () => void load({ silent: true }),
  });

  // ─── View ──────────────────────────────────────────────────────────────────

  return (
    <div className={styles.page}>
      <Link to="/" className={styles.back}>
        <Icon name="arrowLeft" size={18} />
        All lists
      </Link>

      <div className={styles.header}>
        <h1 tabIndex={-1}>{state.name ?? "Loading list…"}</h1>
        {state.status === "ready" && (
          <div className={styles.headerActions}>
            <button
              type="button"
              className="button button-small"
              onClick={() => {
                setDialog({ type: "rename" });
              }}
            >
              <Icon name="pencil" size={16} />
              Rename
            </button>
            <button
              type="button"
              className="button button-small"
              onClick={() => {
                setDialog({ type: "settings" });
              }}
            >
              <Icon name="sliders" size={16} />
              Settings
            </button>
            <a
              href="#members-heading"
              className={`button button-small ${styles.narrowOnly}`}
              onClick={(event) => {
                event.preventDefault();
                const heading = document.getElementById("members-heading");
                heading?.scrollIntoView();
                heading?.focus();
              }}
            >
              <Icon name="users" size={16} />
              Members ({state.members.length})
            </a>
            <button
              type="button"
              className="button button-small button-quiet button-danger-text"
              onClick={() => {
                setDialog({ type: "deleteList" });
              }}
            >
              <Icon name="trash" size={16} />
              Delete list
            </button>
          </div>
        )}
      </div>

      {state.status === "ready" && (
        <SettingsSummary settings={state.settings} deleteAt={deleteAt} />
      )}

      {state.status === "loading" && <ListSkeleton />}
      {state.status === "error" && <ListError state={state} onRetry={retry} />}

      {state.status === "ready" && (
        <div className={styles.layout}>
          <section aria-labelledby="items-heading" className={styles.itemsSection}>
            <div className={styles.itemsHeader}>
              <h2 id="items-heading" ref={itemsHeadingRef} tabIndex={-1}>
                Items
              </h2>
              <Progress done={doneCount} total={items.length} />
            </div>

            <AddItemForm onAdd={addItem} />

            {items.length > 0 && (
              <div className={styles.toolbar}>
                <div className={styles.search}>
                  <label className="label" htmlFor="item-search">
                    Search
                  </label>
                  <input
                    id="item-search"
                    type="search"
                    className="input"
                    value={query}
                    onChange={(event) => {
                      setQuery(event.target.value);
                    }}
                    autoComplete="off"
                  />
                </div>
                <fieldset className={styles.filter}>
                  <legend className="label">Show</legend>
                  <div className={styles.segments}>
                    {FILTERS.map(({ value, label }) => (
                      <label key={value} className={styles.segment}>
                        <input
                          type="radio"
                          name="item-filter"
                          className="visually-hidden"
                          value={value}
                          checked={filter === value}
                          onChange={() => {
                            setFilter(value);
                          }}
                        />
                        {filter === value && <Icon name="check" size={14} />}
                        {label}
                        <span className={styles.segmentCount}>
                          {value === "all"
                            ? items.length - hiddenCount
                            : value === "done"
                              ? doneCount
                              : items.length - doneCount}
                        </span>
                      </label>
                    ))}
                  </div>
                </fieldset>
              </div>
            )}

            <p role="status" className={`hint ${styles.resultCount}`}>
              {items.length > 0 && (filter !== "all" || query.trim() || hiddenCount > 0)
                ? `Showing ${visibleItems.length} of ${items.length} items`
                : ""}
            </p>

            {hiddenCount > 0 && filter === "all" && (
              <p className={`hint ${styles.hiddenNote}`}>
                {hiddenCount === 1
                  ? "1 done item is hidden."
                  : `${hiddenCount} done items are hidden.`}
                <button
                  type="button"
                  className="button button-small button-quiet"
                  onClick={() => {
                    // This button disappears with the filter change, so move focus to the filter.
                    document
                      .querySelector<HTMLInputElement>('input[name="item-filter"][value="done"]')
                      ?.focus();
                    setFilter("done");
                  }}
                >
                  Show done items
                </button>
              </p>
            )}

            {visibleItems.length > 0 ? (
              <ul className={styles.items} aria-labelledby="items-heading">
                {visibleItems.map((item) => (
                  <ItemRow key={item.id} item={item} onToggle={toggle} onEdit={openEditor} />
                ))}
              </ul>
            ) : (
              <div className={styles.emptyItems}>
                {items.length === 0 ? (
                  <p>This list is empty. Add your first item above.</p>
                ) : (
                  <p>
                    {query.trim()
                      ? `No items match “${query.trim()}”.`
                      : filter === "done"
                        ? "Nothing is done yet."
                        : "Everything is done. Nice work!"}
                  </p>
                )}
              </div>
            )}
          </section>

          <MembersPanel
            members={state.members}
            currentUserId={userId}
            onAdd={addMember}
            onRemove={(memberId) => {
              setDialog({ type: "removeMember", memberId });
            }}
          />
        </div>
      )}

      <EditItemDialog
        item={editingItem}
        onSave={saveItem}
        onDelete={(item) => {
          setEditingId(null);
          setDialog({ type: "deleteItem", item });
        }}
        onClose={() => {
          setEditingId(null);
        }}
      />

      <RenameListDialog
        open={dialog?.type === "rename"}
        currentName={state.name ?? ""}
        onRename={renameList}
        onClose={() => {
          setDialog(null);
        }}
      />

      {state.status === "ready" && (
        <ListSettingsDialog
          open={dialog?.type === "settings"}
          settings={state.settings}
          onSave={saveSettings}
          onClose={() => {
            setDialog(null);
          }}
        />
      )}

      <ConfirmDialog
        open={dialog?.type === "deleteList"}
        title={`Delete “${listName}”?`}
        description={`This permanently deletes the list and its ${items.length} ${
          items.length === 1 ? "item" : "items"
        } for every member.`}
        confirmLabel="Delete list"
        pending={dialogPending}
        onConfirm={() => void deleteList()}
        onCancel={() => {
          setDialog(null);
        }}
      />

      <ConfirmDialog
        open={dialog?.type === "deleteItem"}
        title={dialog?.type === "deleteItem" ? `Delete “${dialog.item.name}”?` : "Delete item?"}
        description="This permanently deletes the item for every member."
        confirmLabel="Delete item"
        pending={dialogPending}
        onConfirm={() => {
          if (dialog?.type === "deleteItem") void deleteItem(dialog.item);
        }}
        onCancel={() => {
          setDialog(null);
        }}
      />

      <ConfirmDialog
        open={dialog?.type === "removeMember"}
        title={
          dialog?.type === "removeMember" && dialog.memberId === userId
            ? `Leave “${listName}”?`
            : "Remove member?"
        }
        description={
          dialog?.type === "removeMember" && dialog.memberId === userId
            ? "You'll lose access until another member adds you again."
            : "They'll lose access to this list until a member adds them again."
        }
        confirmLabel={
          dialog?.type === "removeMember" && dialog.memberId === userId
            ? "Leave list"
            : "Remove member"
        }
        pending={dialogPending}
        onConfirm={() => {
          if (dialog?.type === "removeMember") void removeMember(dialog.memberId);
        }}
        onCancel={() => {
          setDialog(null);
        }}
      />
    </div>
  );
}

function SettingsSummary({
  settings,
  deleteAt,
}: {
  settings: ItemGroupSettings;
  deleteAt: number | null;
}) {
  const { hideCompletedAfterMinutes: hide, autoDeleteAfterMinutes: autoDelete } = settings;
  const rules = [
    hide === null
      ? null
      : hide === 0
        ? "Done items are hidden right away."
        : `Done items are hidden after ${formatDuration(hide)}.`,
    autoDelete === null
      ? null
      : `The list is deleted ${formatDuration(autoDelete)} after every item is done.`,
  ].filter((rule) => rule !== null);

  return (
    <>
      {rules.length > 0 && <p className={`hint ${styles.settingsSummary}`}>{rules.join(" ")}</p>}
      {/* Always rendered, so checking the last item announces the deletion date. */}
      <div role="status" className={styles.autoDeleteStatus}>
        {deleteAt !== null && (
          <p className="notice">
            Every item is done, so this list will be deleted on {dateTimeFormat.format(deleteAt)}.
            Uncheck an item or change the settings to keep it.
          </p>
        )}
      </div>
    </>
  );
}

function Progress({ done, total }: { done: number; total: number }) {
  return (
    <div className={styles.progress}>
      <p className="hint">
        {done} of {total} done
      </p>
      <progress value={done} max={Math.max(total, 1)} aria-hidden="true" />
    </div>
  );
}

function AddItemForm({ onAdd }: { onAdd: (name: string) => Promise<string | null> }) {
  const [name, setName] = useState("");
  const [error, setError] = useState<string | null>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  async function handleSubmit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault();
    const trimmed = name.trim();
    if (!trimmed) {
      setError("Type an item name first.");
      inputRef.current?.focus();
      return;
    }
    // Clear straight away so several items can be added in quick succession.
    setError(null);
    setName("");
    const failure = await onAdd(trimmed);
    if (failure) {
      setName((current) => current || trimmed);
      setError(failure);
    }
  }

  return (
    <form
      className={styles.addItem}
      noValidate
      onSubmit={(event) => {
        void handleSubmit(event);
      }}
    >
      <label className="label" htmlFor="new-item">
        Add an item
      </label>
      <div className="inline-form">
        <input
          ref={inputRef}
          id="new-item"
          className="input"
          value={name}
          onChange={(event) => {
            setName(event.target.value);
          }}
          autoComplete="off"
          required
          aria-invalid={error ? true : undefined}
          aria-describedby={error ? "new-item-error" : undefined}
        />
        <button type="submit" className="button button-primary">
          <Icon name="plus" size={18} />
          Add
        </button>
      </div>
      {error && (
        <p className="field-error" id="new-item-error">
          {error}
        </p>
      )}
    </form>
  );
}

function ListError({
  state,
  onRetry,
}: {
  state: Extract<ListState, { status: "error" }>;
  onRetry: () => void;
}) {
  if (state.forbidden) {
    return (
      <div className={`card ${styles.message}`}>
        <h2>You can't open this list</h2>
        <p className="muted">
          It doesn't exist, or you aren't a member. Ask a member to add you using your user ID.
        </p>
        <Link to="/">Go to your lists</Link>
      </div>
    );
  }
  return (
    <div className="alert" role="alert">
      <Icon name="alert" />
      <div className={styles.errorBody}>
        <p>Couldn't load this list. {state.message}</p>
        <button type="button" className="button button-small" onClick={onRetry}>
          Try again
        </button>
      </div>
    </div>
  );
}

function ListSkeleton() {
  return (
    <>
      <p className="visually-hidden">Loading list…</p>
      <div className={styles.skeleton} aria-hidden="true">
        {Array.from({ length: 6 }, (_, index) => (
          <div key={index} className={`skeleton ${styles.skeletonRow}`} />
        ))}
      </div>
    </>
  );
}

function ListNotFound() {
  useDocumentTitle("List not found");
  return (
    <div className={styles.page}>
      <h1 tabIndex={-1}>List not found</h1>
      <p className="muted">That address doesn't point to a list.</p>
      <Link to="/">Go to your lists</Link>
    </div>
  );
}
