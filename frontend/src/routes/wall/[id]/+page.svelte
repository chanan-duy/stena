<script lang="ts">
	import WallView from '#lib/WallView.svelte';

	let { data } = $props();
</script>

<svelte:head><title>{data.wall.name} · Stena</title></svelte:head>

{#key data.wall.id}
	<WallView wall={data.wall} />
{/key}
