import { useRef } from "react";

export function useRenderCount(label: string): number {
  const count = useRef(0);
  count.current += 1;

  if (import.meta.env.DEV) {
    console.debug(`[render] ${label}: ${count.current}`);
  }

  return count.current;
}
