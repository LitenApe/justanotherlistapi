import { BrowserRouter, Link, Route, Routes } from "react-router";
import { RequireAuth } from "./auth/RequireAuth";
import { SignInPage } from "./auth/SignInPage";
import { Layout } from "./components/Layout";
import { ToastProvider } from "./components/Toasts";
import { useDocumentTitle } from "./components/useDocumentTitle";
import { ListRoute } from "./list/ListPage";
import { ListsPage } from "./lists/ListsPage";

export function App() {
  return (
    <BrowserRouter>
      <ToastProvider>
        <AppRoutes />
      </ToastProvider>
    </BrowserRouter>
  );
}

export function AppRoutes() {
  return (
    <Routes>
      <Route element={<Layout />}>
        <Route path="sign-in" element={<SignInPage />} />
        <Route element={<RequireAuth />}>
          <Route index element={<ListsPage />} />
          <Route path="lists/:listId" element={<ListRoute />} />
        </Route>
        <Route path="*" element={<NotFoundPage />} />
      </Route>
    </Routes>
  );
}

function NotFoundPage() {
  useDocumentTitle("Page not found");
  return (
    <div style={{ display: "grid", gap: "var(--space-3)" }}>
      <h1 tabIndex={-1}>Page not found</h1>
      <p className="muted">There's nothing at this address.</p>
      <p>
        <Link to="/">Go to your lists</Link>
      </p>
    </div>
  );
}
