import { useSyncExternalStore } from "react";
import { sessionStore, type Session } from "./session";

export function useSession(): Session | null {
  return useSyncExternalStore(sessionStore.subscribe, sessionStore.getSession, () => null);
}
