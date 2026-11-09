export const ssr = false;

export async function load() {
	await document.fonts.load('1em "Manrope Variable"').catch(() => {});
}
