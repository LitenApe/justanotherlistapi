import test, { expect } from 'playwright/test';

test.beforeEach(async ({ page }) => {
  await page.goto('/');
});

test.describe('When the user visit the landing the', () => {
  test('Then they are greeted with a link to react routers documentation', async ({
    page,
  }) => {
    const link = page.getByRole('link', { name: 'React Router Docs' });

    await expect(link).toBeInViewport();
    await expect(link).toHaveAttribute('href', 'https://reactrouter.com/docs');
  });
});
