import { expect, expectAccessible, PASSWORD, test } from "./support/test";

test.describe("signing up", () => {
  test("creates an account and opens your lists", async ({ page, backend }) => {
    await page.goto("/sign-up");
    await expect(page).toHaveTitle("Sign up – JustAnotherList");
    await expectAccessible(page);

    await page.getByLabel("Name").fill("  Sam Lee ");
    await page.getByLabel("Email").fill("sam@example.com");
    await page.getByLabel("Password", { exact: true }).fill(PASSWORD);
    await page.getByRole("button", { name: "Sign up" }).click();

    await expect(page.getByRole("heading", { level: 1, name: "Your lists" })).toBeFocused();
    expect(backend.account("sam@example.com")).toMatchObject({
      name: "Sam Lee",
      email: "sam@example.com",
      password: PASSWORD,
    });
    await page.getByRole("button", { name: "Account" }).click();
    await expect(page.getByText("Sam Lee", { exact: true })).toBeVisible();
  });

  test("explains each problem next to its field, without sending anything", async ({
    page,
    backend,
  }) => {
    await page.goto("/sign-up");
    await page.getByLabel("Name").fill("   ");
    await page.getByLabel("Email").fill("sam");
    await page.getByLabel("Password", { exact: true }).fill("too short");

    await page.getByRole("button", { name: "Sign up" }).click();

    const name = page.getByLabel("Name");
    await expect(name).toHaveAttribute("aria-invalid", "true");
    await expect(name).toHaveAccessibleDescription(/Enter your name\./);
    await expect(name).toBeFocused();
    await expect(page.getByLabel("Email")).toHaveAccessibleDescription(
      /Enter an email address, like name@example\.com\./,
    );
    await expect(page.getByLabel("Password", { exact: true })).toHaveAccessibleDescription(
      /Use at least 12 characters\./,
    );
    expect(backend.calls).toEqual([]);
    await expectAccessible(page);
  });

  test("says when an account already uses the email, and offers to sign in", async ({ page }) => {
    await page.goto("/sign-up");
    await page.getByLabel("Name").fill("Another Robin");
    await page.getByLabel("Email").fill("Robin@Example.com");
    await page.getByLabel("Password", { exact: true }).fill(PASSWORD);

    await page.getByRole("button", { name: "Sign up" }).click();

    const alert = page
      .getByRole("alert")
      .filter({ hasText: "An account already uses that email." });
    await expect(alert).toBeVisible();
    await alert.getByRole("link", { name: "Sign in instead" }).click();
    await expect(page.getByRole("heading", { level: 1, name: "Sign in" })).toBeVisible();
  });

  test("sends people who are signed in to their lists", async ({ page, app }) => {
    await app.signIn();

    await page.goto("/sign-up");

    await expect(page).toHaveURL("/");
    await expect(page.getByRole("heading", { level: 1, name: "Your lists" })).toBeVisible();
  });
});
