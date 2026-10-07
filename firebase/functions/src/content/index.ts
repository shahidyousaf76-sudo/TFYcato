import { ACHIEVEMENTS, DAILY_REWARDS, ECONOMY, MYSTERY_BOX, POWER_UPS, SHOP, TIPS, USERNAME_PREFIXES } from './catalog';
import { LEVELS } from './levels';
import { OBSTACLES } from './obstacles';
import { GameContent } from './types';
import { WORLDS } from './worlds';

export * from './types';

/** Bump when the shape or defaults change; clients cache content by version. */
export const CONTENT_VERSION = 1;

export const DEFAULT_CONTENT: GameContent = {
  version: CONTENT_VERSION,
  worlds: WORLDS,
  obstacles: OBSTACLES,
  levels: LEVELS,
  shop: SHOP,
  achievements: ACHIEVEMENTS,
  dailyRewards: DAILY_REWARDS,
  mysteryBox: MYSTERY_BOX,
  powerUps: POWER_UPS,
  economy: ECONOMY,
  usernameWords: { prefixes: USERNAME_PREFIXES },
  tips: TIPS,
};
