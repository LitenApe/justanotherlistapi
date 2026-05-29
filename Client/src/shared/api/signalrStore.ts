import {
  type HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  HttpTransportType,
  LogLevel,
} from "@microsoft/signalr";

import { authStore } from "./authStore";

export type ConnectionState =
  | "disconnected"
  | "connecting"
  | "connected"
  | "reconnecting";

export interface SignalRStoreDeps {
  buildConnection: (
    url: string,
    accessTokenFactory: () => string,
  ) => HubConnection;
}

export interface SignalRStore {
  connect: (token: string) => void;
  disconnect: () => void;
  joinGroup: (groupId: string) => Promise<void>;
  leaveGroup: (groupId: string) => Promise<void>;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  on: (event: string, handler: (...args: any[]) => void) => void;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  off: (event: string, handler: (...args: any[]) => void) => void;
  getConnectionId: () => string | null;
  subscribe: (listener: () => void) => () => void;
  getSnapshot: () => ConnectionState;
  onReconnected: (callback: () => void) => () => void;
}

function defaultBuildConnection(
  url: string,
  accessTokenFactory: () => string,
): HubConnection {
  return new HubConnectionBuilder()
    .withUrl(url, {
      accessTokenFactory,
      skipNegotiation: true,
      transport: HttpTransportType.WebSockets,
    })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build();
}

export function createSignalRStore(
  deps: SignalRStoreDeps = { buildConnection: defaultBuildConnection },
): SignalRStore {
  let connection: HubConnection | null = null;
  let state: ConnectionState = "disconnected";
  const listeners = new Set<() => void>();
  const reconnectCallbacks = new Set<() => void>();
  const pendingGroups = new Set<string>();

  function notify(): void {
    listeners.forEach((l) => l());
  }

  function mapState(hubState: HubConnectionState): ConnectionState {
    switch (hubState) {
      case HubConnectionState.Connected:
        return "connected";
      case HubConnectionState.Connecting:
        return "connecting";
      case HubConnectionState.Reconnecting:
        return "reconnecting";
      default:
        return "disconnected";
    }
  }

  function updateState(): void {
    const next = connection ? mapState(connection.state) : "disconnected";
    if (next !== state) {
      state = next;
      notify();
    }
  }

  async function flushPendingGroups(): Promise<void> {
    if (connection?.state !== HubConnectionState.Connected) return;
    await Promise.all(
      Array.from(pendingGroups).map((groupId) =>
        connection!.invoke("JoinGroup", groupId),
      ),
    );
  }

  function connect(token: string): void {
    if (connection) return;

    state = "connecting";
    notify();

    connection = deps.buildConnection("/hubs/checklist", () => token);

    connection.onclose(() => updateState());
    connection.onreconnecting(() => updateState());
    connection.onreconnected(() => {
      updateState();
      flushPendingGroups();
      reconnectCallbacks.forEach((cb) => cb());
    });

    connection.start().then(
      () => {
        updateState();
        flushPendingGroups();
      },
      () => updateState(),
    );
  }

  function disconnect(): void {
    if (!connection) return;
    const conn = connection;
    connection = null;
    state = "disconnected";
    notify();
    conn.stop();
  }

  async function joinGroup(groupId: string): Promise<void> {
    pendingGroups.add(groupId);
    if (connection?.state === HubConnectionState.Connected) {
      await connection.invoke("JoinGroup", groupId);
    }
  }

  async function leaveGroup(groupId: string): Promise<void> {
    pendingGroups.delete(groupId);
    if (connection?.state === HubConnectionState.Connected) {
      await connection.invoke("LeaveGroup", groupId);
    }
  }

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  function on(event: string, handler: (...args: any[]) => void): void {
    connection?.on(event, handler);
  }

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  function off(event: string, handler: (...args: any[]) => void): void {
    connection?.off(event, handler);
  }

  function getConnectionId(): string | null {
    return connection?.connectionId ?? null;
  }

  return {
    connect,
    disconnect,
    joinGroup,
    leaveGroup,
    on,
    off,
    getConnectionId,
    subscribe: (listener: () => void) => {
      listeners.add(listener);
      return () => {
        listeners.delete(listener);
      };
    },
    getSnapshot: () => state,
    onReconnected: (callback: () => void) => {
      reconnectCallbacks.add(callback);
      return () => {
        reconnectCallbacks.delete(callback);
      };
    },
  };
}

export const signalRStore = createSignalRStore();

// Auto-connect when auth token changes
authStore.subscribe(() => {
  const token = authStore.getToken();
  if (token) {
    signalRStore.connect(token);
  } else {
    signalRStore.disconnect();
  }
});

// Connect immediately if a token already exists
const initialToken = authStore.getToken();
if (initialToken) {
  signalRStore.connect(initialToken);
}
