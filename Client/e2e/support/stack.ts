import { spawn, type ChildProcess } from "node:child_process";
import { randomBytes } from "node:crypto";
import { createWriteStream } from "node:fs";
import { mkdir, rm } from "node:fs/promises";
import { createServer } from "node:net";
import { tmpdir } from "node:os";
import { join } from "node:path";
import {
  MSSQLServerContainer,
  type StartedMSSQLServerContainer,
} from "@testcontainers/mssqlserver";

/**
 * The whole application, running locally for end-to-end tests: SQL Server in a
 * container (the image Aspire/Program.cs uses), the real Api backend, which
 * signs people in itself, and the production client build behind the same
 * proxy the dev server uses. Nothing is faked.
 */

/** Keep in sync with Aspire/Program.cs. */
const SQL_SERVER_IMAGE = "mcr.microsoft.com/mssql/server:2025-latest";
export const DATABASE = "JustAnotherList";
export const SQLCMD = "/opt/mssql-tools18/bin/sqlcmd";

export const CLIENT_PORT = 4174;

const clientDir = join(import.meta.dirname, "..", "..");
const repoDir = join(clientDir, "..");
const logDir = join(clientDir, "test-results", "e2e-logs");
const apiBuildDir = join(tmpdir(), "justanotherlist-e2e", "api");

export interface RunningStack {
  clientUrl: string;
  apiUrl: string;
  /** For tests that arrange data the UI can't, like backdating a completion. */
  database: { containerId: string; user: string; password: string };
  stop: () => Promise<void>;
}

export async function startStack(): Promise<RunningStack> {
  await mkdir(logDir, { recursive: true });
  const stoppers: (() => Promise<unknown>)[] = [];
  const stop = async () => {
    // Last started, first stopped.
    for (const stopOne of stoppers.reverse()) await stopOne().catch(() => undefined);
  };

  try {
    // Containers and both builds are independent, so they run side by side.
    const [sql] = await Promise.all([
      startSqlServer(),
      buildApi(),
      run("client-build", process.execPath, [
        join(clientDir, "node_modules", "@react-router", "dev", "bin.cjs"),
        "build",
      ]),
    ]);
    stoppers.push(() => sql.stop());

    const apiUrl = `http://127.0.0.1:${(await freePort()).toString()}`;

    const api = startProcess("api", "dotnet", [join(apiBuildDir, "Api.dll")], {
      cwd: apiBuildDir,
      env: {
        ASPNETCORE_ENVIRONMENT: "Production",
        ASPNETCORE_URLS: apiUrl,
        ConnectionStrings__database: [
          `Server=${sql.getHost()},${sql.getPort().toString()}`,
          `Database=${DATABASE}`,
          `User Id=${sql.getUsername()}`,
          `Password=${sql.getPassword()}`,
          "TrustServerCertificate=True",
        ].join(";"),
        // Production refuses to start without a signing key; each run gets its own.
        Auth__SigningKey: randomBytes(32).toString("base64"),
        // Every test signs up its users from this one address, far more than 20 a minute.
        Auth__RateLimit__PermitLimit: "100000",
        // No telemetry collector runs during tests.
        OTEL_SDK_DISABLED: "true",
      },
    });
    stoppers.push(() => stopProcess(api));
    // Api creates its tables before it starts listening; 401 means it's ready.
    await waitForStatus(`${apiUrl}/api/list`, 401, api);

    const client = startProcess(
      "client",
      process.execPath,
      [
        join(clientDir, "node_modules", "vite", "bin", "vite.js"),
        "preview",
        "--port",
        CLIENT_PORT.toString(),
        "--strictPort",
      ],
      {
        cwd: clientDir,
        // vite.config.ts reads these, exactly as Aspire sets them.
        env: { services__Api__http__0: apiUrl },
        unset: ["services__Api__https__0"],
      },
    );
    stoppers.push(() => stopProcess(client));
    const clientUrl = `http://localhost:${CLIENT_PORT.toString()}`;
    // Through the client's proxy to Api: the pieces are connected.
    await waitForStatus(`${clientUrl}/api/list`, 401, client);

    return {
      clientUrl,
      apiUrl,
      database: { containerId: sql.getId(), user: sql.getUsername(), password: sql.getPassword() },
      stop,
    };
  } catch (error) {
    await stop();
    throw error;
  }
}

