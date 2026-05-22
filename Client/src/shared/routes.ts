export const routes = {
  home: () => "/" as const,
  checklist: (groupId: string) => `/${groupId}` as const,
  itemCreate: (groupId: string) => `/${groupId}/items/new` as const,
  itemEdit: (groupId: string, itemId: string) =>
    `/${groupId}/items/${itemId}` as const,
  login: () => "/login" as const,
} as const;
