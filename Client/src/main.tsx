import { StrictMode } from "react";
import { createRoot } from "react-dom/client";

function App() {
  return <h1>JustAnotherList</h1>;
}

const root = createRoot(document.getElementById("root")!);
const app = <App />;

root.render(import.meta.env.DEV ? <StrictMode>{app}</StrictMode> : app);

window.addEventListener("unhandledrejection", (event) => {
  console.error("Unhandled promise rejection:", event.reason);
});
