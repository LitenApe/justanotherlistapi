import { type ActivityLog, activityLog } from "./activityLog";

export function createPendingService(log: ActivityLog) {
  const active = new Set<string>();
  const listeners = new Set<() => void>();
  const startTimes = new Map<string, number>();

  const notify = () => listeners.forEach((l) => l());

  function begin(id: string): () => void {
    active.add(id);
    const now = Date.now();
    startTimes.set(id, now);
    log.append({
      id: crypto.randomUUID(),
      operationId: id,
      event: "start",
      timestamp: now,
    });
    notify();
    return () => end(id);
  }

  function end(id: string): void {
    active.delete(id);
    const startTime = startTimes.get(id);
    startTimes.delete(id);
    const now = Date.now();
    log.append({
      id: crypto.randomUUID(),
      operationId: id,
      event: "complete",
      timestamp: now,
      ...(startTime !== undefined && { duration: now - startTime }),
    });
    notify();
  }

  function error(id: string): void {
    active.delete(id);
    const startTime = startTimes.get(id);
    startTimes.delete(id);
    const now = Date.now();
    log.append({
      id: crypto.randomUUID(),
      operationId: id,
      event: "error",
      timestamp: now,
      ...(startTime !== undefined && { duration: now - startTime }),
    });
    notify();
  }

  async function track<T>(id: string, promise: Promise<T>): Promise<T> {
    begin(id);
    try {
      const result = await promise;
      end(id);
      return result;
    } catch (e) {
      error(id);
      throw e;
    }
  }

  return {
    begin,
    end,
    track,
    subscribe: (listener: () => void) => {
      listeners.add(listener);
      return () => {
        listeners.delete(listener);
      };
    },
    getSnapshot: (): boolean => active.size > 0,
    getActiveOperations: (): readonly string[] => [...active],
  };
}

export type PendingService = ReturnType<typeof createPendingService>;
export const pendingService = createPendingService(activityLog);
