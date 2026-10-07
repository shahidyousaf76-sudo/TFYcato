using System;
using System.Collections.Generic;

namespace TFY.CatAdventure.Domain
{
    /// <summary>
    /// Remote Config values (mirrors firebase/functions/src/domain/remoteConfig.ts). Every value is clamped, so a bad
    /// remote value can never break play. Jump height, gravity and lanes are NOT remote: offline play must feel the same.
    /// </summary>
    [Serializable]
    public class RuntimeConfig
    {
        public float run_speed_scale = 1f;
        public float difficulty_density_scale = 1f;
        public float coin_reward_multiplier = 1f;
        public int starting_hearts = 3;
        public PowerUpDurations powerup_durations = new PowerUpDurations();
        public List<DailyRewardDef> daily_rewards = new List<DailyRewardDef>();
        public bool daily_rewards_enabled = true, shop_enabled = true, mystery_boxes_enabled = true, leaderboard_enabled = false, events_enabled = true;
        public string maintenance_message = "";

        public RuntimeConfig Sanitized()
        {
            var c = (RuntimeConfig)MemberwiseClone();
            c.run_speed_scale = Clamp(run_speed_scale, 0.8f, 1.2f, 1f);
            c.difficulty_density_scale = Clamp(difficulty_density_scale, 0.6f, 1.2f, 1f);
            c.coin_reward_multiplier = Clamp(coin_reward_multiplier, 0.5f, 3f, 1f);
            c.starting_hearts = (int)Math.Round(Clamp(starting_hearts, 1, 5, 3));
            var d = powerup_durations ?? new PowerUpDurations();
            c.powerup_durations = new PowerUpDurations
            {
                magnet = Clamp(d.magnet, 2, 20, 8), super_jump = Clamp(d.super_jump, 2, 20, 8),
                speed_boost = Clamp(d.speed_boost, 2, 12, 5), double_coins = Clamp(d.double_coins, 2, 30, 10),
            };
            c.maintenance_message = (maintenance_message ?? "").Length > 200 ? maintenance_message.Substring(0, 200) : maintenance_message ?? "";
            c.daily_rewards = daily_rewards ?? new List<DailyRewardDef>();
            return c;
        }

        public float DurationFor(string powerUpId)
        {
            switch (powerUpId)
            {
                case "magnet": return powerup_durations.magnet;
                case "super_jump": return powerup_durations.super_jump;
                case "speed_boost": return powerup_durations.speed_boost;
                case "double_coins": return powerup_durations.double_coins;
                default: return 0f; // shield lasts until it absorbs a bump
            }
        }

        private static float Clamp(float v, float min, float max, float fallback) => float.IsNaN(v) || float.IsInfinity(v) ? fallback : Math.Min(max, Math.Max(min, v));
    }

    [Serializable]
    public class PowerUpDurations { public float magnet = 8, super_jump = 8, speed_boost = 5, double_coins = 10; }
}
