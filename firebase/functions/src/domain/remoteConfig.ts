import { DailyRewardDef } from '../content/types';
import { DAILY_REWARDS } from '../content/catalog';

/**
 * Remote Config parameters. Everything has a safe default and a clamp, so a bad value can never break play.
 * Core movement (jump height, gravity, lanes) is deliberately NOT remote — offline play must feel identical.
 * The admin dashboard publishes these to Remote Config (for clients) and mirrors them to config/runtime (for functions).
 */
export interface RuntimeConfig {
  run_speed_scale: number;          // multiplies each level's base/max speed
  difficulty_density_scale: number; // multiplies obstacle density
  coin_reward_multiplier: number;
  starting_hearts: number;
  powerup_durations: Record<'magnet' | 'super_jump' | 'speed_boost' | 'double_coins', number>;
  daily_rewards: DailyRewardDef[];
  daily_rewards_enabled: boolean;
  shop_enabled: boolean;
  mystery_boxes_enabled: boolean;
  leaderboard_enabled: boolean;     // off by default for a kids' game; admins can turn it on
  events_enabled: boolean;
  maintenance_message: string;
}

export const DEFAULT_RUNTIME: RuntimeConfig = {
  run_speed_scale: 1,
  difficulty_density_scale: 1,
  coin_reward_multiplier: 1,
  starting_hearts: 3,
  powerup_durations: { magnet: 8, super_jump: 8, speed_boost: 5, double_coins: 10 },
  daily_rewards: DAILY_REWARDS,
  daily_rewards_enabled: true,
  shop_enabled: true,
  mystery_boxes_enabled: true,
  leaderboard_enabled: false,
  events_enabled: true,
  maintenance_message: '',
};

const clamp = (v: unknown, min: number, max: number, d: number) => (typeof v === 'number' && Number.isFinite(v) ? Math.min(max, Math.max(min, v)) : d);
const bool = (v: unknown, d: boolean) => (typeof v === 'boolean' ? v : d);

function dailyOk(v: unknown): v is DailyRewardDef[] {
  return Array.isArray(v) && v.length >= 1 && v.length <= 14 && v.every((r, i) =>
    r && typeof r === 'object' && r.day === i + 1 && ['coins', 'gems', 'item'].includes(r.type) &&
    Number.isInteger(r.amount) && r.amount > 0 && r.amount <= (r.type === 'gems' ? 10 : 2000) &&
    typeof r.label === 'string' && r.label.length <= 40 && (r.type !== 'item' || typeof r.itemId === 'string'));
}

/** Parses untrusted values (admin input or a stored doc) into a complete, clamped config. Unknown keys are dropped. */
export function sanitizeRuntime(input: unknown): RuntimeConfig {
  const s = (input && typeof input === 'object' ? input : {}) as Record<string, unknown>;
  const d = DEFAULT_RUNTIME;
  const pd = (s.powerup_durations && typeof s.powerup_durations === 'object' ? s.powerup_durations : {}) as Record<string, unknown>;
  return {
    run_speed_scale: clamp(s.run_speed_scale, 0.8, 1.2, d.run_speed_scale),
    difficulty_density_scale: clamp(s.difficulty_density_scale, 0.6, 1.2, d.difficulty_density_scale),
    coin_reward_multiplier: clamp(s.coin_reward_multiplier, 0.5, 3, d.coin_reward_multiplier),
    starting_hearts: Math.round(clamp(s.starting_hearts, 1, 5, d.starting_hearts)),
    powerup_durations: {
      magnet: clamp(pd.magnet, 2, 20, d.powerup_durations.magnet),
      super_jump: clamp(pd.super_jump, 2, 20, d.powerup_durations.super_jump),
      speed_boost: clamp(pd.speed_boost, 2, 12, d.powerup_durations.speed_boost),
      double_coins: clamp(pd.double_coins, 2, 30, d.powerup_durations.double_coins),
    },
    daily_rewards: dailyOk(s.daily_rewards) ? s.daily_rewards : d.daily_rewards,
    daily_rewards_enabled: bool(s.daily_rewards_enabled, d.daily_rewards_enabled),
    shop_enabled: bool(s.shop_enabled, d.shop_enabled),
    mystery_boxes_enabled: bool(s.mystery_boxes_enabled, d.mystery_boxes_enabled),
    leaderboard_enabled: bool(s.leaderboard_enabled, d.leaderboard_enabled),
    events_enabled: bool(s.events_enabled, d.events_enabled),
    maintenance_message: typeof s.maintenance_message === 'string' ? s.maintenance_message.slice(0, 200) : d.maintenance_message,
  };
}

/** Which keys are JSON-typed in the Remote Config template. */
export const JSON_KEYS: (keyof RuntimeConfig)[] = ['powerup_durations', 'daily_rewards'];
