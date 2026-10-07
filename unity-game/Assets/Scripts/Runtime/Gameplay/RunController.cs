using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TFY.CatAdventure.Audio;
using TFY.CatAdventure.Backend;
using TFY.CatAdventure.Domain;
using TFY.CatAdventure.Graphics;
using UnityEngine;

namespace TFY.CatAdventure.Gameplay
{
    /// <summary>
    /// Owns one level attempt: forward motion with smooth acceleration, collisions, collectibles, hearts, power-ups,
    /// checkpoints (restart from the last flag, not the whole level), finish and failure. Talks to the backend only after the
    /// finish line — never during play — so running and jumping never depend on the network.
    /// </summary>
    public class RunController : MonoBehaviour
    {
        public CatController Cat;
        public CameraRig Camera;
        public TrackBuilder Track;
        public PowerUps Power = new PowerUps();
        public TutorialDirector Tutorial = new TutorialDirector();
        public event Action<LevelResult, RunStats> Finished;
        public event Action Failed;

        public LevelDef Level { get; private set; }
        public LevelPlan Plan { get; private set; }
        public float Distance { get; private set; }
        public bool Playing { get; private set; }
        public bool HasCheckpoint => snapshot != null;

        private App app => App.I;
        private int hearts, maxHearts, coins, coinValue, fish, gems, starTokens, boxes, checkpointsUsed, nextCheckpoint, sectionIndex;
        private float time, startDelay;
        private string runId, lastObstacle;
        private readonly HashSet<int> collected = new HashSet<int>();
        private readonly Dictionary<string, LevelPlan> planCache = new Dictionary<string, LevelPlan>();
        private Snapshot snapshot;

        private class Snapshot { public float distance, time; public int coins, coinValue, fish, gems, starTokens, boxes, jumps, checkpointIndex; public HashSet<int> collected; }
        public struct RunStats { public int coins, fish, gems, starTokens; }

        // Start, not Awake: Awake runs inside AddComponent, before App has assigned Cat/Camera/Track.
        private void Start()
        {
            Cat.FellIntoGap += OnFellIntoGap;
            GameEvents.Landed += OnLanded; GameEvents.Jumped += OnJumped; GameEvents.DoubleJumped += OnDoubleJumped; GameEvents.LongJumped += OnLongJumped; GameEvents.Slid += OnSlid;
            GameEvents.ObstacleWarning += OnWarning;
        }

        private void OnDestroy()
        {
            if (Cat) Cat.FellIntoGap -= OnFellIntoGap;
            GameEvents.Landed -= OnLanded; GameEvents.Jumped -= OnJumped; GameEvents.DoubleJumped -= OnDoubleJumped; GameEvents.LongJumped -= OnLongJumped; GameEvents.Slid -= OnSlid;
            GameEvents.ObstacleWarning -= OnWarning;
        }

        public LevelPlan PlanFor(LevelDef level)
        {
            string key = level.id + "@" + level.seed + "@" + app.Content.Runtime.run_speed_scale + "@" + app.Content.Runtime.difficulty_density_scale;
            if (!planCache.TryGetValue(key, out var plan)) planCache[key] = plan = LevelPlanner.Plan(level, app.Content.Content, app.Content.Runtime);
            return plan;
        }

