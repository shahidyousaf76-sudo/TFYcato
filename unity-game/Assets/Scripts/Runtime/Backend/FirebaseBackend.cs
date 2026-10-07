#if TFY_FIREBASE
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using Firebase.Extensions;
using Firebase.Firestore;
using Firebase.Functions;
using Firebase.RemoteConfig;
using TFY.CatAdventure.Data;
using TFY.CatAdventure.Domain;
using UnityEngine;

namespace TFY.CatAdventure.Backend
{
    /// <summary>
    /// Firebase implementation: anonymous Auth, Firestore (read own profile, write settings), Cloud Functions for every
    /// trusted operation, Remote Config. Compiled only when the TFY_FIREBASE scripting define is set
    /// (TFY ▸ Firebase ▸ Enable after importing the Firebase Unity SDK), so the project always compiles without it.
    /// </summary>
    public class FirebaseBackend : IBackend
    {
        public const string Region = "europe-west1";
        public string Name => "firebase";
        private FirebaseAuth auth;
        private FirebaseFirestore db;
        private FirebaseFunctions functions;
        private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(12);

        public async Task<bool> InitializeAsync()
        {
            try
            {
                var status = await FirebaseApp.CheckAndFixDependenciesAsync();
                if (status != DependencyStatus.Available) { Debug.LogWarning("[Firebase] dependencies: " + status); return false; }
                auth = FirebaseAuth.DefaultInstance;
                db = FirebaseFirestore.DefaultInstance; // offline persistence is on by default on Android/iOS
                functions = FirebaseFunctions.GetInstance(FirebaseApp.DefaultInstance, Region);
                AnalyticsService.EnableFirebase();
                CrashReporting.EnableFirebase();
                return true;
            }
            catch (Exception e) { Debug.LogWarning("[Firebase] init failed: " + e.Message); return false; }
        }

        public async Task<Result<string>> SignInAnonymouslyAsync()
        {
            try
            {
                if (auth.CurrentUser != null) return Result<string>.Ok(auth.CurrentUser.UserId); // restored from the device keystore
                var r = await WithTimeout(auth.SignInAnonymouslyAsync());
                return Result<string>.Ok(r.User.UserId);
            }
            catch (Exception e) { return Offline<string>(e); }
        }

        public Task<Result<CreatePlayerResponse>> CreatePlayerAsync(string proposed) => Call<CreatePlayerResponse>("createPlayer", new Dictionary<string, object> { ["username"] = proposed });

        public async Task<Result<PlayerData>> GetPlayerAsync()
        {
            try
            {
                var snap = await WithTimeout(db.Collection("players").Document(auth.CurrentUser.UserId).GetSnapshotAsync(Source.Server));
                return snap.Exists ? Result<PlayerData>.Ok(Json.Convert<PlayerData>(snap.ToDictionary()).Normalize()) : Result<PlayerData>.Fail("not-found", "No profile yet.");
            }
            catch (Exception e) { return Offline<PlayerData>(e); }
        }

        public Task<Result<CompleteLevelResponse>> CompleteLevelAsync(RunReport r) => Call<CompleteLevelResponse>("completeLevel", RunToMap(r));

        public Task<Result<SyncResponse>> SyncOfflineRunsAsync(List<RunReport> runs) =>
            Call<SyncResponse>("syncOfflineRuns", new Dictionary<string, object> { ["runs"] = runs.Select(r => (object)RunToMap(r)).ToList() });

        public Task<Result<DailyResponse>> GetDailyAsync(int off) => Call<DailyResponse>("getDailyReward", new Dictionary<string, object> { ["utcOffsetMinutes"] = off });
        public Task<Result<DailyClaimResponse>> ClaimDailyAsync(int off) => Call<DailyClaimResponse>("claimDailyReward", new Dictionary<string, object> { ["utcOffsetMinutes"] = off });
        public Task<Result<PurchaseResponse>> PurchaseAsync(string id) => Call<PurchaseResponse>("purchaseItem", new Dictionary<string, object> { ["itemId"] = id });
        public Task<Result<BoxResponse>> OpenMysteryBoxAsync() => Call<BoxResponse>("openMysteryBox", null);

