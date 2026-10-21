// Real styles, so visibility, layout and colour contrast behave as in the app.
import "../styles/global.css";
import { afterEach } from "vitest";

afterEach(() => {
  localStorage.clear();
  sessionStorage.clear();
});
