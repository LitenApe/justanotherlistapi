import { type ReactNode } from "react";
import { Navigate } from "react-router";
import { authStore } from "../api/authStore";

interface Props {
  children: ReactNode;
}

export function ProtectedRoute({ children }: Props) {
  const token = authStore.getToken();
  if (!token) {
    return <Navigate to="/login" replace />;
  }
  return <>{children}</>;
}
