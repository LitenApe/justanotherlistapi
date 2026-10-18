import { isGuid, normalizeGuid } from "../lib/guid";
import type { Session } from "./session";

/*
 * Local development signs in against the mock OAuth server started by Aspire
 * (proxied by Vite under /default). It uses the client credentials grant, where
 * the client ID becomes the token's `sub` claim, which the API uses as the user ID.
 * A production deployment should replace this with an authorization code + PKCE flow.
 */
const TOKEN_ENDPOINT = "/default/token";
const DEV_CLIENT_SECRET = "dev";
const DEFAULT_LIFETIME_SECONDS = 3600;

export const DEFAULT_USER_ID = "00000000-0000-0000-0000-000000000001";

interface TokenResponse {
  access_token: string;
  expires_in?: number;
}

export async function requestSession(userId: string): Promise<Session> {
  let response: Response;
  try {
    response = await fetch(TOKEN_ENDPOINT, {
      method: "POST",
      headers: { "Content-Type": "application/x-www-form-urlencoded" },
      body: new URLSearchParams({
        grant_type: "client_credentials",
        client_id: userId,
        client_secret: DEV_CLIENT_SECRET,
      }),
    });
  } catch {
    throw new Error("Can't reach the sign-in server. Check that it is running and try again.");
  }

  if (!response.ok) {
    throw new Error(`The sign-in server rejected the request (HTTP ${response.status}).`);
  }

  const token = (await response.json()) as TokenResponse;
  const claims = decodeJwtPayload(token.access_token);
  if (!isGuid(claims.sub)) {
    throw new Error("The sign-in server returned a token without a valid user ID.");
  }

  const expiresAt =
    typeof claims.exp === "number"
      ? claims.exp * 1000
      : Date.now() + (token.expires_in ?? DEFAULT_LIFETIME_SECONDS) * 1000;

  return { accessToken: token.access_token, userId: normalizeGuid(claims.sub), expiresAt };
}

function decodeJwtPayload(jwt: string): Record<string, unknown> {
  const payload = jwt.split(".")[1];
  if (!payload) return {};
  try {
    const base64 = payload.replace(/-/g, "+").replace(/_/g, "/");
    const bytes = Uint8Array.from(atob(base64), (c) => c.charCodeAt(0));
    return JSON.parse(new TextDecoder().decode(bytes)) as Record<string, unknown>;
  } catch {
    return {};
  }
}
