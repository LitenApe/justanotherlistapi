import { describe, expect, test, vi } from 'vitest';

import { render } from 'vitest-browser-react';

describe('Button', () => {
  test('Call "onClick" when clicked', async () => {
    const mockOnClick = vi.fn();
    const { getByRole } = render(<button onClick={mockOnClick} />);

    const button = getByRole('button');
    await button.click();

    expect(mockOnClick).toHaveBeenCalled();
  });
});
