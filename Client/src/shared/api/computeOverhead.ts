export function createComputeOverheadStore() {
  let overhead = 0;
  const listeners = new Set<() => void>();

  const notify = () => listeners.forEach((l) => l());

  return {
    getOverhead: () => overhead,
    setOverhead: (ms: number) => {
      overhead = Math.max(0, ms);
      notify();
    },
    subscribe: (listener: () => void) => {
      listeners.add(listener);
      return () => {
        listeners.delete(listener);
      };
    },
    getSnapshot: () => overhead,
  };
}

export type ComputeOverheadStore = ReturnType<
  typeof createComputeOverheadStore
>;
export const computeOverheadStore = createComputeOverheadStore();
