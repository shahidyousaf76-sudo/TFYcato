import { AchievementDef } from '../content/types';
import { PlayerDoc } from './player';

export interface EarnedAchievement { id: string; name: string; rewardCoins: number; rewardGems: number }

export function statValue(p: PlayerDoc, stat: AchievementDef['stat']): number {
  return stat === 'stars' ? p.stars : p.stats[stat] ?? 0;
}

/** Marks newly reached achievements on `p` (mutates) and grants their rewards. Returns what was earned. */
export function awardAchievements(p: PlayerDoc, defs: AchievementDef[]): EarnedAchievement[] {
  const earned: EarnedAchievement[] = [];
  for (const a of defs) {
    if (p.achievements[a.id] || statValue(p, a.stat) < a.target) continue;
    p.achievements[a.id] = true;
    p.coins += a.rewardCoins; p.gems += a.rewardGems;
    earned.push({ id: a.id, name: a.name, rewardCoins: a.rewardCoins, rewardGems: a.rewardGems });
  }
  return earned;
}
