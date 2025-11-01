'use server';

import {
  getSessionCookie,
  removeSessionCookie,
} from '../cookie/Cookie.service';
import { getTokens, removeTokens, updateTokens } from './TokenCache.service';

import { http } from '../http';

export async function refreshToken(sessionId: string) {
  const tokens = await getTokens(sessionId);

  if (tokens == null) {
    return false;
  }

  const payload = {
    refreshToken: tokens.refresh,
  };

  try {
    console.info(`Refreshing tokens for [session=${sessionId}]`);
    const response = await http.post(
      'http://localhost:55733/refresh',
      payload,
      {
        headers: {
          Authorization: `Bearer ${tokens.access}`,
        },
      },
    );

    const newTokens = response.data;
    await updateTokens(sessionId, {
      access: newTokens.accessToken,
      refresh: newTokens.refreshToken,
    });

    console.info(`Updated tokens for [session=${sessionId}]`);
    return true;
  } catch {
    console.info(`Failed to refresh tokens for [session=${sessionId}]`);
    await removeSessionCookie();
    await removeTokens(sessionId);
    return false;
  }
}

export async function isAuthenticated(): Promise<Boolean> {
  const sessionId = await getSessionCookie();

  if (sessionId == null) {
    return false;
  }

  return refreshToken(sessionId);
}
