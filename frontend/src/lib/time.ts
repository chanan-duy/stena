const relative = new Intl.RelativeTimeFormat('en', { numeric: 'auto' });

let least: number | null = null;

// Date has whole seconds and arrives late, so each response only proves the server time is at least this much.
export function syncClock(date: string | null) {
	if (date) {
		const sample = Date.parse(date) - performance.now();
		least = least === null ? sample : Math.max(least, sample);
	}
}

export function serverNow() {
	const local = Date.now();
	if (least === null) {
		return local;
	}
	const server = least + performance.now();
	return local >= server && local < server + 2000 ? local : server + 500;
}

export function ago(iso: string, now: number) {
	const s = (new Date(iso).getTime() - now) / 1000;
	const [value, unit] =
		Math.abs(s) < 60
			? [s, 'second']
			: Math.abs(s) < 3600
				? [s / 60, 'minute']
				: Math.abs(s) < 86400
					? [s / 3600, 'hour']
					: [s / 86400, 'day'];
	return relative.format(Math.round(value), unit as Intl.RelativeTimeFormatUnit);
}

export function clock(ms: number) {
	const s = Math.max(0, Math.ceil(ms / 1000));
	return `${Math.floor(s / 60)}:${String(s % 60).padStart(2, '0')}`;
}

export function toLocalInput(date: Date) {
	const local = new Date(date.getTime() - date.getTimezoneOffset() * 60000);
	return local.toISOString().slice(0, 16);
}
