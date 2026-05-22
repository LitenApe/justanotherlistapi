import { BrowserRouter, Navigate, Route, Routes } from "react-router";

import { Layout } from "./components/Layout";
import { Login } from "./slices/auth";
import { ProtectedRoute } from "@shared/components";

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
          <Route path=":groupId" element={<p>Checklist detail (Phase 5)</p>} />
          <Route
            path=":groupId/items/new"
            element={<p>New item (Phase 5)</p>}
          />
          <Route
            path=":groupId/items/:itemId"
            element={<p>Edit item (Phase 5)</p>}
          />
        </Route>
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  );
}
