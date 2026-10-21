// Serves build/client the way a static host serves the SPA in production:
// files as-is, every other path falls back to index.html. API, hub and OAuth
// paths return 404 so a request the tests forgot to fake fails loudly instead
// of receiving index.html.
import { createReadStream } from "node:fs";
import { stat } from "node:fs/promises";
import { createServer } from "node:http";
import { extname, join, normalize } from "node:path";

const root = join(import.meta.dirname, "..", "..", "build", "client");
const port = Number(process.argv[2] ?? 4173);
const backendPaths = /^\/(api|hubs|default)(\/|$)/;

const types = {
  ".html": "text/html; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".svg": "image/svg+xml",
  ".json": "application/json",
  ".map": "application/json",
};

async function isFile(path) {
  try {
    return (await stat(path)).isFile();
  } catch {
    return false;
  }
}

createServer(async (request, response) => {
  const { pathname } = new URL(request.url ?? "/", "http://localhost");
  if (backendPaths.test(pathname)) {
    response.writeHead(404).end("Not faked by the test");
    return;
  }

  const requested = normalize(join(root, decodeURIComponent(pathname)));
  const file =
    requested.startsWith(root) && (await isFile(requested)) ? requested : join(root, "index.html");

  response.writeHead(200, { "Content-Type": types[extname(file)] ?? "application/octet-stream" });
  createReadStream(file).pipe(response);
}).listen(port, () => {
  console.log(`Serving build/client on http://localhost:${port}`);
});
