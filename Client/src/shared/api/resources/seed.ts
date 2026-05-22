import type { ApiClient } from "../client";
import { apiClient } from "../client";

export interface SeedResource {
  seed(): Promise<void>;
}

export function createSeedResource(client: ApiClient): SeedResource {
  return {
    seed() {
      return client.post<void>("/api/dev/seed");
    },
  };
}

export const seedResource: SeedResource = createSeedResource(apiClient);
