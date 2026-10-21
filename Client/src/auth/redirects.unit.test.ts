import { describe, expect, it } from "vitest";
import { pathOf, safeRedirect, signInPath } from "./redirects";

describe("redirects", () => {
  it("builds a sign-in path that returns to the requested page", () => {
    expect(signInPath("/lists/1?filter=done")).toBe(
      "/sign-in?redirectTo=%2Flists%2F1%3Ffilter%3Ddone",
    );
    expect(signInPath("/")).toBe("/sign-in");
  });

  it("takes the path and query from a request", () => {
    expect(pathOf(new Request("http://localhost:5173/lists/1?x=2#ignored"))).toBe("/lists/1?x=2");
  });

  it("only returns to pages on this site", () => {
    expect(safeRedirect("/lists/1?x=2")).toBe("/lists/1?x=2");
    expect(safeRedirect("https://evil.example")).toBe("/");
    expect(safeRedirect("//evil.example")).toBe("/");
    expect(safeRedirect("/\\evil.example")).toBe("/");
    expect(safeRedirect("lists/1")).toBe("/");
    expect(safeRedirect(null)).toBe("/");
  });
});
