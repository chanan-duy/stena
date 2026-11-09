<script lang="ts">
	import { untrack } from 'svelte';
	import { ArrowLeft, Ban, Check, Film, ImageDown, LoaderCircle, Pencil, Undo2, X } from '@lucide/svelte';
	import Account from './Account.svelte';
	import { ApiError, api } from './api';
	import { auth } from './auth.svelte';
	import { watch } from './live';
	import Palette from './Palette.svelte';
	import { ago, serverNow, toLocalInput } from './time';
	import type { Area, Charges, PixelInfo, PlaceResponse, Rollback, Timelapse, Wall } from './types';
	import WallCanvas from './WallCanvas.svelte';

	let { wall }: { wall: Wall } = $props();
	const id = untrack(() => wall.id);

	let canvas = $state<WallCanvas>();
	let selected = $state<{ x: number; y: number } | null>(null);
	let info = $state<PixelInfo | null | 'none'>(null);
	let banned = $state<string | null>(null);
	let color = $state(untrack(() => Math.min(3, wall.palette.length - 1)));
	let charges = $state<Charges | null>(null);
	let fetchedAt = $state(0);
	let now = $state(serverNow());
	let notice = $state<{ text: string; ok: boolean } | null>(null);
	let placing = $state(false);

	let picking = $state(false);
	let area = $state<Area | null>(null);
	let moment = $state(toLocalInput(new Date(serverNow() - 10 * 60000)));
	let banVandal = $state(true);
	let rollback = $state<Rollback | null>(null);
	let timelapse = $state<Timelapse | null>(null);

	const status = $derived(now < Date.parse(wall.startsAt) ? 'Upcoming' : now < Date.parse(wall.endsAt) ? 'Live' : 'Finished');
	const live = $derived(status === 'Live');
	const rules = $derived(`${wall.chargeSeconds}/${wall.maxCharges}`);
	const nextMs = $derived(charges ? Math.max(0, charges.nextChargeMs - (now - fetchedAt)) : 0);
	const vandal = $derived(info && info !== 'none' && info.userId !== auth.user?.id ? info : null);

	$effect(() => {
		const feed = watch(id, {
			bitmap: (b) => canvas?.setBitmap(b),
			pixels: (p) => canvas?.applyBatch(p),
			wall: async () => {
				wall = await api<Wall>(`/walls/${id}`);
				color = Math.min(color, wall.palette.length - 1);
				if (selected && (selected.x >= wall.width || selected.y >= wall.height)) {
					selected = null;
				}
			},
		});
		return () => void feed.stop();
	});

	$effect(() => {
		const timer = setInterval(() => (now = serverNow()), 250);
		return () => clearInterval(timer);
	});

	$effect(() => {
		void rules;
		if (auth.user && live) {
			void loadCharges();
		}
	});

	$effect(() => {
		if (charges && charges.count < wall.maxCharges && nextMs === 0) {
			void loadCharges();
		}
	});

	let alive = true;
	$effect(() => () => (alive = false));

	$effect(() => {
		api<Timelapse>(`/render/walls/${id}/timelapse`).then((t) => follow(t, false), () => {});
	});

	async function loadCharges() {
		charges = await api<Charges>(`/walls/${id}/charges`);
		now = fetchedAt = serverNow();
	}

	let lookup = 0;

	async function select(p: { x: number; y: number }) {
		selected = p;
		const n = ++lookup;
		info = null;
		const found = await api<PixelInfo>(`/history/walls/${id}/pixels/${p.x}/${p.y}`).catch(() => 'none' as const);
		if (n === lookup) {
			info = found;
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

	function newId() {
		const hex = [...crypto.getRandomValues(new Uint8Array(16))].map((b) => b.toString(16).padStart(2, '0')).join('');
		return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
	}

	async function place() {
		const at = selected;
		const user = auth.user;
		const c = color;
		if (!at || !user) {
			return;
		}
		placing = true;
		try {
			const res = await api<PlaceResponse>(`/walls/${id}/pixels`, {
				method: 'POST',
				json: { x: at.x, y: at.y, color: c, requestId: newId() },
			});
			charges = { count: res.charges, nextChargeMs: res.nextChargeMs };
			now = fetchedAt = serverNow();
			if (selected === at) {
				lookup++;
				info = { ...at, color: c, userId: user.id, userName: user.name, at: new Date(serverNow()).toISOString() };
			}
		} catch (e) {
			fail(e);
			if (e instanceof ApiError && e.status === 429) {
				void loadCharges();
			}
		} finally {
			placing = false;
		}
	}

	async function ban(userId: string) {
		try {
			await api(`/auth/users/${userId}/ban`, { method: 'POST' });
			banned = userId;
		} catch (e) {
			fail(e);
		}
	}

	function togglePicking() {
		picking = !picking;
		area = null;
		rollback = null;
	}

	async function startRollback() {
		if (!area) {
			return;
		}
		try {
			let job = (rollback = await api<Rollback>(`/walls/${id}/rollbacks`, {
				method: 'POST',
				json: { ...area, at: new Date(moment).toISOString(), vandalId: banVandal && vandal ? vandal.userId : null },
			}));
			while (alive && !['Completed', 'Failed'].includes(job.state)) {
				await new Promise((r) => setTimeout(r, 500));
				job = await api<Rollback>(`/walls/${id}/rollbacks/${job.id}`);
				if (picking) {
					rollback = job;
				}
			}
			if (job.state === 'Completed') {
				say(job.changed ? `${job.changed} ${job.changed === 1 ? 'pixel' : 'pixels'} restored` : 'Nothing to restore', true);
			} else if (job.state === 'Failed') {
				say(job.error ?? 'Rollback failed');
			}
		} catch (e) {
			fail(e);
		}
	}

	async function follow(job: Timelapse, announce: boolean) {
		timelapse = job;
		while (alive && ['Queued', 'Running'].includes(job.status)) {
			await new Promise((r) => setTimeout(r, 1500));
			try {
				job = timelapse = await api<Timelapse>(`/render/walls/${id}/timelapse`);
			} catch {
				continue;
			}
			announce = true;
		}
		if (announce && job.status === 'Failed') {
			say(job.error ?? 'Timelapse failed');
		}
	}

	async function makeTimelapse() {
		try {
			await follow(await api<Timelapse>(`/render/walls/${id}/timelapse`, { method: 'POST' }), true);
		} catch (e) {
			fail(e);
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
		{area}
		{picking}
		onselect={select}
		onarea={(a) => (area = a)}
	/>

	<header class="pointer-events-none absolute inset-x-0 top-0 flex items-start justify-between gap-3 p-3">
		<div class="panel pointer-events-auto flex h-11 items-center gap-1 pr-4 pl-1">
			<a class="icon-btn" href="/" aria-label="All walls" title="All walls"><ArrowLeft size={18} /></a>
			<span class="font-semibold text-white">{wall.name}</span>
			{#if !live}
				<span class="text-zinc-400">{status === 'Upcoming' ? `starts ${ago(wall.startsAt, now)}` : 'finished'}</span>
			{/if}
		</div>

		<div class="panel pointer-events-auto flex h-11 items-center gap-1 px-1">
			<a class="icon-btn" href="/render/walls/{id}/image.png?scale=4" target="_blank" aria-label="Image" title="Image">
				<ImageDown size={18} />
			</a>
			{#if timelapse?.status === 'Done'}
				<a class="icon-btn" href={timelapse.video} target="_blank" aria-label="Timelapse" title="Timelapse"><Film size={18} /></a>
			{:else if auth.admin}
				<button
					class="icon-btn"
					aria-label="Make timelapse" title="Make timelapse"
					disabled={timelapse?.status === 'Queued' || timelapse?.status === 'Running'}
					onclick={makeTimelapse}
				>
					{#if timelapse?.status === 'Queued' || timelapse?.status === 'Running'}
						<LoaderCircle size={18} class="animate-spin" />
					{:else}
						<Film size={18} />
					{/if}
				</button>
			{/if}
			{#if auth.admin}
				<a class="icon-btn" href="/wall/{id}/edit" aria-label="Edit wall" title="Edit wall"><Pencil size={18} /></a>
				<button class="icon-btn {picking ? 'bg-zinc-700 text-white' : ''}" aria-label="Roll back an area" title="Roll back an area" onclick={togglePicking}>
					<Undo2 size={18} />
				</button>
			{/if}
			<div class="ml-1"><Account /></div>
		</div>
	</header>

	<footer class="pointer-events-none absolute inset-x-0 bottom-0 flex flex-col items-center gap-2 p-3">
		{#if notice}
			<div
				class="pointer-events-auto flex h-9 items-center gap-1.5 rounded-lg px-3 font-medium {notice.ok ? 'panel text-white' : 'bg-red-500/90 text-white'}"
				role={notice.ok ? 'status' : 'alert'}
			>
				{#if notice.ok}<Check size={16} />{/if}
				{notice.text}
			</div>
		{/if}

		{#if selected && !picking}
			<div class="panel pointer-events-auto flex h-9 items-center gap-3 pr-1 pl-3" data-testid="pixel-info">
				<span class="text-zinc-400 tabular-nums">{selected.x}, {selected.y}</span>
				{#if info === null}
					<LoaderCircle size={14} class="animate-spin text-zinc-400" />
				{:else if info === 'none'}
					<span class="text-zinc-400">empty</span>
				{:else}
					<span class="h-3.5 w-3.5 rounded-sm border border-black/30" style="background: {wall.palette[info.color]}"></span>
					<span class="font-medium text-white">{info.userName}</span>
					<span class="text-zinc-400">{ago(info.at, now)}</span>
					{#if auth.admin && vandal}
						{#if banned === vandal.userId}
							<span class="flex items-center gap-1 pr-2 text-zinc-400"><Check size={14} /> banned</span>
						{:else}
							<button class="icon-btn h-7 w-7 hover:text-red-400" aria-label="Ban {vandal.userName}" title="Ban {vandal.userName}" onclick={() => ban(vandal.userId)}>
								<Ban size={15} />
							</button>
						{/if}
					{/if}
				{/if}
				{#if !(auth.admin && vandal)}<span class="w-1"></span>{/if}
			</div>
		{/if}

		{#if picking}
			<div class="panel pointer-events-auto flex flex-wrap items-center gap-2 p-2">
				<span class="px-1 text-zinc-300 tabular-nums">{area ? `${area.width} × ${area.height}` : 'Drag over the wall'}</span>
				<input class="input w-auto" type="datetime-local" bind:value={moment} aria-label="Restore to" title="Restore to" />
				{#if vandal}
					<label class="flex h-9 items-center gap-2 rounded-lg px-2 text-zinc-300 hover:bg-zinc-800">
						<input type="checkbox" class="accent-white" bind:checked={banVandal} /> Ban {vandal.userName}
					</label>
				{/if}
				<button
					class="btn-primary w-24"
					disabled={!area || (rollback !== null && !['Completed', 'Failed'].includes(rollback.state))}
					onclick={startRollback}
				>
					{#if rollback && !['Completed', 'Failed'].includes(rollback.state)}
						<LoaderCircle size={16} class="animate-spin" />
					{:else}
						Roll back
					{/if}
				</button>
				<button class="icon-btn" aria-label="Close" title="Close" onclick={togglePicking}><X size={18} /></button>
			</div>
		{:else if live && auth.user && charges}
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
