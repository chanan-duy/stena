const key = 'stena.token';

export interface User {
	id: string;
	name: string;
	role: 'player' | 'admin';
}

function decode(token: string | null): User | null {
	if (!token) {
		return null;
	}
	try {
		const claims = JSON.parse(atob(token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')));
		return claims.exp * 1000 > Date.now() ? { id: claims.sub, name: claims.name, role: claims.role } : null;
	} catch {
		return null;
	}
}

class Auth {
	token = $state<string | null>(null);
	user = $derived(decode(this.token));
	admin = $derived(this.user?.role === 'admin');

	constructor() {
		if (typeof localStorage !== 'undefined') {
			this.token = localStorage.getItem(key);
		}
	}

	set(token: string | null) {
		this.token = token;
		if (token) {
			localStorage.setItem(key, token);
		} else {
			localStorage.removeItem(key);
		}
	}
}

export const auth = new Auth();
