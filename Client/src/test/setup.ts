import "@testing-library/jest-dom/vitest";
import { cleanup } from "@testing-library/react";
import { afterEach, vi } from "vitest";
import { sessionStore } from "../auth/session";

// Run the real hub logic over an in-memory connection (see fakeConnection.ts).
vi.mock("../realtime/hub", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../realtime/hub")>();
  const { buildFakeConnection } = await import("./fakeConnection");
  return { ...actual, checklistHub: actual.createChecklistHub(buildFakeConnection) };
});

// jsdom implements <dialog> without its methods.
if (typeof HTMLDialogElement.prototype.showModal !== "function") {
  Object.defineProperty(HTMLDialogElement.prototype, "open", {
    configurable: true,
    get(this: HTMLDialogElement) {
      return this.hasAttribute("open");
    },
  });
  HTMLDialogElement.prototype.showModal = function showModal(this: HTMLDialogElement) {
    this.setAttribute("open", "");
  };
  HTMLDialogElement.prototype.close = function close(this: HTMLDialogElement) {
    if (!this.hasAttribute("open")) return;
    this.removeAttribute("open");
    this.dispatchEvent(new Event("close"));
  };
}

afterEach(() => {
  cleanup();
  sessionStore.signOut("user");
  sessionStorage.clear();
  localStorage.clear();
  vi.unstubAllGlobals();
});
