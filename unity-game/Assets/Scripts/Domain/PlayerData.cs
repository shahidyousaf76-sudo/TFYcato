using System;
using System.Collections.Generic;

namespace TFY.CatAdventure.Domain
{
    /// <summary>Mirror of players/{uid} (firebase/functions/src/domain/player.ts). Field names match Firestore exactly.</summary>
    [Serializable]
    public class PlayerData
    {
        public string uid, username;
        public long createdAt, updatedAt;
        public int schemaVersion = 1;
        public int currentLevel = 1, coins, stars, fish, gems, highScore;
        public string selectedCat = "orange_cat";
        public Dictionary<string, string> equipped = NewEquipped();
        public PlayerSettings settings = new PlayerSettings();
        public Dictionary<string, bool> unlockedCats = new Dictionary<string, bool> { { "orange_cat", true } };
        public Dictionary<string, bool> unlockedItems = new Dictionary<string, bool>();
        public Dictionary<string, bool> achievements = new Dictionary<string, bool>();
        public Dictionary<string, LevelProgress> levels = new Dictionary<string, LevelProgress>();
        public PlayerStats stats = new PlayerStats();
        public DailyState daily = new DailyState();
        public int pendingBoxes;

        public static readonly string[] Slots = { "hat", "glasses", "shoes", "backpack", "cape", "scarf", "trail" };
        public static Dictionary<string, string> NewEquipped()
        {
            var d = new Dictionary<string, string>();
            foreach (var s in Slots) d[s] = "";
            return d;
        }

        public bool Owns(string itemId) =>
            (unlockedCats != null && unlockedCats.TryGetValue(itemId, out var c) && c) || (unlockedItems != null && unlockedItems.TryGetValue(itemId, out var i) && i);

        public LevelProgress Level(string levelId) => levels != null && levels.TryGetValue(levelId, out var p) ? p : null;
        public bool IsCompleted(int levelNumber) => Level(Rules.LevelId(levelNumber))?.completed ?? false;
        public bool IsUnlocked(int levelNumber) => levelNumber <= currentLevel;

        public PlayerData Clone()
        {
            var p = (PlayerData)MemberwiseClone();
            p.equipped = new Dictionary<string, string>(equipped ?? NewEquipped());
            p.settings = (settings ?? new PlayerSettings()).Clone();
            p.unlockedCats = new Dictionary<string, bool>(unlockedCats ?? new Dictionary<string, bool>());
            p.unlockedItems = new Dictionary<string, bool>(unlockedItems ?? new Dictionary<string, bool>());
            p.achievements = new Dictionary<string, bool>(achievements ?? new Dictionary<string, bool>());
            p.levels = new Dictionary<string, LevelProgress>();
            if (levels != null) foreach (var kv in levels) p.levels[kv.Key] = (LevelProgress)kv.Value.Clone();
            p.stats = (PlayerStats)(stats ?? new PlayerStats()).Clone();
            p.daily = (DailyState)(daily ?? new DailyState()).Clone();
            return p;
        }

        /// <summary>Repairs anything missing (older saves, partial documents) so the game never crashes on a null.</summary>
        public PlayerData Normalize()
        {
            if (currentLevel < 1) currentLevel = 1;
            equipped = equipped ?? NewEquipped();
            foreach (var s in Slots) if (!equipped.ContainsKey(s) || equipped[s] == null) equipped[s] = "";
            settings = settings ?? new PlayerSettings();
            unlockedCats = unlockedCats ?? new Dictionary<string, bool>();
            unlockedCats["orange_cat"] = true;
            unlockedItems = unlockedItems ?? new Dictionary<string, bool>();
            achievements = achievements ?? new Dictionary<string, bool>();
            levels = levels ?? new Dictionary<string, LevelProgress>();
            stats = stats ?? new PlayerStats();
            daily = daily ?? new DailyState();
            if (string.IsNullOrEmpty(selectedCat) || !Owns(selectedCat)) selectedCat = "orange_cat";
            return this;
        }
    }

    [Serializable]
    public class PlayerSettings
    {
        public bool music = true, sound = true, vibration = true, reduceMotion;
        public string graphics = "auto"; // auto | low | medium | high | ultra
        public string language = "en";
        public float musicVolume = 0.8f, soundVolume = 1f;
        /// <summary>slow | normal | fast — scales the level's time (0.75× / 1× / 1.25×), so every jump and gap keeps its size.</summary>
        public string gameSpeed = "normal";
        public float GameSpeedScale => gameSpeed == "slow" ? 0.75f : gameSpeed == "fast" ? 1.25f : 1f;
        public PlayerSettings Clone() => (PlayerSettings)MemberwiseClone();
    }

    [Serializable]
    public class LevelProgress
    {
        public bool completed;
        public int bestStars, bestScore, bestCoins, bestStarTokens, gemsFound, boxesFound, attempts, completions;
        public float bestTimeSec;
        public object Clone() => MemberwiseClone();
    }

    [Serializable]
    public class PlayerStats { public int runs, jumps, coinsCollected, levelsCompleted, fishCollected, gemsFound, itemsBought, dailyClaims, perfectLevels; public object Clone() => MemberwiseClone(); }

    [Serializable]
    public class DailyState { public string lastClaimDay; public long lastClaimAt; public int nextDay = 1; public object Clone() => MemberwiseClone(); }

    /// <summary>What the game reports when a level is finished (server: RunReport). The server computes score, stars and rewards.</summary>
    [Serializable]
    public class RunReport
    {
        public string runId, levelId;
        public int levelNumber;
        public int coins, coinValue, fish, gems, starTokens, mysteryBoxes, heartsLeft, jumps, checkpointsUsed, failedAttempts;
        public float durationSec;
        public bool offline;
        public long finishedAt;
    }

    /// <summary>Server result (LevelResult in progress.ts), also produced locally as a provisional result while offline.</summary>
    [Serializable]
    public class LevelResult
    {
        public string levelId;
        public int levelNumber, stars, score, coinsEarned, bonusCoins, fishEarned, gemsEarned, mysteryBoxesEarned;
        public int? unlockedLevel;
        public bool firstCompletion, newBestScore;
        public List<EarnedAchievement> achievements = new List<EarnedAchievement>();
        public ResultTotals totals = new ResultTotals();
        public bool provisional; // true = computed on the device while offline; the server confirms later
    }

    [Serializable] public class EarnedAchievement { public string id, name; public int rewardCoins, rewardGems; }
    [Serializable] public class ResultTotals { public int coins, gems, fish, stars, currentLevel, highScore, pendingBoxes; }
}
