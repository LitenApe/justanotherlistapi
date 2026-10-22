import { startStack } from "./stack";

/** Starts the whole application once per run; the returned function stops it. */
export default async function globalSetup() {
  const started = Date.now();
  const stack = await startStack();
  // Workers start after global setup and inherit these (see support/database.ts).
  process.env.E2E_SQL_CONTAINER_ID = stack.database.containerId;
  process.env.E2E_SQL_USER = stack.database.user;
  process.env.E2E_SQL_PASSWORD = stack.database.password;
  console.log(
    `End-to-end stack ready in ${Math.round((Date.now() - started) / 1000).toString()}s: ` +
      `client ${stack.clientUrl}, Core ${stack.coreUrl}, OAuth ${stack.oauthUrl}`,
  );
  return stack.stop;
}
