using System;
using System.Collections.Generic;
using System.Linq;

namespace TFY.CatAdventure.Domain
{
    /// <summary>
    /// Offline-first save model. `server` is the last profile confirmed by Firebase; `pending` are finished runs that have not
    /// been confirmed yet. What the child sees is the server profile with the pending runs replayed on top (provisional),
    /// so progress continues without a connection. Conflicts are safe by construction:
    ///  • currency, unlocks and progress are only ever decided by the server (pending runs are re-validated there);
    ///  • progress is monotonic (best stars/scores, highest level), so replaying in order can't lose anything;
    ///  • settings use last-writer-wins by timestamp.
    /// </summary>
    [Serializable]
    public class SaveState
    {
        public int saveVersion = 1;
        public string uid;
        public PlayerData server;
        public List<RunReport> pending = new List<RunReport>();
        public PlayerSettings localSettings;
        public long localSettingsAt;
        public int failsSinceWin; // per current level, sent with the next completion (difficulty analytics)
        public long savedAt;

        public const int MaxPending = 50;

        public PlayerData Effective(GameContent content, float coinMultiplier = 1f)
        {
            var p = (server ?? new PlayerData()).Clone().Normalize();
            foreach (var r in pending.OrderBy(r => r.finishedAt))
            {
                var level = content.LevelById(r.levelId);
                if (level == null || level.number > p.currentLevel) continue; // would be rejected by the server too
                p = Progress.Apply(p, level, r, content, coinMultiplier, r.finishedAt).player;
            }
            if (localSettings != null && localSettingsAt > (server?.updatedAt ?? 0)) p.settings = localSettings.Clone();
            return p;
        }

        public void Enqueue(RunReport r)
        {
            if (pending.Any(x => x.runId == r.runId)) return;
            pending.Add(r);
            while (pending.Count > MaxPending) pending.RemoveAt(0);
        }

        /// <summary>After a sync: drop runs the server accepted or permanently rejected; keep the rest for the next try.</summary>
        public void Acknowledge(IEnumerable<string> finishedRunIds) { var set = new HashSet<string>(finishedRunIds); pending.RemoveAll(r => set.Contains(r.runId)); }

        /// <summary>Accepts a newer server profile. Settings changed locally after the server's copy are kept.</summary>
        public void AcceptServer(PlayerData p)
        {
            if (p == null) return;
            server = p.Normalize();
            uid = p.uid;
            if (localSettings != null && localSettingsAt <= p.updatedAt) localSettings = null;
        }
    }

    /// <summary>Error codes the server returns that mean "this run will never be accepted" (drop it instead of retrying forever).</summary>
    public static class SyncErrors
    {
        public static bool IsPermanent(string code) => code == "invalid-argument" || code == "not-found" || code == "failed-precondition" || code == "permission-denied";
    }
}
