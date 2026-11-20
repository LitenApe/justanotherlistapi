export interface Item {
  id: string;
  name: string;
  description: string | null;
  isComplete: boolean;
  /** ISO timestamp set by the server when the item is checked; null while it's open. */
  completedAt: string | null;
  itemGroupId: string;
  /** The member who will do the item, or null while nobody has reserved it. */
  reservedFor: string | null;
  /** Who reserved it: the same as reservedFor when people reserve items for themselves. */
  reservedBy: string | null;
}

/** Shared by every member of a list. Null turns a setting off. */
export interface ItemGroupSettings {
  autoDeleteAfterMinutes: number | null;
  hideCompletedAfterMinutes: number | null;
  /** When auto-delete last changed; its countdown never starts before this. */
  autoDeleteUpdatedAt: string | null;
  /** Only the member an item is reserved for, and whoever reserved it, can change the reservation. */
  protectReservations: boolean;
}

/** A shared checklist. The API calls these "item groups". */
export interface ItemGroup {
  id: string;
  name: string;
  items: Item[];
  members: string[];
  settings: ItemGroupSettings;
}

export interface ItemInput {
  name: string;
  description: string | null;
  isComplete: boolean;
}

export type ItemGroupSettingsInput = Pick<
  ItemGroupSettings,
  "autoDeleteAfterMinutes" | "hideCompletedAfterMinutes" | "protectReservations"
>;
