import { AchievementDef, MysteryEntry, ShopItem } from '../content/types';
import { awardAchievements, EarnedAchievement } from './achievements';
import { levelIdFor } from './levels';
import { clonePlayer, EQUIP_SLOTS, EquipSlot, owns, PlayerDoc } from './player';

type Fail = { ok: false; code: string; message: string };
const fail = (code: string, message: string): Fail => ({ ok: false, code, message });

export function purchase(before: PlayerDoc, shop: ShopItem[], itemId: unknown, achievements: AchievementDef[], now: number):
  { ok: true; player: PlayerDoc; item: ShopItem; paidCoins: number; paidGems: number; achievements: EarnedAchievement[] } | Fail {
  const item = shop.find(i => i.id === itemId);
  if (!item || !item.enabled) return fail('not-found', 'Item not found.');
  if (item.source !== 'shop') return fail('failed-precondition', 'This item is a special reward.');
  if (owns(before, item.id)) return fail('already-exists', 'You already have this!');
  if (item.unlockLevel > 0 && !before.levels[levelIdFor(item.unlockLevel)]?.completed) return fail('failed-precondition', `Finish level ${item.unlockLevel} first!`);
  const byGems = item.priceGems > 0;
  if (byGems ? before.gems < item.priceGems : before.coins < item.priceCoins) return fail('resource-exhausted', byGems ? 'Not enough gems yet.' : 'Not enough coins yet.');

  const p = clonePlayer(before);
  if (byGems) p.gems -= item.priceGems; else p.coins -= item.priceCoins;
  if (item.slot === 'cat') p.unlockedCats[item.id] = true; else p.unlockedItems[item.id] = true;
  p.stats.itemsBought += 1;
  const earned = awardAchievements(p, achievements);
  p.updatedAt = now;
  return { ok: true, player: p, item, paidCoins: byGems ? 0 : item.priceCoins, paidGems: byGems ? item.priceGems : 0, achievements: earned };
}

export interface Loadout { selectedCat?: unknown; equipped?: unknown }

/** Equips owned items only. Empty string clears a slot. Capes/scarves/crowns use their own slot rules. */
export function equip(before: PlayerDoc, shop: ShopItem[], loadout: Loadout, now: number): { ok: true; player: PlayerDoc } | Fail {
  const p = clonePlayer(before);
  if (loadout.selectedCat !== undefined) {
    const cat = shop.find(i => i.id === loadout.selectedCat && i.slot === 'cat');
    if (!cat || !p.unlockedCats[cat.id]) return fail('permission-denied', 'You have not unlocked this cat yet.');
    p.selectedCat = cat.id;
  }
  if (loadout.equipped !== undefined) {
    if (!loadout.equipped || typeof loadout.equipped !== 'object') return fail('invalid-argument', 'Bad loadout.');
    for (const [slot, value] of Object.entries(loadout.equipped as Record<string, unknown>)) {
      if (!EQUIP_SLOTS.includes(slot as EquipSlot)) return fail('invalid-argument', `Unknown slot ${slot}.`);
      if (value === '') { p.equipped[slot as EquipSlot] = ''; continue; }
      const it = shop.find(i => i.id === value);
      if (!it || it.slot !== slot || !p.unlockedItems[it.id]) return fail('permission-denied', 'You have not unlocked this item yet.');
      p.equipped[slot as EquipSlot] = it.id;
    }
  }
  p.updatedAt = now;
  return { ok: true, player: p };
}

export interface BoxReward { type: 'coins' | 'gems' | 'item'; amount: number; itemId: string | null; label: string }

/** Opens one pending mystery box. `rng` must be a server-side random source in [0, 1). */
export function openMysteryBox(before: PlayerDoc, table: MysteryEntry[], shop: ShopItem[], rng: () => number, now: number):
  { ok: true; player: PlayerDoc; reward: BoxReward } | Fail {
  if (before.pendingBoxes <= 0) return fail('failed-precondition', 'No mystery boxes to open.');
  const p = clonePlayer(before);
  p.pendingBoxes -= 1;
  const total = table.reduce((a, e) => a + Math.max(0, e.weight), 0);
  let roll = rng() * total; let entry = table[table.length - 1];
  for (const e of table) { roll -= Math.max(0, e.weight); if (roll < 0) { entry = e; break; } }

  let reward: BoxReward = { type: entry.type, amount: entry.amount, itemId: null, label: entry.label };
  if (entry.type === 'item') {
    const candidates = (entry.pool ?? []).filter(id => shop.some(i => i.id === id && i.enabled) && !owns(p, id));
    if (candidates.length === 0) reward = { type: 'coins', amount: 150, itemId: null, label: '150 Coins' };
    else {
      const id = candidates[Math.floor(rng() * candidates.length) % candidates.length];
      const it = shop.find(i => i.id === id)!;
      if (it.slot === 'cat') p.unlockedCats[id] = true; else p.unlockedItems[id] = true;
      reward = { type: 'item', amount: 1, itemId: id, label: it.name };
    }
  }
  if (reward.type === 'coins') p.coins += reward.amount;
  if (reward.type === 'gems') p.gems += reward.amount;
  p.updatedAt = now;
  return { ok: true, player: p, reward };
}
