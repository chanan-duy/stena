import { api } from '#lib/api.ts';
import type { Wall } from '#lib/types.ts';

export async function load({ params, fetch }) {
	return { wall: await api<Wall>(`/walls/${params.id}`, { fetch }) };
}
