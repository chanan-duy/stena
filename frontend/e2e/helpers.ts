import { expect, type Browser, type Page } from '@playwright/test';

export async function login(page: Page, email: string, password: string) {
	await page.goto('/login');
	await page.getByPlaceholder('Email').fill(email);
	await page.getByPlaceholder('Password').fill(password);
	await page.getByRole('button', { name: 'Log in', exact: true }).last().click();
	await expect(page).toHaveURL('/');
}

export async function token(page: Page) {
	return page.evaluate(() => localStorage.getItem('stena.token'));
}

export async function createWall(page: Page, name: string, width = 8, height = 8) {
	const now = Date.now();
	const res = await page.request.post('/walls', {
		headers: { authorization: `Bearer ${await token(page)}` },
		data: {
			name,
			width,
			height,
			palette: ['#ffffff', '#000000', '#ff0000', '#0000ff'],
			chargeSeconds: 1,
			maxCharges: 20,
			startsAt: new Date(now - 60_000).toISOString(),
			endsAt: new Date(now + 3_600_000).toISOString(),
		},
	});
	expect(res.status()).toBe(201);
	return (await res.json()).id as number;
}

export async function register(browser: Browser, name: string) {
	const page = await (await browser.newContext()).newPage();
	await page.goto('/login');
	await page.getByRole('button', { name: 'Sign up' }).first().click();
	await page.getByPlaceholder('Email').fill(`${name}@example.com`);
	await page.getByPlaceholder('Name').fill(name);
	await page.getByPlaceholder('Password').fill(`${name}-password`);
	await page.getByRole('button', { name: 'Sign up' }).last().click();
	await expect(page).toHaveURL('/');
	return page;
}

async function view(page: Page) {
	const canvas = page.getByTestId('wall');
	await expect(canvas).toHaveAttribute('data-view', /^\d/);
	const [scale, ox, oy] = (await canvas.getAttribute('data-view'))!.split(',').map(Number);
	const box = (await canvas.boundingBox())!;
	return { scale, ox, oy, box };
}

export async function clickCell(page: Page, x: number, y: number) {
	const v = await view(page);
	await page.mouse.click(v.box.x + v.ox + (x + 0.5) * v.scale, v.box.y + v.oy + (y + 0.5) * v.scale);
}

export async function dragCells(page: Page, from: [number, number], to: [number, number]) {
	const v = await view(page);
	const at = ([x, y]: [number, number]) => [v.box.x + v.ox + (x + 0.5) * v.scale, v.box.y + v.oy + (y + 0.5) * v.scale] as const;
	await page.mouse.move(...at(from));
	await page.mouse.down();
	await page.mouse.move(...at(to), { steps: 5 });
	await page.mouse.up();
}

export async function cellColor(page: Page, x: number, y: number) {
	const v = await view(page);
	return page.getByTestId('wall').evaluate(
		(canvas: HTMLCanvasElement, p) => {
			const dpr = window.devicePixelRatio;
			const [r, g, b] = canvas.getContext('2d')!.getImageData(Math.round(p.x * dpr), Math.round(p.y * dpr), 1, 1).data;
			return `#${[r, g, b].map((c) => c.toString(16).padStart(2, '0')).join('')}`;
		},
		{ x: v.ox + (x + 0.5) * v.scale, y: v.oy + (y + 0.5) * v.scale },
	);
}
