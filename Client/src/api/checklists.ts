import { apiRequest } from "./http";
import type {
  Item,
  ItemGroup,
  ItemGroupSettings,
  ItemGroupSettingsInput,
  ItemInput,
} from "./types";

const listPath = (listId: string) => `/api/list/${encodeURIComponent(listId)}`;
const itemPath = (listId: string, itemId: string) =>
  `${listPath(listId)}/${encodeURIComponent(itemId)}`;
const memberPath = (listId: string, memberId: string) =>
  `${listPath(listId)}/member/${encodeURIComponent(memberId)}`;

export const checklistApi = {
  getLists: (signal?: AbortSignal) =>
    apiRequest<ItemGroup[]>("GET", "/api/list", signal ? { signal } : {}),

  getList: (listId: string, signal?: AbortSignal) =>
    apiRequest<ItemGroup>("GET", listPath(listId), signal ? { signal } : {}),

  createList: (name: string) => apiRequest<ItemGroup>("POST", "/api/list", { body: { name } }),

  renameList: (listId: string, name: string) =>
    apiRequest<undefined>("PUT", listPath(listId), { body: { name } }),

  deleteList: (listId: string) => apiRequest<undefined>("DELETE", listPath(listId)),

  updateSettings: (listId: string, input: ItemGroupSettingsInput) =>
    apiRequest<ItemGroupSettings>("PUT", `${listPath(listId)}/settings`, { body: input }),

  createItem: (listId: string, input: ItemInput) =>
    apiRequest<Item>("POST", listPath(listId), { body: input }),

  updateItem: (listId: string, itemId: string, input: ItemInput) =>
    apiRequest<undefined>("PUT", itemPath(listId, itemId), { body: input }),

  deleteItem: (listId: string, itemId: string) =>
    apiRequest<undefined>("DELETE", itemPath(listId, itemId)),

  addMember: (listId: string, memberId: string) =>
    apiRequest<undefined>("POST", memberPath(listId, memberId)),

  removeMember: (listId: string, memberId: string) =>
    apiRequest<undefined>("DELETE", memberPath(listId, memberId)),
};

export const toItemInput = ({ name, description, isComplete }: Item): ItemInput => ({
  name,
  description,
  isComplete,
});
