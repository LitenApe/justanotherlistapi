import { expect, test } from "./support/test";

test.describe("signing in", () => {
  test("gets a token from the OAuth server that the API accepts", async ({ newUser }) => {
    const user = await newUser();
    const { page } = user;

    // GET /api/list succeeded with the token: a new user has no lists yet.
    await expect(page.getByRole("heading", { name: "No lists yet" })).toBeVisible();
    await page.getByRole("button", { name: "Account" }).click();
    await expect(page.getByText(user.id, { exact: true })).toBeVisible();
  });

  test("keeps you signed in across reloads, and out after signing out", async ({ newUser }) => {
    const { page } = await newUser();
    const listId = await (async () => {
      await page.getByLabel("New list").fill("Errands");
      await page.getByRole("button", { name: "Create list" }).click();
      await expect(page.getByRole("heading", { level: 1, name: "Errands" })).toBeVisible();
      return new URL(page.url()).pathname.split("/").at(-1) ?? "";
    })();

    await page.reload();
    await expect(page.getByRole("heading", { level: 1, name: "Errands" })).toBeVisible();

    await page.getByRole("button", { name: "Account" }).click();
    await page.getByRole("button", { name: "Sign out" }).click();
    await expect(page.getByRole("heading", { level: 1, name: "Sign in" })).toBeVisible();
    await page.goto(`/lists/${listId}`);
    await expect(page).toHaveURL(/\/sign-in\?redirectTo=/);
  });
});
