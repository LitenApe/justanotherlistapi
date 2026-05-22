import { BrowserRouter, Navigate, Route, Routes } from "react-router";
import {
  ErrorBoundary,
  PendingBoundary,
  ProtectedRoute,
} from "@shared/components";

import { ChecklistDetail } from "./slices/checklist-detail/ChecklistDetail";
import { ItemCreatePage } from "./components/ItemCreatePage";
import { ItemEditPage } from "./components/ItemEditPage";
import { Layout } from "./components/Layout";
import { Login } from "./slices/auth";

export function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/login" element={<Login />} />
        <Route
          path="/"
          element={
            <ProtectedRoute>
              <Layout />
            </ProtectedRoute>
          }
        >
          <Route index element={<p>Select a checklist to get started.</p>} />
          <Route
            path=":groupId"
            element={
              <ErrorBoundary
                fallback={(err, reset) => (
                  <p>
                    Error: {err.message} <button onClick={reset}>Retry</button>
                  </p>
                )}
              >
                <PendingBoundary>
                  <ChecklistDetail />
                </PendingBoundary>
              </ErrorBoundary>
            }
          />
          <Route path=":groupId/items/new" element={<ItemCreatePage />} />
          <Route
            path=":groupId/items/:itemId"
            element={
              <ErrorBoundary
                fallback={(err, reset) => (
                  <p>
                    Error: {err.message} <button onClick={reset}>Retry</button>
                  </p>
                )}
              >
                <PendingBoundary>
                  <ItemEditPage />
                </PendingBoundary>
              </ErrorBoundary>
            }
          />
        </Route>
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  );
}
