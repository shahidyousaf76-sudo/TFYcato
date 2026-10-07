using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TFY.CatAdventure.Data;
using TFY.CatAdventure.Domain;
using UnityEngine;

namespace TFY.CatAdventure.Backend
{
    /// <summary>
    /// Orchestrates identity, cloud save and sync. Offline-first: the local save is always written first, the server is
    /// asked second, and any failure means "keep playing, sync later" with a friendly message — never a blocking error.
    /// </summary>
    public class BackendService
    {
        private readonly IBackend backend;
        private readonly ContentService content;
        private readonly SaveService save;
        private bool syncing;
        public bool Online { get; private set; }
        public bool FirebaseAvailable { get; private set; }
        public List<Announcement> Announcements { get; private set; } = new List<Announcement>();
        public string BackendName => backend.Name;

        public BackendService(IBackend backend, ContentService content, SaveService save) { this.backend = backend; this.content = content; this.save = save; }

        public struct BootResult { public bool isNewPlayer, offline; }

        /// <summary>
        /// CHECK EXISTING PLAYER → (new) anonymous account + automatic username + profile, or (existing) load player data.
        /// The local profile exists before any network call, so the child can start playing almost immediately.
        /// </summary>
        public async Task<BootResult> BootAsync(Action<float> progress)
        {
            save.Load();
            if (save.LastProblem != null) { backend.ReportIssue(save.LastProblem); GameEvents.RaiseToast(Loc.T("error.corrupt")); }

            bool isNew = !save.HasProfile;
            if (isNew) save.CreateLocalProfile(Rules.GenerateUsername(content.Content.usernameWords.prefixes, new System.Random()));
            progress(0.25f);

            FirebaseAvailable = await backend.InitializeAsync();
            if (!FirebaseAvailable) { Online = false; progress(1f); return new BootResult { isNewPlayer = isNew, offline = true }; }

            var signIn = await backend.SignInAnonymouslyAsync();
            progress(0.45f);
            if (!signIn.ok) { Online = false; GameEvents.RaiseToast(Loc.T("error.auth")); return new BootResult { isNewPlayer = isNew, offline = true }; }

            // Idempotent: returns the existing profile, or creates it with our automatic username (server may pick another if taken).
            var created = await backend.CreatePlayerAsync(save.State.server?.username);
            progress(0.6f);
            if (!created.ok) { Online = false; GameEvents.RaiseToast(Loc.T("error.offline")); return new BootResult { isNewPlayer = isNew, offline = true }; }
            Online = true;
            save.AcceptServer(created.data.player);

            var rc = backend.FetchRemoteConfigAsync(content.Runtime);
            var online = backend.FetchContentAsync();
            var ann = backend.FetchAnnouncementsAsync();
            await Task.WhenAll(rc, online, ann);
            if (rc.Result.ok) content.ApplyRuntime(rc.Result.data);
            if (online.Result.ok) content.ApplyOnline(online.Result.data.levels, online.Result.data.worlds, online.Result.data.shop, online.Result.data.achievements);
            if (ann.Result.ok) Announcements = ann.Result.data;
            progress(0.85f);

            await SyncPendingAsync();
            progress(1f);
            return new BootResult { isNewPlayer = isNew || created.data.created, offline = false };
        }

        /// <summary>
        /// Called when a level is finished. The run is saved locally first (never lost), a provisional result is shown
        /// instantly, then the server validates it and its result replaces the provisional one.
        /// </summary>
        public async Task<LevelResult> SubmitRunAsync(RunReport run, LevelDef level)
        {
            var before = save.Player;
            var provisional = Progress.Apply(before, level, run, content.Content, content.Runtime.coin_reward_multiplier, run.finishedAt).result;
            provisional.provisional = true;
            save.AddPendingRun(run);
            if (!Online) return provisional;
            var confirmed = await SyncPendingAsync(run.runId);
            return confirmed ?? provisional;
        }

        /// <summary>Sends queued runs in play order. Returns the server result for `wantRunId` if it was processed.</summary>
        public async Task<LevelResult> SyncPendingAsync(string wantRunId = null)
        {
            if (syncing || !FirebaseAvailable || save.State.pending.Count == 0) return null;
            syncing = true;
            try
            {
                var batch = save.State.pending.OrderBy(r => r.finishedAt).Take(25).ToList();
                var done = new List<string>(); LevelResult wanted = null;
                if (batch.Count == 1)
                {
                    var r = await backend.CompleteLevelAsync(batch[0]);
                    if (r.offline) { Online = false; return null; }
                    done.Add(batch[0].runId);
                    if (r.ok) { wanted = r.data.result; var p = await backend.GetPlayerAsync(); if (p.ok) save.AcceptServer(p.data); }
                    else backend.ReportIssue("RUN_REJECTED");
                }
                else
                {
                    var r = await backend.SyncOfflineRunsAsync(batch);
                    if (r.offline) { Online = false; return null; }
                    if (!r.ok) return null;
                    foreach (var item in r.data.results)
                    {
                        if (item.ok || SyncErrors.IsPermanent(item.code)) done.Add(item.runId);
                        if (!item.ok && SyncErrors.IsPermanent(item.code)) backend.ReportIssue("RUN_REJECTED");
                        if (item.ok && item.runId == wantRunId) wanted = item.result;
                    }
                    if (r.data.player != null) save.AcceptServer(r.data.player);
                }
                Online = true;
                save.Acknowledge(done);
                return wanted;
            }
            catch (Exception e) { CrashReporting.NonFatal(e); return null; }
            finally { syncing = false; }
        }

