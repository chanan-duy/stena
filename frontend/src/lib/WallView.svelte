<script lang="ts">
	import { untrack } from 'svelte';
	import { ArrowLeft, ImageDown, LoaderCircle } from '@lucide/svelte';
	import Account from './Account.svelte';
	import { ApiError, api } from './api';
	import { auth } from './auth.svelte';
	import { watch } from './live';
	import Palette from './Palette.svelte';
	import { ago } from './time';
	import type { Charges, PixelInfo, PlaceResponse, Wall } from './types';
	import WallCanvas from './WallCanvas.svelte';

	let { wall }: { wall: Wall } = $props();

	let canvas = $state<WallCanvas>();
	let selected = $state<{ x: number; y: number } | null>(null);
	let info = $state<PixelInfo | null | 'none'>(null);
	let color = $state(untrack(() => Math.min(3, wall.palette.length - 1)));
	let charges = $state<Charges | null>(null);
	let fetchedAt = $state(0);
	let now = $state(Date.now());
	let notice = $state<{ text: string; ok: boolean } | null>(null);
	let placing = $state(false);

	const live = $derived(wall.status === 'Live');
	const nextMs = $derived(charges ? Math.max(0, charges.nextChargeMs - (now - fetchedAt)) : 0);

	$effect(() => {
		const feed = watch(wall.id, {
			bitmap: (b) => canvas?.setBitmap(b),
			pixels: (p) => canvas?.applyBatch(p),
		});
		return () => void feed.stop();
	});

	$effect(() => {
		const timer = setInterval(() => (now = Date.now()), 250);
		return () => clearInterval(timer);
	});

	$effect(() => {
		if (auth.user && live) {
			void loadCharges();
		}
	});

	$effect(() => {
		if (charges && charges.count < wall.maxCharges && nextMs === 0) {
			void loadCharges();
		}
	});

	async function loadCharges() {
		charges = await api<Charges>(`/walls/${wall.id}/charges`);
		fetchedAt = Date.now();
	}

	async function select(p: { x: number; y: number }) {
		selected = p;
		info = null;
		try {
			info = await api<PixelInfo>(`/history/walls/${wall.id}/pixels/${p.x}/${p.y}`);
		} catch {
			info = 'none';
		}
	}

	function say(text: string, ok = false) {
		const shown = { text, ok };
		notice = shown;
		setTimeout(() => notice === shown && (notice = null), 4000);
	}

	function fail(e: unknown) {
		say(e instanceof Error ? e.message : 'Something went wrong');
	}

	async function place() {
		if (!selected || !auth.user) {
			return;
		}
		placing = true;
		try {
			const res = await api<PlaceResponse>(`/walls/${wall.id}/pixels`, {
				method: 'POST',
				json: { x: selected.x, y: selected.y, color, requestId: crypto.randomUUID() },
			});
			canvas?.setPixel(selected.x, selected.y, color);
			charges = { count: res.charges, nextChargeMs: res.nextChargeMs };
			fetchedAt = Date.now();
			info = { ...selected, color, userId: auth.user.id, userName: auth.user.name, at: new Date().toISOString() };
		} catch (e) {
			fail(e);
			if (e instanceof ApiError && e.status === 429) {
				void loadCharges();
			}
		} finally {
			placing = false;
		}
	}
</script>

<div class="fixed inset-0 bg-zinc-950">
	<WallCanvas
		bind:this={canvas}
		width={wall.width}
		height={wall.height}
		palette={wall.palette}
		{selected}
		area={null}
		picking={false}
		onselect={select}
		onarea={() => {}}
	/>

	<header class="pointer-events-none absolute inset-x-0 top-0 flex items-start justify-between gap-3 p-3">
		<div class="panel pointer-events-auto flex h-11 items-center gap-1 pr-4 pl-1">
			<a class="icon-btn" href="/" aria-label="All walls" title="All walls"><ArrowLeft size={18} /></a>
			<span class="font-semibold text-white">{wall.name}</span>
			{#if !live}
				<span class="text-zinc-400">{wall.status === 'Upcoming' ? `starts ${ago(wall.startsAt)}` : 'finished'}</span>
			{/if}
		</div>

		<div class="panel pointer-events-auto flex h-11 items-center gap-1 px-1">
			<a class="icon-btn" href="/render/walls/{wall.id}/image.png?scale=4" target="_blank" aria-label="Image" title="Image">
				<ImageDown size={18} />
			</a>
			<div class="ml-1"><Account /></div>
		</div>
	</header>

	<footer class="pointer-events-none absolute inset-x-0 bottom-0 flex flex-col items-center gap-2 p-3">
		{#if notice}
			<div
				class="pointer-events-auto flex h-9 items-center gap-1.5 rounded-lg px-3 font-medium {notice.ok ? 'panel text-white' : 'bg-red-500/90 text-white'}"
				role={notice.ok ? 'status' : 'alert'}
			>
				{notice.text}
			</div>
		{/if}

		{#if selected}
			<div class="panel pointer-events-auto flex h-9 items-center gap-3 pr-1 pl-3" data-testid="pixel-info">
				<span class="text-zinc-400 tabular-nums">{selected.x}, {selected.y}</span>
				{#if info === null}
					<LoaderCircle size={14} class="animate-spin text-zinc-400" />
				{:else if info === 'none'}
					<span class="text-zinc-400">empty</span>
				{:else}
					<span class="h-3.5 w-3.5 rounded-sm border border-black/30" style="background: {wall.palette[info.color]}"></span>
					<span class="font-medium text-white">{info.userName}</span>
					<span class="text-zinc-400">{ago(info.at)}</span>
				{/if}
				<span class="w-1"></span>
			</div>
		{/if}

		{#if live && auth.user && charges}
			<div class="panel pointer-events-auto flex flex-wrap items-center justify-center gap-x-4 gap-y-2 p-2">
				<Palette
					palette={wall.palette}
					{color}
					charges={charges.count}
					maxCharges={wall.maxCharges}
					{nextMs}
					canPlace={selected !== null && charges.count > 0 && !placing}
					onpick={(c) => (color = c)}
					onplace={place}
				/>
			</div>
		{/if}
	</footer>
</div>