        /// <summary>Starts a level from the beginning or from the last checkpoint.</summary>
        public void Begin(LevelDef level, bool fromCheckpoint)
        {
            bool sameLevel = Level != null && Level.id == level.id;
            Level = level; Plan = PlanFor(level);
            var cp = fromCheckpoint && sameLevel ? snapshot : null;
            if (!fromCheckpoint || !sameLevel) { snapshot = null; runId = Guid.NewGuid().ToString("N"); checkpointsUsed = 0; }
            if (cp != null) checkpointsUsed++;

            Distance = cp?.distance ?? 0; time = cp?.time ?? 0;
            coins = cp?.coins ?? 0; coinValue = cp?.coinValue ?? 0; fish = cp?.fish ?? 0; gems = cp?.gems ?? 0; starTokens = cp?.starTokens ?? 0; boxes = cp?.boxes ?? 0;
            nextCheckpoint = cp?.checkpointIndex ?? 0;
            collected.Clear(); if (cp != null) foreach (var i in cp.collected) collected.Add(i);
            maxHearts = app.Content.Runtime.starting_hearts; hearts = maxHearts; // full hearts again after a checkpoint: kind, not punishing
            lastObstacle = null; sectionIndex = 0;

            Power.Clear(); Power.Attach(Cat.transform, app.Content.Runtime);
            Track.Begin(Plan, Distance, collected);
            Cat.ResetTo(0, 1);
            Cat.transform.localPosition = new Vector3(0, 0, -Distance);
            Tutorial.Begin(level, app.Save.Player.IsCompleted(level.number));
            Camera.SetMode(CameraMode.Follow); Camera.SnapBehind();
            startDelay = 0.6f;
            Playing = true;
            Cat.StopRun(CatAnim.Idle);

            GameEvents.RaiseHearts(hearts, maxHearts); PushCounters(); GameEvents.RaiseProgress(Distance / Plan.length);
            AnalyticsService.LevelStarted(level.number, cp != null);
        }

        public void Input(GameInput i)
        {
            if (!Playing || startDelay > 0) return;
            if (Tutorial.Intercept(i, Distance, Cat)) return;
            Perform(i);
        }

        private void Perform(GameInput i)
        {
            switch (i)
            {
                case GameInput.Tap: Cat.Tap(); break;
                case GameInput.SwipeUp: Cat.SwipeUp(); break;
                case GameInput.SwipeDown: Cat.SwipeDown(); break;
                case GameInput.SwipeLeft: Cat.SwipeLeft(); break;
                case GameInput.SwipeRight: Cat.SwipeRight(); break;
            }
        }

        private void Update()
        {
            if (!Playing) return;
            float dt = Time.deltaTime;
            if (startDelay > 0) { startDelay -= dt; if (startDelay <= 0) { Cat.BeginRun(); app.Audio.Meow("meow_happy"); } return; }

            float speed = Rules.SpeedAt(Level, Distance, app.Content.Runtime.run_speed_scale) * Power.SpeedMultiplier;
            float move = speed * dt * Tutorial.TimeScale;
            if (!Cat.InGap) Distance += move;
            time += dt;
            Cat.ForwardSpeed = speed;
            Cat.JumpMultiplier = Power.JumpMultiplier;
            Cat.Boosting = Power.Active("speed_boost");
            Cat.LongJumpZone = Track.InLongJumpZone(Distance);
            Cat.SetGround(Track.HasGround(Distance));
            var p = Cat.transform.localPosition; p.z = -Distance; Cat.transform.localPosition = p;
            Camera.SpeedBoost = Mathf.MoveTowards(Camera.SpeedBoost, Cat.Boosting ? 1 : 0, dt * 2);

            Track.Tick(Distance);
            app.Environment.Tick(Distance);
            app.Sky.Tick(Distance, app.Quality.Current.nightLights);
            Tutorial.Tick(Distance, Plan, Cat, Perform);
            Power.Tick(dt);
            Collide(dt);
            Collect(dt);

            while (sectionIndex + 1 < Plan.sectionStarts.Count && Distance >= Plan.sectionStarts[sectionIndex + 1]) sectionIndex++;
            if (nextCheckpoint < Plan.checkpoints.Count && Distance >= Plan.checkpoints[nextCheckpoint]) ReachCheckpoint();
            GameEvents.RaiseProgress(Mathf.Clamp01(Distance / Plan.length));
            if (Distance >= Plan.finishZ) Finish();
        }

