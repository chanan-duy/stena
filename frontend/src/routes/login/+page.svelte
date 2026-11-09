<script lang="ts">
	import { goto } from '$app/navigation';
	import { page } from '$app/state';
	import { api } from '#lib/api.ts';
	import { auth } from '#lib/auth.svelte.ts';

	let signup = $state(false);
	let email = $state('');
	let name = $state('');
	let password = $state('');
	let error = $state('');
	let busy = $state(false);

	async function submit(e: SubmitEvent) {
		e.preventDefault();
		busy = true;
		error = '';
		try {
			const res = await api<{ token: string }>(signup ? '/auth/register' : '/auth/login', {
				method: 'POST',
				json: signup ? { email, name, password } : { email, password },
			});
			auth.set(res.token);
			await goto(page.url.searchParams.get('next') ?? '/');
		} catch (e) {
			error = e instanceof Error ? e.message : 'Something went wrong';
		} finally {
			busy = false;
		}
	}
</script>

<svelte:head><title>{signup ? 'Sign up' : 'Log in'} · Stena</title></svelte:head>

<div class="flex min-h-full justify-center p-6 pt-[18vh]">
	<form class="w-full max-w-xs" onsubmit={submit}>
		<a href="/" class="mb-8 block text-center text-lg font-semibold tracking-tight text-white">Stena</a>
		<div class="mb-5 grid grid-cols-2 rounded-lg bg-zinc-900 p-1">
			<button type="button" class="btn h-8 {signup ? 'text-zinc-400' : 'bg-zinc-700 text-white'}" onclick={() => ((signup = false), (error = ''))}>
				Log in
			</button>
			<button type="button" class="btn h-8 {signup ? 'bg-zinc-700 text-white' : 'text-zinc-400'}" onclick={() => ((signup = true), (error = ''))}>
				Sign up
			</button>
		</div>
		<div class="space-y-2">
			<input class="input" type="email" placeholder="Email" autocomplete="email" required bind:value={email} />
			{#if signup}
				<input class="input" placeholder="Name" autocomplete="username" required bind:value={name} />
			{/if}
			<input
				class="input"
				type="password"
				placeholder="Password"
				autocomplete={signup ? 'new-password' : 'current-password'}
				required
				bind:value={password}
			/>
		</div>
		<button class="btn-primary mt-5 w-full" disabled={busy}>{signup ? 'Sign up' : 'Log in'}</button>
		{#if error}<p class="mt-3 text-red-400" role="alert">{error}</p>{/if}
	</form>
</div>
