'use server';

import { ResponseCookie } from 'next/dist/compiled/@edge-runtime/cookies';
import { cookies } from 'next/headers';

export async function setCookie(
  key: string,
  value: string,
  options?: Partial<ResponseCookie>,
) {
  const cookie = await cookies();
  cookie.set(key, value, options);
}

const SESSION_COOKIE_KEY = 'sessionId';
export async function setSessionCookie(value: string) {
  return setCookie(SESSION_COOKIE_KEY, value, { httpOnly: true });
}

export async function getSessionCookie() {
  const cookie = await cookies();
  const sessionId = cookie.get(SESSION_COOKIE_KEY);
  return sessionId?.value ?? null;
}

export async function removeSessionCookie() {
  const cookie = await cookies();
  cookie.delete(SESSION_COOKIE_KEY);
}