        // ------------------------------------------------------------------ collisions
        private void Collide(float dt)
        {
            foreach (var o in Track.LiveObstacles)
            {
                o.Tick(dt, Distance);
                if (!o.Active || o.def.clearance == "gap") continue;
                if (Mathf.Abs(o.CenterDistance - Distance) > o.HalfDepth + 0.35f) continue;
                if (o.def.lanes < 3 && !(o is MovingCritterView) && Mathf.Abs(o.CurrentX - Cat.X) > 0.95f) continue;
                if (!o.Hits(Cat)) continue;
                if (Cat.Invulnerable && !Power.Invincible) continue; // blinking after a bump: pass through, keep the shield

                lastObstacle = o.def.id;
                bool shield = Power.Shield;
                bool costsHeart = Cat.TakeHit(shield, Power.Invincible);
                o.KnockAway();
                Vfx.Burst("bump", o.transform.position + Vector3.up);
                GameEvents.RaiseBumped(o.transform.position, !costsHeart);
                if (shield && !costsHeart && !Power.Invincible) { Power.UseShield(); app.Audio.Sfx("sfx_shield"); app.UI.Toast(Loc.T("shield.used")); continue; }
                if (!costsHeart) continue;
                LoseHeart();
                app.Audio.Sfx("sfx_bump"); app.Audio.Meow("meow_surprised", true);
                if (app.Save.Player.settings.vibration) Haptics.Tap(60);
                Camera.OnBump();
                Tutorial.NotifyHit();
                if (!Playing) return;
            }
        }

        private void OnFellIntoGap()
        {
            if (!Playing) return;
            lastObstacle = "broken_bridge";
            app.Audio.Sfx("sfx_fall");
            LoseHeart();
            if (!Playing) return;
            Distance = Track.GapEndAfter(Distance) + 1f;
            var p = Cat.transform.localPosition; p.z = -Distance; Cat.transform.localPosition = p;
            Cat.ClimbOut();
        }

        private void LoseHeart()
        {
            hearts = Mathf.Max(0, hearts - 1);
            GameEvents.RaiseHearts(hearts, maxHearts);
            if (hearts == 0) Fail();
        }

        // ------------------------------------------------------------------ collectibles
        private void Collect(float dt)
        {
            var catLocal = Cat.transform.localPosition;
            for (int i = Track.LiveCollectibles.Count - 1; i >= 0; i--)
            {
                var c = Track.LiveCollectibles[i];
                c.Tick(dt, catLocal, Power.MagnetRadius);
                if (!c.InReach(catLocal, Cat.Sliding)) continue;
                Track.LiveCollectibles.RemoveAt(i);
                int index = TrackBuilder.IndexOf(c); if (index >= 0) collected.Add(index);
                var pos = c.transform.position;
                switch (c.kind)
                {
                    case PlacementKind.Coin: coins++; coinValue += Power.CoinValue; app.Audio.Coin(); Vfx.Burst("coin", pos); GameEvents.RaiseCollected(CollectKind.Coin, pos); break;
                    case PlacementKind.Fish: fish++; app.Audio.Sfx("sfx_fish"); app.Audio.Meow("meow_happy"); Vfx.Burst("fish", pos); GameEvents.RaiseCollected(CollectKind.Fish, pos); break;
                    case PlacementKind.Gem: gems++; app.Audio.Sfx("sfx_gem"); Vfx.Burst("gem", pos); GameEvents.RaiseCollected(CollectKind.Gem, pos); break;
                    case PlacementKind.StarToken: starTokens++; app.Audio.Sfx("sfx_star"); Vfx.Burst("star", pos); GameEvents.RaiseCollected(CollectKind.Star, pos); break;
                    case PlacementKind.MysteryBox: boxes++; app.Audio.Sfx("sfx_box"); app.Audio.Meow("meow_excited", true); Vfx.Burst("star", pos); GameEvents.RaiseCollected(CollectKind.MysteryBox, pos); break;
                    case PlacementKind.PowerUp: Power.Activate(c.id); app.Audio.Sfx("sfx_powerup"); Vfx.Burst("powerup", pos); app.Post.Pulse(0.4f); GameEvents.RaiseCollected(CollectKind.PowerUp, pos, c.id); break;
                }
                if (app.Save.Player.settings.vibration && c.kind != PlacementKind.Coin) Haptics.Tap(20);
                c.Release();
                PushCounters();
            }
            app.Post.Relax(dt, app.Quality.Current.bloom);
        }

        private void PushCounters() => GameEvents.RaiseCounters(coinValue, fish, gems, starTokens);
        public float PowerUpRemaining(string id) => Power.Remaining(id);

