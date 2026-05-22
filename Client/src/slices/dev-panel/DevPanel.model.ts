import {
  activityLog,
  delayStore,
  errorRateStore,
  seedResource,
} from "@shared/api";
import { useCallback, useEffect, useState, useSyncExternalStore } from "react";

import type { LogEntry } from "@shared/api";
import { computeOverheadStore } from "@shared/api/computeOverhead";
import type { FeatureFlags } from "./FeaturesContext";
import { useFeatures } from "./FeaturesContext";

export interface Preset {
  label: string;
  delay: number;
  errorRate: number;
  overhead: number;
}

export const presets: Preset[] = [
  { label: "Fast", delay: 0, errorRate: 0, overhead: 0 },
  { label: "Slow API", delay: 2000, errorRate: 0, overhead: 0 },
  { label: "Flaky", delay: 500, errorRate: 30, overhead: 0 },
  { label: "Heavy CPU", delay: 0, errorRate: 0, overhead: 200 },
  { label: "Chaos", delay: 1000, errorRate: 20, overhead: 100 },
  { label: "Offline", delay: 0, errorRate: 100, overhead: 0 },
  { label: "Real World", delay: 300, errorRate: 5, overhead: 50 },
];

export interface DevPanelModel {
  open: boolean;
  setOpen: (open: boolean) => void;
  delay: number;
  setDelay: (value: number) => void;
  errorRate: number;
  setErrorRate: (value: number) => void;
  overhead: number;
  setOverhead: (value: number) => void;
  entries: readonly LogEntry[];
  seeding: boolean;
  flags: FeatureFlags;
  setFlag: <K extends keyof FeatureFlags>(
    key: K,
    value: FeatureFlags[K],
  ) => void;
  applyPreset: (preset: Preset) => void;
  handleSeed: () => void;
}

export function useDevPanelModel(): DevPanelModel {
  const [open, setOpen] = useState(false);
  const { flags, setFlag } = useFeatures();

  const delay = useSyncExternalStore(
    delayStore.subscribe,
    delayStore.getSnapshot,
  );
  const errorRate = useSyncExternalStore(
    errorRateStore.subscribe,
    errorRateStore.getSnapshot,
  );
  const overhead = useSyncExternalStore(
    computeOverheadStore.subscribe,
    computeOverheadStore.getSnapshot,
  );
  const entries = useSyncExternalStore(
    activityLog.subscribe,
    activityLog.getSnapshot,
  );
  const [seeding, setSeeding] = useState(false);

  useEffect(() => {
    function handler(e: KeyboardEvent) {
      if (e.ctrlKey && e.shiftKey && e.key === "D") {
        e.preventDefault();
        setOpen((v) => !v);
      }
    }
    window.addEventListener("keydown", handler);
    return () => window.removeEventListener("keydown", handler);
  }, []);

  const applyPreset = useCallback((p: Preset) => {
    delayStore.setDelay(p.delay);
    errorRateStore.setRate(p.errorRate);
    computeOverheadStore.setOverhead(p.overhead);
  }, []);

  const handleSeed = useCallback(async () => {
    setSeeding(true);
    try {
      await seedResource.seed();
    } catch {
      /* ignore */
    }
    setSeeding(false);
  }, []);

  return {
    open,
    setOpen,
    delay,
    setDelay: delayStore.setDelay,
    errorRate,
    setErrorRate: errorRateStore.setRate,
    overhead,
    setOverhead: computeOverheadStore.setOverhead,
    entries,
    seeding,
    flags,
    setFlag,
    applyPreset,
    handleSeed,
  };
}
