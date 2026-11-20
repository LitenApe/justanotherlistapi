import { describe, expect, it } from "vitest";
import { ME, OTHER } from "../test/builders";
import { memberLabel, reservationText } from "./reservations";

const SAM = "5a3e9c71-2b4d-4e8f-9a01-b2c3d4e5f607";
const members = [ME, OTHER, SAM];

describe("memberLabel", () => {
  it("calls you “you” and shortens everyone else's user ID", () => {
    expect(memberLabel(ME, ME)).toBe("you");
    expect(memberLabel(SAM, ME)).toBe("5a3e9c71…");
  });
});

describe("reservationText", () => {
  const text = (reservedFor: string | null, reservedBy: string | null, among = members) =>
    reservationText({ reservedFor, reservedBy }, ME, among);

  it("says nothing about a free item", () => {
    expect(text(null, null)).toBeNull();
  });

  it("names only one person when they reserved the item for themselves", () => {
    expect(text(ME, ME)).toBe("Reserved by you");
    expect(text(SAM, SAM)).toBe("Reserved by 5a3e9c71…");
  });

  it("names both people when someone reserved the item for someone else", () => {
    expect(text(ME, SAM)).toBe("Reserved for you by 5a3e9c71…");
    expect(text(SAM, ME)).toBe("Reserved for 5a3e9c71… by you");
    expect(text(SAM, OTHER)).toBe("Reserved for 5a3e9c71… by 00000000…");
  });

  it("leaves out who reserved the item once they have left the list", () => {
    expect(text(ME, SAM, [ME, OTHER])).toBe("Reserved for you");
  });
});