        // ------------------------------------------------------------------ checkpoints, finish, fail
        private void ReachCheckpoint()
        {
            snapshot = new Snapshot
            {
                distance = Plan.checkpoints[nextCheckpoint] + 0.5f, time = time, coins = coins, coinValue = coinValue, fish = fish, gems = gems,
                starTokens = starTokens, boxes = boxes, jumps = Cat.Jumps, checkpointIndex = nextCheckpoint + 1, collected = new HashSet<int>(collected),
            };
            nextCheckpoint++;
            app.Audio.Sfx("sfx_checkpoint");
            Vfx.Burst("checkpoint", Cat.transform.position + Vector3.up * 2);
            GameEvents.RaiseCheckpoint(nextCheckpoint);
            AnalyticsService.CheckpointReached(Level.number, nextCheckpoint);
        }

        private async void Finish()
        {
            Playing = false;
            Power.Clear();
            Cat.StopRun(CatAnim.Victory);
            Camera.SetMode(CameraMode.Celebrate);
            app.Audio.Sfx("sfx_level_complete"); app.Audio.Meow("meow_victory", true);
            Vfx.Burst("confetti", Cat.transform.position + Vector3.up * 3);
            GameEvents.RaiseRunFinished();
            AnalyticsService.LevelCompleted(Level.number, Rules.Stars(Level, coins, starTokens), time, hearts);
            AnalyticsService.CoinsCollected(Level.number, coins);

            var report = new RunReport
            {
                runId = runId, levelId = Level.id, levelNumber = Level.number, coins = coins, coinValue = coinValue, fish = fish, gems = gems,
                starTokens = starTokens, mysteryBoxes = boxes, heartsLeft = Mathf.Max(1, hearts), durationSec = time, // real time: the server checks it is humanly possible
                jumps = Cat.Jumps + (snapshot?.jumps ?? 0), checkpointsUsed = checkpointsUsed, failedAttempts = app.Save.State.failsSinceWin,
                offline = !app.Backend.Online, finishedAt = SaveServiceNow(),
            };
            snapshot = null;
            var submit = app.Backend.SubmitRunAsync(report, Level);
            await Task.Delay(1600); // let the celebration play
            var result = await submit;
            Cat.StopRun(CatAnim.Dancing);
            Finished?.Invoke(result, new RunStats { coins = coinValue, fish = fish, gems = gems, starTokens = starTokens });
        }

        private static long SaveServiceNow() => Data.SaveService.Now();

        private void Fail()
        {
            Playing = false;
            Power.Clear();
            Cat.StopRun(CatAnim.Sleeping);
            app.Audio.Sfx("sfx_fail");
            app.Save.RecordFail();
            AnalyticsService.LevelFailed(Level.number, sectionIndex, lastObstacle, Distance / Plan.length);
            GameEvents.RaiseRunFailed();
            Failed?.Invoke();
        }

        public void Stop()
        {
            Playing = false; Power.Clear(); Track.Clear();
        }

        // ------------------------------------------------------------------ feedback
        private void OnJumped() { if (!Playing) return; app.Audio.Sfx("sfx_jump", 0.8f); if (UnityEngine.Random.value < 0.15f) app.Audio.Meow("meow_jump"); }
        private void OnDoubleJumped() { if (!Playing) return; app.Audio.Sfx("sfx_double_jump", 0.8f); Vfx.Burst("doublejump", Cat.transform.position + Vector3.up * 0.5f); }
        private void OnLongJumped() { if (!Playing) return; app.Audio.Sfx("sfx_long_jump", 0.8f); app.Audio.Meow("meow_excited"); }
        private void OnLanded() { if (!Playing) return; app.Audio.Sfx("sfx_land", 0.5f); Vfx.Burst("landing", Cat.transform.position); Camera.OnLanded(); }
        private void OnSlid() { if (!Playing) return; app.Audio.Sfx("sfx_slide", 0.6f); }
        private void OnWarning(Vector3 pos) { if (!Playing) return; app.Audio.Sfx("sfx_warning", 0.7f, 0.02f); }
    }
}
