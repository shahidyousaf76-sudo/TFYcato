using System;
using System.Collections.Generic;
using System.Linq;

namespace TFY.CatAdventure.Domain
{
    /// <summary>
    /// Port of firebase/functions/src/domain/progress.ts applyLevelResult(). Used on the device to show results instantly
    /// and to keep playing offline (provisional). The server's answer always replaces the provisional one when it arrives.
    /// Parity with the TypeScript version is checked by tests against shared/parity-fixtures.json.
    /// </summary>
    public static class Progress
    {
        public static (PlayerData player, LevelResult result) Apply(PlayerData before, LevelDef level, RunReport run, GameContent content, float coinMultiplier, long now)
        {
            var p = before.Clone().Normalize();
            var e = content.economy;
            var lp = p.Level(level.id) ?? new LevelProgress();
            int stars = Rules.Stars(level, run.coins, run.starTokens);
            int score = Rules.Score(level, e, run.coinValue, run.fish, run.gems, run.starTokens, run.heartsLeft);
            bool first = !lp.completed;
            int newStars = Math.Max(0, stars - lp.bestStars);

            double mult = Math.Min(3, Math.Max(0.5, coinMultiplier));
            int coinsEarned = RoundHalfUp(run.coinValue * mult);
            int bonus = RoundHalfUp(((first ? e.firstClearBonusBase + e.firstClearBonusPerLevel * level.number : 0) + newStars * e.newStarBonus) * mult);
            int gemsEarned = Math.Max(0, run.gems - lp.gemsFound);
            int boxesEarned = Math.Max(0, run.mysteryBoxes - lp.boxesFound);

            lp.attempts += 1 + run.failedAttempts;
            lp.completions += 1;
            lp.completed = true;
            lp.bestStars = Math.Max(lp.bestStars, stars);
            bool newBest = score > lp.bestScore;
            lp.bestScore = Math.Max(lp.bestScore, score);
            lp.bestCoins = Math.Max(lp.bestCoins, run.coins);
            lp.bestStarTokens = Math.Max(lp.bestStarTokens, run.starTokens);
            lp.gemsFound = Math.Max(lp.gemsFound, run.gems);
            lp.boxesFound = Math.Max(lp.boxesFound, run.mysteryBoxes);
            lp.bestTimeSec = lp.bestTimeSec > 0 ? Math.Min(lp.bestTimeSec, run.durationSec) : run.durationSec;
            p.levels[level.id] = lp;

            p.coins += coinsEarned + bonus;
            p.fish += run.fish;
            p.gems += gemsEarned;
            p.pendingBoxes += boxesEarned;
            p.highScore = Math.Max(p.highScore, score);
            p.stars = p.levels.Values.Sum(l => l.bestStars);

            int? unlocked = null;
            var next = content.levels.Where(l => l.enabled && l.number > level.number).OrderBy(l => l.number).FirstOrDefault();
            if (next != null && next.number > p.currentLevel) { p.currentLevel = next.number; unlocked = next.number; }

            p.stats.runs += 1;
            p.stats.jumps += run.jumps;
            p.stats.coinsCollected += run.coins;
            p.stats.fishCollected += run.fish;
            p.stats.gemsFound += gemsEarned;
            p.stats.levelsCompleted = p.levels.Values.Count(l => l.completed);
            p.stats.perfectLevels = p.levels.Values.Count(l => l.bestStars >= 3);

            var earned = AwardAchievements(p, content.achievements);
            p.updatedAt = now;

            return (p, new LevelResult
            {
                levelId = level.id, levelNumber = level.number, stars = stars, score = score, firstCompletion = first, newBestScore = newBest,
                coinsEarned = coinsEarned, bonusCoins = bonus, fishEarned = run.fish, gemsEarned = gemsEarned, mysteryBoxesEarned = boxesEarned,
                unlockedLevel = unlocked, achievements = earned,
                totals = new ResultTotals { coins = p.coins, gems = p.gems, fish = p.fish, stars = p.stars, currentLevel = p.currentLevel, highScore = p.highScore, pendingBoxes = p.pendingBoxes },
            });
        }

        public static List<EarnedAchievement> AwardAchievements(PlayerData p, List<AchievementDef> defs)
        {
            var earned = new List<EarnedAchievement>();
            foreach (var a in defs)
            {
                if (p.achievements.TryGetValue(a.id, out var done) && done) continue;
                if (StatValue(p, a.stat) < a.target) continue;
                p.achievements[a.id] = true;
                p.coins += a.rewardCoins; p.gems += a.rewardGems;
                earned.Add(new EarnedAchievement { id = a.id, name = a.name, rewardCoins = a.rewardCoins, rewardGems = a.rewardGems });
            }
            return earned;
        }

        public static int StatValue(PlayerData p, string stat)
        {
            switch (stat)
            {
                case "stars": return p.stars;
                case "jumps": return p.stats.jumps;
                case "coinsCollected": return p.stats.coinsCollected;
                case "levelsCompleted": return p.stats.levelsCompleted;
                case "fishCollected": return p.stats.fishCollected;
                case "gemsFound": return p.stats.gemsFound;
                case "itemsBought": return p.stats.itemsBought;
                case "dailyClaims": return p.stats.dailyClaims;
                case "perfectLevels": return p.stats.perfectLevels;
                default: return 0;
            }
        }

        /// <summary>JavaScript Math.round semantics (x.5 rounds up), so both sides agree to the coin.</summary>
        public static int RoundHalfUp(double v) => (int)Math.Floor(v + 0.5);
    }
}
