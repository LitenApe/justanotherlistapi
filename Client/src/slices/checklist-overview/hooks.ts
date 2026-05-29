import type { Item, ItemGroup } from "@shared/types";
import {
  startTransition,
  use,
  useCallback,
  useEffect,
  useOptimistic,
  useSyncExternalStore,
} from "react";

import { activityLog } from "@shared/api";
import { createChecklist, deleteChecklist, fetchChecklists } from "./api";
import { signalRStore } from "@shared/api/signalrStore";
import { useTrackedTransition } from "@shared/hooks";

// ─── Cache ────────────────────────────────────────────────────────────────────

let overviewPromise: Promise<ItemGroup[]> | null = null;
let overviewVersion = 0;
const overviewListeners = new Set<() => void>();

function notifyOverview(): void {
  overviewListeners.forEach((l) => l());
}

function getOverviewPromise(): Promise<ItemGroup[]> {
  if (!overviewPromise) {
    overviewPromise = fetchChecklists();
  }
  return overviewPromise;
}

export function invalidateOverview(): void {
  overviewPromise = null;
}

/**
 * Mutate the cached overview data without refetching.
 * Creates a pre-resolved promise tagged for React's use() to read synchronously.
 */
function updateOverviewCache(
  updater: (groups: ItemGroup[]) => ItemGroup[],
): void {
  const existing = overviewPromise as
    | (Promise<ItemGroup[]> & { status?: string; value?: ItemGroup[] })
    | undefined;
  if (!existing || existing.status !== "fulfilled" || !existing.value) return;

  const updated = updater(existing.value);
  const resolved = Promise.resolve(updated) as Promise<ItemGroup[]> & {
    status: string;
    value: ItemGroup[];
  };
  resolved.status = "fulfilled";
  resolved.value = updated;
  overviewPromise = resolved;
  overviewVersion++;
  notifyOverview();
}

/**
 * Update a single item within the overview cache.
 */
export function updateOverviewItem(
  groupId: string,
  updater: (items: Item[]) => Item[],
): void {
  updateOverviewCache((groups) =>
    groups.map((g) =>
      g.id === groupId ? { ...g, items: updater(g.items) } : g,
    ),
  );
}

// ─── Hook ─────────────────────────────────────────────────────────────────────

type OverviewAction =
  | { type: "remove-group"; id: string }
  | { type: "toggle-item"; groupId: string; itemId: string };

function overviewReducer(
  groups: ItemGroup[],
  action: OverviewAction,
): ItemGroup[] {
  switch (action.type) {
    case "remove-group":
      return groups.filter((g) => g.id !== action.id);
    case "toggle-item":
      return groups.map((g) =>
        g.id === action.groupId
          ? {
              ...g,
              items: g.items.map((i) =>
                i.id === action.itemId
                  ? { ...i, isComplete: !i.isComplete }
                  : i,
              ),
            }
          : g,
      );
  }
}

export function useOverview() {
  useSyncExternalStore(
    (cb) => {
      overviewListeners.add(cb);
      return () => {
        overviewListeners.delete(cb);
      };
    },
    () => overviewVersion,
  );

  const checklists = use(getOverviewPromise());
  const [optimistic, addOptimistic] = useOptimistic(
    checklists,
    overviewReducer,
  );
  const [isPending, startTransitionTracked] =
    useTrackedTransition("overview/mutation");

  useRealtimeSync(checklists);

  const refresh = useCallback(() => {
    startTransitionTracked(async () => {
      invalidateOverview();
      await getOverviewPromise();
    });
  }, [startTransitionTracked]);

  const add = useCallback(
    (name: string): Promise<ItemGroup> => {
      return new Promise((resolve, reject) => {
        startTransitionTracked(async () => {
          try {
            const created = await createChecklist(name);
            invalidateOverview();
            await getOverviewPromise();
            resolve(created);
          } catch (e) {
            reject(e);
          }
        });
      });
    },
    [startTransitionTracked],
  );

  const removeGroup = useCallback(
    (id: string) => {
      startTransitionTracked(async () => {
        addOptimistic({ type: "remove-group", id });
        await deleteChecklist(id);
        invalidateOverview();
        await getOverviewPromise();
      });
    },
    [startTransitionTracked, addOptimistic],
  );

  const toggleItem = useCallback(
    (groupId: string, item: Item) => {
      addOptimistic({ type: "toggle-item", groupId, itemId: item.id });
    },
    [addOptimistic],
  );

  return {
    checklists: optimistic,
    isPending,
    refresh,
    add,
    removeGroup,
    toggleItem,
  };
}

// ─── Real-time sync ───────────────────────────────────────────────────────────

function useRealtimeSync(checklists: ItemGroup[]): void {
  const groupIds = checklists.map((g) => g.id);

  useEffect(() => {
    for (const id of groupIds) {
      signalRStore.joinGroup(id);
    }

    function logSignalREvent(eventName: string): void {
      activityLog.append({
        id: crypto.randomUUID(),
        operationId: eventName,
        event: "complete",
        source: "signalr",
        timestamp: Date.now(),
      });
    }

    const handleItemCreated = (groupId: string, item: Item) => {
      logSignalREvent("ItemCreated");
      startTransition(() => {
        updateOverviewItem(groupId, (items) => items.concat(item));
      });
    };

    const handleItemUpdated = (groupId: string, item: Item) => {
      logSignalREvent("ItemUpdated");
      startTransition(() => {
        updateOverviewItem(groupId, (items) =>
          items.map((i) => (i.id === item.id ? item : i)),
        );
      });
    };

    const handleItemDeleted = (groupId: string, itemId: string) => {
      logSignalREvent("ItemDeleted");
      startTransition(() => {
        updateOverviewItem(groupId, (items) =>
          items.filter((i) => i.id !== itemId),
        );
      });
    };

    const handleGroupRenamed = (groupId: string, name: string) => {
      logSignalREvent("GroupRenamed");
      startTransition(() => {
        updateOverviewCache((groups) =>
          groups.map((g) => (g.id === groupId ? { ...g, name } : g)),
        );
      });
    };

    const handleGroupDeleted = (groupId: string) => {
      logSignalREvent("GroupDeleted");
      startTransition(() => {
        updateOverviewCache((groups) => groups.filter((g) => g.id !== groupId));
      });
    };

    signalRStore.on("ItemCreated", handleItemCreated as never);
    signalRStore.on("ItemUpdated", handleItemUpdated as never);
    signalRStore.on("ItemDeleted", handleItemDeleted as never);
    signalRStore.on("GroupRenamed", handleGroupRenamed as never);
    signalRStore.on("GroupDeleted", handleGroupDeleted as never);

    const unsubReconnect = signalRStore.onReconnected(() => {
      for (const id of groupIds) {
        signalRStore.joinGroup(id);
      }
      invalidateOverview();
      getOverviewPromise();
      overviewVersion++;
      notifyOverview();
    });

    return () => {
      for (const id of groupIds) {
        signalRStore.leaveGroup(id);
      }
      signalRStore.off("ItemCreated", handleItemCreated as never);
      signalRStore.off("ItemUpdated", handleItemUpdated as never);
      signalRStore.off("ItemDeleted", handleItemDeleted as never);
      signalRStore.off("GroupRenamed", handleGroupRenamed as never);
      signalRStore.off("GroupDeleted", handleGroupDeleted as never);
      unsubReconnect();
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [groupIds.join(",")]);
}