        public Task<Result<LoadoutResponse>> UpdateLoadoutAsync(string cat, Dictionary<string, string> equipped)
        {
            var data = new Dictionary<string, object>();
            if (cat != null) data["selectedCat"] = cat;
            if (equipped != null) data["equipped"] = equipped.ToDictionary(kv => kv.Key, kv => (object)kv.Value);
            return Call<LoadoutResponse>("updateLoadout", data);
        }

        public async Task<Result<bool>> SaveSettingsAsync(PlayerSettings s, long at)
        {
            try
            {
                // Firestore queues this write offline and sends it later; the rules only allow settings + updatedAt.
                var write = db.Collection("players").Document(auth.CurrentUser.UserId).UpdateAsync(new Dictionary<string, object>
                {
                    ["settings"] = Json.ToDictionary(s), ["updatedAt"] = at,
                });
                await WithTimeout(write);
                return Result<bool>.Ok(true);
            }
            catch (Exception e) { return Offline<bool>(e); }
        }

        public async Task<Result<bool>> DeleteMyDataAsync()
        {
            var r = await Call<Dictionary<string, object>>("deleteMyData", null);
            if (!r.ok) return new Result<bool> { ok = false, offline = r.offline, code = r.code, message = r.message };
            try { auth.SignOut(); } catch { /* already deleted */ }
            return Result<bool>.Ok(true);
        }

        public async Task<Result<RuntimeConfig>> FetchRemoteConfigAsync(RuntimeConfig defaults)
        {
            try
            {
                var rc = FirebaseRemoteConfig.DefaultInstance;
                var d = Json.ToDictionary(defaults);
                var flat = d.ToDictionary(kv => kv.Key, kv => kv.Value is Dictionary<string, object> || kv.Value is List<object> ? (object)Json.Write(kv.Value) : kv.Value);
                await rc.SetDefaultsAsync(flat);
                await WithTimeout(rc.FetchAndActivateAsync());
                var map = new Dictionary<string, object>();
                foreach (var key in flat.Keys)
                {
                    var v = rc.GetValue(key);
                    var def = flat[key];
                    if (def is bool) map[key] = v.BooleanValue;
                    else if (def is long || def is double || def is int || def is float) map[key] = v.DoubleValue;
                    else if (key == "powerup_durations" || key == "daily_rewards") map[key] = Newtonsoft.Json.JsonConvert.DeserializeObject(v.StringValue);
                    else map[key] = v.StringValue;
                }
                return Result<RuntimeConfig>.Ok(Json.Convert<RuntimeConfig>(map).Sanitized());
            }
            catch (Exception e) { return Offline<RuntimeConfig>(e); }
        }

        public async Task<Result<OnlineContent>> FetchContentAsync()
        {
            try
            {
                async Task<List<T>> All<T>(string col)
                {
                    var q = await WithTimeout(db.Collection(col).GetSnapshotAsync());
                    var list = new List<T>();
                    foreach (var d in q.Documents) { try { list.Add(Json.Convert<T>(d.ToDictionary())); } catch (Exception e) { Debug.LogWarning($"[Content] skip {col}/{d.Id}: {e.Message}"); } }
                    return list;
                }
                return Result<OnlineContent>.Ok(new OnlineContent
                {
                    levels = await All<LevelDef>("levels"), worlds = await All<WorldDef>("worlds"),
                    shop = await All<ShopItem>("catalog"), achievements = await All<AchievementDef>("achievements"),
                });
            }
            catch (Exception e) { return Offline<OnlineContent>(e); }
        }

