import { expect, test } from '@playwright/test';
import { cellColor, clickCell, createWall, dragCells, login, register } from './helpers';

test('a visitor opens a wall without logging in', async ({ page }) => {
	await page.goto('/');
	await page.getByText('Main', { exact: true }).click();
	await expect.poll(() => cellColor(page, 0, 0)).toBe('#ffffff');
	await expect(page.getByText('Log in to place pixels')).toBeVisible();
});

test('a placed pixel reaches another viewer live', async ({ page, browser }) => {
	await login(page, 'admin@stena.test', 'admin123');
	const wall = await createWall(page, `Live ${Date.now()}`);

	const viewer = await (await browser.newContext()).newPage();
	await viewer.goto(`/wall/${wall}`);
	await expect.poll(() => cellColor(viewer, 2, 3)).toBe('#ffffff');

	const name = `p${Date.now()}`;
	const player = await register(browser, name);
	await player.goto(`/wall/${wall}`);
	await clickCell(player, 2, 3);
	await player.getByRole('button', { name: 'Color #ff0000' }).click();
	await player.getByRole('button', { name: 'Place' }).click();

	await expect.poll(() => cellColor(viewer, 2, 3)).toBe('#ff0000');
	await clickCell(viewer, 2, 3);
	await expect(viewer.getByTestId('pixel-info')).toContainText(name);
});

test('an admin rolls back an area and bans the vandal', async ({ page, browser }) => {
	await login(page, 'admin@stena.test', 'admin123');
	const wall = await createWall(page, `Rollback ${Date.now()}`);

	const name = `v${Date.now()}`;
	const vandal = await register(browser, name);
	await vandal.goto(`/wall/${wall}`);
	for (const x of [0, 1, 2]) {
		await clickCell(vandal, x, 0);
		await vandal.getByRole('button', { name: 'Color #000000' }).click();
		const placed = vandal.waitForResponse((r) => r.url().endsWith(`/walls/${wall}/pixels`) && r.request().method() === 'POST');
		await vandal.getByRole('button', { name: 'Place' }).click();
		expect((await placed).ok()).toBe(true);
	}

	await page.goto(`/wall/${wall}`);
	await expect.poll(() => cellColor(page, 1, 0)).toBe('#000000');
	await clickCell(page, 1, 0);
	await expect(page.getByTestId('pixel-info')).toContainText(name);
	await page.getByRole('button', { name: 'Roll back an area' }).click();
	await dragCells(page, [0, 0], [2, 0]);
	await page.getByRole('checkbox', { name: `Ban ${name}` }).check();
	await page.getByLabel('Restore to').fill(new Date(Date.now() - 2 * 60_000 - new Date().getTimezoneOffset() * 60_000).toISOString().slice(0, 16));
	await page.getByRole('button', { name: 'Roll back', exact: true }).click();
	await expect(page.getByRole('status')).toHaveText('3 pixels restored');
	await expect.poll(() => cellColor(page, 1, 0)).toBe('#ffffff');

	await clickCell(vandal, 5, 5);
	await vandal.getByRole('button', { name: 'Place' }).click();
	await expect(vandal.getByRole('alert')).toHaveText('User is banned');
});
