export function createDelayStore() {
  let delay = 0;
  const listeners = new Set<() => void>();

  const notify = () => listeners.forEach((l) => l());

  return {
    getDelay: () => delay,
    setDelay: (ms: number) => {
      delay = Math.max(0, ms);
      notify();
    },
    subscribe: (listener: () => void) => {
      listeners.add(listener);
      return () => {
        listeners.delete(listener);
      };
    },
    getSnapshot: () => delay,
  };
}

export type DelayStore = ReturnType<typeof createDelayStore>;
export const delayStore = createDelayStore();
