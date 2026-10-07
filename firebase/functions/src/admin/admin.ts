import { AggregateField, FieldValue } from 'firebase-admin/firestore';
import { getRemoteConfig } from 'firebase-admin/remote-config';
import { CallableRequest, onCall } from 'firebase-functions/v2/https';
import { DEFAULT_CONTENT } from '../content';
import { ObstacleId, WorldDef } from '../content/types';
import { validateAchievement, validateAnnouncement, validateEvent, validateLevel, validateShopItem, validateWorld } from '../domain/adminValidation';
import { JSON_KEYS, RuntimeConfig, sanitizeRuntime } from '../domain/remoteConfig';
import { invalidateContentCache, liveContent } from '../util/content';
import { auth, db, fail, REGION, requireAdmin, Role } from '../util/firebase';

/** Every admin write is recorded (who, what, when) — visible on the dashboard's Audit page. */
async function audit(req: CallableRequest, action: string, target: string, details: Record<string, unknown> = {}) {
  await db.collection('adminAudit').add({ uid: req.auth!.uid, email: req.auth!.token.email ?? null, action, target, details, at: Date.now() });
}

const opts = { region: REGION };

// ---------------------------------------------------------------- statistics (aggregate only, no personal data)
export const adminGetStats = onCall(opts, async (req) => {
  requireAdmin(req);
  const day = 24 * 3_600_000; const now = Date.now();
  const players = db.collection('players');
  const [total, new24h, new7d, sums, levelStats, flagged, errors] = await Promise.all([
    players.count().get(),
    players.where('createdAt', '>=', now - day).count().get(),
    players.where('createdAt', '>=', now - 7 * day).count().get(),
    players.aggregate({ coins: AggregateField.sum('coins'), gems: AggregateField.sum('gems'), stars: AggregateField.sum('stars'), avgLevel: AggregateField.average('currentLevel') }).get(),
    db.collection('levelStats').get(),
    db.collectionGroup('entries').where('flagged', '==', true).count().get(),
    db.collection('clientIssues').orderBy('count', 'desc').limit(20).get(),
  ]);
  // Players stuck at each level (currentLevel histogram) shows where children get stuck.
  const content = await liveContent();
  const stuck = await Promise.all(content.levels.map(async l => ({ level: l.number, players: (await players.where('currentLevel', '==', l.number).count().get()).data().count })));
  const s = sums.data();
  return {
    players: { total: total.data().count, new24h: new24h.data().count, new7d: new7d.data().count, avgLevel: s.avgLevel ?? 0 },
    economy: { coins: s.coins ?? 0, gems: s.gems ?? 0, stars: s.stars ?? 0 },
    levels: levelStats.docs.map(d => ({ id: d.id, ...d.data() })),
    playersAtLevel: stuck,
    leaderboardFlagged: flagged.data().count,
    topIssues: errors.docs.map(d => ({ id: d.id, ...d.data() })),
  };
});

// ---------------------------------------------------------------- levels & content
export const adminUpsertLevel = onCall(opts, async (req) => {
  requireAdmin(req);
  const level = (req.data as { level?: unknown })?.level;
  const worlds = (await db.collection('worlds').get()).docs.map(d => d.data() as WorldDef);
  const problems = validateLevel(level, worlds.length ? worlds : DEFAULT_CONTENT.worlds, DEFAULT_CONTENT.obstacles.map(o => o.id as ObstacleId));
  if (problems.length) fail('invalid-argument', problems.join(' '));
  const l = level as { id: string };
  await db.doc(`levels/${l.id}`).set(level as object);
  await audit(req, 'level.upsert', l.id);
  invalidateContentCache();
  return { id: l.id };
});

export const adminSetLevelEnabled = onCall(opts, async (req) => {
  requireAdmin(req);
  const { levelId, enabled } = (req.data ?? {}) as { levelId?: string; enabled?: boolean };
  if (typeof levelId !== 'string' || typeof enabled !== 'boolean') fail('invalid-argument', 'levelId and enabled required.');
  const ref = db.doc(`levels/${levelId}`);
  if (!(await ref.get()).exists) fail('not-found', 'Level not found (seed content first).');
  await ref.update({ enabled });
  await audit(req, enabled ? 'level.enable' : 'level.disable', levelId);
  invalidateContentCache();
  return { levelId, enabled };
});

const CONTENT_VALIDATORS = { worlds: validateWorld, catalog: validateShopItem, achievements: validateAchievement } as const;

export const adminUpsertContent = onCall(opts, async (req) => {
  requireAdmin(req);
  const { collection, doc } = (req.data ?? {}) as { collection?: keyof typeof CONTENT_VALIDATORS; doc?: { id?: string } };
  if (!collection || !(collection in CONTENT_VALIDATORS)) fail('invalid-argument', 'collection must be worlds, catalog or achievements.');
  const problems = CONTENT_VALIDATORS[collection](doc);
  if (problems.length) fail('invalid-argument', problems.join(' '));
  await db.doc(`${collection}/${doc!.id}`).set(doc as object);
  await audit(req, `${collection}.upsert`, doc!.id!);
  invalidateContentCache();
  return { id: doc!.id };
});

