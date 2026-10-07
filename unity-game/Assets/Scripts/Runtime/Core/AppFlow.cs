using System.Linq;
using System.Threading.Tasks;
using TFY.CatAdventure.Audio;
using TFY.CatAdventure.Backend;
using TFY.CatAdventure.Domain;
using TFY.CatAdventure.Gameplay;
using TFY.CatAdventure.UI;
using UnityEngine;

namespace TFY.CatAdventure
{
    /// <summary>
    /// Startup and navigation:
    /// TFY LOGO → GAME LOADING → CHECK EXISTING PLAYER → (new: anonymous account + automatic username + profile | existing: load)
    /// → HOME. Then PLAY → level → LEVEL COMPLETE → rewards → next level. Every network step is optional.
    /// </summary>
    public class AppFlow
    {
        private readonly App app;
        public bool DailyAvailable { get; private set; }
        private float reconnectTimer;

        public AppFlow(App app) { this.app = app; app.Run.Finished += OnFinished; app.Run.Failed += OnFailed; }

        public bool OnScreenButtons
        {
            get => PlayerPrefs.GetInt("tfy.buttons", 0) == 1;
            set { PlayerPrefs.SetInt("tfy.buttons", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public async Task Boot()
        {
            app.UI.Show<SplashScreen>();
            app.Audio.Sfx("meow_happy", 0.6f);
            await Wait(1.3f);

            var loading = app.UI.Show<LoadingScreen>();
            loading.SetProgress(0.1f);
            var boot = await app.Backend.BootAsync(p => loading.SetProgress(0.1f + p * 0.75f));
            ApplySettingsLocal(app.Save.Player.settings);
            loading.SetProgress(0.9f);
            var level = CurrentLevel();
            app.Audio.Preload(app.Content.Content.World(level.world));
            app.Audio.PlayMusic("music_menu");
            loading.SetProgress(1f);
            await Wait(0.4f);

            GoHome();
            var name = app.Save.Player.username;
            app.UI.Toast(boot.isNewPlayer ? Loc.T("welcome.new", name) : Loc.T("welcome.back", name));
            if (boot.offline && app.Backend.FirebaseAvailable) app.UI.Toast(Loc.T("error.offline"));
            AnalyticsService.GameStarted(boot.isNewPlayer, boot.offline);
            _ = RefreshDailyFlag();
            if (!heartbeatRunning) { heartbeatRunning = true; app.StartCoroutine(Heartbeat()); }
        }
        private bool heartbeatRunning;

        /// <summary>Every minute: reconnect and sync queued offline runs.</summary>
        private System.Collections.IEnumerator Heartbeat()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(60f);
                if (!app.Run.Playing) { var t = app.Backend.TryReconnectAsync(); while (!t.IsCompleted) yield return null; }
            }
        }

        private async Task RefreshDailyFlag()
        {
            var r = await app.Backend.GetDailyAsync();
            DailyAvailable = r.ok && r.data.enabled && r.data.status != null && r.data.status.canClaim;
        }

        public LevelDef CurrentLevel()
        {
            var c = app.Content.Content; var p = app.Save.Player;
            return c.levels.Where(l => l.enabled && l.number <= p.currentLevel).OrderByDescending(l => l.number).FirstOrDefault() ?? c.levels.First();
        }

        // ------------------------------------------------------------------ navigation
        public void GoHome()
        {
            Time.timeScale = 1; app.Audio.PauseAll(false);
            app.UI.ShowOverlay<PauseScreen>(false);
            app.Run.Stop();
            app.Audio.PlayMusic("music_menu");
            app.Audio.PlayAmbience(null);
            app.UI.Show<HomeScreen>();
        }

        public void PlayCurrent() => PlayLevel(CurrentLevel().number);

        public async void PlayLevel(int number)
        {
            var level = app.Content.Content.Level(number);
            if (level == null || !level.enabled) { app.UI.Toast(Loc.T("levels.comingSoon")); return; }
            if (!app.Save.Player.IsUnlocked(number)) { app.UI.Toast(Loc.T("levels.locked", number - 1)); return; }
            if (!string.IsNullOrEmpty(app.Content.Runtime.maintenance_message)) app.UI.Toast(app.Content.Runtime.maintenance_message);

            var loading = app.UI.Show<LoadingScreen>(); loading.SetProgress(0.3f);
            await Wait(0.15f);
            StartLevel(level, false);
            loading.SetProgress(1f);
            await Wait(0.25f);
            app.UI.Show<HudScreen>();
        }

        /// <summary>Game speed setting: the whole level runs slower or faster (menus always run at normal speed).</summary>
        private float PlayScale => app.Save.Player.settings.GameSpeedScale;

        private void StartLevel(LevelDef level, bool fromCheckpoint)
        {
            Time.timeScale = PlayScale; app.Audio.PauseAll(false);
            app.UI.ShowOverlay<PauseScreen>(false);
            var world = app.Content.Content.World(level.world);
            var plan = app.Run.PlanFor(level);
            var gaps = plan.items.Where(i => i.kind == PlacementKind.Obstacle && i.id == "broken_bridge").Select(i => (i.z, i.z + i.length)).ToList();
            app.Environment.Build(world, level.seed, d => !gaps.Any(g => d > g.Item1 + 0.3f && d < g.Item2 - 0.3f));
            app.Sky.Apply(world, level.timeOfDay, app.Quality.Current.fog);
            app.Post.SetMenu(false); app.Post.SetNight(app.Sky.IsNight);
            app.Audio.Preload(world);
            app.Audio.PlayMusic(world.music);
            app.Audio.PlayAmbience(world.ambience);
            app.Stage.RebuildCat();
            app.CameraRig.ReduceMotion = app.Save.Player.settings.reduceMotion;
            app.Run.Begin(level, fromCheckpoint);
            WeatherFor(world);
        }

        private ParticleSystem weather;
        private void WeatherFor(WorldDef w)
        {
            if (weather) Object.Destroy(weather.gameObject);
            float budget = app.Save.Player.settings.reduceMotion ? 0.3f : app.Quality.Current.particles;
            weather = Graphics.Vfx.Weather(app.CameraRig.transform, w.weather, budget);
        }

        public void Pause()
        {
            if (!app.Run.Playing || Time.timeScale == 0) return;
            Time.timeScale = 0; app.Audio.PauseAll(true);
            app.UI.ShowOverlay<PauseScreen>(true);
        }

        public void Resume()
        {
            Time.timeScale = PlayScale; app.Audio.PauseAll(false);
            app.UI.ShowOverlay<PauseScreen>(false);
        }

        public void Restart()
        {
            if (app.Run.Level == null) { GoHome(); return; }
            StartLevel(app.Run.Level, false);
            app.UI.Show<HudScreen>();
        }

        public void FromCheckpoint()
        {
            if (app.Run.Level == null) { GoHome(); return; }
            StartLevel(app.Run.Level, true);
            app.UI.Show<HudScreen>();
        }

        public void NextLevel()
        {
            var cur = app.Run.Level?.number ?? 0;
            var next = app.Content.Content.levels.Where(l => l.enabled && l.number > cur).OrderBy(l => l.number).FirstOrDefault();
            if (next == null) GoHome(); else PlayLevel(next.number);
        }

        private void OnFinished(LevelResult result, RunController.RunStats stats)
        {
            var cur = app.Run.Level.number;
            bool hasNext = app.Content.Content.levels.Any(l => l.enabled && l.number > cur);
            app.UI.Show<CompleteScreen>().Show(result, stats.coins, stats.fish, stats.gems, hasNext);
        }

        private async void OnFailed()
        {
            await Wait(0.9f);
            if (!app.Run.Playing) app.UI.Show<FailScreen>();
        }

        // ------------------------------------------------------------------ settings
        public void ApplySettings(PlayerSettings s)
        {
            string oldLanguage = app.Save.Player.settings.language, oldGraphics = app.Save.Player.settings.graphics;
            app.Backend.SaveSettings(s);
            ApplySettingsLocal(s, oldGraphics != s.graphics);
            if (oldLanguage != s.language) { app.UI.Rebuild(); app.UI.Show<SettingsScreen>(); }
        }

        private void ApplySettingsLocal(PlayerSettings s, bool graphicsChanged = true)
        {
            Loc.Language = s.language;
            app.Audio.ApplySettings(s);
            Haptics.Enabled = s.vibration;
            app.CameraRig.ReduceMotion = s.reduceMotion;
            if (graphicsChanged) app.Quality.Apply(s.graphics);
            app.Post.ApplyTier(app.Quality.Current, s.reduceMotion);
        }

        public async Task<bool> DeleteAllData()
        {
            bool ok = await app.Backend.DeleteAllDataAsync();
            if (!ok) return false;
            PlayerPrefs.DeleteAll();
            app.Run.Tutorial.ResetHints();
            await Boot(); // fresh start: new anonymous player, new automatic name
            return true;
        }

        private static async Task Wait(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) await Task.Yield();
        }
    }
}
