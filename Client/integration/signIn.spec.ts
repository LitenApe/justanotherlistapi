import { expect, expectAccessible, makeList, ME, PASSWORD, test } from "./support/test";

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

  test("signs in with your email and password, and opens your lists", async ({ page, backend }) => {
    backend.seed(makeList("Groceries", [], [ME]));

    await page.goto("/sign-in");
    // Emails match ignoring case, like the real API.
    await page.getByLabel("Email").fill("ROBIN@example.com");
    await page.getByLabel("Password", { exact: true }).fill(PASSWORD);
    await page.getByRole("button", { name: "Sign in" }).click();

    await expect(page.getByRole("heading", { level: 1, name: "Your lists" })).toBeFocused();
    await expect(page.getByRole("link", { name: "Groceries" })).toBeVisible();
    // The sign-in itself goes without a token; everything after it as Robin.
    expect(backend.calls.map((call) => [call.path, call.userId])).toEqual([
      ["/api/auth/sign-in", null],
      ["/api/list", ME],
    ]);
    expect(backend.calls[0]?.body).toEqual({ email: "ROBIN@example.com", password: PASSWORD });

    await page.getByRole("button", { name: "Account" }).click();
    await expect(page.getByText("Robin", { exact: true })).toBeVisible();
    await expect(page.getByText("robin@example.com", { exact: true })).toBeVisible();
  });

  test("returns to the page you asked for", async ({ page, backend }) => {
    const list = makeList("Groceries", ["Milk"]);
    backend.seed(list);

    await page.goto(`/lists/${list.id}`);
    await expect(page).toHaveURL(`/sign-in?redirectTo=%2Flists%2F${list.id}`);
    await page.getByLabel("Email").fill("robin@example.com");
    await page.getByLabel("Password", { exact: true }).fill(PASSWORD);
    await page.getByRole("button", { name: "Sign in" }).click();

    await expect(page).toHaveURL(`/lists/${list.id}`);
    await expect(page.getByRole("heading", { level: 1, name: "Groceries" })).toBeFocused();
  });

  test("explains a missing email or password next to its field", async ({ page, backend }) => {
    await page.goto("/sign-in");
    const email = page.getByLabel("Email");
    const password = page.getByLabel("Password", { exact: true });

    await page.getByRole("button", { name: "Sign in" }).click();

    await expect(email).toHaveAttribute("aria-invalid", "true");
    await expect(email).toHaveAccessibleDescription("Enter your email address.");
    await expect(email).toBeFocused();
    await expect(password).toHaveAccessibleDescription("Enter your password.");
    await expectAccessible(page);

    await email.fill("robin@example.com");
    await page.getByRole("button", { name: "Sign in" }).click();

    await expect(email).not.toHaveAttribute("aria-invalid");
    await expect(password).toBeFocused();
    expect(backend.calls).toEqual([]);
  });

  test("says the email or password is wrong, without saying which", async ({ page }) => {
    await page.goto("/sign-in");

    for (const [email, password] of [
      ["robin@example.com", "not the password"],
      ["nobody@example.com", PASSWORD],
    ] as const) {
      await page.getByLabel("Email").fill(email);
      await page.getByLabel("Password", { exact: true }).fill(password);
      await page.getByRole("button", { name: "Sign in" }).click();

      await expect(
        page.getByRole("alert").filter({ hasText: "The email or password is wrong." }),
      ).toBeVisible();
      await expect(page).toHaveURL("/sign-in");
    }
  });

  test("says so when too many wrong passwords locked the account", async ({ page, backend }) => {
    backend.failNext("POST", /^\/api\/auth\/sign-in$/, 423);
    await page.goto("/sign-in");

    await page.getByLabel("Email").fill("robin@example.com");
    await page.getByLabel("Password", { exact: true }).fill(PASSWORD);
    await page.getByRole("button", { name: "Sign in" }).click();

    await expect(
      page.getByRole("alert").filter({ hasText: "Try again in a few minutes." }),
    ).toBeVisible();
  });

  test("says so when the server can't be reached", async ({ page, backend }) => {
    backend.authUnreachable = true;
    await page.goto("/sign-in");

    await page.getByLabel("Email").fill("robin@example.com");
    await page.getByLabel("Password", { exact: true }).fill(PASSWORD);
    await page.getByRole("button", { name: "Sign in" }).click();

    await expect(
      page.getByRole("alert").filter({ hasText: "Can't reach the server" }),
    ).toBeVisible();
    await expect(page).toHaveURL("/sign-in");
  });

  test("links to signing up, still heading where you were going", async ({ page }) => {
    await page.goto("/sign-in?redirectTo=%2Flists%2F42");

    await page.getByRole("link", { name: "Sign up" }).click();

    await expect(page).toHaveURL("/sign-up?redirectTo=%2Flists%2F42");
    await expect(page.getByRole("heading", { level: 1, name: "Sign up" })).toBeVisible();
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
