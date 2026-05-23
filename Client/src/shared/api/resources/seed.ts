import type { ApiClient } from "@shared/api/client";
import { apiClient } from "@shared/api/client";

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
