const key = 'stena.token';

export interface User {
	id: string;
	name: string;
	role: 'player' | 'admin';
	expires: number;
}

function decode(token: string | null): User | null {
	if (!token) {
		return null;
	}
	try {
		const claims = JSON.parse(atob(token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')));
		return claims.exp * 1000 > Date.now() ? { id: claims.sub, name: claims.name, role: claims.role, expires: claims.exp * 1000 } : null;
	} catch {
		return null;
	}
}

class Auth {
	token = $state<string | null>(null);
	user = $derived(decode(this.token));
	admin = $derived(this.user?.role === 'admin');
	private expiry: ReturnType<typeof setTimeout> | undefined;

	constructor() {
		if (typeof localStorage !== 'undefined') {
			this.set(localStorage.getItem(key));
		}
	}

	set(token: string | null) {
		const user = decode(token);
		this.token = user ? token : null;
		if (user) {
			localStorage.setItem(key, token!);
		} else {
			localStorage.removeItem(key);
		}
		clearTimeout(this.expiry);
		if (user) {
			this.expiry = setTimeout(() => this.set(null), user.expires - Date.now());
		}
	}
}

export const auth = new Auth();
