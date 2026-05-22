import { type ReactNode, useSyncExternalStore } from "react";
import { Navigate } from "react-router";
import { authStore } from "../api/authStore";

interface Props {
  children: ReactNode;
}

export function ProtectedRoute({ children }: Props) {
  const token = useSyncExternalStore(
    authStore.subscribe,
    authStore.getSnapshot,
  );
  if (!token) {
    return <Navigate to="/login" replace />;
  }
  return <>{children}</>;
}
