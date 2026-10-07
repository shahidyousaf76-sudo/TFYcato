import { AchievementDef, EconomyConfig, LevelDef } from '../content/types';
import { awardAchievements, EarnedAchievement } from './achievements';
import { computeScore, computeStars } from './levels';
import { clonePlayer, emptyLevelProgress, PlayerDoc } from './player';
import { RunReport } from './runValidation';

export interface LevelResult {
  levelId: string;
  levelNumber: number;
  stars: number;
  score: number;
  firstCompletion: boolean;
  newBestScore: boolean;
  coinsEarned: number;       // collected coins × multiplier
  bonusCoins: number;        // first-clear bonus + new-star bonus
  fishEarned: number;
  gemsEarned: number;        // only gems not found before on this level
  mysteryBoxesEarned: number;
  unlockedLevel: number | null;
  achievements: EarnedAchievement[];
  totals: { coins: number; gems: number; fish: number; stars: number; currentLevel: number; highScore: number; pendingBoxes: number };
}

export interface ApplyContext { levels: LevelDef[]; economy: EconomyConfig; achievements: AchievementDef[]; coinMultiplier: number; now: number }

/**
 * Applies a validated run to a player. Pure: returns a new player document and the result shown on the Level Complete screen.
 * Replays still earn collected coins and fish; bonuses, gems and mystery boxes are granted once per level.
 */
export function applyLevelResult(before: PlayerDoc, level: LevelDef, run: RunReport, ctx: ApplyContext): { player: PlayerDoc; result: LevelResult } {
  const p = clonePlayer(before);
  const lp = { ...emptyLevelProgress(), ...p.levels[level.id] };
  const stars = computeStars(level, run);
  const score = computeScore(level, run, ctx.economy);
  const first = !lp.completed;
  const newStars = Math.max(0, stars - lp.bestStars);

  const multiplier = Math.min(3, Math.max(0.5, ctx.coinMultiplier));
  const coinsEarned = Math.round(run.coinValue * multiplier);
  const bonusCoins = Math.round(((first ? ctx.economy.firstClearBonusBase + ctx.economy.firstClearBonusPerLevel * level.number : 0) + newStars * ctx.economy.newStarBonus) * multiplier);
  const gemsEarned = Math.max(0, run.gems - lp.gemsFound);
  const boxesEarned = Math.max(0, run.mysteryBoxes - lp.boxesFound);

  lp.attempts += 1 + run.failedAttempts;
  lp.completions += 1;
  lp.completed = true;
  lp.bestStars = Math.max(lp.bestStars, stars);
  const newBestScore = score > lp.bestScore;
  lp.bestScore = Math.max(lp.bestScore, score);
  lp.bestCoins = Math.max(lp.bestCoins, run.coins);
  lp.bestStarTokens = Math.max(lp.bestStarTokens, run.starTokens);
  lp.gemsFound = Math.max(lp.gemsFound, run.gems);
  lp.boxesFound = Math.max(lp.boxesFound, run.mysteryBoxes);
  lp.bestTimeSec = lp.bestTimeSec > 0 ? Math.min(lp.bestTimeSec, run.durationSec) : run.durationSec;
  p.levels[level.id] = lp;

  p.coins += coinsEarned + bonusCoins;
  p.fish += run.fish;
  p.gems += gemsEarned;
  p.pendingBoxes += boxesEarned;
  p.highScore = Math.max(p.highScore, score);
  p.stars = Object.values(p.levels).reduce((a, l) => a + (l.bestStars || 0), 0);

  // Unlock the next enabled level (disabled levels are skipped so progress never gets stuck).
  const next = ctx.levels.filter(l => l.enabled && l.number > level.number).sort((a, b) => a.number - b.number)[0];
  let unlockedLevel: number | null = null;
  if (next && next.number > p.currentLevel) { p.currentLevel = next.number; unlockedLevel = next.number; }

  p.stats.runs += 1;
  p.stats.jumps += run.jumps;
  p.stats.coinsCollected += run.coins;
  p.stats.fishCollected += run.fish;
  p.stats.gemsFound += gemsEarned;
  p.stats.levelsCompleted = Object.values(p.levels).filter(l => l.completed).length;
  p.stats.perfectLevels = Object.values(p.levels).filter(l => l.bestStars >= 3).length;

  const achievements = awardAchievements(p, ctx.achievements);
  p.updatedAt = ctx.now;

  return {
    player: p,
    result: {
      levelId: level.id, levelNumber: level.number, stars, score, firstCompletion: first, newBestScore,
      coinsEarned, bonusCoins, fishEarned: run.fish, gemsEarned, mysteryBoxesEarned: boxesEarned, unlockedLevel, achievements,
      totals: { coins: p.coins, gems: p.gems, fish: p.fish, stars: p.stars, currentLevel: p.currentLevel, highScore: p.highScore, pendingBoxes: p.pendingBoxes },
    },
  };
}