export const adminUpdateRewards = onCall(opts, async (req) => {
  requireAdmin(req);
  const { economy, mysteryBox } = (req.data ?? {}) as { economy?: Record<string, unknown>; mysteryBox?: unknown[] };
  const update: Record<string, unknown> = {};
  if (economy) {
    const clean: Record<string, number> = {};
    for (const [k, v] of Object.entries(economy)) {
      if (!(k in DEFAULT_CONTENT.economy) || k === 'startingHearts') continue; // hearts live in Remote Config
      if (typeof v !== 'number' || !Number.isFinite(v) || v < 0 || v > 10_000) fail('invalid-argument', `economy.${k} must be 0–10000.`);
      clean[k] = v;
    }
    update.economy = clean;
  }
  if (mysteryBox) {
    const shopIds = new Set((await liveContent()).shop.map(i => i.id));
    const ok = Array.isArray(mysteryBox) && mysteryBox.length > 0 && mysteryBox.length <= 20 && mysteryBox.every((m) => {
      const e = m as { type?: string; amount?: number; weight?: number; pool?: string[]; label?: string };
      return ['coins', 'gems', 'item'].includes(e.type ?? '') && Number.isInteger(e.amount) && e.amount! > 0 && e.amount! <= (e.type === 'gems' ? 10 : 2000)
        && typeof e.weight === 'number' && e.weight >= 0 && e.weight <= 1000 && typeof e.label === 'string' && e.label.length <= 40
        && (e.type !== 'item' || (Array.isArray(e.pool) && e.pool.length > 0 && e.pool.every(id => shopIds.has(id))));
    });
    if (!ok) fail('invalid-argument', 'mysteryBox entries are invalid.');
    update.mysteryBox = mysteryBox;
  }
  await db.doc('config/rewards').set(update, { merge: true });
  await audit(req, 'rewards.update', 'config/rewards', { keys: Object.keys(update) });
  invalidateContentCache();
  return { updated: Object.keys(update) };
});

// ---------------------------------------------------------------- events & announcements
export const adminUpsertEvent = onCall(opts, async (req) => {
  requireAdmin(req);
  const ev = (req.data as { event?: Record<string, unknown> })?.event;
  const problems = validateEvent(ev);
  if (problems.length) fail('invalid-argument', problems.join(' '));
  const ref = typeof ev!.id === 'string' && ev!.id ? db.doc(`events/${ev!.id}`) : db.collection('events').doc();
  const { id: _ignored, ...data } = ev!;
  await ref.set({ ...data, updatedAt: Date.now() });
  await audit(req, 'event.upsert', ref.id);
  invalidateContentCache();
  return { id: ref.id };
});

export const adminPublishAnnouncement = onCall(opts, async (req) => {
  requireAdmin(req);
  const a = (req.data as { announcement?: Record<string, unknown> })?.announcement;
  const problems = validateAnnouncement(a);
  if (problems.length) fail('invalid-argument', problems.join(' '));
  const ref = typeof a!.id === 'string' && a!.id ? db.doc(`announcements/${a!.id}`) : db.collection('announcements').doc();
  const { id: _ignored, ...data } = a!;
  await ref.set({ ...data, updatedAt: Date.now() });
  await audit(req, 'announcement.publish', ref.id);
  return { id: ref.id };
});

// ---------------------------------------------------------------- Remote Config
export const adminGetRemoteConfig = onCall(opts, async (req) => {
  requireAdmin(req);
  const snap = await db.doc('config/runtime').get();
  return { values: sanitizeRuntime(snap.data()), updatedAt: snap.data()?.updatedAt ?? null };
});

/**
 * Validates and clamps every value, publishes it to Firebase Remote Config (what the game reads) and mirrors it
 * to config/runtime (what Cloud Functions read), so both sides always agree.
 */
export const adminPublishRemoteConfig = onCall(opts, async (req) => {
  requireAdmin(req);
  const values = sanitizeRuntime((req.data as { values?: unknown })?.values);
  const rc = getRemoteConfig();
  const template = await rc.getTemplate();
  for (const [key, value] of Object.entries(values) as [keyof RuntimeConfig, unknown][]) {
    const isJson = JSON_KEYS.includes(key);
    const valueType = isJson ? 'JSON' : typeof value === 'boolean' ? 'BOOLEAN' : typeof value === 'number' ? 'NUMBER' : 'STRING';
    template.parameters[key] = { defaultValue: { value: isJson ? JSON.stringify(value) : String(value) }, valueType, description: 'Managed by TFY admin dashboard' };
  }
  const validated = await rc.validateTemplate(template);
  await rc.publishTemplate(validated);
  await db.doc('config/runtime').set({ ...values, updatedAt: Date.now() });
  await audit(req, 'remoteConfig.publish', 'remote-config', { values });
  invalidateContentCache();
  return { values };
});

