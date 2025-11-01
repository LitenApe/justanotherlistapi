'use server';

import { v7 as uuid } from 'uuid';

type SessionId = string;
type Tokens = {
  access: string;
  refresh: string;
};
type TokenCache = Record<SessionId, Tokens>;
const cache: TokenCache = {};

export async function addTokens(tokens: Tokens): Promise<SessionId> {
  if (tokens.access == null || tokens.refresh == null) {
    throw new Error('Attempted to add undefined tokens to TokenCache');
  }

  const sessionId = uuid();
  cache[sessionId] = tokens;

  console.info(`Added new tokens to TokenCache for [session=${sessionId}]`);

  return sessionId;
}

export async function updateTokens(sessionId: SessionId, tokens: Tokens) {
  if (sessionId == null) {
    throw new Error('Attempted to update tokens for undefined session');
  }

  if (tokens.access == null || tokens.refresh == null) {
    throw new Error('Attempted to add undefined tokens to TokenCache');
  }

  cache[sessionId] = tokens;
}

export async function getTokens(sessionId: SessionId): Promise<Tokens | null> {
  const tokens = cache[sessionId];

  if (tokens == null) {
    console.info(
      `Attempted to retrieve missing tokens for [session=${sessionId}]`,
    );
    return null;
  }

  return tokens;
}

export async function removeTokens(sessionId: SessionId) {
  delete cache[sessionId];
}
