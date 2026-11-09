<script lang="ts">
	import type { Area } from './types';

	interface Point {
		x: number;
		y: number;
	}

	interface Props {
		width: number;
		height: number;
		palette: string[];
		selected: Point | null;
		area: Area | null;
		picking: boolean;
		onselect: (p: Point) => void;
		onarea: (a: Area) => void;
	}

	let { width, height, palette, selected, area, picking, onselect, onarea }: Props = $props();

	let view: HTMLCanvasElement;
	let box: HTMLDivElement;
	let scale = $state(1);
	let ox = $state(0);
	let oy = $state(0);
	let hover = $state<Point | null>(null);
	let size = { w: 0, h: 0 };
	let fitted = false;
	let frame = 0;

	const buffer = $derived.by(() => {
		const c = document.createElement('canvas');
		c.width = width;
		c.height = height;
		return c;
	});
	const bctx = $derived(buffer.getContext('2d')!);
	const image = $derived(bctx.createImageData(width, height));
	const pixels = $derived(new Uint32Array(image.data.buffer));
	const colors = $derived(
		palette.map((c) => {
			const n = parseInt(c.slice(1), 16);
			return ((255 << 24) | ((n & 0xff) << 16) | (n & 0xff00) | (n >> 16)) >>> 0;
		}),
	);

	export function setBitmap(bytes: Uint8Array) {
		for (let i = 0; i < pixels.length; i++) {
			pixels[i] = colors[bytes[i]] ?? colors[0];
		}
		flush();
	}

	export function applyBatch(bytes: Uint8Array) {
		const data = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
		for (let i = 0; i + 5 <= bytes.length; i += 5) {
			pixels[data.getUint16(i + 2, true) * width + data.getUint16(i, true)] = colors[bytes[i + 4]] ?? colors[0];
		}
		flush();
	}

	function flush() {
		bctx.putImageData(image, 0, 0);
		draw();
	}

	function draw() {
		cancelAnimationFrame(frame);
		frame = requestAnimationFrame(paint);
	}

	function paint() {
		const ctx = view.getContext('2d')!;
		const dpr = devicePixelRatio;
		ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
		ctx.clearRect(0, 0, size.w, size.h);
		ctx.imageSmoothingEnabled = false;
		ctx.drawImage(buffer, ox, oy, width * scale, height * scale);

		if (scale >= 12) {
			ctx.strokeStyle = 'rgba(0, 0, 0, 0.12)';
			ctx.lineWidth = 1;
			ctx.beginPath();
			for (let x = 0; x <= width; x++) {
				ctx.moveTo(Math.round(ox + x * scale) + 0.5, oy);
				ctx.lineTo(Math.round(ox + x * scale) + 0.5, oy + height * scale);
			}
			for (let y = 0; y <= height; y++) {
				ctx.moveTo(ox, Math.round(oy + y * scale) + 0.5);
				ctx.lineTo(ox + width * scale, Math.round(oy + y * scale) + 0.5);
			}
			ctx.stroke();
		}

		if (hover && !picking) {
			outline(hover.x, hover.y, 1, 1, 'rgba(255, 255, 255, 0.5)');
		}
		if (selected) {
			outline(selected.x, selected.y, 1, 1, '#fff');
		}
		if (area) {
			outline(area.x, area.y, area.width, area.height, '#fff', [6, 4]);
		}
	}

	function outline(x: number, y: number, w: number, h: number, color: string, dash: number[] = []) {
		const ctx = view.getContext('2d')!;
		const rect = [Math.round(ox + x * scale) - 1.5, Math.round(oy + y * scale) - 1.5, w * scale + 3, h * scale + 3] as const;
		ctx.setLineDash(dash);
		ctx.lineWidth = 3;
		ctx.strokeStyle = 'rgba(0, 0, 0, 0.6)';
		ctx.strokeRect(...rect);
		ctx.lineWidth = 1;
		ctx.strokeStyle = color;
		ctx.strokeRect(...rect);
		ctx.setLineDash([]);
	}

	function resize() {
		const r = box.getBoundingClientRect();
		size = { w: r.width, h: r.height };
		view.width = Math.round(r.width * devicePixelRatio);
		view.height = Math.round(r.height * devicePixelRatio);
		if (!fitted && r.width > 0) {
			const [top, bottom] = [64, 112];
			const fit = Math.min(64, (r.width * 0.85) / width, (r.height - top - bottom) / height);
			scale = fit >= 2 ? Math.floor(fit) : fit;
			ox = (r.width - width * scale) / 2;
			oy = top + (r.height - top - bottom - height * scale) / 2;
			fitted = true;
		}
		draw();
	}

	function cell(e: PointerEvent | WheelEvent): Point | null {
		const r = view.getBoundingClientRect();
		const x = Math.floor((e.clientX - r.left - ox) / scale);
		const y = Math.floor((e.clientY - r.top - oy) / scale);
		return x >= 0 && y >= 0 && x < width && y < height ? { x, y } : null;
	}

	let drag: { x: number; y: number; ox: number; oy: number; from: Point | null; moved: boolean } | null = null;

	function down(e: PointerEvent) {
		if (e.button !== 0) {
			return;
		}
		view.setPointerCapture(e.pointerId);
		drag = { x: e.clientX, y: e.clientY, ox, oy, from: cell(e), moved: false };
	}

	function move(e: PointerEvent) {
		hover = cell(e);
		if (!drag) {
			draw();
			return;
		}
		const dx = e.clientX - drag.x;
		const dy = e.clientY - drag.y;
		drag.moved ||= Math.abs(dx) + Math.abs(dy) > 4;
		if (picking && drag.from) {
			const to = clamp(e);
			onarea({
				x: Math.min(drag.from.x, to.x),
				y: Math.min(drag.from.y, to.y),
				width: Math.abs(to.x - drag.from.x) + 1,
				height: Math.abs(to.y - drag.from.y) + 1,
			});
		} else if (drag.moved) {
			ox = drag.ox + dx;
			oy = drag.oy + dy;
		}
	}

	function up(e: PointerEvent) {
		if (drag && !drag.moved && !picking) {
			const p = cell(e);
			if (p) {
				onselect(p);
			}
		}
		drag = null;
	}

	function clamp(e: PointerEvent): Point {
		const r = view.getBoundingClientRect();
		return {
			x: Math.min(width - 1, Math.max(0, Math.floor((e.clientX - r.left - ox) / scale))),
			y: Math.min(height - 1, Math.max(0, Math.floor((e.clientY - r.top - oy) / scale))),
		};
	}

	function wheel(e: WheelEvent) {
		e.preventDefault();
		const r = view.getBoundingClientRect();
		const cx = e.clientX - r.left;
		const cy = e.clientY - r.top;
		const delta = e.deltaMode === WheelEvent.DOM_DELTA_LINE ? e.deltaY * 33 : e.deltaY;
		const next = Math.min(64, Math.max(0.25, scale * Math.exp(-delta * 0.002)));
		ox = cx - ((cx - ox) * next) / scale;
		oy = cy - ((cy - oy) * next) / scale;
		scale = next;
	}

	$effect(() => {
		const observer = new ResizeObserver(resize);
		observer.observe(box);
		return () => observer.disconnect();
	});

	$effect(() => {
		void [selected, area, scale, ox, oy, picking];
		draw();
	});
</script>

<div bind:this={box} class="absolute inset-0 overflow-hidden">
	<canvas
		bind:this={view}
		class="block h-full w-full touch-none {picking ? 'cursor-crosshair' : 'cursor-default'}"
		data-testid="wall"
		data-view="{scale},{ox},{oy}"
		onpointerdown={down}
		onpointermove={move}
		onpointerup={up}
		onpointerleave={() => (hover = null)}
		onwheel={wheel}
	></canvas>
</div>
