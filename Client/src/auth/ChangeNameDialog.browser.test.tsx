import { describe, expect, it, vi } from "vitest";
import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import { expectNoAxeViolations } from "../test/axe";
import { ChangeNameDialog } from "./ChangeNameDialog";

async function renderDialog(onSave: (name: string) => Promise<string | null>) {
  const save = vi.fn(onSave);
  const onClose = vi.fn();
  await render(<ChangeNameDialog open currentName="Robin" onSave={save} onClose={onClose} />);
  return { onSave: save, onClose, dialog: page.getByRole("dialog", { name: "Change your name" }) };
}

describe("ChangeNameDialog", () => {
  it("starts with your name, focused, and saves the new one trimmed", async () => {
    const { dialog, onSave } = await renderDialog(() => Promise.resolve(null));
    const input = dialog.getByLabelText("Name");

    await expect.element(input).toHaveValue("Robin");
    await expect.element(input).toHaveFocus();
    await expectNoAxeViolations(dialog.element());

    await input.fill("  Robin Lee ");
    await dialog.getByRole("button", { name: "Save" }).click();

    expect(onSave).toHaveBeenCalledWith("Robin Lee");
  });

  it("explains a name that breaks the rule next to the field, without saving", async () => {
    const { dialog, onSave } = await renderDialog(() => Promise.resolve(null));
    const input = dialog.getByLabelText("Name");

    await input.fill("   ");
    await dialog.getByRole("button", { name: "Save" }).click();
    await expect.element(input).toHaveAccessibleDescription("Enter your name.");
    await expect.element(input).toHaveAttribute("aria-invalid", "true");

    await input.fill("a".repeat(51));
    await dialog.getByRole("button", { name: "Save" }).click();
    await expect.element(input).toHaveAccessibleDescription("Use at most 50 characters.");
    expect(onSave).not.toHaveBeenCalled();
    await expectNoAxeViolations(dialog.element());
  });

  it("shows the problem saving ran into, and lets you try again", async () => {
    const results = ["Couldn't change your name. Try again.", null];
    const { dialog, onSave } = await renderDialog(() => Promise.resolve(results.shift() ?? null));
    const input = dialog.getByLabelText("Name");

    await dialog.getByRole("button", { name: "Save" }).click();
    await expect.element(input).toHaveAccessibleDescription(/Couldn't change your name/);
    await expect.element(input).toHaveFocus();

    await dialog.getByRole("button", { name: "Save" }).click();
    expect(onSave).toHaveBeenCalledTimes(2);
  });
});
