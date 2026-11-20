import type { Item } from "../api/types";

/**
 * How a member is shown: "you", or the start of their user ID. Members have no
 * names yet; when they do, this is the one place to show them.
 */
export function memberLabel(memberId: string, userId: string): string {
  return memberId === userId ? "you" : `${memberId.slice(0, 8)}…`;
}

/**
 * Who an item is reserved for, and by whom, as people read it: "Reserved by
 * you", "Reserved for you by 3fa85f64…". Leaves out who reserved it when that
 * member reserved it for themselves or has left the list. Null while the item
 * is free.
 */
export function reservationText(
  item: Pick<Item, "reservedFor" | "reservedBy">,
  userId: string,
  members: readonly string[],
): string | null {
  const { reservedFor, reservedBy } = item;
  if (reservedFor === null) return null;
  const forWhom = memberLabel(reservedFor, userId);
  if (reservedBy === null || reservedBy === reservedFor) return `Reserved by ${forWhom}`;
  if (!members.includes(reservedBy)) return `Reserved for ${forWhom}`;
  return `Reserved for ${forWhom} by ${memberLabel(reservedBy, userId)}`;
}
