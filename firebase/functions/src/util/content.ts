import { DEFAULT_CONTENT } from '../content';
import { AchievementDef, EconomyConfig, LevelDef, MysteryEntry, ShopItem } from '../content/types';
import { DEFAULT_RUNTIME, RuntimeConfig, sanitizeRuntime } from '../domain/remoteConfig';
import { db } from './firebase';

/**
 * Live content (levels, shop, achievements, economy, runtime config) read from Firestore, with the bundled defaults as a fallback.
 * Cached per function instance for 60 s so admin edits propagate quickly without a read per call.
 */
export interface LiveContent {
  levels: LevelDef[];
  shop: ShopItem[];
  achievements: AchievementDef[];
  mysteryBox: MysteryEntry[];
  economy: EconomyConfig;
  runtime: RuntimeConfig;
  events: GameEvent[];
}

export interface GameEvent { id: string; title: string; description: string; active: boolean; startsAt: number; endsAt: number; coinMultiplier?: number; worldId?: string }

/** Coin multiplier for a level right now: Remote Config × the best running event (events are optional and capped). */
export function coinMultiplierFor(c: LiveContent, worldId: string, now: number): number {
  let event = 1;
  if (c.runtime.events_enabled) for (const e of c.events) {
    if (e.active && now >= e.startsAt && now < e.endsAt && (!e.worldId || e.worldId === worldId)) event = Math.max(event, Math.min(3, e.coinMultiplier ?? 1));
  }
  return c.runtime.coin_reward_multiplier * event;
}

let cache: { at: number; value: LiveContent } | null = null;
const TTL_MS = 60_000;

export function invalidateContentCache() { cache = null; }

export async function liveContent(): Promise<LiveContent> {
  if (cache && Date.now() - cache.at < TTL_MS) return cache.value;
  const [levels, shop, achievements, rewards, runtime, events] = await Promise.all([
    db.collection('levels').get(), db.collection('catalog').get(), db.collection('achievements').get(),
    db.doc('config/rewards').get(), db.doc('config/runtime').get(), db.collection('events').where('active', '==', true).get(),
  ]);
  const r = rewards.data() ?? {};
  const value: LiveContent = {
    levels: levels.empty ? DEFAULT_CONTENT.levels : levels.docs.map(d => d.data() as LevelDef).sort((a, b) => a.number - b.number),
    shop: shop.empty ? DEFAULT_CONTENT.shop : shop.docs.map(d => d.data() as ShopItem),
    achievements: achievements.empty ? DEFAULT_CONTENT.achievements : achievements.docs.map(d => d.data() as AchievementDef),
    mysteryBox: Array.isArray(r.mysteryBox) && r.mysteryBox.length ? r.mysteryBox : DEFAULT_CONTENT.mysteryBox,
    economy: { ...DEFAULT_CONTENT.economy, ...(r.economy ?? {}) },
    runtime: runtime.exists ? sanitizeRuntime(runtime.data()) : DEFAULT_RUNTIME,
    events: events.docs.map(d => ({ ...(d.data() as GameEvent), id: d.id })),
  };
  value.economy.startingHearts = value.runtime.starting_hearts;
  cache = { at: Date.now(), value };
  return value;
}
