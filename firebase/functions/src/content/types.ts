// Shared content schema. The same JSON is read by Unity (Resources/default_content.json),
// the web prototype and the admin dashboard, and is seeded into Firestore.

export type Difficulty = 'easy' | 'medium' | 'hard' | 'combo';
export type TimeOfDay = 'sunrise' | 'morning' | 'day' | 'sunset' | 'evening' | 'night';

export type ObstacleId =
  | 'log' | 'rock' | 'branch' | 'barrel' | 'wall' | 'broken_bridge' | 'falling_rocks' | 'moving_critter';

/** How the cat gets past an obstacle. Collision is resolved by lane and height, never by physics. */
export type Clearance =
  | 'jump'        // low: jump over (or change lane)
  | 'jump_or_slide' // branch: jump over it or slide under it
  | 'high_jump'   // barrel: higher than a log, still a single jump
  | 'wall'        // needs a double jump, or change lane
  | 'gap'         // broken bridge: long jump (or double jump) across all lanes
  | 'timed';      // falling rocks / moving critter: watch, then jump or dodge

export interface ObstacleDef {
  id: ObstacleId;
  name: string;
  emoji: string;
  difficulty: 'easy' | 'medium' | 'hard';
  clearance: Clearance;
  requiredAction: string;
  height: number;       // metres; for 'jump_or_slide' this is the underside height
  topHeight?: number;   // for 'jump_or_slide'
  depth: number;        // metres along the track
  lanes: 1 | 2 | 3;     // how many lanes one placement blocks (gap = 3)
  warning: string;      // what the child sees/hears before it
  warningDistance: number; // metres ahead the warning starts
  minLevel: number;
}

export type SectionKind = 'start' | 'easy' | 'collectibles' | 'obstacles' | 'medium' | 'challenge' | 'finish';

export type ChallengeId =
  | 'log_hop' | 'branch_duck' | 'coin_climb' | 'barrel_alley' | 'wall_weave' | 'mixed_medium'
  | 'bridge_leap' | 'rock_fall' | 'critter_parade' | 'grand_finale' | 'combo_wall_critter' | 'combo_bridge_rocks';

export interface SectionDef {
  kind: SectionKind;
  length: number;            // metres
  obstacles: ObstacleId[];   // pool for this section (empty = none)
  density: number;           // 0..1 — share of rows that carry obstacles
  twoLaneChance: number;     // 0..1 — chance a row blocks two lanes (never three, except gaps)
  coins: number;             // exact number of coins placed in this section
  fish: number;
  gems: number;
  starTokens: number;
  mysteryBoxes: number;
  powerUps: number;
  challenge?: ChallengeId;
  checkpointAfter: boolean;  // a checkpoint flag stands at the end of this section
}

export interface LevelDef {
  id: string;                // "level_01"
  number: number;
  name: string;
  world: string;
  difficulty: Difficulty;
  timeOfDay: TimeOfDay;
  baseSpeed: number;         // m/s at the start
  maxSpeed: number;          // m/s at the finish (smooth ramp)
  targetCoins: number;       // coins needed for 2 stars
  threeStarCoins: number;    // coins needed (with all star tokens) for 3 stars
  targetStars: 3;
  checkpointCount: number;
  seed: number;
  enabled: boolean;
  tutorial: boolean;
  tip: string;
  sections: SectionDef[];
}

export interface WorldDef {
  id: string;
  name: string;
  order: number;
  difficultyLabel: string;
  enabled: boolean;
  comingSoon: boolean;
  music: string;
  ambience: string[];
  weather: 'clear' | 'petals' | 'leaves' | 'birds' | 'mist' | 'snow' | 'sea_breeze';
  palette: { skyTop: string; skyHorizon: string; ground: string; path: string; pathEdge: string; accent: string; fog: string; water?: string };
  fogDensity: number;
  decor: string[];
  /** World-famous landmarks shown along the route (one every ~200 m), e.g. 'eiffel_tower'. */
  landmarks: string[];
}

export type Slot = 'cat' | 'hat' | 'glasses' | 'shoes' | 'backpack' | 'cape' | 'scarf' | 'trail';
export type ShopCategory = 'CATS' | 'HATS' | 'GLASSES' | 'SHOES' | 'BACKPACKS' | 'CAPES_SCARVES' | 'TRAILS';

export interface ShopItem {
  id: string;
  name: string;
  slot: Slot;
  category: ShopCategory;
  priceCoins: number;        // 0 = not sold for coins
  priceGems: number;         // 0 = not sold for gems
  unlockLevel: number;       // must have completed this level to buy (0 = none)
  source: 'default' | 'shop' | 'daily' | 'mystery' | 'achievement';
  colors: string[];          // used by procedural models / previews
  description: string;
  enabled: boolean;
}

export interface AchievementDef {
  id: string;
  name: string;
  description: string;
  stat: 'jumps' | 'coinsCollected' | 'levelsCompleted' | 'stars' | 'fishCollected' | 'gemsFound' | 'itemsBought' | 'dailyClaims' | 'perfectLevels';
  target: number;
  rewardCoins: number;
  rewardGems: number;
  icon: string;
}

export interface DailyRewardDef { day: number; type: 'coins' | 'gems' | 'item'; amount: number; itemId?: string; label: string }

export interface MysteryEntry { type: 'coins' | 'gems' | 'item'; amount: number; weight: number; pool?: string[]; label: string }

export interface PowerUpDef { id: 'magnet' | 'shield' | 'super_jump' | 'speed_boost' | 'double_coins'; name: string; durationSec: number; color: string; description: string }

export interface EconomyConfig {
  firstClearBonusBase: number;
  firstClearBonusPerLevel: number;
  newStarBonus: number;
  coinRewardMultiplier: number;
  scorePerMetre: number;
  scorePerCoin: number;
  scorePerFish: number;
  scorePerGem: number;
  scorePerStarToken: number;
  scorePerHeartLeft: number;
  startingHearts: number;
}

export interface GameContent {
  version: number;
  worlds: WorldDef[];
  obstacles: ObstacleDef[];
  levels: LevelDef[];
  shop: ShopItem[];
  achievements: AchievementDef[];
  dailyRewards: DailyRewardDef[];
  mysteryBox: MysteryEntry[];
  powerUps: PowerUpDef[];
  economy: EconomyConfig;
  usernameWords: { prefixes: string[] };
  tips: string[];
}
