import { DEFAULT_CONTENT } from '../../src/content';
import { levelTotals } from '../../src/domain/levels';
import { newPlayer, PlayerDoc } from '../../src/domain/player';
import { RunReport } from '../../src/domain/runValidation';

export const C = DEFAULT_CONTENT;
export const level = (n: number) => C.levels.find(l => l.number === n)!;
export const player = (o: Partial<PlayerDoc> = {}): PlayerDoc => ({ ...newPlayer('uid1', 'Kitty4821', 1_000), ...o });

/** A plausible, fully valid run for level n. */
export function run(n: number, o: Partial<RunReport> = {}): RunReport {
  const l = level(n); const t = levelTotals(l);
  return {
    runId: 'run_abcdef12', levelId: l.id, coins: Math.round(t.coins * 0.6), coinValue: Math.round(t.coins * 0.6), fish: 1, gems: 0,
    starTokens: 1, mysteryBoxes: 0, heartsLeft: 2, durationSec: t.length / l.baseSpeed, jumps: 12, checkpointsUsed: 0, failedAttempts: 0, offline: false, ...o,
  };
}
