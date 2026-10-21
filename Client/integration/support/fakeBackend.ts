import type { Page, Request, Route, WebSocketRoute } from "@playwright/test";
import type {
  Item,
  ItemGroup,
  ItemGroupSettings,
  ItemGroupSettingsInput,
  ItemInput,
} from "../../src/api/types";
import { defaultSettings } from "../../src/test/builders";

/**
 * An in-memory stand-in for everything on the other side of the network: the
 * checklist REST API (specifications/checklist.md), the mock OAuth token
 * endpoint and the SignalR hub. It is installed with Playwright's network
 * routing, so the app runs unchanged and only the network is faked.
 *
 * Like the real server, every change is broadcast to the list's SignalR group,
 * except to the connection named in X-SignalR-Connection-Id. Tests act as other
 * members through `as(userId)`, which changes data and broadcasts the same way.
 */
export class FakeBackend {
  readonly lists: ItemGroup[] = [];
  /** Every REST request the app made, in order. */
  readonly calls: ApiCall[] = [];
  /** How long issued tokens stay valid. */
  tokenLifetimeSeconds = 3600;
  /** Makes the token endpoint unreachable, like a stopped OAuth server. */
  signInServerDown = false;

  private readonly hubClients = new Set<HubClient>();
  private readonly failures: { method: string; pattern: RegExp; status: number }[] = [];
  private readonly holds: { method: string; pattern: RegExp; released: Promise<undefined> }[] = [];

  async install(page: Page): Promise<void> {
    await page.route("**/default/token", (route) => this.token(route));
    await page.route("**/api/**", (route) => this.api(route));
    await page.routeWebSocket(/\/hubs\/checklist/, (socket) => {
      this.connect(socket);
    });
  }

  /** Adds lists as they exist on the server before the test starts. */
  seed(...lists: ItemGroup[]): void {
    this.lists.push(...structuredClone(lists));
  }

  list(listId: string): ItemGroup | undefined {
    return this.lists.find((list) => list.id === listId);
  }

  /** Makes the next matching REST request fail with the given HTTP status. */
  failNext(method: string, pattern: RegExp, status: number): void {
    this.failures.push({ method, pattern, status });
  }

  /**
   * Holds the next matching REST request until the returned function is called,
   * to look at the app while it waits (like a slow network).
   */
  holdNext(method: string, pattern: RegExp): () => void {
    const { promise, resolve } = Promise.withResolvers<undefined>();
    this.holds.push({ method, pattern, released: promise });
    return () => {
      resolve(undefined);
    };
  }

  callsTo(method: string, pattern: RegExp): ApiCall[] {
    return this.calls.filter((call) => call.method === method && pattern.test(call.path));
  }

  /** Whether one of the user's live connections has joined the list's group. */
  isSubscribed(listId: string, userId?: string): boolean {
    return [...this.hubClients].some(
      (client) => client.groups.has(listId) && (userId === undefined || client.userId === userId),
    );
  }

  hubConnectionCount(): number {
    return this.hubClients.size;
  }

  /** Drops every SignalR connection, like a server restart or network blip. */
  async dropHubConnections(): Promise<void> {
    await Promise.all([...this.hubClients].map((client) => client.socket.close({ code: 1011 })));
    this.hubClients.clear();
  }

  /** What ChecklistCleanupService does once a list's auto-delete time has passed. */
  deleteListAutomatically(listId: string): void {
    const list = this.list(listId);
    if (list) this.deleteList(list, null);
  }

  /** Changes made by another member, through the same code paths as the API. */
  as(userId: string) {
    const member = (listId: string) => this.requireMember(listId, userId);
    return {
      createItem: (listId: string, input: Partial<ItemInput> & Pick<ItemInput, "name">) =>
        this.createItem(member(listId), { description: null, isComplete: false, ...input }, null),
      updateItem: (listId: string, itemId: string, changes: Partial<ItemInput>) => {
        const list = member(listId);
        const item = list.items.find((candidate) => candidate.id === itemId);
        if (!item) throw new Error(`No item ${itemId} in ${list.name}.`);
        this.updateItem(list, itemId, { ...item, ...changes }, null);
      },
      deleteItem: (listId: string, itemId: string) => {
        this.deleteItem(member(listId), itemId, null);
      },
      renameList: (listId: string, name: string) => {
        this.renameList(member(listId), name, null);
      },
      updateSettings: (listId: string, input: ItemGroupSettingsInput) =>
        this.updateSettings(member(listId), input, null),
      deleteList: (listId: string) => {
        this.deleteList(member(listId), null);
      },
      addMember: (listId: string, memberId: string) => {
        this.addMember(member(listId), memberId, null);
      },
      removeMember: (listId: string, memberId: string) => {
        this.removeMember(member(listId), memberId, null);
      },
    };
  }

