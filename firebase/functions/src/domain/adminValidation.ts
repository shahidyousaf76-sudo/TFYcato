import { AchievementDef, ChallengeId, LevelDef, ObstacleId, SectionKind, ShopItem, WorldDef } from '../content/types';
import { levelIdFor, levelTotals } from './levels';

const SECTION_KINDS: SectionKind[] = ['start', 'easy', 'collectibles', 'obstacles', 'medium', 'challenge', 'finish'];
const CHALLENGES: ChallengeId[] = ['log_hop', 'branch_duck', 'coin_climb', 'barrel_alley', 'wall_weave', 'mixed_medium', 'bridge_leap', 'rock_fall', 'critter_parade', 'grand_finale', 'combo_wall_critter', 'combo_bridge_rocks'];
const isInt = (v: unknown, min: number, max: number) => typeof v === 'number' && Number.isInteger(v) && v >= min && v <= max;
const isNum = (v: unknown, min: number, max: number) => typeof v === 'number' && Number.isFinite(v) && v >= min && v <= max;
const isStr = (v: unknown, max: number) => typeof v === 'string' && v.length > 0 && v.length <= max;

/** Returns a list of problems (empty = valid). Keeps admins from publishing a level that is impossible or breaks clients. */
export function validateLevel(input: unknown, worlds: Pick<WorldDef, 'id'>[], obstacleIds: ObstacleId[]): string[] {
  const e: string[] = [];
  if (!input || typeof input !== 'object') return ['Level must be an object.'];
  const l = input as LevelDef;
  if (!isInt(l.number, 1, 999)) e.push('number must be 1–999.');
  else if (l.id !== levelIdFor(l.number)) e.push(`id must be ${levelIdFor(l.number)}.`);
  if (!isStr(l.name, 40)) e.push('name is required (max 40).');
  if (!worlds.some(w => w.id === l.world)) e.push('world does not exist.');
  if (!['easy', 'medium', 'hard', 'combo'].includes(l.difficulty)) e.push('difficulty is invalid.');
  if (!['sunrise', 'morning', 'day', 'sunset', 'evening', 'night'].includes(l.timeOfDay)) e.push('timeOfDay is invalid.');
  if (!isNum(l.baseSpeed, 5, 16) || !isNum(l.maxSpeed, 5, 16) || l.maxSpeed < l.baseSpeed) e.push('speeds must be 5–16 m/s and maxSpeed ≥ baseSpeed.');
  if (!isInt(l.seed, 0, 2 ** 31 - 1)) e.push('seed must be a positive integer.');
  if (typeof l.enabled !== 'boolean' || typeof l.tutorial !== 'boolean') e.push('enabled/tutorial must be true or false.');
  if (typeof l.tip !== 'string' || l.tip.length > 120) e.push('tip max 120 characters.');
  if (!Array.isArray(l.sections) || l.sections.length < 3 || l.sections.length > 24) { e.push('sections must have 3–24 entries.'); return e; }
  if (l.sections[0].kind !== 'start') e.push('first section must be "start".');
  if (l.sections[l.sections.length - 1].kind !== 'finish') e.push('last section must be "finish".');
  l.sections.forEach((s, i) => {
    const at = `sections[${i}]`;
    if (!SECTION_KINDS.includes(s.kind)) e.push(`${at}.kind is invalid.`);
    if (!isInt(s.length, 20, 600)) e.push(`${at}.length must be 20–600.`);
    if (!Array.isArray(s.obstacles) || s.obstacles.some(o => !obstacleIds.includes(o))) e.push(`${at}.obstacles has an unknown obstacle.`);
    if (!isNum(s.density, 0, 1) || !isNum(s.twoLaneChance, 0, 0.6)) e.push(`${at} density 0–1, twoLaneChance 0–0.6.`);
    for (const k of ['coins', 'fish', 'gems', 'starTokens', 'mysteryBoxes', 'powerUps'] as const) if (!isInt(s[k], 0, k === 'coins' ? 150 : 10)) e.push(`${at}.${k} is out of range.`);
    if (s.challenge !== undefined && !CHALLENGES.includes(s.challenge)) e.push(`${at}.challenge is invalid.`);
    if (typeof s.checkpointAfter !== 'boolean') e.push(`${at}.checkpointAfter must be true/false.`);
    // Room for the content: coin groups need ~2 m each; every section needs room for at least one safe gap.
    if (isInt(s.length, 20, 600) && isInt(s.coins, 0, 150) && s.coins * 2.2 > s.length * 1.6) e.push(`${at} has too many coins for its length.`);
  });
  const t = levelTotals(l);
  if (!isInt(l.targetCoins, 0, t.coins) || !isInt(l.threeStarCoins, l.targetCoins ?? 0, t.coins)) e.push('0 ≤ targetCoins ≤ threeStarCoins ≤ total coins.');
  const cps = l.sections.filter(s => s.checkpointAfter).length;
  if (l.checkpointCount !== cps) e.push(`checkpointCount must equal the number of sections with checkpointAfter (${cps}).`);
  if (l.targetStars !== 3) e.push('targetStars must be 3.');
  return e;
}

