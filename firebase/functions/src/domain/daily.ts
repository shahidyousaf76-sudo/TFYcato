import { AchievementDef, DailyRewardDef, ShopItem } from '../content/types';
import { awardAchievements, EarnedAchievement } from './achievements';
import { clonePlayer, PlayerDoc } from './player';

const MAX_OFFSET_MIN = 14 * 60;
const MIN_HOURS_BETWEEN_CLAIMS = 18; // stops time-zone hopping to claim twice

/** Local calendar day for the player (server clock + the device's UTC offset, clamped to real time zones). */
export function localDayKey(now: number, utcOffsetMinutes: number): string {
  const off = Math.max(-MAX_OFFSET_MIN, Math.min(MAX_OFFSET_MIN, Math.round(utcOffsetMinutes || 0)));
  return new Date(now + off * 60_000).toISOString().slice(0, 10);
}

export interface DailyStatus { today: string; claimedToday: boolean; canClaim: boolean; day: number; reward: DailyRewardDef; nextClaimAt: number }

export function dailyStatus(p: PlayerDoc, rewards: DailyRewardDef[], now: number, utcOffsetMinutes: number): DailyStatus {
  const today = localDayKey(now, utcOffsetMinutes);
  const claimedToday = p.daily.lastClaimDay === today;
  const cooldownEnds = p.daily.lastClaimAt + MIN_HOURS_BETWEEN_CLAIMS * 3_600_000;
  const day = ((p.daily.nextDay - 1) % rewards.length) + 1;
  return { today, claimedToday, canClaim: !claimedToday && now >= cooldownEnds, day, reward: rewards[day - 1], nextClaimAt: Math.max(cooldownEnds, now) };
}

export interface DailyClaim { day: number; label: string; coins: number; gems: number; itemId: string | null; achievements: EarnedAchievement[] }

/**
 * Claims today's reward. Missing a day never resets the cycle — the child simply continues with the next day
 * (no fear-of-missing-out pressure). If an item reward is already owned, it becomes 200 coins instead.
 */
export function claimDaily(before: PlayerDoc, rewards: DailyRewardDef[], shop: ShopItem[], achievements: AchievementDef[], now: number, utcOffsetMinutes: number):
  { ok: true; player: PlayerDoc; claim: DailyClaim } | { ok: false; code: string; message: string } {
  const status = dailyStatus(before, rewards, now, utcOffsetMinutes);
  if (!status.canClaim) return { ok: false, code: 'failed-precondition', message: status.claimedToday ? 'Already claimed today.' : 'Come back a little later.' };
  const p = clonePlayer(before);
  const r = status.reward;
  const claim: DailyClaim = { day: status.day, label: r.label, coins: 0, gems: 0, itemId: null, achievements: [] };
  if (r.type === 'coins') claim.coins = r.amount;
  else if (r.type === 'gems') claim.gems = r.amount;
  else {
    const item = shop.find(i => i.id === r.itemId);
    const owned = item && (item.slot === 'cat' ? p.unlockedCats[item.id] : p.unlockedItems[item.id]);
    if (!item || owned) { claim.coins = 200; claim.label = '200 Coins'; }
    else { claim.itemId = item.id; if (item.slot === 'cat') p.unlockedCats[item.id] = true; else p.unlockedItems[item.id] = true; }
  }
  p.coins += claim.coins; p.gems += claim.gems;
  p.daily = { lastClaimDay: status.today, lastClaimAt: now, nextDay: (status.day % rewards.length) + 1 };
  p.stats.dailyClaims += 1;
  claim.achievements = awardAchievements(p, achievements);
  p.updatedAt = now;
  return { ok: true, player: p, claim };
}
