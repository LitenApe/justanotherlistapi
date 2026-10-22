import { execFile } from "node:child_process";
import { promisify } from "node:util";
import { DATABASE, SQLCMD } from "./stack";

const exec = promisify(execFile);

/**
 * Runs SQL against the end-to-end database, for arranging what the UI can't,
 * such as moving a completion into the past. Use sparingly: tests should act
 * through the app wherever they can.
 */
export async function runSql(query: string): Promise<void> {
  const {
    E2E_SQL_CONTAINER_ID: container,
    E2E_SQL_USER: user,
    E2E_SQL_PASSWORD: password,
  } = process.env;
  if (!container || !user || !password) {
    throw new Error("The end-to-end database isn't running (start tests with test:e2e).");
  }
  // -b makes sqlcmd fail on SQL errors instead of printing them.
  await exec("docker", [
    "exec",
    container,
    SQLCMD,
    "-S",
    "localhost",
    "-U",
    user,
    "-P",
    password,
    "-C",
    "-b",
    "-d",
    DATABASE,
    "-Q",
    query,
  ]);
}
