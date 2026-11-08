import { defineConfig } from '@playwright/test';

export default defineConfig({
	testDir: 'e2e',
	timeout: 30_000,
	use: {
		baseURL: process.env.BASE ?? 'http://localhost:8080',
		viewport: { width: 1280, height: 800 },
		launchOptions: process.env.CHROMIUM ? { executablePath: process.env.CHROMIUM } : {},
	},
});
