using System.Collections.Generic;
using System.Threading.Tasks;
using TFY.CatAdventure.Domain;

namespace TFY.CatAdventure.Backend
{
    /// <summary>
    /// Everything the game asks of the backend. Gameplay never waits on these: running and jumping are local,
    /// and every call can fail with `offline` without stopping play.
    /// </summary>
    public interface IBackend
    {
        string Name { get; }
        Task<bool> InitializeAsync();
        Task<Result<string>> SignInAnonymouslyAsync();
        Task<Result<CreatePlayerResponse>> CreatePlayerAsync(string proposedUsername);
        Task<Result<PlayerData>> GetPlayerAsync();
        Task<Result<CompleteLevelResponse>> CompleteLevelAsync(RunReport run);
        Task<Result<SyncResponse>> SyncOfflineRunsAsync(List<RunReport> runs);
        Task<Result<DailyResponse>> GetDailyAsync(int utcOffsetMinutes);
        Task<Result<DailyClaimResponse>> ClaimDailyAsync(int utcOffsetMinutes);
        Task<Result<PurchaseResponse>> PurchaseAsync(string itemId);
        Task<Result<LoadoutResponse>> UpdateLoadoutAsync(string selectedCat, Dictionary<string, string> equipped);
        Task<Result<BoxResponse>> OpenMysteryBoxAsync();
        Task<Result<bool>> SaveSettingsAsync(PlayerSettings settings, long at);
        Task<Result<bool>> DeleteMyDataAsync();
        Task<Result<RuntimeConfig>> FetchRemoteConfigAsync(RuntimeConfig defaults);
        Task<Result<OnlineContent>> FetchContentAsync();
        Task<Result<List<Announcement>>> FetchAnnouncementsAsync();
        void ReportIssue(string code);
    }

    /// <summary>Used when the Firebase SDK is not installed (or not configured): the whole game runs locally.</summary>
    public class OfflineBackend : IBackend
    {
        public string Name => "offline";
        public Task<bool> InitializeAsync() => Task.FromResult(false);
        private static Task<Result<T>> Off<T>() => Task.FromResult(Result<T>.Offline("Firebase SDK not installed"));
        public Task<Result<string>> SignInAnonymouslyAsync() => Off<string>();
        public Task<Result<CreatePlayerResponse>> CreatePlayerAsync(string u) => Off<CreatePlayerResponse>();
        public Task<Result<PlayerData>> GetPlayerAsync() => Off<PlayerData>();
        public Task<Result<CompleteLevelResponse>> CompleteLevelAsync(RunReport r) => Off<CompleteLevelResponse>();
        public Task<Result<SyncResponse>> SyncOfflineRunsAsync(List<RunReport> r) => Off<SyncResponse>();
        public Task<Result<DailyResponse>> GetDailyAsync(int o) => Off<DailyResponse>();
        public Task<Result<DailyClaimResponse>> ClaimDailyAsync(int o) => Off<DailyClaimResponse>();
        public Task<Result<PurchaseResponse>> PurchaseAsync(string i) => Off<PurchaseResponse>();
        public Task<Result<LoadoutResponse>> UpdateLoadoutAsync(string c, Dictionary<string, string> e) => Off<LoadoutResponse>();
        public Task<Result<BoxResponse>> OpenMysteryBoxAsync() => Off<BoxResponse>();
        public Task<Result<bool>> SaveSettingsAsync(PlayerSettings s, long at) => Off<bool>();
        public Task<Result<bool>> DeleteMyDataAsync() => Off<bool>();
        public Task<Result<RuntimeConfig>> FetchRemoteConfigAsync(RuntimeConfig d) => Off<RuntimeConfig>();
        public Task<Result<OnlineContent>> FetchContentAsync() => Off<OnlineContent>();
        public Task<Result<List<Announcement>>> FetchAnnouncementsAsync() => Off<List<Announcement>>();
        public void ReportIssue(string code) { }
    }
}
