import type { FeatureFlags } from "@shared/features";

export interface DevPanelState {
  open: boolean;
  delay: number;
  errorRate: number;
  overhead: number;
  flags: FeatureFlags;
}

const KEY = "dev-panel-state";

export function save(state: DevPanelState): void {
  try {
    sessionStorage.setItem(KEY, JSON.stringify(state));
  } catch {
    /* quota exceeded or private mode */
  }
}

export function load(): DevPanelState | null {
  try {
    const raw = sessionStorage.getItem(KEY);
    if (!raw) return null;
    return JSON.parse(raw) as DevPanelState;
  } catch {
    return null;
  }
}
