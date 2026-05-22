import type { ItemGroup } from "@shared/types";
import { computeOverheadStore } from "@shared/api/computeOverhead";

export function filterChecklists(
  checklists: ItemGroup[],
  query: string,
): ItemGroup[] {
  // Simulate compute overhead for useDeferredValue demo
  const overhead = computeOverheadStore.getOverhead();
  if (overhead > 0) {
    const start = performance.now();
    while (performance.now() - start < overhead) {
      // busy-wait
    }
  }

  if (!query.trim()) return checklists;
  const lower = query.toLowerCase();
  return checklists.filter((g) => g.name.toLowerCase().includes(lower));
}
