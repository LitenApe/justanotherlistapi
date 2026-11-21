import type { Session, User } from "../auth/session";
import { apiRequest } from "./http";

/**
 * The account endpoints under /api/auth (specifications/accounts.md). They need
 * no token, since they're how people get one. Each throws an ApiError whose
 * message can be shown as it is.
 */

interface SessionResponse {
  accessToken: string;
  /** ISO timestamp: the token's expiry. */
  expiresAt: string;
  user: User;
}

const toSession = ({ accessToken, expiresAt, user }: SessionResponse): Session => ({
  accessToken,
  expiresAt: Date.parse(expiresAt),
  user,
});

/** The account endpoints' rate limit: too many requests from this address. */
const TOO_MANY_REQUESTS = "Too many attempts from this device. Wait a minute and try again.";

export const authApi = {
  /** Creates an account and signs its owner in. 409: another account uses the email. */
  signUp: async (input: { name: string; email: string; password: string }) =>
    toSession(
      await apiRequest<SessionResponse>("POST", "/api/auth/sign-up", {
        body: input,
        anonymous: true,
        messages: { 409: "An account already uses that email.", 429: TOO_MANY_REQUESTS },
      }),
    ),

  /**
   * Asks for a link to choose a new password. The server answers the same
   * whether or not an account uses the email.
   */
  requestPasswordReset: (email: string) =>
    apiRequest<undefined>("POST", "/api/auth/forgot-password", {
      body: { email },
      anonymous: true,
      messages: { 429: TOO_MANY_REQUESTS },
    }),

  /** Sets a new password with the user ID and code from the emailed link. 400: the link is spent. */
  resetPassword: (input: { userId: string; code: string; password: string }) =>
    apiRequest<undefined>("POST", "/api/auth/reset-password", {
      body: input,
      anonymous: true,
      messages: {
        400: "This link has expired or was already used.",
        429: TOO_MANY_REQUESTS,
      },
    }),

  /** Signs in. 401: wrong email or password; 423: locked out for a few minutes. */
  signIn: async (input: { email: string; password: string }) =>
    toSession(
      await apiRequest<SessionResponse>("POST", "/api/auth/sign-in", {
        body: input,
        anonymous: true,
        messages: {
          401: "The email or password is wrong.",
          423: "Too many wrong passwords in a row. Try again in a few minutes.",
          429: TOO_MANY_REQUESTS,
        },
      }),
    ),
};
