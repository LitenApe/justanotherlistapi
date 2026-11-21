import type { Item, Member } from "../api/types";
import { memberLabel } from "./members";

/**
 * Whether the user may change or cancel the item's reservation, as the server
 * decides it (specifications/checklist.md#reservations): anyone may reserve a
 * free item, and any member may change a reservation, unless the list protects
 * reservations: then only the member it's for and whoever reserved it may.
 */
export function canChangeReservation(
  item: Pick<Item, "reservedFor" | "reservedBy">,
  userId: string,
  protectReservations: boolean,
): boolean {
  return (
    !protectReservations ||
    item.reservedFor === null ||
    item.reservedFor === userId ||
    item.reservedBy === userId
  );
}

/**
 * Who an item is reserved for, and by whom, as people read it: "Reserved by
 * you", "Reserved for you by Kim". Leaves out who reserved it when that member
 * reserved it for themselves or has left the list. Null while the item is free.
 */
export function reservationText(
  item: Pick<Item, "reservedFor" | "reservedBy">,
  userId: string,
  members: readonly Member[],
): string | null {
  const { reservedFor, reservedBy } = item;
  if (reservedFor === null) return null;
  const forWhom = memberLabel(reservedFor, userId, members);
  if (reservedBy === null || reservedBy === reservedFor) return `Reserved by ${forWhom}`;
  if (!members.some((member) => member.id === reservedBy)) return `Reserved for ${forWhom}`;
  return `Reserved for ${forWhom} by ${memberLabel(reservedBy, userId, members)}`;
}
