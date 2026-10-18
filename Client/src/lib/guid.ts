const GUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export function isGuid(value: unknown): value is string {
  return typeof value === "string" && GUID_PATTERN.test(value.trim());
}

/** The API serializes GUIDs in lowercase; normalize user input to match. */
export function normalizeGuid(value: string): string {
  return value.trim().toLowerCase();
}

export function newGuid(): string {
  return crypto.randomUUID();
}
