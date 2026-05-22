import type { LogEntry } from "@shared/api";
import type { FeatureFlags } from "./FeaturesContext";
import type { Preset } from "./DevPanel.model";
import { presets } from "./DevPanel.model";
import styles from "./DevPanel.module.css";

export interface DevPanelViewProps {
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

export function DevPanelView({
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
