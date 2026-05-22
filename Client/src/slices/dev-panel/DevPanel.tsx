import { DevPanelView } from "./DevPanel.view";
import { useDevPanelModel } from "./DevPanel.model";

export function DevPanel() {
  const model = useDevPanelModel();
  return <DevPanelView {...model} />;
}
