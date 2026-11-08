<script lang="ts">
	import WallForm from '#lib/WallForm.svelte';

	let { data } = $props();
</script>

<svelte:head><title>Edit {data.wall.name} · Stena</title></svelte:head>

<WallForm wall={data.wall} />
