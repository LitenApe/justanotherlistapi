import { describe, expect, it } from "vitest";
import { formatDuration, toDuration } from "./duration";

describe("duration", () => {
  it("uses the largest unit that divides the minutes evenly", () => {
    expect(toDuration(2 * 24 * 60)).toEqual({ amount: 2, unit: "days" });
    expect(toDuration(36 * 60)).toEqual({ amount: 36, unit: "hours" });
    expect(toDuration(90)).toEqual({ amount: 90, unit: "minutes" });
    expect(toDuration(0)).toEqual({ amount: 0, unit: "minutes" });
  });

  it("formats singular and plural units", () => {
    expect(formatDuration(24 * 60)).toBe("1 day");
    expect(formatDuration(60)).toBe("1 hour");
    expect(formatDuration(1)).toBe("1 minute");
    expect(formatDuration(7 * 24 * 60)).toBe("7 days");
  });
});
