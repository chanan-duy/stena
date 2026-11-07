<script lang="ts">
	import { clock } from './time';

	interface Props {
		palette: string[];
		color: number;
		charges: number;
		maxCharges: number;
		nextMs: number;
		canPlace: boolean;
		onpick: (c: number) => void;
		onplace: () => void;
	}

	let { palette, color, charges, maxCharges, nextMs, canPlace, onpick, onplace }: Props = $props();
</script>

<div class="flex flex-wrap items-center justify-center gap-1">
	{#each palette as c, i (i)}
		<button
			class="h-7 w-7 rounded-md border transition-transform {i === color
				? 'scale-110 border-white'
				: 'border-black/30 hover:scale-105'}"
			style="background: {c}"
			aria-label="Color {c}"
			aria-pressed={i === color}
			onclick={() => onpick(i)}
		></button>
	{/each}
</div>
<div class="flex items-center gap-3">
	<div class="flex items-center gap-1" title="Charges">
		{#if maxCharges <= 12}
			{#each { length: maxCharges }, i (i)}
				<span class="h-2.5 w-2.5 rounded-[3px] {i < charges ? 'bg-white' : 'bg-zinc-700'}"></span>
			{/each}
		{:else}
			<span class="font-medium tabular-nums">{charges}/{maxCharges}</span>
		{/if}
	</div>
	<button class="btn-primary min-w-20 tabular-nums" disabled={!canPlace} onclick={onplace}>
		{charges > 0 ? 'Place' : clock(nextMs)}
	</button>
</div>
