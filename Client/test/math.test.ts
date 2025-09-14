import { describe, expect, test } from 'vitest';

describe('Math', () => {
  test('Calculate radian based on angle', () => {
    const angle = 90;
    const radian = angle * (Math.PI / 180);

    expect(radian).toBe(1.5707963267948966);
  });
});