export function validateShopItem(i: unknown): string[] {
  const e: string[] = []; const s = i as ShopItem;
  if (!s || typeof s !== 'object') return ['Item must be an object.'];
  if (!/^[a-z0-9_]{2,40}$/.test(s.id ?? '')) e.push('id: lowercase letters, digits, underscore.');
  if (!isStr(s.name, 40)) e.push('name required.');
  if (!['cat', 'hat', 'glasses', 'shoes', 'backpack', 'cape', 'scarf', 'trail'].includes(s.slot)) e.push('slot invalid.');
  if (!['CATS', 'HATS', 'GLASSES', 'SHOES', 'BACKPACKS', 'CAPES_SCARVES', 'TRAILS'].includes(s.category)) e.push('category invalid.');
  if (!isInt(s.priceCoins, 0, 100000) || !isInt(s.priceGems, 0, 1000)) e.push('prices out of range.');
  if (s.priceCoins > 0 && s.priceGems > 0) e.push('use either a coin price or a gem price.');
  if (!isInt(s.unlockLevel, 0, 999)) e.push('unlockLevel out of range.');
  if (!['default', 'shop', 'daily', 'mystery', 'achievement'].includes(s.source)) e.push('source invalid.');
  if (!Array.isArray(s.colors) || s.colors.some(c => !/^#[0-9A-Fa-f]{6}$/.test(c))) e.push('colors must be #RRGGBB.');
  if (typeof s.enabled !== 'boolean') e.push('enabled must be true/false.');
  return e;
}

export function validateAchievement(a: unknown): string[] {
  const e: string[] = []; const s = a as AchievementDef;
  if (!s || typeof s !== 'object') return ['Achievement must be an object.'];
  if (!/^[a-z0-9_]{2,40}$/.test(s.id ?? '')) e.push('id invalid.');
  if (!isStr(s.name, 40) || !isStr(s.description, 120)) e.push('name/description required.');
  if (!['jumps', 'coinsCollected', 'levelsCompleted', 'stars', 'fishCollected', 'gemsFound', 'itemsBought', 'dailyClaims', 'perfectLevels'].includes(s.stat)) e.push('stat invalid.');
  if (!isInt(s.target, 1, 1_000_000) || !isInt(s.rewardCoins, 0, 5000) || !isInt(s.rewardGems, 0, 50)) e.push('numbers out of range.');
  return e;
}

export function validateWorld(w: unknown): string[] {
  const e: string[] = []; const s = w as WorldDef;
  if (!s || typeof s !== 'object') return ['World must be an object.'];
  if (!/^[a-z0-9_]{2,30}$/.test(s.id ?? '')) e.push('id invalid.');
  if (!isStr(s.name, 40)) e.push('name required.');
  if (typeof s.enabled !== 'boolean') e.push('enabled must be true/false.');
  if (!s.palette || Object.values(s.palette).some(c => !/^#[0-9A-Fa-f]{6}$/.test(String(c)))) e.push('palette colours must be #RRGGBB.');
  if (!isNum(s.fogDensity, 0, 0.05)) e.push('fogDensity 0–0.05.');
  return e;
}

/** Announcements are shown to children: plain text only, short, no links. */
export function validateAnnouncement(a: unknown): string[] {
  const e: string[] = []; const s = a as { title?: unknown; message?: unknown; active?: unknown; startsAt?: unknown; endsAt?: unknown };
  if (!isStr(s?.title, 40) || !isStr(s?.message, 200)) e.push('title (≤40) and message (≤200) required.');
  if (typeof s?.message === 'string' && /(https?:\/\/|www\.|@)/i.test(s.message)) e.push('no links or contact details in messages for children.');
  if (typeof s?.active !== 'boolean') e.push('active must be true/false.');
  if (!isInt(s?.startsAt, 0, 9e15) || !isInt(s?.endsAt, 0, 9e15) || (s.endsAt as number) <= (s.startsAt as number)) e.push('endsAt must be after startsAt.');
  return e;
}

export function validateEvent(ev: unknown): string[] {
  const e = validateAnnouncement({ ...(ev as object), message: (ev as { description?: unknown })?.description });
  const s = ev as { coinMultiplier?: unknown; worldId?: unknown };
  if (s.coinMultiplier !== undefined && !isNum(s.coinMultiplier, 1, 3)) e.push('coinMultiplier 1–3.');
  if (s.worldId !== undefined && s.worldId !== '' && !isStr(s.worldId, 30)) e.push('worldId invalid.');
  return e;
}
