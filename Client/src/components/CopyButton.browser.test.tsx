import { describe, expect, it } from "vitest";
import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import { CopyButton } from "./CopyButton";
import { ToastProvider } from "./Toasts";

describe("CopyButton", () => {
  it("copies the value to the clipboard and confirms it", async () => {
    await render(
      <ToastProvider>
        <CopyButton value="3f2504e0-4f89-41d3-9a0c-0305e82c3301" label="Copy user ID" />
      </ToastProvider>,
    );

    await page.getByRole("button", { name: "Copy user ID" }).click();

    await expect.element(page.getByRole("button", { name: "Copied" })).toBeVisible();
    expect(await navigator.clipboard.readText()).toBe("3f2504e0-4f89-41d3-9a0c-0305e82c3301");
    await expect.element(page.getByText("Copied to clipboard.")).toBeInTheDocument();
    // The confirmation reverts after two seconds.
    await expect
      .element(page.getByRole("button", { name: "Copy user ID" }), { timeout: 3_000 })
      .toBeVisible();
  });
});
