import { EconomyConfig, LevelDef } from '../content/types';
import { levelTotals, minimumDurationSec } from './levels';
import { PlayerDoc } from './player';

export interface RunReport {
  runId: string;
  levelId: string;
  coins: number;          // coin pickups
  coinValue: number;      // coins earned (Double Coins counts twice)
  fish: number;
  gems: number;
  starTokens: number;
  mysteryBoxes: number;
  heartsLeft: number;
  durationSec: number;
  jumps: number;
  checkpointsUsed: number;
  failedAttempts: number; // failures on this level since the last report (for difficulty analytics)
  offline: boolean;       // finished without a connection, synced later
}

export type Validation = { ok: true; run: RunReport } | { ok: false; code: string; message: string };

const int = (v: unknown, min: number, max: number) => typeof v === 'number' && Number.isInteger(v) && v >= min && v <= max;

/** Validates shape, bounds and plausibility. Never trusts the client with rewards or score. */
export function validateRun(input: unknown, level: LevelDef | undefined, player: PlayerDoc, economy: EconomyConfig): Validation {
  const fail = (code: string, message: string): Validation => ({ ok: false, code, message });
  if (!input || typeof input !== 'object') return fail('invalid-argument', 'Missing run.');
  const r = input as Record<string, unknown>;
  if (typeof r.runId !== 'string' || !/^[A-Za-z0-9_-]{8,64}$/.test(r.runId)) return fail('invalid-argument', 'Bad runId.');
  if (!level) return fail('not-found', 'Unknown level.');
  if (!level.enabled) return fail('failed-precondition', 'Level is disabled.');
  if (level.number > player.currentLevel) return fail('permission-denied', 'Level is locked.');

  const t = levelTotals(level);
  const coinValue = r.coinValue === undefined ? r.coins : r.coinValue;
  const checks: [unknown, number, number, string][] = [
    [r.coins, 0, t.coins, 'coins'], [coinValue, 0, t.coins * 2, 'coinValue'], [r.fish, 0, t.fish, 'fish'], [r.gems, 0, t.gems, 'gems'],
    [r.starTokens, 0, t.starTokens, 'starTokens'], [r.mysteryBoxes ?? 0, 0, t.mysteryBoxes, 'mysteryBoxes'],
    [r.heartsLeft, 1, Math.max(5, economy.startingHearts), 'heartsLeft'] /* 5 = Remote Config maximum, so a mid-run config change never rejects a run */, [r.jumps ?? 0, 0, 5000, 'jumps'],
    [r.checkpointsUsed ?? 0, 0, 500, 'checkpointsUsed'], [r.failedAttempts ?? 0, 0, 1000, 'failedAttempts'],
  ];
  for (const [v, min, max, name] of checks) if (!int(v, min, max)) return fail('invalid-argument', `Implausible ${name}.`);
  // Double Coins can at most double each pickup.
  if ((coinValue as number) < (r.coins as number) || (coinValue as number) > (r.coins as number) * 2) return fail('invalid-argument', 'Implausible coinValue.');
  if (typeof r.durationSec !== 'number' || !Number.isFinite(r.durationSec) || r.durationSec > 7200) return fail('invalid-argument', 'Bad duration.');
  if (r.durationSec < minimumDurationSec(level)) return fail('invalid-argument', 'Run was too fast.');

  return {
    ok: true,
    run: {
      runId: r.runId, levelId: level.id, coins: r.coins as number, coinValue: coinValue as number, fish: r.fish as number, gems: r.gems as number,
      starTokens: r.starTokens as number, mysteryBoxes: (r.mysteryBoxes ?? 0) as number, heartsLeft: r.heartsLeft as number,
      durationSec: r.durationSec, jumps: (r.jumps ?? 0) as number, checkpointsUsed: (r.checkpointsUsed ?? 0) as number,
      failedAttempts: (r.failedAttempts ?? 0) as number, offline: r.offline === true,
    },
  };
}
