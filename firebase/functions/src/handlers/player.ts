import { randomInt } from 'node:crypto';
import { onCall } from 'firebase-functions/v2/https';
import { DEFAULT_CONTENT } from '../content';
import { newPlayer, PlayerDoc } from '../domain/player';
import { equip } from '../domain/shop';
import { generateUsername, isValidUsername, usernameKey } from '../domain/username';
import { liveContent } from '../util/content';
import { auth, db, fail, REGION, requireUser } from '../util/firebase';

const rng = () => randomInt(0, 1_000_000) / 1_000_000;
const PREFIXES = DEFAULT_CONTENT.usernameWords.prefixes;

/**
 * Called once after anonymous sign-in. Idempotent: an existing profile is returned unchanged, so reinstalling
 * or retrying never creates a second player. The device proposes an automatic username (e.g. Kitty4821);
 * the server keeps it if it is valid and free, otherwise picks a new one.
 */
export const createPlayer = onCall({ region: REGION, enforceAppCheck: false }, async (req) => {
  const uid = requireUser(req);
  const ref = db.doc(`players/${uid}`);
  const proposed = (req.data as { username?: unknown } | undefined)?.username;

  for (let attempt = 0; attempt < 6; attempt++) {
    const candidate = attempt === 0 && isValidUsername(proposed, PREFIXES) ? proposed : generateUsername(PREFIXES, rng);
    const result = await db.runTransaction(async (tx) => {
      const existing = await tx.get(ref);
      if (existing.exists) return { player: existing.data() as PlayerDoc, created: false };
      const nameRef = db.doc(`usernames/${usernameKey(candidate)}`);
      if ((await tx.get(nameRef)).exists) return null; // taken → try another
      const player = newPlayer(uid, candidate, Date.now());
      tx.set(ref, player);
      tx.set(nameRef, { uid, createdAt: player.createdAt });
      return { player, created: true };
    });
    if (result) return result;
  }
  fail('resource-exhausted', 'Could not pick a name, please try again.');
});

/** Selects a cat and equips owned accessories (validated against ownership). */
export const updateLoadout = onCall({ region: REGION }, async (req) => {
  const uid = requireUser(req);
  const content = await liveContent();
  return db.runTransaction(async (tx) => {
    const snap = await tx.get(db.doc(`players/${uid}`));
    if (!snap.exists) fail('not-found', 'Player not found.');
    const r = equip(snap.data() as PlayerDoc, content.shop, (req.data ?? {}) as object, Date.now());
    if (!r.ok) fail(r.code, r.message);
    tx.update(snap.ref, { selectedCat: r.player.selectedCat, equipped: r.player.equipped, updatedAt: r.player.updatedAt });
    return { selectedCat: r.player.selectedCat, equipped: r.player.equipped };
  });
});

/**
 * Parent-facing "Delete my data" (Settings ▸ Parents). Removes the profile, username reservation, leaderboard
 * entries and run records, then deletes the anonymous account. The app then starts fresh.
 */
export const deleteMyData = onCall({ region: REGION }, async (req) => {
  const uid = requireUser(req);
  await deletePlayerData(uid);
  await auth.deleteUser(uid).catch(() => undefined);
  return { deleted: true };
});

export async function deletePlayerData(uid: string) {
  const ref = db.doc(`players/${uid}`);
  const snap = await ref.get();
  const username = (snap.data() as PlayerDoc | undefined)?.username;
  const batch = db.batch();
  batch.delete(ref);
  if (username) batch.delete(db.doc(`usernames/${usernameKey(username)}`));
  const entries = await db.collectionGroup('entries').where('uid', '==', uid).get();
  entries.docs.forEach(d => batch.delete(d.ref));
  await batch.commit();
  await db.recursiveDelete(db.collection(`runs/${uid}/results`));
}