  // ─── OAuth ────────────────────────────────────────────────────────────────

  private async token(route: Route): Promise<void> {
    if (this.signInServerDown) {
      await route.abort("connectionrefused");
      return;
    }
    // Like the mock OAuth server: the client ID becomes the subject.
    const clientId = new URLSearchParams(route.request().postData() ?? "").get("client_id") ?? "";
    const now = Math.floor(Date.now() / 1000);
    await route.fulfill({
      json: {
        access_token: unsignedJwt({
          sub: clientId,
          iat: now,
          exp: now + this.tokenLifetimeSeconds,
        }),
        token_type: "Bearer",
        expires_in: this.tokenLifetimeSeconds,
      },
    });
  }

  private authenticate(authorization: string | undefined): string | null {
    const token = /^Bearer (.+)$/.exec(authorization ?? "")?.[1];
    const claims = token ? readJwt(token) : null;
    if (typeof claims?.sub !== "string" || typeof claims.exp !== "number") return null;
    return claims.exp * 1000 > Date.now() ? claims.sub.toLowerCase() : null;
  }

  // ─── REST API ─────────────────────────────────────────────────────────────

  private async api(route: Route): Promise<void> {
    const request = route.request();
    const method = request.method();
    const path = new URL(request.url()).pathname;
    const body = jsonBody(request);
    const headers = request.headers();
    const userId = this.authenticate(headers.authorization);
    this.calls.push({ method, path, body, headers, userId });

    const hold = this.holds.find((h) => h.method === method && h.pattern.test(path));
    if (hold) {
      this.holds.splice(this.holds.indexOf(hold), 1);
      await hold.released;
    }

    const failure = this.failures.find((f) => f.method === method && f.pattern.test(path));
    if (failure) {
      this.failures.splice(this.failures.indexOf(failure), 1);
      await route.fulfill({ status: failure.status });
      return;
    }
    if (!userId) {
      await route.fulfill({ status: 401 });
      return;
    }

    const response = this.handle(method, path, body, userId, headers["x-signalr-connection-id"]);
    await route.fulfill(response.json === undefined ? { status: response.status } : response);
  }

  private handle(
    method: string,
    path: string,
    body: unknown,
    userId: string,
    connectionId: string | undefined,
  ): { status: number; json?: unknown } {
    const exclude = connectionId ?? null;
    const [, api, resource, listId, second, third] = path.split("/");
    if (api !== "api" || resource !== "list") return { status: 404 };

    if (!listId) {
      if (method === "GET") {
        // Lists you belong to, with open items only and no member IDs.
        const mine = this.lists.filter((list) => list.members.includes(userId));
        return {
          status: 200,
          json: mine.map((list) => ({
            ...list,
            items: list.items.filter((item) => !item.isComplete),
            members: [],
          })),
        };
      }
      if (method === "POST") {
        const { name } = body as { name: string };
        if (!name.trim()) return { status: 400 };
        const list: ItemGroup = {
          id: crypto.randomUUID(),
          name,
          items: [],
          members: [userId],
          settings: defaultSettings(),
        };
        this.lists.push(list);
        return { status: 201, json: list };
      }
      return { status: 405 };
    }

    // Membership is checked before existence, so non-members can't probe for lists.
    const list = this.list(listId);
    if (!list?.members.includes(userId)) return { status: 403 };

    if (!second) {
      switch (method) {
        case "GET":
          return { status: 200, json: list };
        case "PUT":
          this.renameList(list, (body as { name: string }).name, exclude);
          return { status: 204 };
        case "DELETE":
          this.deleteList(list, exclude);
          return { status: 204 };
        case "POST":
          return { status: 201, json: this.createItem(list, body as ItemInput, exclude) };
      }
    }

    if (second === "settings" && method === "PUT") {
      return {
        status: 200,
        json: this.updateSettings(list, body as ItemGroupSettingsInput, exclude),
      };
    }

    if (second === "member" && third) {
      if (method === "POST") {
        if (list.members.includes(third)) return { status: 409 };
        this.addMember(list, third, exclude);
        return { status: 204 };
      }
      if (method === "DELETE") {
        if (list.members.length === 1 && list.members[0] === third) return { status: 409 };
        this.removeMember(list, third, exclude);
        return { status: 204 };
      }
    }

    if (second && second !== "member" && second !== "settings") {
      if (method === "PUT") {
        this.updateItem(list, second, body as ItemInput, exclude);
        return { status: 204 };
      }
      if (method === "DELETE") {
        this.deleteItem(list, second, exclude);
        return { status: 204 };
      }
    }

    return { status: 405 };
  }