        public async Task<Result<List<Announcement>>> FetchAnnouncementsAsync()
        {
            try
            {
                var q = await WithTimeout(db.Collection("announcements").WhereEqualTo("active", true).GetSnapshotAsync());
                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var list = q.Documents.Select(d => { var a = Json.Convert<Announcement>(d.ToDictionary()); a.id = d.Id; return a; })
                    .Where(a => a.startsAt <= now && a.endsAt > now).OrderByDescending(a => a.startsAt).ToList();
                return Result<List<Announcement>>.Ok(list);
            }
            catch (Exception e) { return Offline<List<Announcement>>(e); }
        }

        public void ReportIssue(string code)
        {
            _ = Call<Dictionary<string, object>>("reportClientIssue", new Dictionary<string, object>
            {
                ["code"] = code, ["appVersion"] = Application.version, ["platform"] = Application.platform.ToString(),
            });
        }

        // ------------------------------------------------------------------ helpers
        private async Task<Result<T>> Call<T>(string name, Dictionary<string, object> data)
        {
            try
            {
                var res = await WithTimeout(functions.GetHttpsCallable(name).CallAsync(data));
                return Result<T>.Ok(Json.Convert<T>(res.Data));
            }
            catch (Exception e) { return FromException<T>(e); }
        }

        private static Dictionary<string, object> RunToMap(RunReport r) => new Dictionary<string, object>
        {
            ["runId"] = r.runId, ["levelId"] = r.levelId, ["coins"] = r.coins, ["coinValue"] = r.coinValue, ["fish"] = r.fish, ["gems"] = r.gems,
            ["starTokens"] = r.starTokens, ["mysteryBoxes"] = r.mysteryBoxes, ["heartsLeft"] = r.heartsLeft, ["durationSec"] = (double)r.durationSec,
            ["jumps"] = r.jumps, ["checkpointsUsed"] = r.checkpointsUsed, ["failedAttempts"] = r.failedAttempts, ["offline"] = r.offline,
        };

        private static Result<T> FromException<T>(Exception e)
        {
            var fe = Unwrap<FunctionsException>(e);
            if (fe != null)
            {
                switch (fe.ErrorCode)
                {
                    case FunctionsErrorCode.InvalidArgument: return Result<T>.Fail("invalid-argument", fe.Message);
                    case FunctionsErrorCode.NotFound: return Result<T>.Fail("not-found", fe.Message);
                    case FunctionsErrorCode.FailedPrecondition: return Result<T>.Fail("failed-precondition", fe.Message);
                    case FunctionsErrorCode.PermissionDenied: return Result<T>.Fail("permission-denied", fe.Message);
                    case FunctionsErrorCode.AlreadyExists: return Result<T>.Fail("already-exists", fe.Message);
                    case FunctionsErrorCode.ResourceExhausted: return Result<T>.Fail("resource-exhausted", fe.Message);
                    case FunctionsErrorCode.Unauthenticated: return Result<T>.Fail("unauthenticated", fe.Message);
                }
            }
            return Offline<T>(e);
        }

        private static Result<T> Offline<T>(Exception e) { Debug.Log("[Firebase] offline/unavailable: " + e.Message); return Result<T>.Offline(e.Message); }

        private static TE Unwrap<TE>(Exception e) where TE : Exception
        {
            for (var x = e; x != null; x = x.InnerException)
            {
                if (x is TE t) return t;
                if (x is AggregateException ag) foreach (var inner in ag.InnerExceptions) { var r = Unwrap<TE>(inner); if (r != null) return r; }
            }
            return null;
        }

        private static async Task<T> WithTimeout<T>(Task<T> task)
        {
            if (await Task.WhenAny(task, Task.Delay(CallTimeout)) != task) throw new TimeoutException("Request timed out");
            return await task;
        }

        private static async Task WithTimeout(Task task)
        {
            if (await Task.WhenAny(task, Task.Delay(CallTimeout)) != task) throw new TimeoutException("Request timed out");
            await task;
        }
    }
}
#endif
