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
}

// The bitmap is downloaded after joining the wall group, so no batch falls between them.
export function watch(wallId: number, feed: Feed) {
	let cursor: string | null = null;
	let buffer: Batch[] = [];
	let loading: Promise<void> | null = null;

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
			void reload();
		} else {
			feed.pixels(batch.pixels);
			cursor = batch.last;
		}
	}

	function reload() {
		loading ??= (async () => {
			cursor = null;
			const res = await fetch(`/walls/${wallId}/bitmap`);
			const id = res.headers.get('x-stream-id') ?? '0-0';
			feed.bitmap(new Uint8Array(await res.arrayBuffer()));
			cursor = id;
			const pending = buffer;
			buffer = [];
			pending.forEach(apply);
		})().finally(() => (loading = null));
		return loading;
	}

	hub.on('pixels', (id: number, prev: string, last: string, pixels: Uint8Array) => {
		if (id === wallId) {
			apply({ prev, last, pixels });
		}
	});
	hub.onreconnected(async () => {
		await hub.invoke('Watch', wallId);
		await reload();
	});

	const started = (async () => {
		await hub.start();
		await hub.invoke('Watch', wallId);
		await reload();
	})();

	return {
		started,
		stop: async () => {
			await started.catch(() => {});
			if (hub.state !== HubConnectionState.Disconnected) {
				await hub.stop();
			}
		},
	};
}
