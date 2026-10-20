export interface Item {
  id: string;
  name: string;
  description: string | null;
  isComplete: boolean;
  /** ISO timestamp set by the server when the item is checked; null while it's open. */
  completedAt: string | null;
  itemGroupId: string;
}

/** Shared by every member of a list. Null turns a setting off. */
export interface ItemGroupSettings {
  autoDeleteAfterMinutes: number | null;
  hideCompletedAfterMinutes: number | null;
  /** When auto-delete last changed; its countdown never starts before this. */
  autoDeleteUpdatedAt: string | null;
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
  "autoDeleteAfterMinutes" | "hideCompletedAfterMinutes"
>;