  // ─── Changes (shared by the API and other members) ─────────────────────────

  private createItem(list: ItemGroup, input: ItemInput, exclude: string | null): Item {
    const item: Item = {
      id: crypto.randomUUID(),
      itemGroupId: list.id,
      name: input.name,
      description: input.description,
      isComplete: input.isComplete,
      completedAt: input.isComplete ? new Date().toISOString() : null,
    };
    list.items.push(item);
    this.broadcast(list.id, "ItemCreated", [list.id, item], exclude);
    return item;
  }

  private updateItem(list: ItemGroup, itemId: string, input: ItemInput, exclude: string | null) {
    const index = list.items.findIndex((item) => item.id === itemId);
    const existing = list.items[index];
    if (!existing) return;
    const updated: Item = {
      ...existing,
      name: input.name,
      description: input.description,
      isComplete: input.isComplete,
      // Completing sets the time, re-saving keeps it, reopening clears it.
      completedAt: input.isComplete ? (existing.completedAt ?? new Date().toISOString()) : null,
    };
    list.items[index] = updated;
    this.broadcast(list.id, "ItemUpdated", [list.id, updated], exclude);
  }

  private deleteItem(list: ItemGroup, itemId: string, exclude: string | null) {
    list.items = list.items.filter((item) => item.id !== itemId);
    this.broadcast(list.id, "ItemDeleted", [list.id, itemId], exclude);
  }

  private renameList(list: ItemGroup, name: string, exclude: string | null) {
    list.name = name;
    this.broadcast(list.id, "GroupRenamed", [list.id, name], exclude);
  }

  private updateSettings(
    list: ItemGroup,
    input: ItemGroupSettingsInput,
    exclude: string | null,
  ): ItemGroupSettings {
    const autoDeleteChanged = input.autoDeleteAfterMinutes !== list.settings.autoDeleteAfterMinutes;
    list.settings = {
      ...input,
      autoDeleteUpdatedAt: autoDeleteChanged
        ? new Date().toISOString()
        : list.settings.autoDeleteUpdatedAt,
    };
    this.broadcast(list.id, "GroupSettingsChanged", [list.id, list.settings], exclude);
    return list.settings;
  }

  private deleteList(list: ItemGroup, exclude: string | null) {
    this.lists.splice(this.lists.indexOf(list), 1);
    this.broadcast(list.id, "GroupDeleted", [list.id], exclude);
  }

  private addMember(list: ItemGroup, memberId: string, exclude: string | null) {
    list.members.push(memberId);
    this.broadcast(list.id, "MemberAdded", [list.id, memberId], exclude);
  }

  private removeMember(list: ItemGroup, memberId: string, exclude: string | null) {
    list.members = list.members.filter((id) => id !== memberId);
    // The removed member's connections are still in the group, so they hear it too.
    this.broadcast(list.id, "MemberRemoved", [list.id, memberId], exclude);
  }

  private requireMember(listId: string, userId: string): ItemGroup {
    const list = this.list(listId);
    if (!list?.members.includes(userId)) throw new Error(`${userId} isn't a member of ${listId}.`);
    return list;
  }

