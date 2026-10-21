import { screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { installFakeApi, makeList, ME } from "../test/fakeApi";
import { expectNoAxeViolations, renderApp } from "../test/render";
import { safeRedirect } from "./redirects";
import { sessionStore } from "./session";

const USER_ID = "3f2504e0-4f89-41d3-9a0c-0305e82c3301";

function fakeJwt(claims: Record<string, unknown>) {
  const encode = (value: object) =>
    btoa(JSON.stringify(value)).replace(/=+$/, "").replace(/\+/g, "-").replace(/\//g, "_");
  return `${encode({ alg: "none" })}.${encode(claims)}.signature`;
}

/** Serves the mock OAuth token endpoint on top of the fake API. */
function installTokenEndpoint(api: ReturnType<typeof installFakeApi>) {
  const apiFetch = api.fetchMock.getMockImplementation();
  api.fetchMock.mockImplementation((input, init) => {
    if (input === "/default/token") {
      const body = init?.body as URLSearchParams;
      const exp = Math.floor(Date.now() / 1000) + 3600;
      const sub = body.get("client_id")?.toUpperCase();
      return Promise.resolve(Response.json({ access_token: fakeJwt({ sub, exp }) }));
    }
    return apiFetch ? apiFetch(input, init) : Promise.reject(new Error("no fetch"));
  });
}

describe("SignInPage", () => {
  it("redirects signed-out visitors to sign in and has no axe violations", async () => {
    const api = installFakeApi([makeList("Groceries")]);
    renderApp("/");

    expect(await screen.findByRole("heading", { level: 1, name: "Sign in" })).toBeInTheDocument();
    expect(document.title).toBe("Sign in – JustAnotherList");
    // The session check runs before page loaders, so nothing calls the API signed out.
    expect(api.calls).toEqual([]);
    await expectNoAxeViolations();
  });

  it("explains an invalid user ID next to the field and focuses it", async () => {
    const { user } = renderApp("/sign-in");
    const input = await screen.findByLabelText("User ID");

    await user.clear(input);
    await user.type(input, "not-a-guid");
    await user.click(screen.getByRole("button", { name: "Sign in" }));

    expect(input).toHaveAttribute("aria-invalid", "true");
    expect(input).toHaveAccessibleDescription(/Enter a user ID in GUID format/);
    expect(input).toHaveFocus();
    await expectNoAxeViolations();
  });

  it("signs in with the mock OAuth server and opens the lists", async () => {
    const api = installFakeApi();
    installTokenEndpoint(api);

    const { user } = renderApp("/sign-in");
    const input = await screen.findByLabelText("User ID");
    await user.clear(input);
    await user.type(input, USER_ID);
    await user.click(screen.getByRole("button", { name: "Sign in" }));

    expect(await screen.findByRole("heading", { level: 1, name: "Your lists" })).toBeVisible();
    expect(sessionStore.getSession()?.userId).toBe(USER_ID);
    const body = api.fetchMock.mock.calls[0]?.[1]?.body as URLSearchParams;
    expect(body.get("client_id")).toBe(USER_ID);
    expect(body.get("grant_type")).toBe("client_credentials");
  });

  it("returns to the page you asked for after signing in", async () => {
    const list = makeList("Groceries", ["Milk"]);
    const api = installFakeApi([list]);
    installTokenEndpoint(api);

    const { user } = renderApp(`/lists/${list.id}`);
    const input = await screen.findByLabelText("User ID");
    await user.clear(input);
    await user.type(input, ME);
    await user.click(screen.getByRole("button", { name: "Sign in" }));

    expect(await screen.findByRole("heading", { level: 1, name: "Groceries" })).toHaveFocus();
  });

  it("only returns to pages on this site", () => {
    expect(safeRedirect("/lists/1?x=2")).toBe("/lists/1?x=2");
    expect(safeRedirect("https://evil.example")).toBe("/");
    expect(safeRedirect("//evil.example")).toBe("/");
    expect(safeRedirect("/\\evil.example")).toBe("/");
    expect(safeRedirect(null)).toBe("/");
  });

  it("shows an alert when the sign-in server can't be reached", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(() => Promise.reject(new TypeError("Failed to fetch"))),
    );
    const { user } = renderApp("/sign-in");

    await user.click(await screen.findByRole("button", { name: "Sign in" }));

    const message = await screen.findByText(/Can't reach the sign-in server/);
    expect(message.closest("[role=alert]")).not.toBeNull();
  });

  it("generates a fresh user ID on request", async () => {
    const { user } = renderApp("/sign-in");
    const input = await screen.findByLabelText<HTMLInputElement>("User ID");
    const before = input.value;

    await user.click(screen.getByRole("button", { name: "Generate a new user ID" }));

    expect(input.value).not.toBe(before);
    expect(input.value).toMatch(/^[0-9a-f-]{36}$/);
  });
});
