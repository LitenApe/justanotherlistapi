import { HttpResponse, http } from 'msw';
import { afterAll, beforeAll, describe, expect, test } from 'vitest';

import { setupWorker } from 'msw/browser';

const worker = setupWorker();

describe('Today', () => {
  beforeAll(async () => {
    await worker.start({ quiet: true });
  });

  afterAll(() => {
    worker.stop();
  });

  test('API returns todays date', async () => {
    const expected = new Date();
    worker.use(
      http.get('/api/today', () => HttpResponse.text(expected.toISOString())),
    );

    const response = await fetch('/api/today');

    expect(response.status).toBe(200);

    const responseBody = await response.text();
    const actual = new Date(responseBody);

    expect(actual).toStrictEqual(expected);
  });
});
