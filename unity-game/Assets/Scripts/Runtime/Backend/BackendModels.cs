using System;
using System.Collections.Generic;
using TFY.CatAdventure.Domain;

namespace TFY.CatAdventure.Backend
{
    /// <summary>Outcome of any backend call. `offline` = no connection / service unavailable (keep playing); `code` = server error code.</summary>
    public struct Result<T>
    {
        public bool ok, offline;
        public string code, message;
        public T data;
        public static Result<T> Ok(T d) => new Result<T> { ok = true, data = d };
        public static Result<T> Offline(string msg = null) => new Result<T> { offline = true, code = "unavailable", message = msg };
        public static Result<T> Fail(string code, string msg) => new Result<T> { code = code, message = msg };
    }

    [Serializable] public class CreatePlayerResponse { public bool created; public PlayerData player; }
    [Serializable] public class CompleteLevelResponse { public bool replayed; public LevelResult result; }
    [Serializable] public class SyncItem { public string runId; public bool ok, replayed; public LevelResult result; public string code, message; }
    [Serializable] public class SyncResponse { public List<SyncItem> results = new List<SyncItem>(); public PlayerData player; }
    [Serializable] public class DailyStatus { public string today; public bool claimedToday, canClaim; public int day; public DailyRewardDef reward; public long nextClaimAt; }
    [Serializable] public class DailyResponse { public bool enabled; public List<DailyRewardDef> rewards = new List<DailyRewardDef>(); public DailyStatus status; }
    [Serializable] public class DailyClaim { public int day, coins, gems; public string label, itemId; public List<EarnedAchievement> achievements = new List<EarnedAchievement>(); }
    [Serializable] public class Totals { public int coins, gems, pendingBoxes; }
    [Serializable] public class DailyClaimResponse { public DailyClaim claim; public Totals totals; public DailyState daily; }
    [Serializable] public class PurchaseResponse { public string itemId; public int paidCoins, paidGems; public List<EarnedAchievement> achievements = new List<EarnedAchievement>(); public Totals totals; }
    [Serializable] public class LoadoutResponse { public string selectedCat; public Dictionary<string, string> equipped; }
    [Serializable] public class BoxReward { public string type, itemId, label; public int amount; }
    [Serializable] public class BoxResponse { public BoxReward reward; public Totals totals; }
    [Serializable] public class OnlineContent { public List<LevelDef> levels; public List<WorldDef> worlds; public List<ShopItem> shop; public List<AchievementDef> achievements; }
    [Serializable] public class Announcement { public string id, title, message; public bool active; public long startsAt, endsAt; }
}
