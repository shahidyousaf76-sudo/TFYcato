// Mirrors firebase/functions/src/content/types.ts and domain/remoteConfig.ts (kept small on purpose).
import defaults from './default-content.json';

export interface SectionDef {
  kind: 'start' | 'easy' | 'collectibles' | 'obstacles' | 'medium' | 'challenge' | 'finish';
  length: number; obstacles: string[]; density: number; twoLaneChance: number;
  coins: number; fish: number; gems: number; starTokens: number; mysteryBoxes: number; powerUps: number;
  challenge?: string; checkpointAfter: boolean;
}

export interface LevelDef {
  id: string; number: number; name: string; world: string; difficulty: 'easy' | 'medium' | 'hard' | 'combo';
  timeOfDay: 'sunrise' | 'morning' | 'day' | 'sunset' | 'evening' | 'night';
  baseSpeed: number; maxSpeed: number; targetCoins: number; threeStarCoins: number; targetStars: 3;
  checkpointCount: number; seed: number; enabled: boolean; tutorial: boolean; tip: string; sections: SectionDef[];
}

export interface WorldDef {
  id: string; name: string; order: number; difficultyLabel: string; enabled: boolean; comingSoon: boolean; music: string; ambience: string[];
  weather: string; palette: Record<string, string>; fogDensity: number; decor: string[];
}

export interface ShopItem {
  id: string; name: string; slot: string; category: string; priceCoins: number; priceGems: number; unlockLevel: number;
  source: string; colors: string[]; description: string; enabled: boolean;
}

export interface AchievementDef { id: string; name: string; description: string; stat: string; target: number; rewardCoins: number; rewardGems: number; icon: string }
export interface DailyRewardDef { day: number; type: 'coins' | 'gems' | 'item'; amount: number; itemId?: string; label: string }
export interface MysteryEntry { type: 'coins' | 'gems' | 'item'; amount: number; weight: number; pool?: string[]; label: string }

export interface RuntimeConfig {
  run_speed_scale: number; difficulty_density_scale: number; coin_reward_multiplier: number; starting_hearts: number;
  powerup_durations: { magnet: number; super_jump: number; speed_boost: number; double_coins: number };
  daily_rewards: DailyRewardDef[];
  daily_rewards_enabled: boolean; shop_enabled: boolean; mystery_boxes_enabled: boolean; leaderboard_enabled: boolean; events_enabled: boolean;
  maintenance_message: string;
}

export const DEFAULTS = defaults as unknown as {
  worlds: WorldDef[]; levels: (LevelDef & { totals: Record<string, number> })[]; shop: ShopItem[]; achievements: AchievementDef[];
  obstacles: { id: string; name: string; minLevel: number }[]; mysteryBox: MysteryEntry[]; economy: Record<string, number>; dailyRewards: DailyRewardDef[];
};

export function levelTotals(l: LevelDef) {
  const t = { length: 0, coins: 0, fish: 0, gems: 0, starTokens: 0, mysteryBoxes: 0, powerUps: 0 };
  for (const s of l.sections) { t.length += s.length; t.coins += s.coins; t.fish += s.fish; t.gems += s.gems; t.starTokens += s.starTokens; t.mysteryBoxes += s.mysteryBoxes; t.powerUps += s.powerUps; }
  return t;
}