  // ─── SignalR hub (JSON protocol over WebSockets) ───────────────────────────

  private connect(socket: WebSocketRoute) {
    const token = new URL(socket.url()).searchParams.get("access_token");
    const userId = this.authenticate(`Bearer ${token ?? ""}`);
    if (!userId) {
      void socket.close({ code: 1008, reason: "Unauthorized" });
      return;
    }

    const client: HubClient = {
      socket,
      userId,
      connectionId: crypto.randomUUID(),
      groups: new Set(),
      handshakeDone: false,
    };
    this.hubClients.add(client);
    socket.onClose(() => {
      this.hubClients.delete(client);
    });
    socket.onMessage((frame) => {
      const text = typeof frame === "string" ? frame : frame.toString("utf8");
      for (const record of text.split(RECORD_SEPARATOR).filter(Boolean)) {
        this.receive(client, JSON.parse(record) as HubMessage);
      }
    });
  }

  private receive(client: HubClient, message: HubMessage) {
    if (!client.handshakeDone) {
      // {"protocol":"json","version":1} → an empty handshake response means OK.
      client.handshakeDone = true;
      send(client, {});
      return;
    }

    if (message.type === MessageType.Ping) {
      send(client, { type: MessageType.Ping });
      return;
    }

    if (message.type !== MessageType.Invocation) return;
    const groupId = message.arguments?.[0];
    const complete = (error?: string) => {
      if (message.invocationId === undefined) return;
      send(client, {
        type: MessageType.Completion,
        invocationId: message.invocationId,
        ...(error ? { error } : {}),
      });
    };

    if (message.target === "JoinGroup" && typeof groupId === "string") {
      // Like ChecklistHub: only members may listen to a list.
      if (this.list(groupId)?.members.includes(client.userId)) {
        client.groups.add(groupId);
        complete();
      } else {
        complete(
          "An unexpected error occurred invoking 'JoinGroup' on the server. HubException: Forbidden",
        );
      }
    } else if (message.target === "LeaveGroup" && typeof groupId === "string") {
      client.groups.delete(groupId);
      complete();
    } else {
      complete(`Unknown hub method '${String(message.target)}'.`);
    }
  }

  private broadcast(listId: string, target: string, args: unknown[], exclude: string | null) {
    for (const client of this.hubClients) {
      if (client.groups.has(listId) && client.connectionId !== exclude) {
        send(client, { type: MessageType.Invocation, target, arguments: args });
      }
    }
  }
}

export interface ApiCall {
  method: string;
  path: string;
  body: unknown;
  headers: Record<string, string>;
  /** The authenticated user, or null when the token was missing or expired. */
  userId: string | null;
}

interface HubClient {
  socket: WebSocketRoute;
  userId: string;
  /** Server-side connection ID. The app never learns it, because it skips negotiation. */
  connectionId: string;
  groups: Set<string>;
  handshakeDone: boolean;
}

interface HubMessage {
  type?: number;
  invocationId?: string;
  target?: string;
  arguments?: unknown[];
}

/** SignalR message types (https://github.com/dotnet/aspnetcore/blob/main/src/SignalR/docs/specs/HubProtocol.md). */
const MessageType = { Invocation: 1, Completion: 3, Ping: 6 } as const;
const RECORD_SEPARATOR = "\u001e";

function send(client: HubClient, message: object) {
  client.socket.send(JSON.stringify(message) + RECORD_SEPARATOR);
}

function jsonBody(request: Request): unknown {
  const text = request.postData();
  return text ? (JSON.parse(text) as unknown) : undefined;
}

const base64url = (value: object) => Buffer.from(JSON.stringify(value)).toString("base64url");

function unsignedJwt(claims: object): string {
  return `${base64url({ alg: "none", typ: "JWT" })}.${base64url(claims)}.`;
}

function readJwt(token: string): Record<string, unknown> | null {
  const payload = token.split(".")[1];
  if (!payload) return null;
  try {
    return JSON.parse(Buffer.from(payload, "base64url").toString("utf8")) as Record<
      string,
      unknown
    >;
  } catch {
    return null;
  }
}
