import { describe, expect, it, vi } from "vitest";
import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import type { ItemGroupSettings } from "../api/types";
import { expectNoAxeViolations } from "../test/axe";
import { defaultSettings } from "../test/builders";
import { ListSettingsDialog } from "./ListSettingsDialog";

async function renderDialog(settings: ItemGroupSettings = defaultSettings()) {
  const onSave = vi.fn(() => Promise.resolve());
  await render(<ListSettingsDialog open settings={settings} onSave={onSave} onClose={vi.fn()} />);
  return { onSave, dialog: page.getByRole("dialog", { name: "List settings" }) };
}

describe("ListSettingsDialog", () => {
  it("starts with both settings off and focus on the first one", async () => {
    const { dialog } = await renderDialog();

    await expect
      .element(dialog)
      .toHaveAccessibleDescription("These settings apply to everyone in this list.");
    const hide = dialog.getByRole("checkbox", { name: "Hide done items" });
    await expect.element(hide).toHaveFocus();
    await expect.element(hide).not.toBeChecked();
    await expect.element(dialog.getByRole("group")).not.toBeInTheDocument();
    await expectNoAxeViolations(dialog.element());
  });

  it("reveals a duration when a setting is turned on, starting from sensible defaults", async () => {
    const { dialog } = await renderDialog();

    await dialog.getByRole("checkbox", { name: "Hide done items" }).click();
    const hideAfter = dialog.getByRole("group", { name: "Hide after" });
    await expect.element(hideAfter.getByLabelText("Amount")).toHaveValue("1");
    await expect.element(hideAfter.getByLabelText("Unit")).toHaveValue("days");

    await dialog.getByRole("checkbox", { name: "Delete the list when every item is done" }).click();
    const deleteAfter = dialog.getByRole("group", { name: "Delete after" });
    await expect.element(deleteAfter.getByLabelText("Amount")).toHaveValue("7");
    await expectNoAxeViolations(dialog.element());
  });

  it("shows stored minutes in the largest unit that fits", async () => {
    const { dialog } = await renderDialog({
      hideCompletedAfterMinutes: 90,
      autoDeleteAfterMinutes: 2 * 24 * 60,
      autoDeleteUpdatedAt: null,
    });

    const hideAfter = dialog.getByRole("group", { name: "Hide after" });
    await expect.element(hideAfter.getByLabelText("Amount")).toHaveValue("90");
    await expect.element(hideAfter.getByLabelText("Unit")).toHaveValue("minutes");
    const deleteAfter = dialog.getByRole("group", { name: "Delete after" });
    await expect.element(deleteAfter.getByLabelText("Amount")).toHaveValue("2");
    await expect.element(deleteAfter.getByLabelText("Unit")).toHaveValue("days");
  });

  it("enforces the one-day auto-delete minimum, then saves in minutes", async () => {
    const { dialog, onSave } = await renderDialog();
    await dialog.getByRole("checkbox", { name: "Hide done items" }).click();
    const hideAfter = dialog.getByRole("group", { name: "Hide after" });
    await hideAfter.getByLabelText("Amount").fill("2");
    await hideAfter.getByLabelText("Unit").selectOptions("hours");

    await dialog.getByRole("checkbox", { name: "Delete the list when every item is done" }).click();
    const deleteAfter = dialog.getByRole("group", { name: "Delete after" });
    const deleteAmount = deleteAfter.getByLabelText("Amount");
    await deleteAmount.fill("12");
    await deleteAfter.getByLabelText("Unit").selectOptions("hours");
    await dialog.getByRole("button", { name: "Save" }).click();

    await expect.element(deleteAmount).toHaveFocus();
    await expect.element(deleteAmount).toBeInvalid();
    await expect.element(deleteAmount).toHaveAccessibleDescription(/Choose at least 1 day/);
    expect(onSave).not.toHaveBeenCalled();
    await expectNoAxeViolations(dialog.element());

    await deleteAfter.getByLabelText("Unit").selectOptions("days");
    await dialog.getByRole("button", { name: "Save" }).click();

    expect(onSave).toHaveBeenCalledWith({
      hideCompletedAfterMinutes: 120,
      autoDeleteAfterMinutes: 12 * 24 * 60,
    });
  });

  it("rejects amounts that aren't whole numbers or exceed a year", async () => {
    const { dialog, onSave } = await renderDialog();
    await dialog.getByRole("checkbox", { name: "Hide done items" }).click();
    const amount = dialog.getByRole("group", { name: "Hide after" }).getByLabelText("Amount");

    await amount.fill("1.5");
    await dialog.getByRole("button", { name: "Save" }).click();
    await expect.element(amount).toHaveAccessibleDescription("Enter a whole number.");

    await amount.fill("366");
    await dialog.getByRole("button", { name: "Save" }).click();
    await expect.element(amount).toHaveAccessibleDescription("Choose 365 days or less.");
    expect(onSave).not.toHaveBeenCalled();
  });
});
