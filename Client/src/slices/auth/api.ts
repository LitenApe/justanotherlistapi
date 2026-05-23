import { authStore } from "@shared/api/authStore";

export async function login(
  clientId: string,
  clientSecret: string,
): Promise<void> {
  const body = new URLSearchParams({
    grant_type: "client_credentials",
    client_id: clientId,
    client_secret: clientSecret,
  });

  const res = await fetch("/default/token", {
    method: "POST",
    headers: { "Content-Type": "application/x-www-form-urlencoded" },
    body,
  });

  if (!res.ok) {
    const text = await res.text().catch(() => res.statusText);
    throw new Error(`Token request failed: ${text}`);
  }

  const response = (await res.json()) as { access_token: string };
  authStore.setToken(response.access_token);
}

export function logout(): void {
  authStore.clearToken();
}
