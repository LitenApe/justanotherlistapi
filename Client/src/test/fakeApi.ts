import { vi } from "vitest";
import type { Item, ItemGroup, ItemGroupSettings, ItemGroupSettingsInput } from "../api/types";

export const ME = "00000000-0000-0000-0000-000000000001";
export const OTHER = "00000000-0000-0000-0000-000000000002";

interface Call {
  method: string;
  path: string;
  body: unknown;
  headers: Headers;
}

export const defaultSettings = (): ItemGroupSettings => ({
  autoDeleteAfterMinutes: null,
  hideCompletedAfterMinutes: null,
  autoDeleteUpdatedAt: null,
});

export function makeItem(overrides: Partial<Item> & Pick<Item, "name" | "itemGroupId">): Item {
  return {
    id: crypto.randomUUID(),
    description: null,
    isComplete: false,
    completedAt: overrides.isComplete ? new Date().toISOString() : null,
    ...overrides,
  };
}

export function makeList(name: string, itemNames: string[] = [], members = [ME]): ItemGroup {
  const id = crypto.randomUUID();
  return {
    id,
    name,
    members,
    items: itemNames.map((itemName) => makeItem({ name: itemName, itemGroupId: id })),
    settings: defaultSettings(),
  };
}

/** Mirrors the server: completing sets the time, re-saving keeps it, reopening clears it. */
function completedAt(existing: Item | undefined, isComplete: boolean): string | null {
  if (!isComplete) return null;
  return existing?.completedAt ?? new Date().toISOString();
}

const json = (status: number, body?: unknown) =>
  new Response(body === undefined ? null : JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });

/**
 * In-memory implementation of the checklist API, installed as `fetch`.
 * Behaviour mirrors specifications/checklist.md closely enough for UI tests.
 */
export function installFakeApi(initialLists: ItemGroup[] = []) {
  const lists = structuredClone(initialLists);
  const calls: Call[] = [];
  const failures: { method: string; pattern: RegExp; status: number }[] = [];

  const find = (id: string | undefined) => lists.find((list) => list.id === id);

  function handle(method: string, path: string, body: unknown): Response {
    const failure = failures.find((f) => f.method === method && f.pattern.test(path));
    if (failure) {
      failures.splice(failures.indexOf(failure), 1);
      return json(failure.status);
    }

    const [, api, list, listId, second, third] = path.split("/");
    if (api !== "api" || list !== "list") return json(404);
    const group = find(listId);

    if (!listId) {
      if (method === "GET") {
        return json(
          200,
          lists
            .filter((l) => l.members.includes(ME))
            .map((l) => ({ ...l, items: l.items.filter((i) => !i.isComplete), members: [] })),
        );
      }
      if (method === "POST") {
        const created = { id: crypto.randomUUID(), name: (body as { name: string }).name };
        const newList = { ...created, items: [], members: [ME], settings: defaultSettings() };
        lists.push(newList);
        return json(201, newList);
      }
    }

    if (!group?.members.includes(ME)) return json(403);

    if (second === "member" && third) {
      if (method === "POST") {
        if (group.members.includes(third)) return json(409);
        group.members.push(third);
        return json(204);
      }
      if (method === "DELETE") {
        if (group.members.length === 1 && group.members[0] === third) return json(409);
        group.members = group.members.filter((m) => m !== third);
        return json(204);
      }
    }

    if (!second) {
      if (method === "GET") return json(200, group);
      if (method === "PUT") {
        group.name = (body as { name: string }).name;
        return json(204);
      }
      if (method === "DELETE") {
        lists.splice(lists.indexOf(group), 1);
        return json(204);
      }
      if (method === "POST") {
        const input = body as Omit<Item, "id" | "itemGroupId" | "completedAt">;
        const item: Item = {
          id: crypto.randomUUID(),
          itemGroupId: group.id,
          ...input,
          completedAt: completedAt(undefined, input.isComplete),
        };
        group.items.push(item);
        return json(201, item);
      }
    }

    if (second === "settings" && method === "PUT") {
      const input = body as ItemGroupSettingsInput;
      const autoDeleteChanged =
        input.autoDeleteAfterMinutes !== group.settings.autoDeleteAfterMinutes;
      group.settings = {
        ...input,
        autoDeleteUpdatedAt: autoDeleteChanged
          ? new Date().toISOString()
          : group.settings.autoDeleteUpdatedAt,
      };
      return json(200, group.settings);
    }

    if (second) {
      if (method === "PUT") {
        const input = body as Omit<Item, "id" | "itemGroupId" | "completedAt">;
        group.items = group.items.map((i) =>
          i.id === second ? { ...i, ...input, completedAt: completedAt(i, input.isComplete) } : i,
        );
        return json(204);
      }
      if (method === "DELETE") {
        group.items = group.items.filter((i) => i.id !== second);
        return json(204);
      }
    }

    return json(405);
  }

  const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
    const url = new URL(input instanceof Request ? input.url : String(input), "http://localhost");
    const method = init?.method ?? "GET";
    const body: unknown = typeof init?.body === "string" ? JSON.parse(init.body) : undefined;
    calls.push({ method, path: url.pathname, body, headers: new Headers(init?.headers) });
    return Promise.resolve(handle(method, url.pathname, body));
  });

  vi.stubGlobal("fetch", fetchMock);

  return {
    lists,
    calls,
    fetchMock,
    /** Makes the next matching request fail with the given HTTP status. */
    failNext(method: string, pattern: RegExp, status: number) {
      failures.push({ method, pattern, status });
    },
    callsTo(method: string, pattern: RegExp) {
      return calls.filter((call) => call.method === method && pattern.test(call.path));
    },
  };
}
