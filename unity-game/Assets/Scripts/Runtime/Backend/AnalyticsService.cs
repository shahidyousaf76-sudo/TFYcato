using System.Collections.Generic;
using UnityEngine;
#if TFY_FIREBASE
using Firebase.Analytics;
#endif

namespace TFY.CatAdventure.Backend
{
    /// <summary>
    /// Anonymous gameplay analytics (Firebase Analytics). No personal data: no names, no device identifiers in events,
    /// advertising storage and ad personalisation are denied, and analytics can be switched off from the parents' screen
    /// through Remote Config / consent if a market requires it. Used to find levels that are too hard.
    /// </summary>
    public static class AnalyticsService
    {
        private static bool firebase;

        public static void EnableFirebase()
        {
#if TFY_FIREBASE
            firebase = true;
            FirebaseAnalytics.SetConsent(new Dictionary<ConsentType, ConsentStatus>
            {
                { ConsentType.AnalyticsStorage, ConsentStatus.Granted },
                { ConsentType.AdStorage, ConsentStatus.Denied },
                { ConsentType.AdUserData, ConsentStatus.Denied },
                { ConsentType.AdPersonalization, ConsentStatus.Denied },
            });
            FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);
#endif
        }

        public static void GameStarted(bool newPlayer, bool offline) => Log("game_started", ("new_player", newPlayer ? 1 : 0), ("offline", offline ? 1 : 0));
        public static void LevelStarted(int level, bool fromCheckpoint) => Log("level_started", ("level", level), ("from_checkpoint", fromCheckpoint ? 1 : 0));
        public static void LevelCompleted(int level, int stars, float seconds, int heartsLeft) => Log("level_completed", ("level", level), ("stars", stars), ("seconds", Mathf.RoundToInt(seconds)), ("hearts_left", heartsLeft));
        public static void LevelFailed(int level, int section, string obstacle, float progress) => Log("level_failed", ("level", level), ("section", section), ("obstacle", obstacle ?? "none"), ("progress_pct", Mathf.RoundToInt(progress * 100)));
        public static void CheckpointReached(int level, int index) => Log("checkpoint_reached", ("level", level), ("index", index));
        public static void CoinsCollected(int level, int coins) => Log("coins_collected", ("level", level), ("count", coins)); // one summary per run
        public static void PowerUpUsed(string id) => Log("powerup_used", ("type", id));
        public static void CatUnlocked(string id) => Log("cat_unlocked", ("cat", id));
        public static void ItemUnlocked(string id) => Log("item_unlocked", ("item", id));
        public static void DailyRewardClaimed(int day) => Log("daily_reward_claimed", ("day", day));
        public static void MysteryBoxOpened(string type) => Log("mystery_box_opened", ("reward", type));
        public static void GraphicsTier(string tier, bool auto) => Log("graphics_tier", ("tier", tier), ("auto", auto ? 1 : 0));

        private static void Log(string name, params (string key, object value)[] ps)
        {
            if (Debug.isDebugBuild) Debug.Log($"[Analytics] {name} " + string.Join(", ", System.Array.ConvertAll(ps, p => p.key + "=" + p.value)));
#if TFY_FIREBASE
            if (!firebase) return;
            var list = new List<Parameter>();
            foreach (var (k, v) in ps)
            {
                if (v is int i) list.Add(new Parameter(k, i));
                else if (v is long l) list.Add(new Parameter(k, l));
                else if (v is float f) list.Add(new Parameter(k, (double)f));
                else list.Add(new Parameter(k, v?.ToString() ?? ""));
            }
            FirebaseAnalytics.LogEvent(name, list.ToArray());
#endif
        }
    }

    /// <summary>Crashlytics: uncaught exceptions are reported automatically; handled problems are logged as non-fatal.</summary>
    public static class CrashReporting
    {
        public static void EnableFirebase()
        {
#if TFY_FIREBASE
            Firebase.Crashlytics.Crashlytics.ReportUncaughtExceptionsAsFatal = true;
#endif
        }

        public static void NonFatal(System.Exception e)
        {
            Debug.LogWarning(e);
#if TFY_FIREBASE
            Firebase.Crashlytics.Crashlytics.LogException(e);
#endif
        }

        public static void Breadcrumb(string message)
        {
#if TFY_FIREBASE
            Firebase.Crashlytics.Crashlytics.Log(message);
#endif
        }
    }
}
