import adapter from '@sveltejs/adapter-static';
import { sveltekit } from '@sveltejs/kit/vite';
import tailwindcss from '@tailwindcss/vite';
import { defineConfig } from 'vite';

const gateway = process.env.GATEWAY ?? 'http://localhost:8080';

export default defineConfig({
	plugins: [tailwindcss(), sveltekit({ adapter: adapter({ fallback: '200.html' }) })],
	server: {
		proxy: {
			'/auth': gateway,
			'/walls': gateway,
			'/history': gateway,
			'/render': gateway,
			'/hub': { target: gateway, ws: true },
		},
	},
});
