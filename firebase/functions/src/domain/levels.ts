import { EconomyConfig, LevelDef } from '../content/types';

export interface LevelTotals { length: number; coins: number; fish: number; gems: number; starTokens: number; mysteryBoxes: number; powerUps: number }

/** Totals the client's level planner places exactly. The server validates every run against them. */
export function levelTotals(level: LevelDef): LevelTotals {
  const t: LevelTotals = { length: 0, coins: 0, fish: 0, gems: 0, starTokens: 0, mysteryBoxes: 0, powerUps: 0 };
  for (const s of level.sections) {
    t.length += s.length; t.coins += s.coins; t.fish += s.fish; t.gems += s.gems;
    t.starTokens += s.starTokens; t.mysteryBoxes += s.mysteryBoxes; t.powerUps += s.powerUps;
  }
  return t;
}

export interface RunCounts { coins: number; coinValue: number; fish: number; gems: number; starTokens: number; heartsLeft: number }

/**
 * ⭐ finish the level · ⭐⭐ + enough coins · ⭐⭐⭐ + most coins and all big stars.
 * Perfect completion is never needed to unlock the next level.
 */
export function computeStars(level: LevelDef, run: RunCounts): number {
  const totals = levelTotals(level);
  if (run.coins >= level.threeStarCoins && run.starTokens >= totals.starTokens) return 3;
  if (run.coins >= level.targetCoins) return 2;
  return 1;
}

/** The server computes the score itself from validated counts, so a client can't send an arbitrary score. */
export function computeScore(level: LevelDef, run: RunCounts, e: EconomyConfig): number {
  const totals = levelTotals(level);
  return Math.round(
    totals.length * e.scorePerMetre + run.coinValue * e.scorePerCoin + run.fish * e.scorePerFish +
    run.gems * e.scorePerGem + run.starTokens * e.scorePerStarToken + run.heartsLeft * e.scorePerHeartLeft,
  );
}

/** Shortest believable time: the whole level at top speed with a speed boost all the way, minus a margin. */
export function minimumDurationSec(level: LevelDef): number {
  return (levelTotals(level).length / (level.maxSpeed * 1.4)) * 0.85;
}

export const levelIdFor = (n: number) => `level_${String(n).padStart(2, '0')}`;
