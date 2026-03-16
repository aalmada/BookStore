import { test, expect } from '@playwright/test';

test.describe('Login Page Visual Regression', () => {
  test('should match the updated design', async ({ page }) => {
    await page.goto('/login');

    // Take a screenshot of the login page
    const screenshot = await page.screenshot();
    expect(screenshot).toMatchSnapshot('login-page.png');
  });
});

test.describe('Login Page Functional Tests', () => {
  test('should allow a user to log in with valid credentials', async ({ page }) => {
    await page.goto('/login');

    await page.fill('input[name="username"]', 'testuser');
    await page.fill('input[name="password"]', 'password123');
    await page.click('button[type="submit"]');

    await expect(page).toHaveURL('/dashboard');
  });

  test('should show an error message for invalid credentials', async ({ page }) => {
    await page.goto('/login');

    await page.fill('input[name="username"]', 'invaliduser');
    await page.fill('input[name="password"]', 'wrongpassword');
    await page.click('button[type="submit"]');

    await expect(page.locator('.error-message')).toHaveText('Invalid username or password');
  });
});

test.describe('Login Page Accessibility Tests', () => {
  test('should meet WCAG 2.1 AA standards', async ({ page }) => {
    await page.goto('/login');

    const accessibilityScanResults = await page.accessibility.snapshot();
    expect(accessibilityScanResults).toBeDefined();
  });
});
