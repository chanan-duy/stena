<script lang="ts">
	import { page } from '$app/state';
	import { LogOut } from '@lucide/svelte';
	import { auth } from './auth.svelte';

	let open = $state(false);
</script>

{#if auth.user}
	<div class="relative">
		<button
			class="flex h-9 w-9 items-center justify-center rounded-full bg-zinc-700 font-semibold text-white hover:bg-zinc-600"
			aria-label="Account"
			onclick={() => (open = !open)}
		>
			{auth.user.name[0].toUpperCase()}
		</button>
		{#if open}
			<div class="panel absolute top-11 -right-[5px] z-20 w-48 p-1">
				<div class="px-3 py-2">
					<div class="font-medium text-white">{auth.user.name}</div>
					{#if auth.admin}<div class="text-zinc-400">Admin</div>{/if}
				</div>
				<button class="btn-ghost w-full justify-start" onclick={() => (auth.set(null), (open = false))}>
					<LogOut size={16} /> Log out
				</button>
			</div>
		{/if}
	</div>
{:else}
	<a class="btn-primary" href="/login?next={encodeURIComponent(page.url.pathname)}">Log in</a>
{/if}
