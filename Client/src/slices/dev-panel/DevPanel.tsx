import {
  activityLog,
  delayStore,
  errorRateStore,
  seedResource,
} from "@shared/api";
import { useCallback, useEffect, useState, useSyncExternalStore } from "react";

import type { LogEntry } from "@shared/api";
import { computeOverheadStore } from "@shared/api/computeOverhead";
import styles from "./DevPanel.module.css";
import { useFeatures } from "./FeaturesContext";

interface Preset {
  label: string;
  delay: number;
  errorRate: number;
  overhead: number;
}

const presets: Preset[] = [
  { label: "Fast", delay: 0, errorRate: 0, overhead: 0 },
  { label: "Slow API", delay: 2000, errorRate: 0, overhead: 0 },
  { label: "Flaky", delay: 500, errorRate: 30, overhead: 0 },
  { label: "Heavy CPU", delay: 0, errorRate: 0, overhead: 200 },
  { label: "Chaos", delay: 1000, errorRate: 20, overhead: 100 },
  { label: "Offline", delay: 0, errorRate: 100, overhead: 0 },
  { label: "Real World", delay: 300, errorRate: 5, overhead: 50 },
];

export function DevPanel() {
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

  // Keyboard shortcut: Ctrl+Shift+D
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
            onChange={(e) => delayStore.setDelay(Number(e.target.value))}
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
            onChange={(e) => errorRateStore.setRate(Number(e.target.value))}
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
            onChange={(e) =>
              computeOverheadStore.setOverhead(Number(e.target.value))
            }
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
        <div className={styles.sectionTitle}>Feature Flags</div>
        <label className={styles.toggle}>
          <input
            type="checkbox"
            checked={flags.useConcurrent}
            onChange={(e) => setFlag("useConcurrent", e.target.checked)}
          />
          <span className={styles.toggleLabel}>Use Concurrent APIs</span>
        </label>
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
