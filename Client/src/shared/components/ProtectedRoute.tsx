import type { ReactNode } from "react";
import { Navigate } from "react-router";
import { useAuthToken } from "@shared/hooks";

interface Props {
  children: ReactNode;
}

export function ProtectedRoute({ children }: Props) {
  const token = useAuthToken();
  if (!token) {
    return <Navigate to="/login" replace />;
  }
  return <>{children}</>;
}