        /// <summary>Retry when the connection comes back (called periodically by the app).</summary>
        public async Task TryReconnectAsync()
        {
            if (!FirebaseAvailable || Application.internetReachability == NetworkReachability.NotReachable) return;
            if (!Online)
            {
                var s = await backend.SignInAnonymouslyAsync(); if (!s.ok) return;
                var c = await backend.CreatePlayerAsync(save.State.server?.username); if (!c.ok) return;
                save.AcceptServer(c.data.player); Online = true;
            }
            await SyncPendingAsync();
        }

        // ------------------------------------------------------------------ online-only actions (currency is server-side)
        public async Task<Result<PurchaseResponse>> PurchaseAsync(ShopItem item)
        {
            if (!Online) return Result<PurchaseResponse>.Offline();
            var r = await backend.PurchaseAsync(item.id);
            if (r.ok) save.Patch(p =>
            {
                p.coins = r.data.totals.coins; p.gems = r.data.totals.gems;
                if (item.IsCat) p.unlockedCats[item.id] = true; else p.unlockedItems[item.id] = true;
                p.stats.itemsBought++;
                foreach (var a in r.data.achievements) p.achievements[a.id] = true;
            });
            return r;
        }

        public async Task<Result<LoadoutResponse>> EquipAsync(string cat, Dictionary<string, string> equipped)
        {
            // Show it at once; the server double-checks ownership.
            save.Patch(p => { if (cat != null) p.selectedCat = cat; if (equipped != null) foreach (var kv in equipped) p.equipped[kv.Key] = kv.Value; });
            if (!Online) return Result<LoadoutResponse>.Offline();
            return await backend.UpdateLoadoutAsync(cat, equipped);
        }

        public async Task<Result<DailyResponse>> GetDailyAsync() => Online ? await backend.GetDailyAsync(UtcOffset()) : Result<DailyResponse>.Offline();

        public async Task<Result<DailyClaimResponse>> ClaimDailyAsync()
        {
            if (!Online) return Result<DailyClaimResponse>.Offline();
            var r = await backend.ClaimDailyAsync(UtcOffset());
            if (r.ok) save.Patch(p =>
            {
                p.coins = r.data.totals.coins; p.gems = r.data.totals.gems; p.daily = r.data.daily ?? p.daily; p.stats.dailyClaims++;
                if (!string.IsNullOrEmpty(r.data.claim.itemId)) { var it = content.Content.Item(r.data.claim.itemId); if (it != null && it.IsCat) p.unlockedCats[it.id] = true; else p.unlockedItems[r.data.claim.itemId] = true; }
                foreach (var a in r.data.claim.achievements) p.achievements[a.id] = true;
            });
            return r;
        }

        public async Task<Result<BoxResponse>> OpenBoxAsync()
        {
            if (!Online) return Result<BoxResponse>.Offline();
            var r = await backend.OpenMysteryBoxAsync();
            if (r.ok) save.Patch(p =>
            {
                p.coins = r.data.totals.coins; p.gems = r.data.totals.gems; p.pendingBoxes = r.data.totals.pendingBoxes;
                if (r.data.reward.type == "item" && !string.IsNullOrEmpty(r.data.reward.itemId)) { var it = content.Content.Item(r.data.reward.itemId); if (it != null && it.IsCat) p.unlockedCats[it.id] = true; else p.unlockedItems[r.data.reward.itemId] = true; }
            });
            return r;
        }

        public void SaveSettings(PlayerSettings s)
        {
            save.SetSettings(s);
            if (FirebaseAvailable && save.State.server != null && !string.IsNullOrEmpty(save.State.server.uid)) _ = backend.SaveSettingsAsync(s, save.State.localSettingsAt);
        }

        public async Task<bool> DeleteAllDataAsync()
        {
            if (FirebaseAvailable)
            {
                var r = await backend.DeleteMyDataAsync();
                if (!r.ok) return false;
            }
            save.Wipe();
            return true;
        }

        public void ReportIssue(string code) => backend.ReportIssue(code);

        private static int UtcOffset() => (int)TimeZoneInfo.Local.GetUtcOffset(DateTime.Now).TotalMinutes;
    }
}
