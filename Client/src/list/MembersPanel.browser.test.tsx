import { describe, expect, it, vi } from "vitest";
import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import { ToastProvider } from "../components/Toasts";
import { expectNoAxeViolations } from "../test/axe";
import { ME, OTHER } from "../test/builders";
import { MembersPanel } from "./MembersPanel";

async function renderPanel(
  members: string[],
  onAdd: (memberId: string) => Promise<string | null> = () => Promise.resolve(null),
) {
  const onRemove = vi.fn();
  const add = vi.fn(onAdd);
  await render(
    <ToastProvider>
      <MembersPanel members={members} currentUserId={ME} onAdd={add} onRemove={onRemove} />
    </ToastProvider>,
  );
  return { onAdd: add, onRemove, region: page.getByRole("region", { name: /Members/ }) };
}

describe("MembersPanel", () => {
  it("lists you first, and offers to remove others or leave", async () => {
    const { region, onRemove } = await renderPanel([OTHER, ME]);

    const ids = region
      .getByRole("listitem")
      .elements()
      .map((item) => item.textContent);
    expect(ids[0]).toContain(ME);
    await expect.element(region.getByText("You")).toBeVisible();
    await expectNoAxeViolations(region.element());

    await region.getByRole("button", { name: `Remove member ${OTHER}` }).click();
    expect(onRemove).toHaveBeenCalledWith(OTHER);
    await region.getByRole("button", { name: "Leave list" }).click();
    expect(onRemove).toHaveBeenCalledWith(ME);
  });

  it("doesn't offer to leave when you're the only member", async () => {
    const { region } = await renderPanel([ME]);
    await expect
      .element(region.getByRole("button", { name: "Leave list" }))
      .not.toBeInTheDocument();
  });

  it("checks the user ID before adding, with the error next to the field", async () => {
    const { region, onAdd } = await renderPanel([ME, OTHER]);
    const input = region.getByLabelText("Add a member");

    await input.fill("not-a-guid");
    await region.getByRole("button", { name: "Add member" }).click();
    await expect.element(input).toHaveAccessibleDescription(/Enter a user ID in GUID format/);
    await expect.element(input).toHaveFocus();

    await input.fill(OTHER.toUpperCase());
    await region.getByRole("button", { name: "Add member" }).click();
    await expect.element(input).toHaveAccessibleDescription(/That person is already a member/);
    expect(onAdd).not.toHaveBeenCalled();
    await expectNoAxeViolations(region.element());
  });

  it("adds normalized IDs, shows errors the caller reports and clears on success", async () => {
    const results = ["Couldn't add the member. Try again.", null];
    const { region, onAdd } = await renderPanel([ME], () =>
      Promise.resolve(results.shift() ?? null),
    );
    const input = region.getByLabelText("Add a member");
    const newMember = "3F2504E0-4F89-41D3-9A0C-0305E82C3301";

    await input.fill(newMember);
    await region.getByRole("button", { name: "Add member" }).click();
    expect(onAdd).toHaveBeenCalledWith(newMember.toLowerCase());
    await expect.element(input).toHaveAccessibleDescription(/Couldn't add the member/);
    await expect.element(input).toHaveValue(newMember);

    await region.getByRole("button", { name: "Add member" }).click();
    await expect.element(input).toHaveValue("");
  });
});
