import { describe, expect, it, vi } from "vitest";
import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import { ToastProvider } from "../components/Toasts";
import { expectNoAxeViolations } from "../test/axe";
import type { Member } from "../api/types";
import { member, ME, OTHER } from "../test/builders";
import { MembersPanel } from "./MembersPanel";

async function renderPanel(
  members: Member[],
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
    const { region, onRemove } = await renderPanel([member(OTHER), member(ME)]);

    const rows = region
      .getByRole("listitem")
      .elements()
      .map((item) => item.textContent);
    expect(rows[0]).toContain("Robin");
    expect(rows[1]).toContain("Kim");
    await expect.element(region.getByText("You")).toBeVisible();
    await expectNoAxeViolations(region.element());

    await region.getByRole("button", { name: "Remove member Kim" }).click();
    expect(onRemove).toHaveBeenCalledWith(OTHER);
    await region.getByRole("button", { name: "Leave list" }).click();
    expect(onRemove).toHaveBeenCalledWith(ME);
  });

  it("doesn't offer to leave when you're the only member", async () => {
    const { region } = await renderPanel([member(ME)]);
    await expect
      .element(region.getByRole("button", { name: "Leave list" }))
      .not.toBeInTheDocument();
  });

  it("asks for an email before adding, with the error next to the field", async () => {
    const { region, onAdd } = await renderPanel([member(ME), member(OTHER)]);
    const input = region.getByLabelText("Add a member");

    await region.getByRole("button", { name: "Add member" }).click();
    await expect.element(input).toHaveAccessibleDescription(/Enter their email address\./);
    await expect.element(input).toHaveFocus();

    await input.fill("kim");
    await region.getByRole("button", { name: "Add member" }).click();
    await expect
      .element(input)
      .toHaveAccessibleDescription(/Enter an email address, like name@example\.com\./);
    expect(onAdd).not.toHaveBeenCalled();
    await expectNoAxeViolations(region.element());
  });

  it("adds by email, shows the problems the caller reports, and clears on success", async () => {
    const results = ["No one has an account with that email.", null];
    const { region, onAdd } = await renderPanel([member(ME)], () =>
      Promise.resolve(results.shift() ?? null),
    );
    const input = region.getByLabelText("Add a member");

    await input.fill("sam@example.com");
    await region.getByRole("button", { name: "Add member" }).click();
    expect(onAdd).toHaveBeenCalledWith("sam@example.com");
    await expect
      .element(input)
      .toHaveAccessibleDescription(/No one has an account with that email/);
    await expect.element(input).toHaveValue("sam@example.com");

    await region.getByRole("button", { name: "Add member" }).click();
    await expect.element(input).toHaveValue("");
  });

  it("shows names only: no user IDs and no emails", async () => {
    const { region } = await renderPanel([member(ME), member(OTHER)]);

    await expect.element(region).not.toHaveTextContent(ME);
    await expect.element(region).not.toHaveTextContent(OTHER);
    await expect.element(region.getByText("Kim", { exact: true })).toBeVisible();
  });
});
