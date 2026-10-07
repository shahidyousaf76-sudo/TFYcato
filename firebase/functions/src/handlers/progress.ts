import { FieldValue } from 'firebase-admin/firestore';
import { onCall } from 'firebase-functions/v2/https';
import { onSchedule } from 'firebase-functions/v2/scheduler';
import { applyLevelResult, LevelResult } from '../domain/progress';
import { PlayerDoc } from '../domain/player';
import { validateRun } from '../domain/runValidation';
import { coinMultiplierFor, liveContent } from '../util/content';
import { db, fail, REGION, requireUser } from '../util/firebase';

type Outcome = { ok: true; replayed: boolean; result: LevelResult } | { ok: false; code: string; message: string };

/**
 * Trusted level completion. The client reports what it collected; the server validates it against the level's
 * totals and timing, computes stars, score and rewards itself, and applies everything in one transaction.
 * Idempotent per runId: a retry returns the stored result and never grants rewards twice.
 */
export async function processRun(uid: string, data: unknown): Promise<Outcome> {
  const content = await liveContent();
  const runId = (data as { runId?: unknown } | null)?.runId;
  if (typeof runId !== 'string' || !/^[A-Za-z0-9_-]{8,64}$/.test(runId)) return { ok: false, code: 'invalid-argument', message: 'Bad runId.' };
  const levelId = (data as { levelId?: unknown }).levelId;
  const level = content.levels.find(l => l.id === levelId);

  return db.runTransaction(async (tx): Promise<Outcome> => {
    const playerRef = db.doc(`players/${uid}`);
    const runRef = db.doc(`runs/${uid}/results/${runId}`);
    const [playerSnap, runSnap] = await Promise.all([tx.get(playerRef), tx.get(runRef)]);
    if (runSnap.exists) return { ok: true, replayed: true, result: runSnap.data()!.result as LevelResult };
    if (!playerSnap.exists) return { ok: false, code: 'not-found', message: 'Player not found.' };
    const player = playerSnap.data() as PlayerDoc;

    const v = validateRun(data, level, player, content.economy);
    if (!v.ok) {
      if (level) tx.set(db.doc(`levelStats/${level.id}`), { rejectedRuns: FieldValue.increment(1) }, { merge: true });
      return v;
    }
    const now = Date.now();
    const { player: updated, result } = applyLevelResult(player, level!, v.run, {
      levels: content.levels, economy: content.economy, achievements: content.achievements,
      coinMultiplier: coinMultiplierFor(content, level!.world, now), now,
    });
    if (!content.runtime.mystery_boxes_enabled) { updated.pendingBoxes = player.pendingBoxes; result.mysteryBoxesEarned = 0; result.totals.pendingBoxes = player.pendingBoxes; }

    // Leaderboard (only generated usernames are shown; off by default via Remote Config).
    const lbRef = db.doc(`leaderboards/${level!.id}/entries/${uid}`);
    const lbSnap = content.runtime.leaderboard_enabled ? await tx.get(lbRef) : null;

    tx.set(playerRef, updated);
    tx.set(runRef, { result, report: v.run, at: now });
    tx.set(db.doc(`levelStats/${level!.id}`), {
      completions: FieldValue.increment(1),
      failures: FieldValue.increment(v.run.failedAttempts),
      checkpointRestarts: FieldValue.increment(v.run.checkpointsUsed),
      starsTotal: FieldValue.increment(result.stars),
      offlineSynced: FieldValue.increment(v.run.offline ? 1 : 0),
      firstCompletions: FieldValue.increment(result.firstCompletion ? 1 : 0),
    }, { merge: true });
    if (lbSnap && (!lbSnap.exists || (lbSnap.data()!.score ?? 0) < result.score)) {
      tx.set(lbRef, { uid, username: updated.username, score: result.score, stars: result.stars, updatedAt: now, hidden: lbSnap.data()?.hidden ?? false, flagged: false });
    }
    return { ok: true, replayed: false, result };
  });
}

export const completeLevel = onCall({ region: REGION }, async (req) => {
  const uid = requireUser(req);
  const out = await processRun(uid, req.data);
  if (!out.ok) fail(out.code, out.message);
  return { replayed: out.replayed, result: out.result };
});

/**
 * Offline queue: runs finished without a connection are sent in play order when the device is back online.
 * Each is validated independently; a rejected run does not block the others.
 */
export const syncOfflineRuns = onCall({ region: REGION }, async (req) => {
  const uid = requireUser(req);
  const runs = (req.data as { runs?: unknown[] } | null)?.runs;
  if (!Array.isArray(runs) || runs.length === 0 || runs.length > 25) fail('invalid-argument', 'Send 1–25 runs.');
  const results: { runId: unknown; ok: boolean; replayed?: boolean; result?: LevelResult; code?: string; message?: string }[] = [];
  for (const r of runs) {
    const out = await processRun(uid, { ...(r as object), offline: true });
    results.push(out.ok ? { runId: (r as { runId?: unknown }).runId, ok: true, replayed: out.replayed, result: out.result }
      : { runId: (r as { runId?: unknown }).runId, ok: false, code: out.code, message: out.message });
  }
  const player = (await db.doc(`players/${uid}`).get()).data();
  return { results, player };
});

/** Idempotency records only need to outlive retries; keep 30 days. */
export const cleanupRunRecords = onSchedule({ region: REGION, schedule: 'every monday 03:00', timeZone: 'Europe/Copenhagen' }, async () => {
  const cutoff = Date.now() - 30 * 24 * 3_600_000;
  const old = await db.collectionGroup('results').where('at', '<', cutoff).limit(5000).get();
  const writer = db.bulkWriter();
  old.docs.forEach(d => writer.delete(d.ref));
  await writer.close();
});
