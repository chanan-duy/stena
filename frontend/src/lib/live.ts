import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { MessagePackHubProtocol } from '@microsoft/signalr-protocol-msgpack';

function parse(id: string): [bigint, bigint] {
	const [ms, seq] = id.split('-');
	return [BigInt(ms), BigInt(seq)];
}

function after(a: string, b: string) {
	const [am, as] = parse(a);
	const [bm, bs] = parse(b);
	return am > bm || (am === bm && as > bs);
}

interface Batch {
	prev: string;
	last: string;
	pixels: Uint8Array;
}

export interface Feed {
	bitmap(pixels: Uint8Array): void;
	pixels(batch: Uint8Array): void;
	wall(): Promise<void>;
}

// The wall and its bitmap are downloaded after joining the wall group, so no edit or batch falls between them.
export function watch(wallId: number, feed: Feed) {
	let cursor: string | null = null;
	let buffer: Batch[] = [];
	let stale = false;
	let loading = false;
	const abort = new AbortController();
	const pause = (ms: number) => new Promise((r) => setTimeout(r, ms));

	const hub = new HubConnectionBuilder()
		.withUrl('/hub')
		.withHubProtocol(new MessagePackHubProtocol())
		.withAutomaticReconnect()
		.configureLogging(LogLevel.Warning)
		.build();

	function apply(batch: Batch) {
		if (cursor === null) {
			buffer.push(batch);
		} else if (!after(batch.last, cursor)) {
			return;
		} else if (after(batch.prev, cursor)) {
			cursor = null;
			buffer.push(batch);
			void reload();
		} else {
			feed.pixels(batch.pixels);
			cursor = batch.last;
		}
	}

	async function reload() {
		stale = true;
		if (loading) {
			return;
		}
		loading = true;
		while (stale && !abort.signal.aborted) {
			stale = false;
			cursor = null;
			try {
				await feed.wall();
				const res = await fetch(`/walls/${wallId}/bitmap`, { signal: abort.signal });
				const id = res.headers.get('x-stream-id');
				if (!res.ok || !id) {
					throw new Error(`Bitmap download failed: ${res.status}`);
				}
				feed.bitmap(new Uint8Array(await res.arrayBuffer()));
				cursor = id;
				const pending = buffer;
				buffer = [];
				pending.forEach(apply);
			} catch {
				if (!abort.signal.aborted) {
					stale = true;
					await pause(2000);
				}
			}
		}
		loading = false;
	}

	hub.on('pixels', (id: number, prev: string, last: string, pixels: Uint8Array) => {
		if (id === wallId) {
			apply({ prev, last, pixels });
		}
	});
	hub.on('wall', async (id: number) => {
		if (id === wallId) {
			await reload();
		}
	});
	hub.onreconnected(async () => {
		await hub.invoke('Watch', wallId);
		await reload();
	});

	void (async () => {
		while (!abort.signal.aborted) {
			try {
				await hub.start();
				await hub.invoke('Watch', wallId);
				await reload();
				return;
			} catch {
				if (hub.state !== HubConnectionState.Disconnected) {
					await hub.stop();
				}
				await pause(2000);
			}
		}
	})();

	return {
		stop: () => {
			abort.abort();
			return hub.stop();
		},
	};
}
