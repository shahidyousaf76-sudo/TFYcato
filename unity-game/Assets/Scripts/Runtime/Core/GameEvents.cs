using System;
using UnityEngine;

namespace TFY.CatAdventure
{
    public enum AppState { Boot, Home, Loading, Playing, Paused, Finishing, LevelComplete, Failed }

    public enum CollectKind { Coin, Fish, Gem, Star, MysteryBox, PowerUp }

    /// <summary>Decoupled game-wide events: gameplay raises them, UI / audio / VFX / camera / analytics listen.</summary>
    public static class GameEvents
    {
        public static event Action<AppState> StateChanged;
        public static event Action<CollectKind, Vector3, string> Collected;   // kind, world position, id (power-up id)
        public static event Action<int, int, int, int> RunCountersChanged;    // coins(value), fish, gems, big stars
        public static event Action<int, int> HeartsChanged;                   // hearts, max
        public static event Action<float> ProgressChanged;                    // 0..1
        public static event Action<string, float> PowerUpStarted;             // id, duration (0 = until used)
        public static event Action<string> PowerUpEnded;
        public static event Action<Vector3, bool> Bumped;                     // position, absorbed by shield / boost
        public static event Action Jumped, DoubleJumped, LongJumped, Landed, Slid, LaneChanged;
        public static event Action<Vector3> ObstacleWarning;                  // falling rocks shaking etc.
        public static event Action<int> CheckpointReached;
        public static event Action RunFinished, RunFailed;
        public static event Action<string> Toast;                             // friendly message
        public static event Action<string> LandmarkSpotted;                   // e.g. "Eiffel Tower — Paris"

        public static void RaiseState(AppState s) => StateChanged?.Invoke(s);
        public static void RaiseCollected(CollectKind k, Vector3 p, string id = null) => Collected?.Invoke(k, p, id);
        public static void RaiseCounters(int coins, int fish, int gems, int stars) => RunCountersChanged?.Invoke(coins, fish, gems, stars);
        public static void RaiseHearts(int h, int max) => HeartsChanged?.Invoke(h, max);
        public static void RaiseProgress(float p) => ProgressChanged?.Invoke(p);
        public static void RaisePowerUpStarted(string id, float d) => PowerUpStarted?.Invoke(id, d);
        public static void RaisePowerUpEnded(string id) => PowerUpEnded?.Invoke(id);
        public static void RaiseBumped(Vector3 p, bool absorbed) => Bumped?.Invoke(p, absorbed);
        public static void RaiseJumped() => Jumped?.Invoke();
        public static void RaiseDoubleJumped() => DoubleJumped?.Invoke();
        public static void RaiseLongJumped() => LongJumped?.Invoke();
        public static void RaiseLanded() => Landed?.Invoke();
        public static void RaiseSlid() => Slid?.Invoke();
        public static void RaiseLaneChanged() => LaneChanged?.Invoke();
        public static void RaiseObstacleWarning(Vector3 p) => ObstacleWarning?.Invoke(p);
        public static void RaiseCheckpoint(int i) => CheckpointReached?.Invoke(i);
        public static void RaiseRunFinished() => RunFinished?.Invoke();
        public static void RaiseRunFailed() => RunFailed?.Invoke();
        public static void RaiseToast(string msg) => Toast?.Invoke(msg);
        public static void RaiseLandmark(string name) => LandmarkSpotted?.Invoke(name);
    }
}