async function startSqlServer(): Promise<StartedMSSQLServerContainer> {
  const sql = await new MSSQLServerContainer(SQL_SERVER_IMAGE).acceptLicense().start();
  // Aspire creates the database for Api; do the same.
  const created = await sql.exec([
    SQLCMD,
    "-S",
    "localhost",
    "-U",
    sql.getUsername(),
    "-P",
    sql.getPassword(),
    "-C",
    "-Q",
    `CREATE DATABASE [${DATABASE}]`,
  ]);
  if (created.exitCode !== 0) {
    throw new Error(`Couldn't create the database: ${created.output}`);
  }
  return sql;
}

async function buildApi(): Promise<void> {
  await rm(apiBuildDir, { recursive: true, force: true });
  // A separate output folder, so a running dev instance (which locks Api/bin) doesn't matter.
  await run("api-build", "dotnet", [
    "build",
    join(repoDir, "Api", "Api.csproj"),
    "--configuration",
    "Release",
    "--output",
    apiBuildDir,
    // The format check belongs to dotnet build in CI, not to test setup.
    "-p:CSharpierDisable=true",
  ]);
}

interface ProcessOptions {
  cwd?: string;
  env?: Record<string, string>;
  unset?: string[];
}

function startProcess(
  name: string,
  command: string,
  args: string[],
  { cwd = clientDir, env = {}, unset = [] }: ProcessOptions = {},
): ChildProcess {
  const fullEnv: NodeJS.ProcessEnv = { ...process.env, ...env };
  for (const key of unset) Reflect.deleteProperty(fullEnv, key);
  const child = spawn(command, args, { cwd, env: fullEnv, stdio: ["ignore", "pipe", "pipe"] });
  const log = createWriteStream(join(logDir, `${name}.log`));
  child.stdout.pipe(log);
  child.stderr.pipe(log);
  return child;
}

/** Runs a command to completion; its output goes to test-results/e2e-logs/<name>.log. */
function run(name: string, command: string, args: string[]): Promise<void> {
  return new Promise((resolve, reject) => {
    const child = startProcess(name, command, args);
    child.on("error", reject);
    child.on("exit", (code) => {
      if (code === 0) resolve();
      else
        reject(
          new Error(`${name} failed (exit ${String(code)}); see test-results/e2e-logs/${name}.log`),
        );
    });
  });
}

function stopProcess(child: ChildProcess): Promise<void> {
  if (child.exitCode !== null) return Promise.resolve();
  return new Promise((resolve) => {
    child.once("exit", () => {
      resolve();
    });
    child.kill();
  });
}

async function waitForStatus(url: string, status: number, process: ChildProcess) {
  const deadline = Date.now() + 120_000;
  while (Date.now() < deadline) {
    if (process.exitCode !== null) {
      throw new Error(
        `The process serving ${url} exited (code ${String(process.exitCode)}); see test-results/e2e-logs.`,
      );
    }
    try {
      if ((await fetch(url)).status === status) return;
    } catch {
      // Not listening yet.
    }
    await new Promise((resolve) => setTimeout(resolve, 500));
  }
  throw new Error(`${url} didn't answer ${status.toString()} within two minutes.`);
}

function freePort(): Promise<number> {
  return new Promise((resolve, reject) => {
    const server = createServer();
    server.once("error", reject);
    server.listen(0, "127.0.0.1", () => {
      const address = server.address();
      server.close(() => {
        if (address && typeof address === "object") resolve(address.port);
        else reject(new Error("Couldn't find a free port."));
      });
    });
  });
}
