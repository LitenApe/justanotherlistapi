import {
  HttpTransportType,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
  type HubConnection,
} from "@microsoft/signalr";
import type { Item } from "../api/types";

export type ConnectionStatus = "disconnected" | "connecting" | "connected" | "reconnecting";

/** Handlers for server-pushed checklist events. Each receives the list (group) ID first. */
export interface GroupEventHandlers {
  itemCreated?: (groupId: string, item: Item) => void;
  itemUpdated?: (groupId: string, item: Item) => void;
  itemDeleted?: (groupId: string, itemId: string) => void;
  memberAdded?: (groupId: string, memberId: string) => void;
  memberRemoved?: (groupId: string, memberId: string) => void;
  groupRenamed?: (groupId: string, name: string) => void;
  groupDeleted?: (groupId: string) => void;
  /** Called after the connection recovers, when events may have been missed. */
  resync?: (groupId: string) => void;
}

interface Subscription {
  groupId: string;
  handlers: GroupEventHandlers;
}

const HUB_URL = "/hubs/checklist";
const MAX_RETRY_DELAY = 30_000;

const retryDelay = (attempt: number) => Math.min(MAX_RETRY_DELAY, 1000 * 2 ** attempt);

function buildConnection(accessTokenFactory: () => string): HubConnection {
  return new HubConnectionBuilder()
    .withUrl(HUB_URL, {
      accessTokenFactory,
      // Connect straight over WebSockets so the dev proxy doesn't split negotiate and connect.
      skipNegotiation: true,
      transport: HttpTransportType.WebSockets,
    })
    .withAutomaticReconnect({
      nextRetryDelayInMilliseconds: (context) => retryDelay(context.previousRetryCount),
    })
    .configureLogging(LogLevel.Warning)
    .build();
}

/**
 * Owns the single SignalR connection. Pages subscribe per list; the hub joins a
 * SignalR group on the first subscription for that list and leaves after the last.
 */
export function createChecklistHub(build: typeof buildConnection = buildConnection) {
  let connection: HubConnection | null = null;
  let status: ConnectionStatus = "disconnected";
  let retryTimer: ReturnType<typeof setTimeout> | undefined;
  const statusListeners = new Set<() => void>();
  const subscriptions = new Set<Subscription>();
  const groupRefs = new Map<string, number>();

  function setStatus(next: ConnectionStatus): void {
    if (next === status) return;
    status = next;
    for (const listener of statusListeners) listener();
  }

  function emit(groupId: string, invoke: (handlers: GroupEventHandlers) => void): void {
    for (const subscription of subscriptions) {
      if (subscription.groupId === groupId) invoke(subscription.handlers);
    }
  }

  function isConnected(conn: HubConnection | null): conn is HubConnection {
    return conn?.state === HubConnectionState.Connected;
  }

  async function invokeGroup(method: "JoinGroup" | "LeaveGroup", groupId: string): Promise<void> {
    if (!isConnected(connection)) return;
    try {
      await connection.invoke(method, groupId);
    } catch {
      // JoinGroup rejects non-members; the REST call surfaces that as 403.
    }
  }

  async function joinAll(): Promise<void> {
    await Promise.all([...groupRefs.keys()].map((groupId) => invokeGroup("JoinGroup", groupId)));
  }

  function resyncAll(): void {
    for (const { groupId, handlers } of subscriptions) handlers.resync?.(groupId);
  }

  async function connect(conn: HubConnection, attempt: number): Promise<void> {
    if (connection !== conn) return;
    setStatus(attempt === 0 ? "connecting" : "reconnecting");
    try {
      await conn.start();
    } catch {
      if (connection !== conn) return;
      setStatus("reconnecting");
      retryTimer = setTimeout(() => void connect(conn, attempt + 1), retryDelay(attempt));
      return;
    }
    if (connection !== conn) {
      await conn.stop();
      return;
    }
    setStatus("connected");
    await joinAll();
    if (attempt > 0) resyncAll();
  }

  function start(accessTokenFactory: () => string): void {
    if (connection) return;
    const conn = build(accessTokenFactory);
    connection = conn;

    conn.on("ItemCreated", (groupId: string, item: Item) => {
      emit(groupId, (h) => h.itemCreated?.(groupId, item));
    });
    conn.on("ItemUpdated", (groupId: string, item: Item) => {
      emit(groupId, (h) => h.itemUpdated?.(groupId, item));
    });
    conn.on("ItemDeleted", (groupId: string, itemId: string) => {
      emit(groupId, (h) => h.itemDeleted?.(groupId, itemId));
    });
    conn.on("MemberAdded", (groupId: string, memberId: string) => {
      emit(groupId, (h) => h.memberAdded?.(groupId, memberId));
    });
    conn.on("MemberRemoved", (groupId: string, memberId: string) => {
      emit(groupId, (h) => h.memberRemoved?.(groupId, memberId));
    });
    conn.on("GroupRenamed", (groupId: string, name: string) => {
      emit(groupId, (h) => h.groupRenamed?.(groupId, name));
    });
    conn.on("GroupDeleted", (groupId: string) => {
      emit(groupId, (h) => h.groupDeleted?.(groupId));
    });

    conn.onreconnecting(() => {
      setStatus("reconnecting");
    });
    conn.onreconnected(() => {
      setStatus("connected");
      void joinAll().then(resyncAll);
    });
    conn.onclose(() => {
      // Only reached if automatic reconnect gives up; keep trying.
      if (connection === conn) void connect(conn, 1);
    });

    void connect(conn, 0);
  }

  function stop(): void {
    clearTimeout(retryTimer);
    const conn = connection;
    connection = null;
    setStatus("disconnected");
    void conn?.stop();
  }

  function subscribe(groupId: string, handlers: GroupEventHandlers): () => void {
    const subscription = { groupId, handlers };
    subscriptions.add(subscription);
    const refs = groupRefs.get(groupId) ?? 0;
    groupRefs.set(groupId, refs + 1);
    if (refs === 0) void invokeGroup("JoinGroup", groupId);

    return () => {
      subscriptions.delete(subscription);
      const remaining = (groupRefs.get(groupId) ?? 1) - 1;
      if (remaining > 0) {
        groupRefs.set(groupId, remaining);
      } else {
        groupRefs.delete(groupId);
        void invokeGroup("LeaveGroup", groupId);
      }
    };
  }

  return {
    start,
    stop,
    subscribe,
    getStatus: () => status,
    /** Sent with mutations so the server doesn't echo our own changes back to us. */
    getConnectionId: () => (isConnected(connection) ? connection.connectionId : null),
    subscribeStatus: (listener: () => void): (() => void) => {
      statusListeners.add(listener);
      return () => statusListeners.delete(listener);
    },
  };
}

export type ChecklistHub = ReturnType<typeof createChecklistHub>;

export const checklistHub = createChecklistHub();
