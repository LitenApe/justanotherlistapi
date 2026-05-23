import { useEffect, useRef } from "react";

import { pendingService } from "@shared/api";
import { useNavigation } from "react-router";

export function NavigationPendingReporter() {
  const navigation = useNavigation();
  const activeRef = useRef(false);

  useEffect(() => {
    const isNavigating = navigation.state !== "idle";

    if (isNavigating && !activeRef.current) {
      activeRef.current = true;
      pendingService.begin("navigation");
    } else if (!isNavigating && activeRef.current) {
      activeRef.current = false;
      pendingService.end("navigation");
    }
  }, [navigation.state]);

  return null;
}