// ---------------------------------------------------------------- leaderboard review
export const adminReviewLeaderboard = onCall(opts, async (req) => {
  requireAdmin(req);
  const { levelId, uid, hidden } = (req.data ?? {}) as { levelId?: string; uid?: string; hidden?: boolean };
  if (typeof levelId !== 'string' || typeof uid !== 'string' || typeof hidden !== 'boolean') fail('invalid-argument', 'levelId, uid, hidden required.');
  await db.doc(`leaderboards/${levelId}/entries/${uid}`).update({ hidden, flagged: false, reviewedAt: Date.now() });
  await audit(req, hidden ? 'leaderboard.hide' : 'leaderboard.restore', `${levelId}/${uid}`);
  return { ok: true };
});

// ---------------------------------------------------------------- admin accounts (super admin only)
export const adminSetRole = onCall(opts, async (req) => {
  requireAdmin(req, 'super_admin');
  const { email, role } = (req.data ?? {}) as { email?: string; role?: Role | null };
  if (typeof email !== 'string' || !(role === null || role === 'admin' || role === 'super_admin')) fail('invalid-argument', 'email and role (admin | super_admin | null) required.');
  const user = await auth.getUserByEmail(email).catch(() => null);
  if (!user) fail('not-found', 'Create the account in Firebase Authentication first.');
  if (user.uid === req.auth!.uid && role !== 'super_admin') fail('failed-precondition', 'You cannot remove your own super admin role.');
  await auth.setCustomUserClaims(user.uid, role ? { role } : {});
  await auth.revokeRefreshTokens(user.uid); // new role applies on next sign-in
  await audit(req, 'role.set', email, { role });
  return { email, role };
});

export const adminListAdmins = onCall(opts, async (req) => {
  requireAdmin(req, 'super_admin');
  const out: { email?: string; role: string; disabled: boolean }[] = [];
  let page: string | undefined;
  do {
    const r = await auth.listUsers(1000, page);
    for (const u of r.users) if (u.customClaims?.role) out.push({ email: u.email, role: String(u.customClaims.role), disabled: u.disabled });
    page = r.pageToken;
  } while (page && out.length < 500);
  return { admins: out };
});

/** Writes the bundled default content into Firestore. Existing docs are kept unless overwrite is true. */
export const adminSeedContent = onCall(opts, async (req) => {
  requireAdmin(req, 'super_admin');
  const overwrite = (req.data as { overwrite?: boolean })?.overwrite === true;
  const n = await seedContent(overwrite);
  await audit(req, 'content.seed', 'all', { overwrite, written: n });
  invalidateContentCache();
  return { written: n };
});

export async function seedContent(overwrite: boolean): Promise<number> {
  const c = DEFAULT_CONTENT;
  const docs: [string, object][] = [
    ...c.worlds.map(w => [`worlds/${w.id}`, w] as [string, object]),
    ...c.levels.map(l => [`levels/${l.id}`, l] as [string, object]),
    ...c.shop.map(i => [`catalog/${i.id}`, i] as [string, object]),
    ...c.achievements.map(a => [`achievements/${a.id}`, a] as [string, object]),
    ['config/rewards', { economy: c.economy, mysteryBox: c.mysteryBox }],
    ['config/content', { version: c.version, obstacles: c.obstacles, powerUps: c.powerUps, tips: c.tips, dailyRewards: c.dailyRewards }],
    ['config/runtime', { ...sanitizeRuntime({}), updatedAt: Date.now() }],
  ];
  let written = 0;
  const writer = db.bulkWriter();
  for (const [path, data] of docs) {
    if (!overwrite && (await db.doc(path).get()).exists) continue;
    writer.set(db.doc(path), data); written++;
  }
  await writer.close();
  return written;
}

// ---------------------------------------------------------------- client issue reports (non-fatal, no personal data)
/** The game reports friendly-error codes (e.g. SAVE_FAILED) so admins can spot problems. Crashes go to Crashlytics. */
export const reportClientIssue = onCall(opts, async (req) => {
  if (!req.auth?.uid) return { ok: false };
  const { code, appVersion, platform } = (req.data ?? {}) as { code?: string; appVersion?: string; platform?: string };
  if (typeof code !== 'string' || !/^[A-Z_]{3,40}$/.test(code)) fail('invalid-argument', 'Bad code.');
  const safe = (s: unknown) => (typeof s === 'string' ? s.replace(/[^\w.\-]/g, '').slice(0, 20) : 'unknown');
  await db.doc(`clientIssues/${code}`).set({
    code, count: FieldValue.increment(1), lastSeen: Date.now(),
    [`versions.${safe(appVersion).replace(/\./g, '_')}`]: FieldValue.increment(1), [`platforms.${safe(platform)}`]: FieldValue.increment(1),
  }, { merge: true });
  return { ok: true };
});
