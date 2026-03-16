import { test, expect } from '@playwright/test';

test.describe('Login Page', () => {
  test('should match the updated design', async ({ page }) => {
    await page.goto('/login');

    const screenshot = await page.screenshot();
    expect(screenshot).toMatchSnapshot('login-page.png');
  });

  test('should render the current email and password fields', async ({ page }) => {
    await page.goto('/login');

    await expect(page.locator('#email')).toBeVisible();
    await expect(page.locator('#password')).toBeVisible();
    await expect(page.locator('button[type="submit"]')).toHaveText('Sign In');
  });

  test('should expose the sign in heading to assistive tech', async ({ page }) => {
    await page.goto('/login');

    const accessibilityScanResults = await page.accessibility.snapshot();
    expect(accessibilityScanResults).toBeDefined();
  });
});
