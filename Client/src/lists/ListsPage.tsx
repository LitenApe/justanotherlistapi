import { useCallback, useEffect, useId, useMemo, useRef, useState, type SubmitEvent } from "react";
import { Link, useNavigate } from "react-router";
import { checklistApi } from "../api/checklists";
import { errorMessage, isAbortError } from "../api/http";
import type { Item, ItemGroup } from "../api/types";
import { useSession } from "../auth/useSession";
import { Icon } from "../components/Icon";
import { useToast } from "../components/Toasts";
import { useDocumentTitle } from "../components/useDocumentTitle";
import { useGroupEvents } from "../realtime/hooks";
import styles from "./ListsPage.module.css";

type LoadState =
  | { status: "loading" }
  | { status: "error"; message: string }
  | { status: "ready"; lists: ItemGroup[] };

const PREVIEW_COUNT = 3;
const FILTER_THRESHOLD = 6;

export function ListsPage() {
  useDocumentTitle("Your lists");
  const [state, setState] = useState<LoadState>({ status: "loading" });
  const [query, setQuery] = useState("");
  const { lists, reload, update } = useLists(state, setState);

  const visible = useMemo(() => {
    const needle = query.trim().toLowerCase();
    return needle ? lists.filter((list) => list.name.toLowerCase().includes(needle)) : lists;
  }, [lists, query]);

  return (
    <div className={styles.page}>
      <div className={styles.intro}>
        <h1 tabIndex={-1}>Your lists</h1>
        <CreateListForm onCreated={update.added} />
      </div>

      <section aria-labelledby="lists-heading" aria-busy={state.status === "loading"}>
        <h2 id="lists-heading" className="visually-hidden">
          All lists
        </h2>

        {state.status === "loading" && <ListsSkeleton />}

        {state.status === "error" && (
          <div className="alert" role="alert">
            <Icon name="alert" />
            <div className={styles.errorBody}>
              <p>Couldn't load your lists. {state.message}</p>
              <button type="button" className="button button-small" onClick={reload}>
                Try again
              </button>
            </div>
          </div>
        )}

        {state.status === "ready" && lists.length === 0 && (
          <div className={`card ${styles.empty}`}>
            <h3>No lists yet</h3>
            <p className="muted">
              Create your first list above. Lists other people share with you show up here too.
            </p>
          </div>
        )}

        {state.status === "ready" && lists.length > 0 && (
          <>
            {lists.length > FILTER_THRESHOLD && (
              <div className={styles.filter}>
                <label className="label" htmlFor="list-filter">
                  Find a list
                </label>
                <input
                  id="list-filter"
                  type="search"
                  className="input"
                  value={query}
                  onChange={(event) => {
                    setQuery(event.target.value);
                  }}
                  aria-describedby="list-filter-count"
                />
                <p id="list-filter-count" className="hint" role="status">
                  {query.trim()
                    ? `Showing ${visible.length} of ${lists.length} lists`
                    : `${lists.length} lists`}
                </p>
              </div>
            )}
            {visible.length > 0 ? (
              <ul className={styles.grid}>
                {visible.map((list) => (
                  <ListCard key={list.id} list={list} />
                ))}
              </ul>
            ) : (
              <p className="muted">No lists match “{query.trim()}”.</p>
            )}
          </>
        )}
      </section>
    </div>
  );
}

function ListCard({ list }: { list: ItemGroup }) {
  const remaining = list.items.length;
  const previewId = useId();

  return (
    <li className={`card ${styles.listCard}`}>
      <h3 className={styles.cardTitle}>
        <Link
          to={`/lists/${list.id}`}
          state={{ name: list.name }}
          className={styles.cardLink}
          aria-describedby={previewId}
        >
          {list.name}
        </Link>
      </h3>
      <div id={previewId} className={styles.cardBody}>
        <p className={styles.remaining}>
          {remaining === 0 ? (
            <>
              <Icon name="check" size={16} className={styles.doneIcon} /> All done
            </>
          ) : (
            `${remaining} ${remaining === 1 ? "item" : "items"} to do`
          )}
        </p>
        {remaining > 0 && (
          <ul className={styles.preview}>
            {list.items.slice(0, PREVIEW_COUNT).map((item) => (
              <li key={item.id}>{item.name}</li>
            ))}
            {remaining > PREVIEW_COUNT && (
              <li className="muted">and {remaining - PREVIEW_COUNT} more</li>
            )}
          </ul>
        )}
      </div>
    </li>
  );
}

