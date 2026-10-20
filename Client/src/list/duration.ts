export type DurationUnit = "minutes" | "hours" | "days";

export const DURATION_UNITS: readonly DurationUnit[] = ["minutes", "hours", "days"];

export const UNIT_MINUTES: Record<DurationUnit, number> = { minutes: 1, hours: 60, days: 24 * 60 };

/** Mirrors ItemGroupSettings on the server. */
export const MIN_AUTO_DELETE_MINUTES = 24 * 60;
export const MAX_SETTING_MINUTES = 365 * 24 * 60;

/** Splits minutes into the largest unit that divides them evenly. */
export function toDuration(minutes: number): { amount: number; unit: DurationUnit } {
  if (minutes > 0 && minutes % UNIT_MINUTES.days === 0) {
    return { amount: minutes / UNIT_MINUTES.days, unit: "days" };
  }
  if (minutes > 0 && minutes % UNIT_MINUTES.hours === 0) {
    return { amount: minutes / UNIT_MINUTES.hours, unit: "hours" };
  }
  return { amount: minutes, unit: "minutes" };
}

/** For example "1 day", "36 hours" or "90 minutes". */
export function formatDuration(minutes: number): string {
  const { amount, unit } = toDuration(minutes);
  return `${amount} ${amount === 1 ? unit.slice(0, -1) : unit}`;
}
