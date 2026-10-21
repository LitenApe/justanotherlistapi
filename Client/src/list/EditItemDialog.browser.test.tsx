import { describe, expect, it, vi } from "vitest";
import { page, userEvent } from "vitest/browser";
import { render } from "vitest-browser-react";
import { expectNoAxeViolations } from "../test/axe";
import { makeItem } from "../test/builders";
import { EditItemDialog } from "./EditItemDialog";

const milk = makeItem({ name: "Milk", itemGroupId: "list", description: "Oat" });

function renderDialog(overrides: Partial<Parameters<typeof EditItemDialog>[0]> = {}) {
  const props = {
    item: milk,
    onSave: vi.fn(() => Promise.resolve()),
    onDelete: vi.fn(),
    onClose: vi.fn(),
    ...overrides,
  };
  return { props, result: render(<EditItemDialog {...props} />) };
}

describe("EditItemDialog", () => {
  it("opens with the item's details and focus on the name", async () => {
    const { result } = renderDialog();
    await result;

    const dialog = page.getByRole("dialog", { name: "Edit item" });
    await expect.element(dialog.getByLabelText("Name")).toHaveValue("Milk");
    await expect.element(dialog.getByLabelText("Name")).toHaveFocus();
    await expect.element(dialog.getByLabelText(/Notes/)).toHaveValue("Oat");
    await expectNoAxeViolations(dialog.element());
  });

  it("saves trimmed changes and treats blank notes as none", async () => {
    const { props, result } = renderDialog();
    await result;

    await page.getByLabelText("Name").fill("  Oat milk  ");
    await page.getByLabelText(/Notes/).fill("   ");
    await page.getByRole("button", { name: "Save" }).click();

    expect(props.onSave).toHaveBeenCalledWith(milk, { name: "Oat milk", description: null });
  });

  it("explains a missing name next to the field and keeps focus there", async () => {
    const { props, result } = renderDialog();
    await result;
    const name = page.getByLabelText("Name");

    await name.clear();
    await userEvent.keyboard("{Enter}");

    await expect.element(name).toHaveAttribute("aria-invalid", "true");
    await expect.element(name).toHaveAccessibleDescription("The item needs a name.");
    await expect.element(name).toHaveFocus();
    expect(props.onSave).not.toHaveBeenCalled();
    await expectNoAxeViolations(page.getByRole("dialog").element());
  });

  it("stays open with the draft intact if saving fails", async () => {
    const { props, result } = renderDialog({
      onSave: vi.fn(() => Promise.reject(new Error("500"))),
    });
    await result;

    await page.getByLabelText("Name").fill("Oat milk");
    await page.getByRole("button", { name: "Save" }).click();

    expect(props.onSave).toHaveBeenCalledOnce();
    await expect.element(page.getByRole("button", { name: "Save" })).toBeVisible();
    await expect.element(page.getByLabelText("Name")).toHaveValue("Oat milk");
  });

  it("hands deletion to the caller, which asks for confirmation", async () => {
    const { props, result } = renderDialog();
    await result;

    await page.getByRole("button", { name: "Delete item" }).click();

    expect(props.onDelete).toHaveBeenCalledWith(milk);
  });
});
