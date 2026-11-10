import { api } from '#lib/api.ts';
import type { Timelapse, Wall } from '#lib/types.ts';

export async function load({ params, fetch }) {
	const timelapse = api<Timelapse>(`/render/walls/${params.id}/timelapse`, { fetch }).catch(() => null);
	return { wall: await api<Wall>(`/walls/${params.id}`, { fetch }), timelapse: await timelapse };
}
