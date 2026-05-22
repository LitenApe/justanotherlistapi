import * as sessionPersistence from "./sessionPersistence";

import { delayStore, errorRateStore, seedResource } from "@shared/api";
import {
  useActivityEntries,
  useDelay,
  useErrorRate,
  useOverhead,
} from "@shared/hooks";
import { useCallback, useEffect, useState } from "react";

import type { FeatureFlags } from "./FeaturesContext";
import type { LogEntry } from "@shared/api";
import { computeOverheadStore } from "@shared/api/computeOverhead";
import styles from "./DevPanel.module.css";
import { useFeatures } from "./FeaturesContext";

// ─── Model ────────────────────────────────────────────────────────────────────

export interface Preset {
  label: string;
  delay: number;
  errorRate: number;
  overhead: number;
  flags: FeatureFlags;
}

export const presets: Preset[] = [
  {
    label: "Slow Network",
    delay: 3000,
    errorRate: 0,
    overhead: 0,
    flags: { showRenderCounts: false },
  },
  {
    label: "Laggy Device",
    delay: 0,
    errorRate: 0,
    overhead: 300,
    flags: { showRenderCounts: true },
  },
  {
    label: "Unreliable",
    delay: 1500,
    errorRate: 50,
    overhead: 0,
    flags: { showRenderCounts: false },
  },
  {
    label: "Worst Case",
    delay: 2000,
    errorRate: 20,
    overhead: 200,
    flags: { showRenderCounts: true },
  },
  {
    label: "Reset",
    delay: 0,
    errorRate: 0,
    overhead: 0,
    flags: { showRenderCounts: false },
  },
];

interface DevPanelModel {
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

function useDevPanelModel(): DevPanelModel {
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

// ─── View ─────────────────────────────────────────────────────────────────────

interface DevPanelViewProps {
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

function DevPanelView({
  open,
  setOpen,
  delay,
  setDelay,
  errorRate,
  setErrorRate,
  overhead,
  setOverhead,
  entries,
  seeding,
  flags,
  setFlag,
  applyPreset,
  handleSeed,
}: DevPanelViewProps) {
  if (!open) {
    return (
      <button
        type="button"
        className={styles.fab}
        onClick={() => setOpen(true)}
        title="Dev Panel (Ctrl+Shift+D)"
      >
        ⚙
      </button>
    );
  }

  return (
    <div className={styles.panel}>
      <div className={styles.header}>
        <span className={styles.title}>Dev Panel</span>
        <button
          type="button"
          className={styles.closeBtn}
          onClick={() => setOpen(false)}
        >
          ✕
        </button>
      </div>

      <div className={styles.section}>
        <div className={styles.sectionTitle}>Chaos Controls</div>
        <div className={styles.slider}>
          <span className={styles.sliderLabel}>Delay</span>
          <input
            type="range"
            className={styles.sliderInput}
            min={0}
            max={5000}
            step={100}
            value={delay}
            onChange={(e) => setDelay(Number(e.target.value))}
          />
          <span className={styles.sliderValue}>{delay}ms</span>
        </div>
        <div className={styles.slider}>
          <span className={styles.sliderLabel}>Error Rate</span>
          <input
            type="range"
            className={styles.sliderInput}
            min={0}
            max={100}
            step={5}
            value={errorRate}
            onChange={(e) => setErrorRate(Number(e.target.value))}
          />
          <span className={styles.sliderValue}>{errorRate}%</span>
        </div>
        <div className={styles.slider}>
          <span className={styles.sliderLabel}>CPU Load</span>
          <input
            type="range"
            className={styles.sliderInput}
            min={0}
            max={500}
            step={25}
            value={overhead}
            onChange={(e) => setOverhead(Number(e.target.value))}
          />
          <span className={styles.sliderValue}>{overhead}ms</span>
        </div>
      </div>

      <div className={styles.section}>
        <div className={styles.sectionTitle}>Presets</div>
        <div className={styles.presets}>
          {presets.map((p) => (
            <button
              key={p.label}
              type="button"
              className={styles.presetBtn}
              onClick={() => applyPreset(p)}
            >
              {p.label}
            </button>
          ))}
        </div>
      </div>

      <div className={styles.section}>
        <div className={styles.sectionTitle}>Display</div>
        <label className={styles.toggle}>
          <input
            type="checkbox"
            checked={flags.showRenderCounts}
            onChange={(e) => setFlag("showRenderCounts", e.target.checked)}
          />
          <span className={styles.toggleLabel}>Show Render Counts</span>
        </label>
      </div>

      <div className={styles.section}>
        <div className={styles.sectionTitle}>Seed Data</div>
        <button
          type="button"
          className={styles.seedBtn}
          onClick={handleSeed}
          disabled={seeding}
        >
          {seeding ? "Seeding…" : "Seed Database"}
        </button>
      </div>

      <div className={styles.section}>
        <div className={styles.sectionTitle}>
          Activity Log ({entries.length})
        </div>
        <div className={styles.log}>
          {entries
            .slice(-20)
            .reverse()
            .map((entry: LogEntry) => (
              <div key={entry.id} className={styles.logEntry}>
                {entry.event} · {entry.operationId.slice(0, 8)} ·{" "}
                {entry.duration != null ? `${entry.duration}ms` : "—"}
              </div>
            ))}
        </div>
      </div>
    </div>
  );
}

// ─── Controller ───────────────────────────────────────────────────────────────

export function DevPanel() {
  const model = useDevPanelModel();
  return <DevPanelView {...model} />;
}
