import { delayStore, errorRateStore, seedResource } from "@shared/api";
import { useCallback, useEffect, useState } from "react";

import type { LogEntry } from "@shared/api";
import { computeOverheadStore } from "@shared/api/computeOverhead";
import {
  useActivityEntries,
  useDelay,
  useErrorRate,
  useOverhead,
} from "@shared/hooks";
import type { FeatureFlags } from "./FeaturesContext";
import { useFeatures } from "./FeaturesContext";
import * as sessionPersistence from "./sessionPersistence";

export interface Preset {
  label: string;
  delay: number;
  errorRate: number;
  overhead: number;
  flags: FeatureFlags;
}

const ALL_ON: FeatureFlags = {
  suspense: true,
  useTransition: true,
  useDeferredValue: true,
  useOptimistic: true,
  showRenderCounts: true,
};

const ALL_OFF: FeatureFlags = {
  suspense: false,
  useTransition: false,
  useDeferredValue: false,
  useOptimistic: false,
  showRenderCounts: false,
};

export const presets: Preset[] = [
  {
    label: "Suspense",
    delay: 3000,
    errorRate: 0,
    overhead: 0,
    flags: { ...ALL_OFF, suspense: true },
  },
  {
    label: "Transitions",
    delay: 2500,
    errorRate: 0,
    overhead: 0,
    flags: { ...ALL_OFF, suspense: true, useTransition: true },
  },
  {
    label: "Optimistic",
    delay: 2500,
    errorRate: 0,
    overhead: 0,
    flags: { ...ALL_OFF, suspense: true, useOptimistic: true },
  },
  {
    label: "Deferred Value",
    delay: 0,
    errorRate: 0,
    overhead: 300,
    flags: { ...ALL_OFF, useDeferredValue: true, showRenderCounts: true },
  },
  {
    label: "Error Recovery",
    delay: 1500,
    errorRate: 80,
    overhead: 0,
    flags: { ...ALL_OFF, suspense: true, useOptimistic: true },
  },
  {
    label: "Kitchen Sink",
    delay: 2000,
    errorRate: 10,
    overhead: 150,
    flags: ALL_ON,
  },
  { label: "Reset", delay: 0, errorRate: 0, overhead: 0, flags: ALL_OFF },
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
  const saved = sessionPersistence.load();
  const [open, setOpen] = useState(saved?.open ?? false);
  const { flags, setFlag } = useFeatures();

  // Restore chaos controls from session on mount
  useEffect(() => {
    if (saved) {
      delayStore.setDelay(saved.delay);
      errorRateStore.setRate(saved.errorRate);
      computeOverheadStore.setOverhead(saved.overhead);
      for (const key of Object.keys(saved.flags) as (keyof FeatureFlags)[]) {
        setFlag(key, saved.flags[key]);
      }
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const delay = useDelay();
  const errorRate = useErrorRate();
  const overhead = useOverhead();
  const entries = useActivityEntries();
  const [seeding, setSeeding] = useState(false);

  useEffect(() => {
    function handler(e: KeyboardEvent) {
      if ((e.ctrlKey || e.metaKey) && e.shiftKey && e.key === "D") {
        e.preventDefault();
        setOpen((v) => !v);
      }
    }
    window.addEventListener("keydown", handler);
    return () => window.removeEventListener("keydown", handler);
  }, []);

  const applyPreset = useCallback(
    (p: Preset) => {
      delayStore.setDelay(p.delay);
      errorRateStore.setRate(p.errorRate);
      computeOverheadStore.setOverhead(p.overhead);
      for (const key of Object.keys(p.flags) as (keyof FeatureFlags)[]) {
        setFlag(key, p.flags[key]);
      }
    },
    [setFlag],
  );

  const handleSeed = useCallback(async () => {
    setSeeding(true);
    try {
      await seedResource.seed();
    } catch {
      /* ignore */
    }
    setSeeding(false);
  }, []);

  // Persist state to sessionStorage on changes
  useEffect(() => {
    sessionPersistence.save({ open, delay, errorRate, overhead, flags });
  }, [open, delay, errorRate, overhead, flags]);

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
