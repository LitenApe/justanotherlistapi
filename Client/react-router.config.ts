import type { Config } from "@react-router/dev/config";

export default {
  // Keep the existing source folder instead of the default "app".
  appDirectory: "src",
  // The API authenticates with bearer tokens held in the browser, so there is no
  // server rendering: the app builds to static files (SPA mode) and loads data
  // in clientLoader/clientAction.
  ssr: false,
} satisfies Config;
