export type WallStatus = 'Upcoming' | 'Live' | 'Finished';

export interface Wall {
	id: number;
	name: string;
	width: number;
	height: number;
	palette: string[];
	chargeSeconds: number;
	maxCharges: number;
	startsAt: string;
	endsAt: string;
	status: WallStatus;
}

export type WallSpec = Omit<Wall, 'id' | 'status'>;

export interface Charges {
	count: number;
	nextChargeMs: number;
}

export interface PlaceResponse {
	charges: number;
	nextChargeMs: number;
	streamId: string;
}

export interface PixelInfo {
	x: number;
	y: number;
	color: number;
	userId: string;
	userName: string;
	at: string;
}

export interface Rollback {
	id: string;
	wallId: number;
	state: string;
	changed: number;
	error: string | null;
}

export interface Timelapse {
	id: string;
	wallId: number;
	status: 'Queued' | 'Running' | 'Done' | 'Failed';
	error: string | null;
	video: string | null;
}

export interface Area {
	x: number;
	y: number;
	width: number;
	height: number;
}
