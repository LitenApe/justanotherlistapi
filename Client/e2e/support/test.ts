import { test as base, expect, type BrowserContext, type Page } from "@playwright/test";

export { expect };

/**
 * A person using the app in their own browser context (so their own session),
 * signed in through the real sign-in page and the mock OAuth server. Each test
 * makes fresh users with random IDs, so tests never see each other's lists even
 * though they share one database.
 */
export class User {
  readonly id = crypto.randomUUID();
  /** Lists this user's page is receiving live updates for. */
  private readonly liveLists = new Set<string>();
  readonly errors: string[] = [];

  constructor(readonly page: Page) {
    page.on("pageerror", (error) => this.errors.push(error.message));
    this.watchLiveUpdates();
  }

  async signIn(): Promise<void> {
    await this.page.goto("/sign-in");
    await this.page.getByLabel("User ID").fill(this.id);
    await this.page.getByRole("button", { name: "Sign in" }).click();
    await expect(this.page.getByRole("heading", { level: 1, name: "Your lists" })).toBeVisible();
  }

  /** Creates a list from the overview and returns its ID. */
  async createList(name: string): Promise<string> {
    await this.page.goto("/");
    await this.page.getByLabel("New list").fill(name);
    await this.page.getByRole("button", { name: "Create list" }).click();
    await expect(this.page.getByRole("heading", { level: 1, name })).toBeVisible();
    const id = new URL(this.page.url()).pathname.split("/").at(-1);
    if (!id) throw new Error("No list ID in the address.");
    return id;
  }

  /** Opens a list and waits until live updates for it are flowing. */
  async openList(listId: string): Promise<void> {
    await this.page.goto(`/lists/${listId}`);
    await expect(this.page.getByRole("heading", { level: 2, name: "Items" })).toBeVisible();
    await this.receivingLiveUpdates(listId);
  }

  async addItem(name: string): Promise<void> {
    const input = this.page.getByLabel("Add an item");
    await input.fill(name);
    await input.press("Enter");
    await expect(this.page.getByRole("checkbox", { name, exact: true })).toBeVisible();
  }

  /**
   * Checks or unchecks an item and waits for the server to save it. The page
   * updates before the request finishes, so tests that reload must wait.
   */
  async toggle(name: string): Promise<void> {
    const saved = this.page.waitForResponse(
      (response) =>
        response.request().method() === "PUT" &&
        /\/api\/list\/[^/]+\/[^/]+$/.test(new URL(response.url()).pathname),
    );
    await this.page.getByRole("checkbox", { name, exact: true }).click();
    expect((await saved).status()).toBe(204);
  }

  /** Changes the list's settings through the settings dialog. */
  async saveSettings({ hideAfterMinutes, deleteAfterDays }: SettingsChange): Promise<void> {
    await this.page.getByRole("button", { name: "Settings" }).click();
    const dialog = this.page.getByRole("dialog", { name: "List settings" });
    if (hideAfterMinutes !== undefined) {
      await dialog.getByRole("checkbox", { name: "Hide done items" }).check();
      const hideAfter = dialog.getByRole("group", { name: "Hide after" });
      await hideAfter.getByLabel("Amount").fill(hideAfterMinutes.toString());
      await hideAfter.getByLabel("Unit").selectOption("minutes");
    }
    if (deleteAfterDays !== undefined) {
      await dialog
        .getByRole("checkbox", { name: "Delete the list when every item is done" })
        .check();
      const deleteAfter = dialog.getByRole("group", { name: "Delete after" });
      await deleteAfter.getByLabel("Amount").fill(deleteAfterDays.toString());
      await deleteAfter.getByLabel("Unit").selectOption("days");
    }
    await dialog.getByRole("button", { name: "Save" }).click();
    await expect(dialog).toBeHidden();
  }

  async addMember(member: User): Promise<void> {
    await this.page.getByLabel("Add a member").fill(member.id);
    await this.page.getByRole("button", { name: "Add member" }).click();
    await expect(this.members().getByText(member.id, { exact: true })).toBeVisible();
  }

  members() {
    return this.page.getByRole("region", { name: /Members/ });
  }

  /**
   * Waits until the server has accepted this page's JoinGroup for the list, so
   * other members' changes reach it. A change made before that is missed, as
   * it would be in real life.
   */
  async receivingLiveUpdates(listId: string): Promise<void> {
    await expect
      .poll(() => this.liveLists.has(listId), { message: `live updates for ${listId}` })
      .toBe(true);
  }

  /** Follows the SignalR traffic on the page's real WebSocket (read-only). */
  private watchLiveUpdates() {
    this.page.on("websocket", (socket) => {
      if (!socket.url().includes("/hubs/checklist")) return;
      const joining = new Map<string, string>();
      socket.on("framesent", ({ payload }) => {
        for (const message of hubMessages(payload)) {
          const listId = message.arguments?.[0];
          if (message.type !== 1 || typeof listId !== "string") continue;
          if (message.target === "JoinGroup" && message.invocationId) {
            joining.set(message.invocationId, listId);
          } else if (message.target === "LeaveGroup") {
            this.liveLists.delete(listId);
          }
        }
      });
      socket.on("framereceived", ({ payload }) => {
        for (const message of hubMessages(payload)) {
          const listId = message.invocationId ? joining.get(message.invocationId) : undefined;
          if (message.type !== 3 || listId === undefined) continue;
          joining.delete(message.invocationId ?? "");
          if (!message.error) this.liveLists.add(listId);
        }
      });
      socket.on("close", () => {
        this.liveLists.clear();
      });
    });
  }
}

interface SettingsChange {
  hideAfterMinutes?: number;
  deleteAfterDays?: number;
}

interface HubMessage {
  type?: number;
  invocationId?: string;
  target?: string;
  arguments?: unknown[];
  error?: string;
}

/** Splits a SignalR JSON-protocol frame into its messages. */
function hubMessages(payload: string | Buffer): HubMessage[] {
  const text = typeof payload === "string" ? payload : payload.toString("utf8");
  return text
    .split("\u001e")
    .filter(Boolean)
    .map((record) => JSON.parse(record) as HubMessage);
}

export const test = base.extend<{ newUser: () => Promise<User> }>({
  /** Signs in a fresh user in a browser context of their own. */
  newUser: async ({ browser, baseURL }, use) => {
    const contexts: BrowserContext[] = [];
    const users: User[] = [];
    await use(async () => {
      const context = await browser.newContext(baseURL ? { baseURL } : {});
      contexts.push(context);
      const user = new User(await context.newPage());
      users.push(user);
      await user.signIn();
      return user;
    });
    await Promise.all(contexts.map((context) => context.close()));
    // Uncaught errors fail the test even if everything looked right.
    expect(users.flatMap((user) => user.errors)).toEqual([]);
  },
});
