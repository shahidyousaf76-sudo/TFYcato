using System;
using TFY.CatAdventure.Domain;
using UnityEngine;

namespace TFY.CatAdventure.Data
{
    /// <summary>
    /// Local cache of the player (offline-first). Holds the last server-confirmed profile plus pending runs;
    /// exposes the effective profile (server + provisional offline progress). Corrupted saves fall back to the backup,
    /// then to a fresh profile, and the problem is reported (code only, no personal data).
    /// </summary>
    public class SaveService
    {
        private const string File = "player_save.json";
        private readonly ContentService content;
        public SaveState State { get; private set; } = new SaveState();
        public PlayerData Player { get; private set; } = new PlayerData().Normalize();
        public event Action Changed;
        public string LastProblem { get; private set; }

        public SaveService(ContentService content) { this.content = content; }

        public bool HasProfile => State.server != null && !string.IsNullOrEmpty(State.server.username);

        public void Load()
        {
            var text = LocalFile.Read(File, out var status);
            if (status == LocalFile.ReadStatus.RecoveredFromBackup) LastProblem = "SAVE_RECOVERED_FROM_BACKUP";
            if (status == LocalFile.ReadStatus.Corrupt) LastProblem = "SAVE_CORRUPTED";
            if (text != null)
            {
                try { State = Json.Read<SaveState>(text) ?? new SaveState(); }
                catch (Exception e) { Debug.LogWarning("[Save] unreadable save: " + e.Message); State = new SaveState(); LastProblem = "SAVE_CORRUPTED"; }
            }
            State.pending = State.pending ?? new System.Collections.Generic.List<RunReport>();
            Recompute();
        }

        public void Save()
        {
            try { State.savedAt = Now(); LocalFile.Write(File, Json.Write(State)); }
            catch (Exception e) { Debug.LogWarning("[Save] write failed: " + e.Message); LastProblem = "SAVE_FAILED"; GameEvents.RaiseToast(Loc.T("error.save")); }
        }

        /// <summary>A fresh local profile (first launch while offline). The server creates the real one when it can.</summary>
        public void CreateLocalProfile(string username)
        {
            State.server = new PlayerData { uid = "", username = username, createdAt = Now(), updatedAt = 0 }.Normalize();
            Save(); Recompute();
        }

        public void AcceptServer(PlayerData p) { State.AcceptServer(p); Save(); Recompute(); }

        public void AddPendingRun(RunReport r) { State.Enqueue(r); State.failsSinceWin = 0; Save(); Recompute(); }

        public void Acknowledge(System.Collections.Generic.IEnumerable<string> runIds) { State.Acknowledge(runIds); Save(); Recompute(); }

        public void RecordFail() { State.failsSinceWin++; Save(); }

        public void SetSettings(PlayerSettings s) { State.localSettings = s.Clone(); State.localSettingsAt = Now(); Save(); Recompute(); }

        /// <summary>Applies a server-side change that returned only totals (shop, daily reward, mystery box) to the cached profile.</summary>
        public void Patch(Action<PlayerData> change)
        {
            if (State.server == null) return;
            change(State.server);
            Save(); Recompute();
        }

        public void Wipe() { LocalFile.Delete(File); State = new SaveState(); Recompute(); }

        public void Recompute()
        {
            Player = State.Effective(content.Content, content.Runtime.coin_reward_multiplier);
            if (UnlockAllForTesting) Player.currentLevel = Math.Max(Player.currentLevel, content.Content.LastEnabledLevel);
            Changed?.Invoke();
        }

        /// <summary>
        /// Testing only (editor and development builds): every enabled level is playable. Toggle with TFY ▸ Testing ▸ Unlock All Levels.
        /// Release builds ignore it, and the server still validates real progression for synced runs.
        /// </summary>
        public const string UnlockAllKey = "tfy.test.unlockAll";
        public static bool UnlockAllForTesting => Debug.isDebugBuild && PlayerPrefs.GetInt(UnlockAllKey, 0) == 1;

        public static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}
