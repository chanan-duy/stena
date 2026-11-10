<script lang="ts">
	import { Plus } from '@lucide/svelte';
	import Account from '#lib/Account.svelte';
	import { auth } from '#lib/auth.svelte.ts';
	import type { WallStatus } from '#lib/types.ts';

	let { data } = $props();

	const order: Record<WallStatus, number> = { Live: 0, Upcoming: 1, Finished: 2 };
	const walls = $derived([...data.walls].sort((a, b) => order[a.status] - order[b.status] || b.id - a.id));
	const dot: Record<WallStatus, string> = { Live: 'bg-emerald-400', Upcoming: 'bg-amber-400', Finished: 'bg-zinc-500' };
</script>

<div class="mx-auto max-w-6xl px-6 py-5">
	<header class="mb-8 flex items-center justify-between">
		<a href="/" class="text-lg font-semibold tracking-tight text-white">Stena</a>
		<div class="flex items-center gap-2">
			{#if auth.admin}
				<a class="btn-ghost" href="/wall/new"><Plus size={16} /> New wall</a>
			{/if}
			<Account />
		</div>
	</header>

	<div class="grid grid-cols-[repeat(auto-fill,minmax(14rem,1fr))] gap-x-5 gap-y-7">
		{#each walls as wall (wall.id)}
			<a href="/wall/{wall.id}" class="group">
				<div class="flex aspect-square items-center justify-center overflow-hidden rounded-xl bg-zinc-900 p-3 ring-1 ring-zinc-800 transition group-hover:ring-zinc-600">
					<img
						src="/render/walls/{wall.id}/thumbnail.png"
						alt=""
						class="h-full w-full object-contain [image-rendering:pixelated]"
					/>
				</div>
				<div class="mt-2.5 flex items-center gap-2">
					<span class="h-2 w-2 shrink-0 rounded-full {dot[wall.status]}" title={wall.status}></span>
					<span class="truncate font-medium text-white">{wall.name}</span>
					<span class="ml-auto text-zinc-500 tabular-nums">{wall.width}×{wall.height}</span>
				</div>
			</a>
		{/each}
	</div>
</div>
