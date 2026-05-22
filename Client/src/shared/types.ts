export interface ItemGroup {
  id: string;
  name: string;
  items: Item[];
  members: string[];
}

export interface Item {
  id: string;
  name: string;
  description: string | null;
  isComplete: boolean;
  itemGroupId: string;
}
