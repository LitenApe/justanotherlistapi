import { describe, expect, it, vi } from "vitest";
import { buildFakeConnection, latestConnection } from "../test/fakeConnection";
import { createChecklistHub } from "./hub";

async function startedHub() {
  const hub = createChecklistHub(buildFakeConnection);
  hub.start(() => "token");
  await vi.waitFor(() => {
    expect(hub.getStatus()).toBe("connected");
  });
  return { hub, connection: latestConnection() };
}

describe("checklistHub", () => {
  it("joins a group once per list and leaves after the last subscriber", async () => {
    const { hub, connection } = await startedHub();

    const first = hub.subscribe("list-1", {});
    const second = hub.subscribe("list-1", {});
    first();
    expect(connection.invocations).toEqual([{ method: "JoinGroup", args: ["list-1"] }]);

    second();
    expect(connection.invocations.at(-1)).toEqual({ method: "LeaveGroup", args: ["list-1"] });
  });

  it("joins groups that were subscribed before the connection opened", async () => {
    const hub = createChecklistHub(buildFakeConnection);
    hub.subscribe("list-1", {});
    hub.start(() => "token");

    await vi.waitFor(() => {
      expect(latestConnection().invocations).toEqual([{ method: "JoinGroup", args: ["list-1"] }]);
    });
  });

  it("routes events only to subscribers of that list", async () => {
    const { hub, connection } = await startedHub();
    const onList1 = vi.fn();
    const onList2 = vi.fn();
    hub.subscribe("list-1", { groupRenamed: onList1 });
    hub.subscribe("list-2", { groupRenamed: onList2 });

    connection.emit("GroupRenamed", "list-1", "New name");

    expect(onList1).toHaveBeenCalledWith("list-1", "New name");
    expect(onList2).not.toHaveBeenCalled();
  });

  it("rejoins and asks subscribers to resync after reconnecting", async () => {
    const { hub, connection } = await startedHub();
    const resync = vi.fn();
    hub.subscribe("list-1", { resync });

    connection.simulateDrop();
    expect(hub.getStatus()).toBe("reconnecting");
    expect(hub.getConnectionId()).toBeNull();

    connection.simulateReconnect();
    await vi.waitFor(() => {
      expect(resync).toHaveBeenCalledWith("list-1");
    });
    expect(hub.getStatus()).toBe("connected");
    expect(connection.invocations.filter((i) => i.method === "JoinGroup")).toHaveLength(2);
  });

  it("exposes the connection ID only while connected", async () => {
    const { hub } = await startedHub();
    expect(hub.getConnectionId()).toBe("test-connection");
    hub.stop();
    expect(hub.getConnectionId()).toBeNull();
    expect(hub.getStatus()).toBe("disconnected");
  });
});
