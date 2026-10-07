// Automatic, safe usernames: always one of the approved prefixes followed by 3–4 digits (e.g. Kitty4821).
// Free text is never accepted, so a child can't type their real name and nobody can pick a rude one.

export function generateUsername(prefixes: string[], rng: () => number): string {
  const prefix = prefixes[Math.floor(rng() * prefixes.length) % prefixes.length];
  const digits = 100 + Math.floor(rng() * 9900); // 100..9999
  return `${prefix}${digits}`;
}

export function isValidUsername(name: unknown, prefixes: string[]): name is string {
  if (typeof name !== 'string' || name.length > 24) return false;
  const m = /^([A-Za-z]+)(\d{3,4})$/.exec(name);
  if (!m) return false;
  const n = Number(m[2]);
  return prefixes.includes(m[1]) && n >= 100 && n <= 9999 && !m[2].startsWith('0');
}

/** Key for the uniqueness index at usernames/{key}. */
export const usernameKey = (name: string) => name.toLowerCase();
