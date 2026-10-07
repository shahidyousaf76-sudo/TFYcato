import { randomInt } from 'node:crypto';
import { onCall } from 'firebase-functions/v2/https';
import { claimDaily, dailyStatus } from '../domain/daily';
import { PlayerDoc } from '../domain/player';
import { openMysteryBox as openBox, purchase } from '../domain/shop';
import { liveContent } from '../util/content';
import { db, fail, REGION, requireUser } from '../util/firebase';

const offsetOf = (data: unknown) => Number((data as { utcOffsetMinutes?: unknown } | null)?.utcOffsetMinutes ?? 0);

async function loadPlayer(uid: string) {
  const snap = await db.doc(`players/${uid}`).get();
  if (!snap.exists) fail('not-found', 'Player not found.');
  return snap.data() as PlayerDoc;
}

/** Server-timed daily reward status (the device clock is never trusted). */
export const getDailyReward = onCall({ region: REGION }, async (req) => {
  const uid = requireUser(req);
  const content = await liveContent();
  const player = await loadPlayer(uid);
  return { enabled: content.runtime.daily_rewards_enabled, rewards: content.runtime.daily_rewards, status: dailyStatus(player, content.runtime.daily_rewards, Date.now(), offsetOf(req.data)) };
});

export const claimDailyReward = onCall({ region: REGION }, async (req) => {
  const uid = requireUser(req);
  const content = await liveContent();
  if (!content.runtime.daily_rewards_enabled) fail('failed-precondition', 'Daily rewards are resting today.');
  return db.runTransaction(async (tx) => {
    const snap = await tx.get(db.doc(`players/${uid}`));
    if (!snap.exists) fail('not-found', 'Player not found.');
    const r = claimDaily(snap.data() as PlayerDoc, content.runtime.daily_rewards, content.shop, content.achievements, Date.now(), offsetOf(req.data));
    if (!r.ok) fail(r.code, r.message);
    tx.set(snap.ref, r.player);
    return { claim: r.claim, totals: { coins: r.player.coins, gems: r.player.gems }, daily: r.player.daily };
  });
});

export const purchaseItem = onCall({ region: REGION }, async (req) => {
  const uid = requireUser(req);
  const content = await liveContent();
  if (!content.runtime.shop_enabled) fail('failed-precondition', 'The Cat Shop is closed right now.');
  return db.runTransaction(async (tx) => {
    const snap = await tx.get(db.doc(`players/${uid}`));
    if (!snap.exists) fail('not-found', 'Player not found.');
    const r = purchase(snap.data() as PlayerDoc, content.shop, (req.data as { itemId?: unknown } | null)?.itemId, content.achievements, Date.now());
    if (!r.ok) fail(r.code, r.message);
    tx.set(snap.ref, r.player);
    return { itemId: r.item.id, paidCoins: r.paidCoins, paidGems: r.paidGems, achievements: r.achievements, totals: { coins: r.player.coins, gems: r.player.gems } };
  });
});

export const openMysteryBox = onCall({ region: REGION }, async (req) => {
  const uid = requireUser(req);
  const content = await liveContent();
  const rng = () => randomInt(0, 1_000_000) / 1_000_000;
  return db.runTransaction(async (tx) => {
    const snap = await tx.get(db.doc(`players/${uid}`));
    if (!snap.exists) fail('not-found', 'Player not found.');
    const r = openBox(snap.data() as PlayerDoc, content.mysteryBox, content.shop, rng, Date.now());
    if (!r.ok) fail(r.code, r.message);
    tx.set(snap.ref, r.player);
    return { reward: r.reward, totals: { coins: r.player.coins, gems: r.player.gems, pendingBoxes: r.player.pendingBoxes } };
  });
});
