import { auth } from './auth.svelte';
import { syncClock } from './time';

export class ApiError extends Error {
	constructor(
		public status: number,
		message: string,
	) {
		super(message);
	}
}

export async function api<T>(path: string, init: RequestInit & { json?: unknown; fetch?: typeof fetch } = {}): Promise<T> {
	const headers = new Headers(init.headers);
	if (init.json !== undefined) {
		headers.set('content-type', 'application/json');
	}
	if (auth.token) {
		headers.set('authorization', `Bearer ${auth.token}`);
	}

	const res = await (init.fetch ?? fetch)(path, { ...init, headers, body: init.json !== undefined ? JSON.stringify(init.json) : init.body });
	syncClock(res.headers.get('date'));
	if (res.status === 401 && auth.token) {
		auth.set(null);
	}
	if (!res.ok) {
		const problem = await res.json().catch(() => null);
		throw new ApiError(res.status, problem?.detail ?? Object.values(problem?.errors ?? {}).flat()[0] ?? problem?.title ?? res.statusText);
	}

	return res.status === 204 ? (undefined as T) : res.json();
}
