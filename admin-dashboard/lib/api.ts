import { call } from './firebase';
import type { AchievementDef, LevelDef, MysteryEntry, RuntimeConfig, ShopItem, WorldDef } from './types';

export interface Stats {
  players: { total: number; new24h: number; new7d: number; avgLevel: number };
  economy: { coins: number; gems: number; stars: number };
  levels: { id: string; completions?: number; failures?: number; checkpointRestarts?: number; starsTotal?: number; rejectedRuns?: number; offlineSynced?: number; firstCompletions?: number }[];
  playersAtLevel: { level: number; players: number }[];
  leaderboardFlagged: number;
  topIssues: { id: string; code: string; count: number; lastSeen: number }[];
}

export interface GameEvent { id?: string; title: string; description: string; active: boolean; startsAt: number; endsAt: number; coinMultiplier?: number; worldId?: string }
export interface Announcement { id?: string; title: string; message: string; active: boolean; startsAt: number; endsAt: number }

export const api = {
  stats: call<void, Stats>('adminGetStats'),
  upsertLevel: call<{ level: LevelDef }, { id: string }>('adminUpsertLevel'),
  setLevelEnabled: call<{ levelId: string; enabled: boolean }, { levelId: string; enabled: boolean }>('adminSetLevelEnabled'),
  upsertContent: call<{ collection: 'worlds' | 'catalog' | 'achievements'; doc: WorldDef | ShopItem | AchievementDef }, { id: string }>('adminUpsertContent'),
  updateRewards: call<{ economy?: Record<string, number>; mysteryBox?: MysteryEntry[] }, { updated: string[] }>('adminUpdateRewards'),
  upsertEvent: call<{ event: GameEvent }, { id: string }>('adminUpsertEvent'),
  publishAnnouncement: call<{ announcement: Announcement }, { id: string }>('adminPublishAnnouncement'),
  getRemoteConfig: call<void, { values: RuntimeConfig; updatedAt: number | null }>('adminGetRemoteConfig'),
  publishRemoteConfig: call<{ values: RuntimeConfig }, { values: RuntimeConfig }>('adminPublishRemoteConfig'),
  reviewLeaderboard: call<{ levelId: string; uid: string; hidden: boolean }, { ok: boolean }>('adminReviewLeaderboard'),
  setRole: call<{ email: string; role: 'admin' | 'super_admin' | null }, { email: string; role: string | null }>('adminSetRole'),
  listAdmins: call<void, { admins: { email?: string; role: string; disabled: boolean }[] }>('adminListAdmins'),
  seedContent: call<{ overwrite: boolean }, { written: number }>('adminSeedContent'),
};

export function errorMessage(e: unknown): string {
  const m = (e as { message?: string })?.message ?? String(e);
  return m.replace(/^FirebaseError:\s*/, '');
}
