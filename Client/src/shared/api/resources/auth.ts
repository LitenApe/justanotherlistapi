import type { ApiClient } from "@shared/api/client";
import { apiClient } from "@shared/api/client";

export interface TokenResponse {
  access_token: string;
  token_type: string;
  expires_in: number;
}

export interface AuthResource {
  token(clientId: string, clientSecret: string): Promise<TokenResponse>;
}

export function createAuthResource(_client: ApiClient): AuthResource {
  return {
    token(clientId, clientSecret) {
      const body = new URLSearchParams({
        grant_type: "client_credentials",
        client_id: clientId,
        client_secret: clientSecret,
      });

      return fetch("/default/token", {
        method: "POST",
        headers: { "Content-Type": "application/x-www-form-urlencoded" },
        body,
      }).then(async (res) => {
        if (!res.ok) {
          const text = await res.text().catch(() => res.statusText);
          throw new Error(`Token request failed: ${text}`);
        }
        return res.json() as Promise<TokenResponse>;
      });
    },
  };
}

export const authResource: AuthResource = createAuthResource(apiClient);
