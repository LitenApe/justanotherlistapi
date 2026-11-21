import type { User } from "../auth/session";
import { apiRequest } from "./http";

/** The signed-in user's own account, under /api/me (specifications/accounts.md). */
export const accountApi = {
  /**
   * Changes what the signed-in user is called and returns them with the new
   * name. Other members see it the next time they load a list.
   */
  changeName: (name: string) => apiRequest<User>("PUT", "/api/me", { body: { name } }),
};
