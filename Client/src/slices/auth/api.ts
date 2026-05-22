import { authResource } from "@shared/api";
import { authStore } from "@shared/api/authStore";

export async function login(
  clientId: string,
  clientSecret: string,
): Promise<void> {
  const response = await authResource.token(clientId, clientSecret);
  authStore.setToken(response.access_token);
}

export function logout(): void {
  authStore.clearToken();
}
