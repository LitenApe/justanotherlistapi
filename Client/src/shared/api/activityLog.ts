export type LogSource = "api" | "signalr";

export interface LogEntry {
  id: string;
  operationId: string;
  event: "start" | "complete" | "error";
  source: LogSource;
  timestamp: number;
  duration?: number;
}

const MAX_ENTRIES = 200;

export function createActivityLog() {
  let entries: LogEntry[] = [];
  const listeners = new Set<() => void>();

  const notify = () => listeners.forEach((l) => l());

  return {
    append: (entry: LogEntry) => {
      entries = entries.concat(entry).slice(-MAX_ENTRIES);
      notify();
    },
    getSnapshot: (): readonly LogEntry[] => entries,
    clear: () => {
      entries = [];
      notify();
    },
    subscribe: (listener: () => void) => {
      listeners.add(listener);
      return () => {
        listeners.delete(listener);
      };
    },
  };
}

export type ActivityLog = ReturnType<typeof createActivityLog>;
export const activityLog = createActivityLog();
