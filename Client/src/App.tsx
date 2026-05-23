import { BrowserRouter, Navigate, Route, Routes } from "react-router";
import { DevPanel, FeaturesProvider } from "./slices/dev-panel";
import {
  ErrorBoundary,
  PendingBorder,
  ProtectedRoute,
} from "@shared/components";
import { ItemCreatePage, ItemEditPage } from "./slices/items";
import { Suspense, lazy } from "react";

import { ChecklistDetail } from "./slices/checklist-detail/ChecklistDetail";
import { Layout } from "./components/Layout";

const Login = lazy(() =>
  import("./slices/auth/Login").then((m) => ({ default: m.Login })),
);

export function App() {
  return (
    <FeaturesProvider>
      <BrowserRouter>
        <Routes>
          <Route path="/" element={<Layout />}>
            <Route
              path="login"
              element={
                <Suspense fallback={null}>
                  <Login />
                </Suspense>
              }
            />
            <Route
              index
              element={
                <ProtectedRoute>
                  <p>Select a checklist to get started.</p>
                </ProtectedRoute>
              }
            />
            <Route
              path=":groupId"
              element={
                <ProtectedRoute>
                  <ErrorBoundary
                    fallback={(err, reset) => (
                      <p>
                        Error: {err.message}{" "}
                        <button onClick={reset}>Retry</button>
                      </p>
                    )}
                  >
                    <ChecklistDetail />
                  </ErrorBoundary>
                </ProtectedRoute>
              }
            />
            <Route
              path=":groupId/items/new"
              element={
                <ProtectedRoute>
                  <ItemCreatePage />
                </ProtectedRoute>
              }
            />
            <Route
              path=":groupId/items/:itemId"
              element={
                <ProtectedRoute>
                  <ErrorBoundary
                    fallback={(err, reset) => (
                      <p>
                        Error: {err.message}{" "}
                        <button onClick={reset}>Retry</button>
                      </p>
                    )}
                  >
                    <ItemEditPage />
                  </ErrorBoundary>
                </ProtectedRoute>
              }
            />
          </Route>
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </BrowserRouter>
      {import.meta.env.DEV && <PendingBorder />}
      {import.meta.env.DEV && <DevPanel />}
    </FeaturesProvider>
  );
}
