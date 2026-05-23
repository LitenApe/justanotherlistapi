import styles from "./RenderCount.module.css";
import { useFeatures } from "@shared/features";
import { useRenderCount } from "@shared/hooks";

interface RenderCountProps {
  label: string;
}

export function RenderCount({ label }: RenderCountProps) {
  const { flags } = useFeatures();
  const count = useRenderCount(label);

  if (!flags.showRenderCounts) return null;

  return <span className={styles.badge}>{count}</span>;
}
