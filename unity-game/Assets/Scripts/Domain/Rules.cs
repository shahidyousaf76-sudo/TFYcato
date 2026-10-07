using System;
using System.Collections.Generic;

namespace TFY.CatAdventure.Domain
{
    /// <summary>
    /// Game rules shared with the server (firebase/functions/src/domain/levels.ts). The device uses them to show results
    /// instantly and while offline; the server recomputes them from validated counts, so they cannot be cheated.
    /// </summary>
    public static class Rules
    {
        public static string LevelId(int number) => "level_" + number.ToString("00");

        /// <summary>⭐ finish · ⭐⭐ + enough coins · ⭐⭐⭐ + most coins and all big stars. Never required to progress.</summary>
        public static int Stars(LevelDef level, int coins, int starTokens)
        {
            var t = level.Totals();
            if (coins >= level.threeStarCoins && starTokens >= t.starTokens) return 3;
            if (coins >= level.targetCoins) return 2;
            return 1;
        }

        public static int Score(LevelDef level, EconomyConfig e, int coinValue, int fish, int gems, int starTokens, int heartsLeft)
        {
            var t = level.Totals();
            return (int)Math.Round((double)t.length * e.scorePerMetre + coinValue * e.scorePerCoin + fish * e.scorePerFish +
                                   gems * e.scorePerGem + starTokens * e.scorePerStarToken + heartsLeft * e.scorePerHeartLeft);
        }

        /// <summary>Running speed at a distance: smooth ramp from base to max speed (acceleration), scaled by Remote Config.</summary>
        public static float SpeedAt(LevelDef level, float distance, float speedScale)
        {
            float len = Math.Max(1, level.Totals().length);
            float k = Math.Min(1f, Math.Max(0f, distance / len));
            k = k * k * (3f - 2f * k); // smoothstep: gentle start, gentle top-out
            return (level.baseSpeed + (level.maxSpeed - level.baseSpeed) * k) * speedScale;
        }

        public static float MinimumDurationSec(LevelDef level) => level.Totals().length / (level.maxSpeed * 1.4f) * 0.85f;

        /// <summary>Automatic username: approved prefix + 3–4 digits (e.g. Kitty4821). Never free text.</summary>
        public static string GenerateUsername(IList<string> prefixes, Random rng)
        {
            var list = prefixes != null && prefixes.Count > 0 ? prefixes : new[] { "Kitty" };
            return list[rng.Next(list.Count)] + rng.Next(100, 10000);
        }

        public static bool IsValidUsername(string name, IList<string> prefixes)
        {
            if (string.IsNullOrEmpty(name) || name.Length > 24) return false;
            int i = 0;
            while (i < name.Length && char.IsLetter(name[i])) i++;
            var prefix = name.Substring(0, i); var digits = name.Substring(i);
            if (digits.Length < 3 || digits.Length > 4 || digits[0] == '0') return false;
            foreach (var c in digits) if (c < '0' || c > '9') return false;
            return prefixes.Contains(prefix);
        }
    }
}
