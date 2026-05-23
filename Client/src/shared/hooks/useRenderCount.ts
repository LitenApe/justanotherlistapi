import { useRef } from "react";

export function useRenderCount(label: string): number {
  const count = useRef(0);
  // eslint-disable-next-line react-hooks/refs -- intentional: must read/write ref during render to count renders
  count.current += 1;

  if (import.meta.env.DEV) {
    // eslint-disable-next-line react-hooks/refs
    console.debug(`[render] ${label}: ${count.current}`);
  }

  // eslint-disable-next-line react-hooks/refs
  return count.current;
}
