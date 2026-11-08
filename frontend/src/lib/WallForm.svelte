<script lang="ts">
	import { goto } from '$app/navigation';
	import { untrack } from 'svelte';
	import { ArrowLeft } from '@lucide/svelte';
	import { api } from './api';
	import { toLocalInput } from './time';
	import type { Wall, WallSpec } from './types';

	let { wall: given }: { wall?: Wall } = $props();

	const wall = untrack(() => given);

	const defaults = '#ffffff #e4e4e4 #888888 #222222 #ffa7d1 #e50000 #e59500 #a06a42 #e5d900 #94e044 #02be01 #00d3dd #0083c7 #0000ea #cf6ee4 #820080';
	const locked = wall !== undefined && wall.status !== 'Upcoming';

	let name = $state(wall?.name ?? '');
	let width = $state(wall?.width ?? 256);
	let height = $state(wall?.height ?? 256);
	let colors = $state(wall?.palette.join(' ') ?? defaults);
	let chargeSeconds = $state(wall?.chargeSeconds ?? 30);
	let maxCharges = $state(wall?.maxCharges ?? 10);
	let startsAt = $state(toLocalInput(wall ? new Date(wall.startsAt) : new Date()));
	let endsAt = $state(toLocalInput(wall ? new Date(wall.endsAt) : new Date(Date.now() + 7 * 86400000)));
	let error = $state('');

	const palette = $derived(colors.split(/[\s,]+/).filter(Boolean).map((c) => c.toLowerCase()));

	async function submit(e: SubmitEvent) {
		e.preventDefault();
		const spec: WallSpec = {
			name,
			width,
			height,
			palette,
			chargeSeconds,
			maxCharges,
			startsAt: locked ? wall!.startsAt : new Date(startsAt).toISOString(),
			endsAt: new Date(endsAt).toISOString(),
		};
		try {
			const saved = wall
				? await api<Wall>(`/walls/${wall.id}`, { method: 'PUT', json: spec })
				: await api<Wall>('/walls', { method: 'POST', json: spec });
			await goto(`/wall/${saved.id}`);
		} catch (e) {
			error = e instanceof Error ? e.message : 'Something went wrong';
		}
	}
</script>

<div class="mx-auto max-w-lg px-6 py-5">
	<header class="mb-6 flex items-center gap-1">
		<a class="icon-btn -ml-2" href={wall ? `/wall/${wall.id}` : '/'} aria-label="Back"><ArrowLeft size={18} /></a>
		<h1 class="font-semibold text-white">{wall ? 'Edit wall' : 'New wall'}</h1>
	</header>

	<form class="space-y-4" onsubmit={submit}>
		<label class="block space-y-1.5">
			<span class="text-zinc-400">Name</span>
			<input class="input" required bind:value={name} />
		</label>
		<div class="grid grid-cols-2 gap-3">
			<label class="block space-y-1.5">
				<span class="text-zinc-400">Width</span>
				<input class="input" type="number" min="1" max="1024" disabled={locked} bind:value={width} />
			</label>
			<label class="block space-y-1.5">
				<span class="text-zinc-400">Height</span>
				<input class="input" type="number" min="1" max="1024" disabled={locked} bind:value={height} />
			</label>
		</div>
		<label class="block space-y-1.5">
			<span class="text-zinc-400">Palette</span>
			{#if !locked}
				<textarea class="input h-auto py-2 font-mono" rows="3" bind:value={colors}></textarea>
			{/if}
			<div class="flex flex-wrap gap-1 pt-1">
				{#each palette as c, i (i)}
					<span class="h-5 w-5 rounded border border-black/30" style="background: {c}"></span>
				{/each}
			</div>
		</label>
		<div class="grid grid-cols-2 gap-3">
			<label class="block space-y-1.5">
				<span class="text-zinc-400">Charge, seconds</span>
				<input class="input" type="number" min="1" max="3600" bind:value={chargeSeconds} />
			</label>
			<label class="block space-y-1.5">
				<span class="text-zinc-400">Max charges</span>
				<input class="input" type="number" min="1" max="100" bind:value={maxCharges} />
			</label>
		</div>
		<div class="grid grid-cols-2 gap-3">
			<label class="block space-y-1.5">
				<span class="text-zinc-400">Starts</span>
				<input class="input" type="datetime-local" disabled={locked} bind:value={startsAt} />
			</label>
			<label class="block space-y-1.5">
				<span class="text-zinc-400">Ends</span>
				<input class="input" type="datetime-local" bind:value={endsAt} />
			</label>
		</div>
		{#if error}<p class="text-red-400" role="alert">{error}</p>{/if}
		<button class="btn-primary w-full">{wall ? 'Save' : 'Create'}</button>
	</form>
</div>
