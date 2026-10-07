using System.Collections.Generic;
using TFY.CatAdventure.Domain;
using TFY.CatAdventure.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace TFY.CatAdventure.UI
{
    /// <summary>
    /// In-game HUD — only what's needed, around the edges: hearts, big stars (0–3), coins, fish, a pause button, a slim progress
    /// bar with checkpoint flags, power-up timers, tutorial prompts and optional on-screen buttons.
    /// </summary>
    public class HudScreen : UIScreen
    {
        private readonly List<Image> hearts = new List<Image>();
        private Text coins, fish, stars, gems, banner, prompt; private RectTransform progressFill, powerRow, bannerRoot, promptRoot, gesture, buttons;
        private readonly Dictionary<string, (RectTransform rt, Image fill, float duration)> powerUps = new Dictionary<string, (RectTransform, Image, float)>();
        private float bannerTimer; private GameInput promptGesture;
        private RectTransform progressTrack;

        protected override void Build(RectTransform safe)
        {
            var heartRow = UIKit.Rect(safe, "Hearts", new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -130), new Vector2(430, -30));
            var h = heartRow.gameObject.AddComponent<HorizontalLayoutGroup>(); h.spacing = 8; h.childControlWidth = h.childControlHeight = false; h.childAlignment = TextAnchor.MiddleLeft;
            for (int i = 0; i < 5; i++) { var rt = UIKit.At(heartRow, "H" + i, new Vector2(0, 0.5f), new Vector2(92, 92)); hearts.Add(UIKit.Image(rt, Icons.Get("heart"))); }

            stars = UIKit.Counter(safe, "star", new Vector2(1, 1), new Vector2(-170, -30), 220);
            coins = UIKit.Counter(safe, "coin", new Vector2(1, 1), new Vector2(-170, -140), 260);
            fish = UIKit.Counter(safe, "fish", new Vector2(1, 1), new Vector2(-170, -250), 220);
            gems = UIKit.Counter(safe, "gem", new Vector2(1, 1), new Vector2(-170, -360), 220);
            var pause = UIKit.At(safe, "Pause", new Vector2(1, 1), new Vector2(130, 130), new Vector2(-25, -25));
            UIKit.Button(pause, "", new Color(0, 0, 0, 0.35f), () => App.Flow.Pause(), "pause");

            progressTrack = UIKit.Rect(safe, "Progress", new Vector2(0.2f, 1), new Vector2(0.62f, 1), new Vector2(0, -170), new Vector2(0, -145));
            UIKit.Panel(progressTrack, "Bg", new Color(0, 0, 0, 0.3f), 12);
            progressFill = UIKit.Rect(progressTrack, "Fill", Vector2.zero, new Vector2(0, 1)); var pf = progressFill.gameObject.AddComponent<Image>(); pf.sprite = Icons.Rounded(12); pf.type = Image.Type.Sliced; pf.color = UIKit.Yellow;

            powerRow = UIKit.Rect(safe, "PowerUps", new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -260), new Vector2(700, -150));
            var pr = powerRow.gameObject.AddComponent<HorizontalLayoutGroup>(); pr.spacing = 12; pr.childControlWidth = pr.childControlHeight = false; pr.childAlignment = TextAnchor.MiddleLeft;

            bannerRoot = UIKit.At(safe, "Banner", new Vector2(0.5f, 0.72f), new Vector2(900, 160)); UIKit.Panel(bannerRoot, "Bg", new Color(0.18f, 0.77f, 0.71f, 0.9f), 60);
            banner = UIKit.Label(bannerRoot, "", 70, Color.white); bannerRoot.gameObject.SetActive(false);

            promptRoot = UIKit.At(safe, "Prompt", new Vector2(0.5f, 0.38f), new Vector2(960, 360));
            UIKit.Panel(promptRoot, "Bg", new Color(0.1f, 0.05f, 0.25f, 0.6f), 60);
            gesture = UIKit.At(promptRoot, "Gesture", new Vector2(0.5f, 1), new Vector2(170, 170), new Vector2(0, -20)); UIKit.Image(gesture, Icons.Get("play"));
            var pt = UIKit.Rect(promptRoot, "Text", Vector2.zero, new Vector2(1, 0.5f), new Vector2(20, 10), new Vector2(-20, 0)); prompt = UIKit.Label(pt, "", 72, UIKit.Yellow);
            promptRoot.gameObject.SetActive(false);

            buttons = UIKit.Rect(safe, "Buttons", new Vector2(0, 0), new Vector2(1, 0), new Vector2(20, 30), new Vector2(-20, 250));
            void Btn(string icon, float x0, float x1, GameInput i, float rot)
            {
                var rt = UIKit.Rect(buttons, i.ToString(), new Vector2(x0, 0), new Vector2(x1, 1));
                var b = UIKit.Button(rt, "", new Color(1, 1, 1, 0.25f), () => App.Run.Input(i), icon);
                b.transform.GetChild(0).localRotation = Quaternion.Euler(0, 0, rot);
            }
            Btn("back", 0f, 0.23f, GameInput.SwipeLeft, 0); Btn("play", 0.25f, 0.49f, GameInput.Tap, 90); Btn("play", 0.51f, 0.75f, GameInput.SwipeDown, -90); Btn("back", 0.77f, 1f, GameInput.SwipeRight, 180);

            GameEvents.HeartsChanged += OnHearts; GameEvents.RunCountersChanged += OnCounters; GameEvents.ProgressChanged += OnProgress;
            GameEvents.PowerUpStarted += OnPowerStart; GameEvents.PowerUpEnded += OnPowerEnd; GameEvents.CheckpointReached += OnCheckpoint;
            TutorialDirector.Prompt += OnPrompt; TutorialDirector.Hide += OnHidePrompt; TutorialDirector.Great += OnGreat;
            GameEvents.LandmarkSpotted += ShowBanner;
        }

        public override void Dispose()
        {
            GameEvents.HeartsChanged -= OnHearts; GameEvents.RunCountersChanged -= OnCounters; GameEvents.ProgressChanged -= OnProgress;
            GameEvents.PowerUpStarted -= OnPowerStart; GameEvents.PowerUpEnded -= OnPowerEnd; GameEvents.CheckpointReached -= OnCheckpoint;
            TutorialDirector.Prompt -= OnPrompt; TutorialDirector.Hide -= OnHidePrompt; TutorialDirector.Great -= OnGreat;
            GameEvents.LandmarkSpotted -= ShowBanner;
        }

        private void OnHidePrompt() { if (promptRoot) promptRoot.gameObject.SetActive(false); }

        public override void OnShow()
        {
            buttons.gameObject.SetActive(App.Flow.OnScreenButtons);
            promptRoot.gameObject.SetActive(false); bannerRoot.gameObject.SetActive(false);
            foreach (var p in powerUps.Values) Object.Destroy(p.rt.gameObject);
            powerUps.Clear();
            // Checkpoint flags on the progress bar.
            foreach (Transform c in progressTrack) if (c.name.StartsWith("CP")) Object.Destroy(c.gameObject);
            var plan = App.Run.Plan;
            if (plan != null) foreach (var cp in plan.checkpoints)
            {
                var f = UIKit.At(progressTrack, "CP", new Vector2(cp / plan.length, 0.5f), new Vector2(46, 46)); f.pivot = new Vector2(0.5f, 0.5f); UIKit.Image(f, Icons.Get("flag"));
            }
        }

        private void OnHearts(int n, int max) { for (int i = 0; i < hearts.Count; i++) { hearts[i].gameObject.SetActive(i < max); hearts[i].sprite = Icons.Get(i < n ? "heart" : "heart_empty"); } }
        private void OnCounters(int c, int f, int g, int s) { coins.text = c.ToString("N0"); fish.text = f.ToString(); gems.text = g.ToString(); stars.text = $"{s}/3"; gems.transform.parent.gameObject.SetActive(g > 0); }
        private void OnProgress(float p) { progressFill.anchorMax = new Vector2(Mathf.Clamp01(p), 1); }
        private void OnCheckpoint(int i) { ShowBanner(Loc.T("checkpoint")); }
        private void OnGreat() { promptRoot.gameObject.SetActive(false); ShowBanner(Loc.T("tut.great")); }

        private void OnPrompt(string text, GameInput g)
        {
            prompt.text = text; promptGesture = g; promptRoot.gameObject.SetActive(true); promptRoot.SetAsLastSibling();
            gesture.localRotation = Quaternion.Euler(0, 0, g == GameInput.Tap ? 0 : g == GameInput.SwipeUp ? 90 : g == GameInput.SwipeDown ? -90 : g == GameInput.SwipeLeft ? 180 : 0);
        }

        public void ShowBanner(string text) { banner.text = text; bannerRoot.gameObject.SetActive(true); bannerTimer = 1.4f; }

        private void OnPowerStart(string id, float duration)
        {
            if (powerUps.TryGetValue(id, out var existing)) { powerUps[id] = (existing.rt, existing.fill, duration); return; }
            var def = App.Content.Content.PowerUp(id);
            var rt = UIKit.At(powerRow, id, new Vector2(0, 0.5f), new Vector2(100, 100));
            var ring = rt.gameObject.AddComponent<Image>(); ring.sprite = Icons.Rounded(50); ring.type = Image.Type.Filled; ring.fillMethod = Image.FillMethod.Radial360; ring.color = def != null ? Graphics.MaterialLibrary.Hex(def.color) : Color.white;
            var ic = UIKit.Rect(rt, "Icon", new Vector2(0.2f, 0.2f), new Vector2(0.8f, 0.8f));
            UIKit.Image(ic, Icons.Get(id == "magnet" ? "coin" : id == "shield" ? "heart" : id == "super_jump" ? "play" : id == "speed_boost" ? "flag" : "coin"));
            powerUps[id] = (rt, ring, duration);
            ShowBanner(def?.name ?? id);
        }

        private void OnPowerEnd(string id) { if (powerUps.TryGetValue(id, out var p)) { Object.Destroy(p.rt.gameObject); powerUps.Remove(id); } }

        public override void Tick(float dt)
        {
            if (bannerTimer > 0 && (bannerTimer -= dt) <= 0) bannerRoot.gameObject.SetActive(false);
            if (bannerRoot.gameObject.activeSelf) bannerRoot.localScale = Vector3.one * (1 + Mathf.Max(0, bannerTimer - 1.2f) * 0.8f);
            foreach (var kv in powerUps) if (kv.Value.duration > 0) kv.Value.fill.fillAmount = Mathf.Clamp01(App.Run.PowerUpRemaining(kv.Key) / kv.Value.duration);
            if (promptRoot.gameObject.activeSelf)
            {
                float t = Time.unscaledTime * 3f;
                gesture.anchoredPosition = new Vector2(promptGesture == GameInput.SwipeLeft ? -Mathf.Repeat(t, 1) * 60 : promptGesture == GameInput.SwipeRight ? Mathf.Repeat(t, 1) * 60 : 0,
                    -20 + (promptGesture == GameInput.SwipeDown ? -Mathf.Repeat(t, 1) * 40 : promptGesture == GameInput.Tap ? Mathf.Abs(Mathf.Sin(t * 2)) * 20 : 0));
            }
        }

        public override bool OnBack() { App.Flow.Pause(); return true; }
    }

    public class PauseScreen : UIScreen
    {
        protected override void Build(RectTransform safe)
        {
            var dim = Root.gameObject.AddComponent<Image>(); dim.color = new Color(0.08f, 0.05f, 0.2f, 0.75f); Root.SetAsFirstSibling();
            var title = UIKit.At(safe, "Title", new Vector2(0.5f, 0.72f), new Vector2(900, 160)); UIKit.Label(title, Loc.T("pause.title"), 110, Color.white);
            var col = UIKit.At(safe, "Buttons", new Vector2(0.5f, 0.42f), new Vector2(700, 620)); UIKit.Column(col, 30);
            UIKit.Size(UIKit.Button(UIKit.Rect(col, "Resume", Vector2.zero, Vector2.one), Loc.T("pause.resume"), UIKit.Green, () => App.Flow.Resume(), "play", 64), 180);
            UIKit.Size(UIKit.Button(UIKit.Rect(col, "Restart", Vector2.zero, Vector2.one), Loc.T("pause.restart"), UIKit.Orange, () => App.Flow.Restart(), null, 60), 170);
            UIKit.Size(UIKit.Button(UIKit.Rect(col, "Home", Vector2.zero, Vector2.one), Loc.T("pause.home"), UIKit.Purple, () => App.Flow.GoHome(), "back", 60), 170);
        }
        public override bool OnBack() { App.Flow.Resume(); return true; }
    }

    /// <summary>LEVEL COMPLETE! Stars pop in one by one, then coins, fish, gems, score and best score. NEXT LEVEL / REPLAY / HOME.</summary>
    public class CompleteScreen : UIScreen
    {
        private readonly Image[] starImgs = new Image[3];
        private Text coins, fish, gems, score, best, notes; private float anim; private int stars; private Button next;

        protected override void Build(RectTransform safe)
        {
            var dim = Root.gameObject.AddComponent<Image>(); dim.color = new Color(0.08f, 0.05f, 0.2f, 0.55f); Root.SetAsFirstSibling();
            var card = UIKit.At(safe, "Card", new Vector2(0.5f, 0.52f), new Vector2(980, 1500)); UIKit.Panel(card, "Bg", UIKit.Light, 70);
            UIKit.Label(UIKit.Rect(card, "Title", new Vector2(0, 0.88f), new Vector2(1, 0.99f)), Loc.T("complete.title"), 92, UIKit.Orange);
            var starRow = UIKit.Rect(card, "Stars", new Vector2(0.1f, 0.7f), new Vector2(0.9f, 0.88f));
            for (int i = 0; i < 3; i++) { var rt = UIKit.Rect(starRow, "S" + i, new Vector2(i / 3f, i == 1 ? 0.1f : 0), new Vector2((i + 1) / 3f, i == 1 ? 1f : 0.85f)); starImgs[i] = UIKit.Image(rt, Icons.Get("star_empty")); }
            Text Row(string icon, string label, float y)
            {
                var r = UIKit.Rect(card, label, new Vector2(0.1f, y), new Vector2(0.9f, y + 0.065f));
                UIKit.Image(UIKit.At(r, "Icon", new Vector2(0, 0.5f), new Vector2(90, 90)), Icons.Get(icon));
                UIKit.Label(UIKit.Rect(r, "L", Vector2.zero, new Vector2(0.6f, 1), new Vector2(110, 0), Vector2.zero), label, 50, UIKit.Dark, TextAnchor.MiddleLeft, false);
                return UIKit.Label(UIKit.Rect(r, "V", new Vector2(0.6f, 0), Vector2.one), "0", 56, UIKit.Dark, TextAnchor.MiddleRight, false);
            }
            coins = Row("coin", "Coins", 0.61f); fish = Row("fish", "Fish", 0.54f); gems = Row("gem", "Gems", 0.47f);
            score = Row("trophy", Loc.T("complete.score"), 0.40f); best = Row("star", Loc.T("complete.best"), 0.33f);
            notes = UIKit.Label(UIKit.Rect(card, "Notes", new Vector2(0.06f, 0.2f), new Vector2(0.94f, 0.33f)), "", 40, UIKit.Purple, TextAnchor.MiddleCenter, false);
            next = UIKit.Button(UIKit.Rect(card, "Next", new Vector2(0.08f, 0.1f), new Vector2(0.92f, 0.2f)), Loc.T("complete.next"), UIKit.Green, () => App.Flow.NextLevel(), "play", 70);
            UIKit.Button(UIKit.Rect(card, "Replay", new Vector2(0.08f, 0.015f), new Vector2(0.49f, 0.09f)), Loc.T("complete.replay"), UIKit.Orange, () => App.Flow.Restart(), null, 50);
            UIKit.Button(UIKit.Rect(card, "Home", new Vector2(0.51f, 0.015f), new Vector2(0.92f, 0.09f)), Loc.T("complete.home"), UIKit.Purple, () => App.Flow.GoHome(), null, 50);
        }

        public void Show(LevelResult r, int coinsCollected, int fishCollected, int gemsCollected, bool hasNext)
        {
            stars = r.stars; anim = 0;
            for (int i = 0; i < 3; i++) { starImgs[i].sprite = Icons.Get("star_empty"); starImgs[i].transform.localScale = Vector3.one; }
            coins.text = r.bonusCoins > 0 ? $"{coinsCollected}  (+{r.bonusCoins} {Loc.T("complete.bonus")})" : coinsCollected.ToString();
            fish.text = fishCollected.ToString(); gems.text = gemsCollected.ToString();
            score.text = r.score.ToString("N0");
            var p = App.Save.Player.Level(r.levelId);
            best.text = (r.newBestScore ? Loc.T("complete.newBest") + "  " : "") + Mathf.Max(r.score, p?.bestScore ?? 0).ToString("N0");
            var lines = new List<string>();
            if (r.unlockedLevel.HasValue) lines.Add(Loc.T("complete.unlocked", r.unlockedLevel.Value));
            if (r.mysteryBoxesEarned > 0) lines.Add(Loc.T("complete.box"));
            foreach (var a in r.achievements) lines.Add(Loc.T("complete.achievement", a.name));
            if (r.provisional) lines.Add(Loc.T("complete.offline"));
            notes.text = string.Join("\n", lines);
            next.gameObject.SetActive(hasNext);
        }

        public override void Tick(float dt)
        {
            anim += dt;
            for (int i = 0; i < 3; i++)
            {
                float t = anim - 0.35f - i * 0.4f;
                if (t < 0) continue;
                bool earned = i < stars;
                if (earned && starImgs[i].sprite != Icons.Get("star")) { starImgs[i].sprite = Icons.Get("star"); App.Audio.Sfx("sfx_star", 0.8f, 0, 1f + i * 0.12f); }
                float s = earned ? 1f + Mathf.Max(0, 0.4f - t) * 1.5f : 1f;
                starImgs[i].transform.localScale = Vector3.one * s;
            }
        }

        public override bool OnBack() { App.Flow.GoHome(); return true; }
    }

    public class FailScreen : UIScreen
    {
        private Button fromFlag;
        protected override void Build(RectTransform safe)
        {
            var dim = Root.gameObject.AddComponent<Image>(); dim.color = new Color(0.08f, 0.05f, 0.2f, 0.6f); Root.SetAsFirstSibling();
            var card = UIKit.At(safe, "Card", new Vector2(0.5f, 0.5f), new Vector2(940, 1050)); UIKit.Panel(card, "Bg", UIKit.Light, 70);
            UIKit.Label(UIKit.Rect(card, "Title", new Vector2(0, 0.78f), new Vector2(1, 0.96f)), Loc.T("fail.title"), 84, UIKit.Pink);
            UIKit.Image(UIKit.Rect(card, "Cat", new Vector2(0.35f, 0.56f), new Vector2(0.65f, 0.78f)), Icons.Get("cat"));
            fromFlag = UIKit.Button(UIKit.Rect(card, "FromFlag", new Vector2(0.08f, 0.38f), new Vector2(0.92f, 0.53f)), Loc.T("fail.checkpoint"), UIKit.Green, () => App.Flow.FromCheckpoint(), "flag", 64);
            UIKit.Button(UIKit.Rect(card, "Restart", new Vector2(0.08f, 0.21f), new Vector2(0.92f, 0.35f)), Loc.T("fail.restart"), UIKit.Orange, () => App.Flow.Restart(), null, 60);
            UIKit.Button(UIKit.Rect(card, "Home", new Vector2(0.08f, 0.05f), new Vector2(0.92f, 0.18f)), Loc.T("fail.home"), UIKit.Purple, () => App.Flow.GoHome(), "back", 60);
        }
        public override void OnShow() { fromFlag.gameObject.SetActive(App.Run.HasCheckpoint); }
        public override bool OnBack() { App.Flow.GoHome(); return true; }
    }
}
