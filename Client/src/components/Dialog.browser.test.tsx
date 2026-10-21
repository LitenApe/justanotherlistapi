import { useState } from "react";
import { describe, expect, it, vi } from "vitest";
import { page, userEvent } from "vitest/browser";
import { render } from "vitest-browser-react";
import { expectNoAxeViolations } from "../test/axe";
import { ConfirmDialog } from "./ConfirmDialog";
import { Dialog, DialogActions } from "./Dialog";

/** A dialog as the app uses them, with the app's own field and button styles. */
function RenameExample({ onClose }: { onClose: () => void }) {
  const [open, setOpen] = useState(false);
  return (
    <main>
      <button
        type="button"
        className="button"
        onClick={() => {
          setOpen(true);
        }}
      >
        Rename
      </button>
      <Dialog
        open={open}
        title="Rename list"
        description="Everyone in the list sees the new name."
        onClose={() => {
          setOpen(false);
          onClose();
        }}
      >
        <label className="label" htmlFor="name">
          Name
        </label>
        <input id="name" className="input" data-autofocus />
        <DialogActions>
          <button
            type="button"
            className="button"
            onClick={() => {
              setOpen(false);
            }}
          >
            Cancel
          </button>
        </DialogActions>
      </Dialog>
    </main>
  );
}

describe("Dialog", () => {
  it("opens as a modal, named and described, with focus on the marked field", async () => {
    const screen = await render(<RenameExample onClose={vi.fn()} />);

    await screen.getByRole("button", { name: "Rename" }).click();

    const dialog = page.getByRole("dialog", { name: "Rename list" });
    await expect.element(dialog).toBeVisible();
    await expect
      .element(dialog)
      .toHaveAccessibleDescription("Everyone in the list sees the new name.");
    await expect.element(page.getByLabelText("Name")).toHaveFocus();
    expect(dialog.element().matches(":modal")).toBe(true);
    await expectNoAxeViolations(dialog.element());
  });

  it("closes on Escape and returns focus to the button that opened it", async () => {
    const onClose = vi.fn();
    const screen = await render(<RenameExample onClose={onClose} />);
    const opener = screen.getByRole("button", { name: "Rename" });
    await opener.click();
    await expect.element(page.getByLabelText("Name")).toHaveFocus();

    await userEvent.keyboard("{Escape}");

    await expect.element(page.getByRole("dialog")).not.toBeInTheDocument();
    expect(onClose).toHaveBeenCalledOnce();
    await expect.element(opener).toHaveFocus();
  });

  it("makes the rest of the page inert while open", async () => {
    const screen = await render(<RenameExample onClose={vi.fn()} />);
    const opener = screen.getByRole("button", { name: "Rename" });
    await opener.click();

    // Tabbing cycles within the dialog and never reaches the page behind it.
    for (let i = 0; i < 4; i++) {
      await userEvent.keyboard("{Tab}");
      expect(opener.element()).not.toBe(document.activeElement);
    }
  });
});

describe("ConfirmDialog", () => {
  it("starts on Cancel, the safe choice", async () => {
    const onCancel = vi.fn();
    await render(
      <ConfirmDialog
        open
        title="Delete “Milk”?"
        description="This permanently deletes the item for every member."
        confirmLabel="Delete item"
        onConfirm={vi.fn()}
        onCancel={onCancel}
      />,
    );

    const cancel = page.getByRole("button", { name: "Cancel" });
    await expect.element(cancel).toHaveFocus();
    const dialog = page.getByRole("dialog", { name: "Delete “Milk”?" });
    await expect
      .element(dialog)
      .toHaveAccessibleDescription("This permanently deletes the item for every member.");
    await expectNoAxeViolations(dialog.element());

    await cancel.click();
    expect(onCancel).toHaveBeenCalledOnce();
  });

  it("confirms once and ignores repeat clicks while the action is pending", async () => {
    const onConfirm = vi.fn();
    const props = {
      open: true,
      title: "Delete “Groceries”?",
      description: "This permanently deletes the list.",
      confirmLabel: "Delete list",
      onConfirm,
      onCancel: vi.fn(),
    };
    const screen = await render(<ConfirmDialog {...props} />);

    await page.getByRole("button", { name: "Delete list" }).click();
    expect(onConfirm).toHaveBeenCalledOnce();

    await screen.rerender(<ConfirmDialog {...props} pending />);
    const working = page.getByRole("button", { name: "Working…" });
    await expect.element(working).toHaveAttribute("aria-disabled", "true");
    // It still looks and acts like a button (it may hold focus), so click it anyway.
    await working.click({ force: true });
    expect(onConfirm).toHaveBeenCalledOnce();
  });
});
