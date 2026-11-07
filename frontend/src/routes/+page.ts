import { api } from '#lib/api.ts';
import type { Wall } from '#lib/types.ts';

export async function load({ fetch }) {
	return { walls: await api<Wall[]>('/walls', { fetch }) };
}
