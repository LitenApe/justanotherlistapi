import { describe, expect, it } from "vitest";
import { isGuid, newGuid, normalizeGuid } from "./guid";

describe("guid", () => {
  it("accepts GUIDs in either case, with surrounding spaces", () => {
    expect(isGuid("3f2504e0-4f89-41d3-9a0c-0305e82c3301")).toBe(true);
    expect(isGuid("3F2504E0-4F89-41D3-9A0C-0305E82C3301")).toBe(true);
    expect(isGuid("  3f2504e0-4f89-41d3-9a0c-0305e82c3301 ")).toBe(true);
  });

  it("rejects anything that isn't a GUID", () => {
    expect(isGuid("not-a-guid")).toBe(false);
    expect(isGuid("3f2504e0-4f89-41d3-9a0c-0305e82c330")).toBe(false);
    expect(isGuid("{3f2504e0-4f89-41d3-9a0c-0305e82c3301}")).toBe(false);
    expect(isGuid(undefined)).toBe(false);
    expect(isGuid(42)).toBe(false);
  });

  it("normalizes to the lowercase form the API returns", () => {
    expect(normalizeGuid(" 3F2504E0-4F89-41D3-9A0C-0305E82C3301 ")).toBe(
      "3f2504e0-4f89-41d3-9a0c-0305e82c3301",
    );
  });

  it("generates valid, distinct GUIDs", () => {
    const [a, b] = [newGuid(), newGuid()];
    expect(isGuid(a)).toBe(true);
    expect(a).not.toBe(b);
  });
});