function CreateListForm({ onCreated }: { onCreated: (list: ItemGroup) => void }) {
  const navigate = useNavigate();
  const { notify } = useToast();
  const [name, setName] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);

  async function handleSubmit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault();
    if (pending) return;
    const trimmed = name.trim();
    if (!trimmed) {
      setError("Give your list a name.");
      inputRef.current?.focus();
      return;
    }

    setError(null);
    setPending(true);
    try {
      const list = await checklistApi.createList(trimmed);
      onCreated(list);
      notify(`Created “${list.name}”.`);
      void navigate(`/lists/${list.id}`, { state: { name: list.name } });
    } catch (err) {
      setError(errorMessage(err));
      setPending(false);
    }
  }

  return (
    <form
      className={styles.createForm}
      noValidate
      onSubmit={(event) => {
        void handleSubmit(event);
      }}
    >
      <label className="label" htmlFor="new-list-name">
        New list
      </label>
      <div className="inline-form">
        <input
          ref={inputRef}
          id="new-list-name"
          className="input"
          value={name}
          onChange={(event) => {
            setName(event.target.value);
          }}
          placeholder="e.g. Groceries"
          autoComplete="off"
          required
          aria-invalid={error ? true : undefined}
          aria-describedby={error ? "new-list-error" : undefined}
        />
        <button type="submit" className="button button-primary" aria-disabled={pending}>
          <Icon name="plus" size={18} />
          {pending ? "Creating…" : "Create list"}
        </button>
      </div>
      {error && (
        <p className="field-error" id="new-list-error">
          {error}
        </p>
      )}
    </form>
  );
}

function ListsSkeleton() {
  return (
    <>
      <p className="visually-hidden">Loading your lists…</p>
      <div className={styles.grid} aria-hidden="true">
        {Array.from({ length: 6 }, (_, index) => (
          <div key={index} className={`skeleton ${styles.skeletonCard}`} />
        ))}
      </div>
    </>
  );
}

const byName = (a: ItemGroup, b: ItemGroup) =>
  a.name.localeCompare(b.name, undefined, { sensitivity: "base", numeric: true });

/** Loads the overview and keeps it current with realtime events. */
function useLists(state: LoadState, setState: (update: (prev: LoadState) => LoadState) => void) {
  const session = useSession();
  const [reloadKey, setReloadKey] = useState(0);
  const lists = useMemo(() => (state.status === "ready" ? state.lists : []), [state]);

  const reload = useCallback(() => {
    setState(() => ({ status: "loading" }));
    setReloadKey((key) => key + 1);
  }, [setState]);

  const refresh = useCallback(
    async (signal?: AbortSignal) => {
      try {
        const data = await checklistApi.getLists(signal);
        setState(() => ({ status: "ready", lists: data.toSorted(byName) }));
      } catch (error) {
        if (isAbortError(error)) return;
        // Keep showing what we have if a background refresh fails.
        setState((prev) =>
          prev.status === "ready" ? prev : { status: "error", message: errorMessage(error) },
        );
      }
    },
    [setState],
  );

  useEffect(() => {
    const controller = new AbortController();
    void refresh(controller.signal);
    return () => {
      controller.abort();
    };
  }, [refresh, reloadKey]);

  // Lists shared with you while you were away appear when you come back to the tab.
  useEffect(() => {
    const onVisible = () => {
      if (document.visibilityState === "visible") void refresh();
    };
    document.addEventListener("visibilitychange", onVisible);
    return () => {
      document.removeEventListener("visibilitychange", onVisible);
    };
  }, [refresh]);

  const mapLists = useCallback(
    (fn: (lists: ItemGroup[]) => ItemGroup[]) => {
      setState((prev) => (prev.status === "ready" ? { ...prev, lists: fn(prev.lists) } : prev));
    },
    [setState],
  );

  const mapItems = (groupId: string, fn: (items: Item[]) => Item[]) => {
    mapLists((current) =>
      current.map((list) => (list.id === groupId ? { ...list, items: fn(list.items) } : list)),
    );
  };

  // The overview shows open items only, mirroring GET /api/list.
  const upsertOpenItem = (groupId: string, item: Item) => {
    mapItems(groupId, (items) => {
      const index = items.findIndex((existing) => existing.id === item.id);
      if (item.isComplete) return index === -1 ? items : items.toSpliced(index, 1);
      return index === -1 ? [...items, item] : items.with(index, item);
    });
  };

  const removeList = (groupId: string) => {
    mapLists((current) => current.filter((list) => list.id !== groupId));
  };

  useGroupEvents(
    lists.map((list) => list.id),
    {
      itemCreated: upsertOpenItem,
      itemUpdated: upsertOpenItem,
      itemDeleted: (groupId, itemId) => {
        mapItems(groupId, (items) => items.filter((item) => item.id !== itemId));
      },
      groupRenamed: (groupId, name) => {
        mapLists((current) =>
          current.map((list) => (list.id === groupId ? { ...list, name } : list)).toSorted(byName),
        );
      },
      groupDeleted: removeList,
      memberRemoved: (groupId, memberId) => {
        if (memberId === session?.userId) removeList(groupId);
      },
      resync: () => void refresh(),
    },
  );

  const update = {
    added: (list: ItemGroup) => {
      mapLists((current) => [...current, list].toSorted(byName));
    },
  };

  return { lists, reload, update };
}
