import { describe, expect, it } from "vitest";
import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import { expectNoAxeViolations } from "../test/axe";
import { PasswordField } from "./PasswordField";

describe("PasswordField", () => {
  it("hides the password until asked to show it, and back", async () => {
    await render(
      <PasswordField label="Password" name="password" autoComplete="current-password" />,
    );
    const input = page.getByLabelText("Password", { exact: true });
    const toggle = page.getByRole("button", { name: "Show password" });

    await input.fill("correct horse");
    await expect.element(input).toHaveAttribute("type", "password");
    await expect.element(toggle).toHaveAttribute("aria-pressed", "false");

    await toggle.click();
    await expect.element(input).toHaveAttribute("type", "text");
    await expect.element(input).toHaveValue("correct horse");
    await expect.element(toggle).toHaveAttribute("aria-pressed", "true");

    await toggle.click();
    await expect.element(input).toHaveAttribute("type", "password");
  });

  it("describes the field with its hint and its error", async () => {
    await render(
      <PasswordField
        label="Password"
        name="password"
        autoComplete="new-password"
        hint="At least 12 characters."
        error="Use at least 12 characters."
      />,
    );
    const input = page.getByLabelText("Password", { exact: true });

    await expect.element(input).toHaveAttribute("aria-invalid", "true");
    await expect
      .element(input)
      .toHaveAccessibleDescription("At least 12 characters. Use at least 12 characters.");
    await expect.element(input).toHaveAttribute("autocomplete", "new-password");
    await expectNoAxeViolations();
  });
});
