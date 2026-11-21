import { resetLinkSentTo } from "./support/mail";
import { expect, PASSWORD, test } from "./support/test";

test.describe("accounts", () => {
  test("signing up gets a token the API accepts", async ({ newUser }) => {
    const user = await newUser("Robin");
    const { page } = user;

    // GET /api/list succeeded with the token: a new account has no lists yet.
    await expect(page.getByRole("heading", { name: "No lists yet" })).toBeVisible();
    await page.getByRole("button", { name: "Account" }).click();
    await expect(page.getByText("Robin", { exact: true })).toBeVisible();
    await expect(page.getByText(user.email, { exact: true })).toBeVisible();
    await expect(page.getByText(user.id, { exact: true })).toBeVisible();
  });

  test("keeps you signed in across reloads, out after signing out, and back in with your password", async ({
    newUser,
  }) => {
    const user = await newUser();
    const listId = await user.createList("Errands");

    await user.page.reload();
    await expect(user.page.getByRole("heading", { level: 1, name: "Errands" })).toBeVisible();

    await user.page.getByRole("button", { name: "Account" }).click();
    await user.page.getByRole("button", { name: "Sign out" }).click();
    await expect(user.page.getByRole("heading", { level: 1, name: "Sign in" })).toBeVisible();
    await user.page.goto(`/lists/${listId}`);
    await expect(user.page).toHaveURL(/\/sign-in\?redirectTo=/);

    await user.signIn();
    await expect(user.page.getByRole("link", { name: "Errands" })).toBeVisible();
  });

  test("refuses a wrong password without saying which part was wrong", async ({ newUser }) => {
    const user = await newUser();
    await user.page.getByRole("button", { name: "Account" }).click();
    await user.page.getByRole("button", { name: "Sign out" }).click();

    await user.page.getByLabel("Email").fill(user.email);
    await user.page.getByLabel("Password", { exact: true }).fill("not the password");
    await user.page.getByRole("button", { name: "Sign in" }).click();

    await expect(
      user.page.getByRole("alert").filter({ hasText: "The email or password is wrong." }),
    ).toBeVisible();
    await user.signIn(PASSWORD);
  });

  test("refuses a second account with the same email, whatever its case", async ({ newUser }) => {
    const first = await newUser();
    const second = await newUser();
    await second.page.getByRole("button", { name: "Account" }).click();
    await second.page.getByRole("button", { name: "Sign out" }).click();

    await second.page.goto("/sign-up");
    await second.page.getByLabel("Name").fill("Copycat");
    await second.page.getByLabel("Email").fill(first.email.toUpperCase());
    await second.page.getByLabel("Password", { exact: true }).fill(PASSWORD);
    await second.page.getByRole("button", { name: "Sign up" }).click();

    await expect(
      second.page.getByRole("alert").filter({ hasText: "An account already uses that email." }),
    ).toBeVisible();
  });

  test("recovers a forgotten password through the emailed link", async ({ newUser }) => {
    const user = await newUser();
    const { page } = user;
    await page.getByRole("button", { name: "Account" }).click();
    await page.getByRole("button", { name: "Sign out" }).click();

    await page.getByRole("link", { name: "Forgot your password?" }).click();
    // The sign-in page has an Email field too: wait for the new page before filling one in.
    await expect(
      page.getByRole("heading", { level: 1, name: "Forgot your password?" }),
    ).toBeVisible();
    await page.getByLabel("Email").fill(user.email);
    await page.getByRole("button", { name: "Send the link" }).click();
    await expect(page.getByRole("main").getByRole("status")).toContainText("we've sent it a link");

    // The link from the email Mailpit caught, exactly as the person would open it.
    await page.goto(await resetLinkSentTo(user.email));
    await page.getByLabel("New password", { exact: true }).fill("a brand new passphrase");
    await page.getByRole("button", { name: "Save the new password" }).click();
    await expect(page.getByRole("main").getByRole("status")).toHaveText(
      "Your password is changed. Sign in with the new one.",
    );

    await user.signIn("a brand new passphrase");
    await page.getByRole("button", { name: "Account" }).click();
    await page.getByRole("button", { name: "Sign out" }).click();
    await page.getByLabel("Email").fill(user.email);
    await page.getByLabel("Password", { exact: true }).fill(PASSWORD);
    await page.getByRole("button", { name: "Sign in" }).click();
    await expect(
      page.getByRole("alert").filter({ hasText: "The email or password is wrong." }),
    ).toBeVisible();
  });
});
