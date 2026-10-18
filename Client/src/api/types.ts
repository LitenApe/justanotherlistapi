export interface Item {
  id: string;
  name: string;
  description: string | null;
  isComplete: boolean;
  itemGroupId: string;
}

/** A shared checklist. The API calls these "item groups". */
export interface ItemGroup {
  id: string;
  name: string;
  items: Item[];
  members: string[];
}

export interface ItemInput {
  name: string;
  description: string | null;
  isComplete: boolean;
}
