import { expect, expectAccessible, makeList, ME, test } from "./support/test";

test.describe("signing in", () => {
  test("sends signed-out visitors to sign in without calling the API", async ({
    page,
    backend,
  }) => {
    backend.seed(makeList("Groceries"));

    await page.goto("/");

    await expect(page).toHaveURL("/sign-in");
    await expect(page.getByRole("heading", { level: 1, name: "Sign in" })).toBeVisible();
    await expect(page).toHaveTitle("Sign in – JustAnotherList");
    expect(backend.calls).toEqual([]);
    await expectAccessible(page);
  });

  test("signs in with the OAuth server and opens your lists", async ({ page, backend }) => {
    const userId = "3f2504e0-4f89-41d3-9a0c-0305e82c3301";
    backend.seed(makeList("Groceries", [], [userId]));

    await page.goto("/sign-in");
    await page.getByLabel("User ID").fill(userId.toUpperCase());
    await page.getByRole("button", { name: "Sign in" }).click();

    await expect(page.getByRole("heading", { level: 1, name: "Your lists" })).toBeFocused();
    await expect(page.getByRole("link", { name: "Groceries" })).toBeVisible();
    expect(backend.calls.map((call) => call.userId)).toEqual([userId]);

    await page.getByRole("button", { name: "Account" }).click();
    await expect(page.getByText(userId, { exact: true })).toBeVisible();
  });

  test("returns to the page you asked for", async ({ page, backend }) => {
    const list = makeList("Groceries", ["Milk"]);
    backend.seed(list);

    await page.goto(`/lists/${list.id}`);
    await expect(page).toHaveURL(`/sign-in?redirectTo=%2Flists%2F${list.id}`);
    await page.getByRole("button", { name: "Sign in" }).click();

    await expect(page).toHaveURL(`/lists/${list.id}`);
    await expect(page.getByRole("heading", { level: 1, name: "Groceries" })).toBeFocused();
  });

  test("explains an invalid user ID next to the field", async ({ page, backend }) => {
    await page.goto("/sign-in");
    const input = page.getByLabel("User ID");

    await input.fill("not-a-guid");
    await page.getByRole("button", { name: "Sign in" }).click();

    await expect(input).toHaveAttribute("aria-invalid", "true");
    await expect(input).toHaveAccessibleDescription(/Enter a user ID in GUID format/);
    await expect(input).toBeFocused();
    expect(backend.calls).toEqual([]);
    await expectAccessible(page);
  });

  test("says so when the sign-in server can't be reached", async ({ page, backend }) => {
    backend.signInServerDown = true;
    await page.goto("/sign-in");

    await page.getByRole("button", { name: "Sign in" }).click();

    await expect(
      page.getByRole("alert").filter({ hasText: "Can't reach the sign-in server" }),
    ).toBeVisible();
    await expect(page).toHaveURL("/sign-in");
  });

  test("generates a fresh user ID and remembers the last one used", async ({ page }) => {
    await page.goto("/sign-in");
    const input = page.getByLabel("User ID");
    await expect(input).toHaveValue(ME);

    await page.getByRole("button", { name: "Generate a new user ID" }).click();
    const generated = await input.inputValue();
    expect(generated).toMatch(/^[0-9a-f-]{36}$/);
    expect(generated).not.toBe(ME);

    await page.getByRole("button", { name: "Sign in" }).click();
    await expect(page.getByRole("heading", { level: 1, name: "Your lists" })).toBeVisible();
    await page.getByRole("button", { name: "Account" }).click();
    await page.getByRole("button", { name: "Sign out" }).click();

    await expect(page).toHaveURL("/sign-in");
    await expect(page.getByLabel("User ID")).toHaveValue(generated);
  });

  test("signs out from the account menu", async ({ page, app }) => {
    await app.signIn();

    await page.getByRole("button", { name: "Account" }).click();
    await page.getByRole("button", { name: "Sign out" }).click();

    await expect(page.getByRole("heading", { level: 1, name: "Sign in" })).toBeVisible();
    await page.goto("/");
    await expect(page).toHaveURL("/sign-in");
  });

  test("asks you to sign in again when the session expires", async ({ page, backend, app }) => {
    const list = makeList("Groceries");
    backend.tokenLifetimeSeconds = 3;
    await app.openList(list);

    // The app's own expiry timer signs out; no request is needed to notice.
    await expect(page.getByRole("heading", { level: 1, name: "Sign in" })).toBeVisible({
      timeout: 10_000,
    });
    await expect(
      page.getByRole("status").filter({ hasText: "Your session expired" }),
    ).toBeVisible();
    await expect(page).toHaveURL(`/sign-in?redirectTo=%2Flists%2F${list.id}`);
  });

  test("keeps each tab signed in separately", async ({ page, app, context }) => {
    await app.signIn();

    const otherTab = await context.newPage();
    await otherTab.goto("/");

    await expect(otherTab).toHaveURL("/sign-in");
    await expect(page.getByRole("heading", { level: 1, name: "Your lists" })).toBeVisible();
  });
});
