import { afterEach, describe, expect, it, vi } from "vitest";
import { page, userEvent } from "vitest/browser";
import { render } from "vitest-browser-react";
import { expectNoAxeViolations } from "../test/axe";
import { ToastProvider, useToast } from "./Toasts";

function Triggers() {
  const { notify, announce } = useToast();
  return (
    <main>
      <button
        type="button"
        onClick={() => {
          notify("Saved “Milk”.");
        }}
      >
        Info
      </button>
      <button
        type="button"
        onClick={() => {
          notify("Couldn't save “Milk”.", "error");
        }}
      >
        Error
      </button>
      <button
        type="button"
        onClick={() => {
          announce("Added “Milk”.");
        }}
      >
        Announce
      </button>
    </main>
  );
}

const renderToasts = () =>
  render(
    <ToastProvider>
      <Triggers />
    </ToastProvider>,
  );

/** Fakes only the dismissal timers; clicks, rendering and axe use the real clock. */
const fakeDismissalTimers = () => {
  vi.useFakeTimers({ toFake: ["setTimeout", "clearTimeout"] });
};

describe("Toasts", () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it("shows information in a status region", async () => {
    await renderToasts();
    await page.getByRole("button", { name: "Info" }).click();

    const message = page.getByText("Saved “Milk”.");
    await expect.element(message).toBeVisible();
    const region = message.element().closest("[role=status]");
    expect(region).not.toBeNull();
    if (region) await expectNoAxeViolations(region);
  });

  it("removes information after six seconds", async () => {
    fakeDismissalTimers();
    await renderToasts();
    await page.getByRole("button", { name: "Info" }).click();
    const message = page.getByText("Saved “Milk”.");
    await expect.element(message).toBeVisible();

    vi.advanceTimersByTime(5_999);
    await expect.element(message).toBeVisible();
    vi.advanceTimersByTime(1);
    await expect.element(message).not.toBeInTheDocument();
  });

  it("keeps a message on screen while the pointer is over it", async () => {
    fakeDismissalTimers();
    await renderToasts();
    await page.getByRole("button", { name: "Info" }).click();
    const message = page.getByText("Saved “Milk”.");

    await userEvent.hover(message);
    vi.advanceTimersByTime(6_000);
    await expect.element(message).toBeVisible();

    await userEvent.unhover(message);
    vi.advanceTimersByTime(6_000);
    await expect.element(message).not.toBeInTheDocument();
  });

  it("keeps errors in an alert region until they're dismissed", async () => {
    fakeDismissalTimers();
    await renderToasts();
    await page.getByRole("button", { name: "Error" }).click();

    const alert = page.getByRole("alert");
    await expect.element(alert.getByText("Couldn't save “Milk”.")).toBeVisible();
    vi.advanceTimersByTime(60_000);
    await expect.element(alert.getByText("Couldn't save “Milk”.")).toBeVisible();

    await alert.getByRole("button", { name: "Dismiss" }).click();
    await expect.element(page.getByText("Couldn't save “Milk”.")).not.toBeInTheDocument();
  });

  it("shows at most three messages, dropping the oldest", async () => {
    await renderToasts();
    const info = page.getByRole("button", { name: "Info" });
    for (let i = 0; i < 4; i++) await info.click();

    expect(page.getByText("Saved “Milk”.").elements()).toHaveLength(3);
  });

  it("announces to screen readers without showing anything", async () => {
    await renderToasts();
    await page.getByRole("button", { name: "Announce" }).click();

    const announcement = page.getByText("Added “Milk”.");
    await expect.element(announcement).toBeInTheDocument();
    const region = announcement.element().closest("[role=status]");
    // A separate live region from the visible toasts, clipped to nothing on screen.
    expect(region?.querySelector(".toast, button")).toBeNull();
    const box = region?.getBoundingClientRect();
    expect((box?.width ?? 0) * (box?.height ?? 0)).toBeLessThanOrEqual(1);
  });
});
