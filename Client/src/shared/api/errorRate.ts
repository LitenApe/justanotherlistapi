export function createErrorRateStore() {
  let rate = 0;
  const listeners = new Set<() => void>();

  const notify = () => listeners.forEach((l) => l());

  return {
    getRate: () => rate,
    setRate: (percent: number) => {
      rate = Math.max(0, Math.min(100, percent));
      notify();
    },
    subscribe: (listener: () => void) => {
      listeners.add(listener);
      return () => {
        listeners.delete(listener);
      };
    },
    getSnapshot: () => rate,
  };
}

export type ErrorRateStore = ReturnType<typeof createErrorRateStore>;
export const errorRateStore = createErrorRateStore();
