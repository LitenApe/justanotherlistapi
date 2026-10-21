import { waitFor } from "@testing-library/react";
import { HubConnectionState, type HubConnection } from "@microsoft/signalr";

type Handler = (...args: unknown[]) => void;

/** Minimal stand-in for a SignalR HubConnection, driven from tests. */
export class FakeConnection {
  state = HubConnectionState.Disconnected;
  connectionId: string | null = null;
  readonly invocations: { method: string; args: unknown[] }[] = [];
  private readonly handlers = new Map<string, Handler[]>();
  private reconnecting: (() => void) | undefined;
  private reconnected: (() => void) | undefined;

  on(event: string, handler: Handler) {
    this.handlers.set(event, [...(this.handlers.get(event) ?? []), handler]);
  }

  onreconnecting(callback: () => void) {
    this.reconnecting = callback;
  }

  onreconnected(callback: () => void) {
    this.reconnected = callback;
  }

  onclose() {
    // Not exercised by tests.
  }

  start() {
    this.state = HubConnectionState.Connected;
    this.connectionId = "test-connection";
    return Promise.resolve();
  }

  stop() {
    this.state = HubConnectionState.Disconnected;
    this.connectionId = null;
    return Promise.resolve();
  }

  invoke(method: string, ...args: unknown[]) {
    this.invocations.push({ method, args });
    return Promise.resolve();
  }

  /** Simulates the server pushing an event. */
  emit(event: string, ...args: unknown[]) {
    for (const handler of this.handlers.get(event) ?? []) handler(...args);
  }

  simulateDrop() {
    this.state = HubConnectionState.Reconnecting;
    this.reconnecting?.();
  }

  simulateReconnect() {
    this.state = HubConnectionState.Connected;
    this.reconnected?.();
  }
}

export const fakeConnections: FakeConnection[] = [];

export function buildFakeConnection(): HubConnection {
  const connection = new FakeConnection();
  fakeConnections.push(connection);
  return connection as unknown as HubConnection;
}

export function latestConnection(): FakeConnection {
  const connection = fakeConnections.at(-1);
  if (!connection) throw new Error("No hub connection has been started.");
  return connection;
}

/**
 * Waits until the page has joined the list's group. Pages subscribe in an
 * effect after rendering, so an event emitted before that would be missed.
 */
export async function subscribedTo(...groupIds: string[]): Promise<FakeConnection> {
  return waitFor(() => {
    const connection = latestConnection();
    for (const groupId of groupIds) {
      const joined = connection.invocations.some(
        ({ method, args }) => method === "JoinGroup" && args[0] === groupId,
      );
      if (!joined) throw new Error(`The page hasn't subscribed to ${groupId} yet.`);
    }
    return connection;
  });
}
