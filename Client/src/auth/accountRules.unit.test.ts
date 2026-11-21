import { describe, expect, it } from "vitest";
import { emailProblem, isEmailAddress, nameProblem, newPasswordProblem } from "./accountRules";

describe("nameProblem", () => {
  it("asks for a name when there's nothing but spaces", () => {
    expect(nameProblem("   ")).toBe("Enter your name.");
  });

  it("accepts up to 50 characters once trimmed", () => {
    expect(nameProblem(`  ${"a".repeat(50)}  `)).toBeNull();
    expect(nameProblem("a".repeat(51))).toBe("Use at most 50 characters.");
  });
});

describe("isEmailAddress", () => {
  it.each(["kim@example.com", "Kim.Lee+lists@sub.example.org", "a@b", "  kim@example.com "])(
    "accepts %s",
    (value) => {
      expect(isEmailAddress(value)).toBe(true);
    },
  );

  it.each(["", "kim", "@example.com", "kim@", "kim@@example.com", "kim lee@example.com"])(
    "refuses %j",
    (value) => {
      expect(isEmailAddress(value)).toBe(false);
    },
  );

  it("refuses addresses longer than 254 characters", () => {
    const domain = "@example.com";
    const longest = "k".repeat(254 - domain.length) + domain;
    expect(isEmailAddress(longest)).toBe(true);
    expect(isEmailAddress("k" + longest)).toBe(false);
  });
});

describe("emailProblem", () => {
  it("tells an empty field from a malformed one", () => {
    expect(emailProblem(" ")).toBe("Enter your email address.");
    expect(emailProblem("kim")).toBe("Enter an email address, like name@example.com.");
    expect(emailProblem("kim@example.com")).toBeNull();
  });
});

describe("newPasswordProblem", () => {
  it("accepts 12 to 128 characters, spaces included", () => {
    expect(newPasswordProblem("elevenchars")).toBe("Use at least 12 characters.");
    expect(newPasswordProblem("twelve chars")).toBeNull();
    expect(newPasswordProblem(" ".repeat(128))).toBeNull();
    expect(newPasswordProblem("p".repeat(129))).toBe("Use at most 128 characters.");
  });
});
