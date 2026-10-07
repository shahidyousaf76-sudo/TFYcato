using System;
using System.Collections.Generic;

// Mirrors firebase/functions/src/content/types.ts (the single source of truth).
// Plain C# with public fields: no UnityEngine dependency, so it compiles and is unit-tested outside Unity.
namespace TFY.CatAdventure.Domain
{
    [Serializable]
    public class GameContent
    {
        public int version;
        public List<WorldDef> worlds = new List<WorldDef>();
        public List<ObstacleDef> obstacles = new List<ObstacleDef>();
        public List<LevelDef> levels = new List<LevelDef>();
        public List<ShopItem> shop = new List<ShopItem>();
        public List<AchievementDef> achievements = new List<AchievementDef>();
        public List<DailyRewardDef> dailyRewards = new List<DailyRewardDef>();
        public List<MysteryEntry> mysteryBox = new List<MysteryEntry>();
        public List<PowerUpDef> powerUps = new List<PowerUpDef>();
        public EconomyConfig economy = new EconomyConfig();
        public UsernameWords usernameWords = new UsernameWords();
        public List<string> tips = new List<string>();

        public LevelDef Level(int number) => levels.Find(l => l.number == number);
        public LevelDef LevelById(string id) => levels.Find(l => l.id == id);
        public WorldDef World(string id) => worlds.Find(w => w.id == id);
        public ObstacleDef Obstacle(string id) => obstacles.Find(o => o.id == id);
        public ShopItem Item(string id) => shop.Find(i => i.id == id);
        public PowerUpDef PowerUp(string id) => powerUps.Find(p => p.id == id);
        public int LastEnabledLevel { get { int n = 1; foreach (var l in levels) if (l.enabled && l.number > n) n = l.number; return n; } }
    }

    [Serializable] public class UsernameWords { public List<string> prefixes = new List<string>(); }

    [Serializable]
    public class WorldPalette { public string skyTop, skyHorizon, ground, path, pathEdge, accent, fog, water; }

    [Serializable]
    public class WorldDef
    {
        public string id, name, difficultyLabel, music, weather;
        public int order;
        public bool enabled, comingSoon;
        public List<string> ambience = new List<string>();
        public WorldPalette palette = new WorldPalette();
        public float fogDensity;
        public List<string> decor = new List<string>();
        public List<string> landmarks = new List<string>(); // world-famous landmarks along the route
    }

    [Serializable]
    public class ObstacleDef
    {
        public string id, name, emoji, difficulty, clearance, requiredAction, warning;
        public float height, topHeight, depth, warningDistance;
        public int lanes, minLevel;
    }

    public static class SectionKinds
    {
        public const string Start = "start", Easy = "easy", Collectibles = "collectibles", Obstacles = "obstacles", Medium = "medium", Challenge = "challenge", Finish = "finish";
    }

    [Serializable]
    public class SectionDef
    {
        public string kind;
        public int length;
        public List<string> obstacles = new List<string>();
        public float density, twoLaneChance;
        public int coins, fish, gems, starTokens, mysteryBoxes, powerUps;
        public string challenge;
        public bool checkpointAfter;
    }

    [Serializable]
    public class LevelTotals { public int length, coins, fish, gems, starTokens, mysteryBoxes, powerUps; }

    [Serializable]
    public class LevelDef
    {
        public string id, name, world, difficulty, timeOfDay, tip;
        public int number, targetCoins, threeStarCoins, targetStars, checkpointCount, seed;
        public float baseSpeed, maxSpeed;
        public bool enabled, tutorial;
        public List<SectionDef> sections = new List<SectionDef>();

        /// <summary>Same arithmetic as the server's levelTotals(): the planner places exactly these amounts.</summary>
        public LevelTotals Totals()
        {
            var t = new LevelTotals();
            foreach (var s in sections)
            {
                t.length += s.length; t.coins += s.coins; t.fish += s.fish; t.gems += s.gems;
                t.starTokens += s.starTokens; t.mysteryBoxes += s.mysteryBoxes; t.powerUps += s.powerUps;
            }
            return t;
        }
    }

    [Serializable]
    public class ShopItem
    {
        public string id, name, slot, category, source, description;
        public int priceCoins, priceGems, unlockLevel;
        public List<string> colors = new List<string>();
        public bool enabled;
        public bool IsCat => slot == "cat";
    }

    [Serializable]
    public class AchievementDef
    {
        public string id, name, description, stat, icon;
        public int target, rewardCoins, rewardGems;
    }

    [Serializable] public class DailyRewardDef { public int day, amount; public string type, itemId, label; }

    [Serializable] public class MysteryEntry { public string type, label; public int amount, weight; public List<string> pool = new List<string>(); }

    [Serializable] public class PowerUpDef { public string id, name, color, description; public float durationSec; }

    [Serializable]
    public class EconomyConfig
    {
        public int firstClearBonusBase = 50, firstClearBonusPerLevel = 10, newStarBonus = 25;
        public float coinRewardMultiplier = 1;
        public int scorePerMetre = 1, scorePerCoin = 10, scorePerFish = 50, scorePerGem = 150, scorePerStarToken = 300, scorePerHeartLeft = 200, startingHearts = 3;
    }
}
