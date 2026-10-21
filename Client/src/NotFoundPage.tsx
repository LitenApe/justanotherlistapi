import { Link } from "react-router";
import { DocumentTitle } from "./components/DocumentTitle";

export default function NotFoundPage() {
  return (
    <div style={{ display: "grid", gap: "var(--space-3)" }}>
      <DocumentTitle title="Page not found" />
      <h1 tabIndex={-1}>Page not found</h1>
      <p className="muted">There's nothing at this address.</p>
      <p>
        <Link to="/">Go to your lists</Link>
      </p>
    </div>
  );
}
