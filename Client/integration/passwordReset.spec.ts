import { expect, expectAccessible, ME, PASSWORD, test } from "./support/test";

const NEW_PASSWORD = "a brand new passphrase";

test.describe("forgotten passwords", () => {
  test("sends a link, saying the same whether or not an account uses the email", async ({
    page,
    backend,
  }) => {
    await page.goto("/sign-in");
    await page.getByRole("link", { name: "Forgot your password?" }).click();
    await expect(
      page.getByRole("heading", { level: 1, name: "Forgot your password?" }),
    ).toBeVisible();
    await expectAccessible(page);

    for (const email of ["robin@example.com", "nobody@example.com"]) {
      await page.getByLabel("Email").fill(email);
      await page.getByRole("button", { name: "Send the link" }).click();

      await expect(page.getByRole("main").getByRole("status")).toHaveText(
        `If an account uses ${email}, we've sent it a link to choose a new password. The link works for an hour.`,
      );
    }
    expect(backend.resetEmails.map((sent) => sent.email)).toEqual(["robin@example.com"]);
  });

  test("explains an email that isn't one, without sending anything", async ({ page, backend }) => {
    await page.goto("/forgot-password");
    const email = page.getByLabel("Email");

    await email.fill("robin");
    await page.getByRole("button", { name: "Send the link" }).click();

    await expect(email).toHaveAttribute("aria-invalid", "true");
    await expect(email).toHaveAccessibleDescription(
      /Enter an email address, like name@example\.com\./,
    );
    await expect(email).toBeFocused();
    expect(backend.calls).toEqual([]);
  });

  test("sets a new password from the emailed link, then signs in with it", async ({
    page,
    backend,
    app,
  }) => {
    await page.goto("/forgot-password");
    await page.getByLabel("Email").fill("robin@example.com");
    await page.getByRole("button", { name: "Send the link" }).click();
    await expect(page.getByRole("main").getByRole("status")).toContainText("we've sent it a link");
    const sent = backend.resetEmails.at(-1);

    await page.goto(`/reset-password?user=${ME}&code=${sent?.code ?? ""}`);
    await expect(
      page.getByRole("heading", { level: 1, name: "Choose a new password" }),
    ).toBeVisible();
    await expectAccessible(page);
    await page.getByLabel("New password", { exact: true }).fill(NEW_PASSWORD);
    await page.getByRole("button", { name: "Save the new password" }).click();

    await expect(page).toHaveURL("/sign-in?passwordChanged=1");
    await expect(page.getByRole("main").getByRole("status")).toHaveText(
      "Your password is changed. Sign in with the new one.",
    );
    expect(backend.accountById(ME).password).toBe(NEW_PASSWORD);
    await app.signIn();
  });

  test("says when the link has expired or was already used, and offers a new one", async ({
    page,
    backend,
  }) => {
    backend.resetEmails.push({
      email: "robin@example.com",
      userId: ME,
      code: "spent",
      used: true,
    });
    await page.goto(`/reset-password?user=${ME}&code=spent`);

    await page.getByLabel("New password", { exact: true }).fill(NEW_PASSWORD);
    await page.getByRole("button", { name: "Save the new password" }).click();

    const alert = page
      .getByRole("alert")
      .filter({ hasText: "This link has expired or was already used." });
    await expect(alert).toBeVisible();
    expect(backend.accountById(ME).password).toBe(PASSWORD);
    await alert.getByRole("link", { name: "Ask for a new link" }).click();
    await expect(page).toHaveURL("/forgot-password");
  });

  test("explains a link that's missing its code", async ({ page }) => {
    await page.goto(`/reset-password?user=${ME}`);

    await expect(
      page.getByRole("alert").filter({ hasText: "This link is incomplete." }),
    ).toBeVisible();
    await expect(page.getByLabel("New password", { exact: true })).toHaveCount(0);
  });

  test("explains a password that's too short next to its field", async ({ page, backend }) => {
    backend.resetEmails.push({ email: "robin@example.com", userId: ME, code: "c0de", used: false });
    await page.goto(`/reset-password?user=${ME}&code=c0de`);
    const password = page.getByLabel("New password", { exact: true });

    await password.fill("too short");
    await page.getByRole("button", { name: "Save the new password" }).click();

    await expect(password).toHaveAttribute("aria-invalid", "true");
    await expect(password).toHaveAccessibleDescription(/Use at least 12 characters\./);
    await expect(password).toBeFocused();
    await expectAccessible(page);
  });
});
