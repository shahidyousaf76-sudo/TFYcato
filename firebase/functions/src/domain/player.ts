// Player document stored at players/{uid}. Pure types and helpers — no Firebase imports, fully unit-testable.

export type GraphicsQuality = 'auto' | 'low' | 'medium' | 'high' | 'ultra';

export interface PlayerSettings {
  music: boolean;
  sound: boolean;
  vibration: boolean;
  graphics: GraphicsQuality;
  language: string;
  musicVolume: number;
  soundVolume: number;
  reduceMotion: boolean;
  /** Slow / Normal / Fast: scales the whole level's time (0.75× / 1× / 1.25×), so jumps and gaps stay the same size. */
  gameSpeed?: GameSpeed;
}

export type GameSpeed = 'slow' | 'normal' | 'fast';
export const GAME_SPEEDS: GameSpeed[] = ['slow', 'normal', 'fast'];

export interface LevelProgress {
  completed: boolean;
  bestStars: number;
  bestScore: number;
  bestCoins: number;
  bestStarTokens: number;
  gemsFound: number;       // gems are granted once per gem, not per replay
  boxesFound: number;      // mystery boxes too
  attempts: number;
  completions: number;
  bestTimeSec: number;     // 0 = none yet
}

export interface PlayerStats {
  runs: number;
  jumps: number;
  coinsCollected: number;
  levelsCompleted: number;
  fishCollected: number;
  gemsFound: number;
  itemsBought: number;
  dailyClaims: number;
  perfectLevels: number;
}

export type EquipSlot = 'hat' | 'glasses' | 'shoes' | 'backpack' | 'cape' | 'scarf' | 'trail';
export const EQUIP_SLOTS: EquipSlot[] = ['hat', 'glasses', 'shoes', 'backpack', 'cape', 'scarf', 'trail'];

export interface PlayerDoc {
  uid: string;
  username: string;
  createdAt: number;
  updatedAt: number;
  schemaVersion: 1;
  currentLevel: number;      // highest unlocked level number
  coins: number;
  stars: number;             // sum of best star ratings over all levels
  fish: number;
  gems: number;
  highScore: number;
  selectedCat: string;
  equipped: Record<EquipSlot, string>;
  settings: PlayerSettings;
  unlockedCats: Record<string, boolean>;
  unlockedItems: Record<string, boolean>;
  achievements: Record<string, boolean>;
  levels: Record<string, LevelProgress>;
  stats: PlayerStats;
  daily: { lastClaimDay: string | null; lastClaimAt: number; nextDay: number };
  pendingBoxes: number;
}

export const DEFAULT_SETTINGS: PlayerSettings = {
  music: true, sound: true, vibration: true, graphics: 'auto', language: 'en', musicVolume: 0.8, soundVolume: 1, reduceMotion: false, gameSpeed: 'normal',
};

export const emptyLevelProgress = (): LevelProgress => ({
  completed: false, bestStars: 0, bestScore: 0, bestCoins: 0, bestStarTokens: 0, gemsFound: 0, boxesFound: 0, attempts: 0, completions: 0, bestTimeSec: 0,
});

export function newPlayer(uid: string, username: string, now: number): PlayerDoc {
  return {
    uid, username, createdAt: now, updatedAt: now, schemaVersion: 1,
    currentLevel: 1, coins: 0, stars: 0, fish: 0, gems: 0, highScore: 0,
    selectedCat: 'orange_cat',
    equipped: { hat: '', glasses: '', shoes: '', backpack: '', cape: '', scarf: '', trail: '' },
    settings: { ...DEFAULT_SETTINGS },
    unlockedCats: { orange_cat: true },
    unlockedItems: {},
    achievements: {},
    levels: {},
    stats: { runs: 0, jumps: 0, coinsCollected: 0, levelsCompleted: 0, fishCollected: 0, gemsFound: 0, itemsBought: 0, dailyClaims: 0, perfectLevels: 0 },
    daily: { lastClaimDay: null, lastClaimAt: 0, nextDay: 1 },
    pendingBoxes: 0,
  };
}

export const owns = (p: PlayerDoc, itemId: string) => !!(p.unlockedCats[itemId] || p.unlockedItems[itemId]);

/** Deep copy so domain functions never mutate their input. */
export const clonePlayer = (p: PlayerDoc): PlayerDoc => JSON.parse(JSON.stringify(p));

export const GRAPHICS_VALUES: GraphicsQuality[] = ['auto', 'low', 'medium', 'high', 'ultra'];
export const LANGUAGES = ['en', 'da', 'de', 'es', 'fr', 'ur', 'ar'];

/** Validates a settings object sent by a client. Returns a clean copy or null. */
export function sanitizeSettings(input: unknown): PlayerSettings | null {
  if (!input || typeof input !== 'object') return null;
  const s = input as Record<string, unknown>;
  const bool = (k: string) => typeof s[k] === 'boolean';
  const vol = (k: string) => typeof s[k] === 'number' && (s[k] as number) >= 0 && (s[k] as number) <= 1;
  if (!bool('music') || !bool('sound') || !bool('vibration') || !bool('reduceMotion')) return null;
  if (!GRAPHICS_VALUES.includes(s.graphics as GraphicsQuality)) return null;
  if (!LANGUAGES.includes(s.language as string)) return null;
  if (!vol('musicVolume') || !vol('soundVolume')) return null;
  if (s.gameSpeed !== undefined && !GAME_SPEEDS.includes(s.gameSpeed as GameSpeed)) return null;
  return {
    music: s.music as boolean, sound: s.sound as boolean, vibration: s.vibration as boolean, reduceMotion: s.reduceMotion as boolean,
    graphics: s.graphics as GraphicsQuality, language: s.language as string, musicVolume: s.musicVolume as number, soundVolume: s.soundVolume as number,
    gameSpeed: (s.gameSpeed as GameSpeed | undefined) ?? 'normal',
  };
}
