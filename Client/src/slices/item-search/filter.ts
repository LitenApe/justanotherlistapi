import type { Item } from "@shared/types";
import { computeOverheadStore } from "@shared/api/computeOverhead";

export function filterItems(items: Item[], query: string): Item[] {
  // Simulate compute overhead for useDeferredValue demo
  const overhead = computeOverheadStore.getOverhead();
  if (overhead > 0) {
    const start = performance.now();
    while (performance.now() - start < overhead) {
      // busy-wait
    }
  }

  if (!query.trim()) return items;
  const lower = query.toLowerCase();
  return items.filter(
    (item) =>
      item.name.toLowerCase().includes(lower) ||
      (item.description && item.description.toLowerCase().includes(lower)),
  );
}
