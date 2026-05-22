import "@shared/styles/global.css";
import "@shared/api";

import { App } from "./App";
import { StrictMode } from "react";
import { createRoot } from "react-dom/client";

const root = createRoot(document.getElementById("root")!);
const app = <App />;

root.render(import.meta.env.DEV ? <StrictMode>{app}</StrictMode> : app);

window.addEventListener("unhandledrejection", (event) => {
  console.error("Unhandled promise rejection:", event.reason);
});
