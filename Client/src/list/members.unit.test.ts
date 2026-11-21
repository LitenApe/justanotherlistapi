import { describe, expect, it } from "vitest";
import type { Member } from "../api/types";
import { memberLabel, memberName, sortMembers } from "./members";

const ROBIN = "11111111-aaaa-4aaa-8aaa-000000000001";
const KIM = "22222222-bbbb-4bbb-8bbb-000000000002";
const OTHER_KIM = "33333333-cccc-4ccc-8ccc-000000000003";
const OLD_TIMER = "44444444-dddd-4ddd-8ddd-000000000004";

const members: Member[] = [
  { id: ROBIN, name: "Robin" },
  { id: KIM, name: "Kim" },
  { id: OLD_TIMER, name: null },
];

describe("memberName", () => {
  it("is the member's name", () => {
    expect(memberName(KIM, members)).toBe("Kim");
  });

  it("is the start of the user ID for a member without a name, or who isn't a member", () => {
    expect(memberName(OLD_TIMER, members)).toBe("44444444…");
    expect(memberName(OTHER_KIM, members)).toBe("33333333…");
  });

  it("adds the start of the user ID to names two members share, ignoring case", () => {
    const twoKims = [...members, { id: OTHER_KIM, name: "kim" }];

    expect(memberName(KIM, twoKims)).toBe("Kim (22222222…)");
    expect(memberName(OTHER_KIM, twoKims)).toBe("kim (33333333…)");
    expect(memberName(ROBIN, twoKims)).toBe("Robin");
  });
});

describe("memberLabel", () => {
  it("calls you 'you', and others by name", () => {
    expect(memberLabel(ROBIN, ROBIN, members)).toBe("you");
    expect(memberLabel(KIM, ROBIN, members)).toBe("Kim");
  });
});

describe("sortMembers", () => {
  it("puts you first, then everyone else by name", () => {
    const sorted = sortMembers(members, KIM).map((member) => member.id);
    expect(sorted).toEqual([KIM, OLD_TIMER, ROBIN]);
  });
});
