import { useEffect, useId, useMemo, useRef, useState } from "react";
import { Link, redirect, useFetcher, useRevalidator } from "react-router";
import { checklistApi } from "../api/checklists";
import { errorMessage } from "../api/http";
import { loadRouteData, routeErrorMessage } from "../api/routeData";
import type { Item, ItemGroup } from "../api/types";
import { useSession } from "../auth/useSession";
import { DocumentTitle } from "../components/DocumentTitle";
import { Icon } from "../components/Icon";
import { formText } from "../lib/formText";
import { useLoaderState } from "../lib/useLoaderState";
import { useGroupEvents } from "../realtime/hooks";
import type { Route } from "./+types/ListsPage";
import styles from "./ListsPage.module.css";

const PREVIEW_COUNT = 3;
const FILTER_THRESHOLD = 6;

const byName = (a: ItemGroup, b: ItemGroup) =>
  a.name.localeCompare(b.name, undefined, { sensitivity: "base", numeric: true });

export async function clientLoader({ request }: Route.ClientLoaderArgs) {
  const lists = await loadRouteData(request, (signal) => checklistApi.getLists(signal));
  return { lists: lists.toSorted(byName) };
}

export async function clientAction({ request }: Route.ClientActionArgs) {
  const name = formText(await request.formData(), "name").trim();
  if (!name) return { error: "Give your list a name." };
  try {
    const list = await checklistApi.createList(name);
    return redirect(`/lists/${list.id}`);
  } catch (error) {
    return { error: errorMessage(error) };
  }
}

export default function ListsPage({ loaderData }: Route.ComponentProps) {
  const [query, setQuery] = useState("");
  const lists = useLiveLists(loaderData.lists);

  const visible = useMemo(() => {
    const needle = query.trim().toLowerCase();
    return needle ? lists.filter((list) => list.name.toLowerCase().includes(needle)) : lists;
  }, [lists, query]);

  return (
    <div className={styles.page}>
      <DocumentTitle title="Your lists" />
      <div className={styles.intro}>
        <h1 tabIndex={-1}>Your lists</h1>
        <CreateListForm />
      </div>

      <section aria-labelledby="lists-heading">
        <h2 id="lists-heading" className="visually-hidden">
          All lists
        </h2>

        {lists.length === 0 ? (
          <div className={`card ${styles.empty}`}>
            <h3>No lists yet</h3>
            <p className="muted">
              Create your first list above. Lists other people share with you show up here too.
            </p>
          </div>
        ) : (
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

export function ErrorBoundary({ error }: Route.ErrorBoundaryProps) {
  const revalidator = useRevalidator();
  const retrying = revalidator.state === "loading";

  return (
    <div className={styles.page}>
      <DocumentTitle title="Your lists" />
      <h1 tabIndex={-1}>Your lists</h1>
      <div className="alert" role="alert">
        <Icon name="alert" />
        <div className={styles.errorBody}>
          <p>Couldn't load your lists. {routeErrorMessage(error)}</p>
          <button
            type="button"
            className="button button-small"
            aria-disabled={retrying}
            onClick={() => {
              if (!retrying) void revalidator.revalidate();
            }}
          >
            {retrying ? "Trying again…" : "Try again"}
          </button>
        </div>
      </div>
    </div>
  );
}

function ListCard({ list }: { list: ItemGroup }) {
  const remaining = list.items.length;
  const previewId = useId();

  return (
    <li className={`card ${styles.listCard}`}>
      <h3 className={styles.cardTitle}>
        <Link to={`/lists/${list.id}`} className={styles.cardLink} aria-describedby={previewId}>
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

/**
 * Posts to this route's clientAction, which redirects to the new list. A fetcher
 * keeps the address bar and history untouched when validation fails.
 */
function CreateListForm() {
  const fetcher = useFetcher<typeof clientAction>();
  const pending = fetcher.state !== "idle";
  const error = fetcher.data?.error ?? null;
  const inputRef = useRef<HTMLInputElement>(null);

  // Runs for every failed submission, even when the message is the same.
  useEffect(() => {
    if (fetcher.data?.error) inputRef.current?.focus();
  }, [fetcher.data]);

  return (
    <fetcher.Form
      method="post"
      className={styles.createForm}
      noValidate
      onSubmit={(event) => {
        if (pending) event.preventDefault();
      }}
    >
      <label className="label" htmlFor="new-list-name">
        New list
      </label>
      <div className="inline-form">
        <input
          ref={inputRef}
          id="new-list-name"
          name="name"
          className="input"
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
    </fetcher.Form>
  );
}

/** The lists from the loader, kept current with realtime events. */
function useLiveLists(loaded: ItemGroup[]) {
  const session = useSession();
  const { revalidate } = useRevalidator();
  const [lists, setLists] = useLoaderState(loaded);

  // Lists shared with you while you were away appear when you come back to the tab.
  useEffect(() => {
    const onVisible = () => {
      if (document.visibilityState === "visible") void revalidate();
    };
    document.addEventListener("visibilitychange", onVisible);
    return () => {
      document.removeEventListener("visibilitychange", onVisible);
    };
  }, [revalidate]);

  const mapItems = (groupId: string, fn: (items: Item[]) => Item[]) => {
    setLists((current) =>
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
    setLists((current) => current.filter((list) => list.id !== groupId));
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
        setLists((current) =>
          current.map((list) => (list.id === groupId ? { ...list, name } : list)).toSorted(byName),
        );
      },
      groupDeleted: removeList,
      memberRemoved: (groupId, memberId) => {
        if (memberId === session?.userId) removeList(groupId);
      },
      resync: () => void revalidate(),
    },
  );

  return lists;
}
