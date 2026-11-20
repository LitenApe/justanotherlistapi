import { describe, expect, it, vi } from "vitest";
import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import type { Item } from "../api/types";
import { expectNoAxeViolations } from "../test/axe";
import { makeItem, ME, OTHER } from "../test/builders";
import { ReservationDialog } from "./ReservationDialog";

async function renderDialog(item: Item) {
  const onSave = vi.fn(() => Promise.resolve());
  const onClose = vi.fn();
  await render(
    <ReservationDialog
      item={item}
      userId={ME}
      members={[ME, OTHER]}
      onSave={onSave}
      onClose={onClose}
    />,
  );
  return { onSave, onClose, dialog: page.getByRole("dialog", { name: "Reserve “Tent”" }) };
}

describe("ReservationDialog", () => {
  it("offers a free item to you first, so reserving it for yourself is one click", async () => {
    const tent = makeItem({ name: "Tent", itemGroupId: "g" });
    const { dialog, onSave } = await renderDialog(tent);

    await expect.element(dialog.getByText("Nobody has reserved it yet.")).toBeVisible();
    const you = dialog.getByRole("radio", { name: "You" });
    await expect.element(you).toBeChecked();
    await expect.element(you).toHaveFocus();
    await expect.element(dialog.getByRole("radio", { name: "Nobody" })).not.toBeInTheDocument();
    await expectNoAxeViolations(dialog.element());

    await dialog.getByRole("button", { name: "Save" }).click();

    expect(onSave).toHaveBeenCalledWith(tent, ME, null);
  });

  it("reserves the item for another member by their user ID", async () => {
    const tent = makeItem({ name: "Tent", itemGroupId: "g" });
    const { dialog, onSave } = await renderDialog(tent);

    await dialog.getByRole("radio", { name: OTHER }).click();
    await dialog.getByRole("button", { name: "Save" }).click();

    expect(onSave).toHaveBeenCalledWith(tent, OTHER, null);
  });

  it("starts from the current reservation, and can cancel it", async () => {
    const tent = makeItem({ name: "Tent", itemGroupId: "g", reservedFor: OTHER, reservedBy: ME });
    const { dialog, onSave } = await renderDialog(tent);

    await expect.element(dialog.getByText("Reserved for 00000000… by you.")).toBeVisible();
    const other = dialog.getByRole("radio", { name: OTHER });
    await expect.element(other).toBeChecked();
    await expect.element(other).toHaveFocus();
    await expectNoAxeViolations(dialog.element());

    await dialog.getByRole("radio", { name: "Nobody" }).click();
    await dialog.getByRole("button", { name: "Save" }).click();

    // What it replaces is who it was reserved for when the dialog opened.
    expect(onSave).toHaveBeenCalledWith(tent, null, OTHER);
  });

  it("closes without saving on Cancel", async () => {
    const { dialog, onSave, onClose } = await renderDialog(
      makeItem({ name: "Tent", itemGroupId: "g" }),
    );

    await dialog.getByRole("button", { name: "Cancel" }).click();

    expect(onClose).toHaveBeenCalled();
    expect(onSave).not.toHaveBeenCalled();
  });
});
