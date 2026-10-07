using System.Linq;
using TFY.CatAdventure.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace TFY.CatAdventure.UI
{
    public class SplashScreen : UIScreen
    {
        private CanvasGroup group;
        protected override void Build(RectTransform safe)
        {
            var bg = Root.gameObject.AddComponent<Image>(); bg.color = UIKit.Hex("#2A1B5C"); Root.SetAsFirstSibling();
            group = safe.gameObject.AddComponent<CanvasGroup>();
            var logo = UIKit.At(safe, "Logo", new Vector2(0.5f, 0.55f), new Vector2(860, 860));
            var art = Icons.Logo();
            if (art) UIKit.Image(logo, art);
            else
            {   // Placeholder until the official TFY logo is added at Resources/Branding/tfy_logo.png
                var circle = logo.gameObject.AddComponent<Image>(); circle.sprite = Icons.Rounded(300); circle.type = Image.Type.Sliced; circle.color = UIKit.Orange;
                UIKit.Label(logo, "TFY", 220, Color.white);
            }
            var sub = UIKit.At(safe, "Sub", new Vector2(0.5f, 0.28f), new Vector2(900, 120)); UIKit.Label(sub, "presents", 56, new Color(1, 1, 1, 0.8f));
        }
        public override void OnShow() { group.alpha = 0; App.UI.StartCoroutine(App.UI.FadeIn(group, 0.5f)); }
    }

    public class LoadingScreen : UIScreen
    {
        private RectTransform bar, cat; private Text tip; private float progress, shown;
        protected override void Build(RectTransform safe)
        {
            var bg = Root.gameObject.AddComponent<Image>(); bg.color = UIKit.Hex("#6C4AB6"); Root.SetAsFirstSibling();
            var art = Icons.Logo();
            if (art) UIKit.Image(UIKit.At(safe, "Logo", new Vector2(0.5f, 0.78f), new Vector2(560, 560)), art); // official TFY CATO logo
            else { var title = UIKit.At(safe, "Title", new Vector2(0.5f, 0.74f), new Vector2(1000, 260)); UIKit.Label(title, Loc.T("title"), 110, Color.white); }
            cat = UIKit.At(safe, "Cat", new Vector2(0.5f, 0.5f), new Vector2(300, 300)); UIKit.Image(cat, Icons.Get("cat"));
            var track = UIKit.At(safe, "Track", new Vector2(0.5f, 0.33f), new Vector2(800, 60)); UIKit.Panel(track, "Bg", new Color(0, 0, 0, 0.3f), 30);
            bar = UIKit.Rect(track, "Bar", Vector2.zero, new Vector2(0, 1)); var bi = bar.gameObject.AddComponent<Image>(); bi.sprite = Icons.Rounded(30); bi.type = Image.Type.Sliced; bi.color = UIKit.Yellow;
            var tipRt = UIKit.At(safe, "Tip", new Vector2(0.5f, 0.18f), new Vector2(940, 200)); tip = UIKit.Label(tipRt, "", 50, Color.white);
        }
        public void SetProgress(float p) => progress = Mathf.Clamp01(p);
        public override void OnShow() { progress = shown = 0; tip.text = App.Content.RandomTip(); }
        public override void Tick(float dt)
        {
            shown = Mathf.MoveTowards(shown, progress, dt * 1.5f);
            bar.anchorMax = new Vector2(Mathf.Max(0.04f, shown), 1);
            cat.anchoredPosition = new Vector2(0, Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f)) * 60f);
            cat.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.unscaledTime * 6f) * 6f);
        }
    }

    public class HomeScreen : UIScreen
    {
        private Text hello, coins, gems, playLevel, banner, offline; private GameObject rewardsDot, bannerRoot;
        protected override void Build(RectTransform safe)
        {
            coins = UIKit.Counter(safe, "coin", new Vector2(0, 1), new Vector2(30, -30));
            gems = UIKit.Counter(safe, "gem", new Vector2(0, 1), new Vector2(330, -30), 220);
            var offRt = UIKit.At(safe, "Offline", new Vector2(1, 1), new Vector2(300, 80), new Vector2(-30, -40)); offline = UIKit.Label(offRt, "offline", 36, new Color(1, 1, 1, 0.8f));
            var logoArt = Icons.Logo();
            if (logoArt) UIKit.Image(UIKit.At(safe, "Logo", new Vector2(0.5f, 1), new Vector2(380, 380), new Vector2(0, -120)), logoArt); // official TFY CATO logo
            else { var title = UIKit.At(safe, "Title", new Vector2(0.5f, 1), new Vector2(1000, 300), new Vector2(0, -150)); UIKit.Label(title, Loc.T("title"), 112, Color.white); }
            var hi = UIKit.At(safe, "Hello", new Vector2(0.5f, 1), new Vector2(1000, 110), new Vector2(0, -500)); hello = UIKit.Label(hi, "", 66, UIKit.Yellow);

            bannerRoot = UIKit.At(safe, "Announcement", new Vector2(0.5f, 1), new Vector2(960, 130), new Vector2(0, -620)).gameObject;
            UIKit.Panel(bannerRoot.transform, "Bg", new Color(1, 1, 1, 0.85f), 40); banner = UIKit.Label(bannerRoot.transform, "", 40, UIKit.Dark, TextAnchor.MiddleCenter, false);

            // The 3D cat is visible in the middle of the screen (menu camera). Big PLAY button below it.
            var play = UIKit.At(safe, "Play", new Vector2(0.5f, 0), new Vector2(760, 250), new Vector2(0, 560));
            UIKit.Button(play, Loc.T("home.play"), UIKit.Green, () => App.Flow.PlayCurrent(), "play", 110);
            var pl = UIKit.At(play, "Level", new Vector2(0.5f, 0), new Vector2(600, 60), new Vector2(0, -70)); playLevel = UIKit.Label(pl, "", 44, Color.white);

            var grid = UIKit.Rect(safe, "Menu", new Vector2(0.03f, 0), new Vector2(0.97f, 0), new Vector2(0, 40), new Vector2(0, 400));
            var g = grid.gameObject.AddComponent<GridLayoutGroup>(); g.cellSize = new Vector2(330, 160); g.spacing = new Vector2(20, 20); g.childAlignment = TextAnchor.MiddleCenter; g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = 3;
            Item(grid, Loc.T("home.shop"), UIKit.Pink, "cart", () => App.UI.Show<ShopScreen>());
            Item(grid, Loc.T("home.levels"), UIKit.Blue, "flag", () => App.UI.Show<LevelsScreen>());
            var rewards = Item(grid, Loc.T("home.rewards"), UIKit.Orange, "gift", () => App.UI.Show<RewardsScreen>());
            Item(grid, Loc.T("home.achievements"), UIKit.Purple, "trophy", () => App.UI.Show<AchievementsScreen>());
            Item(grid, Loc.T("home.settings"), UIKit.Grey, "gear", () => App.UI.Show<SettingsScreen>());
            var dot = UIKit.At(rewards.transform, "Dot", new Vector2(1, 1), new Vector2(50, 50), new Vector2(10, 10)); var di = dot.gameObject.AddComponent<Image>(); di.sprite = Icons.Rounded(24); di.type = Image.Type.Sliced; di.color = UIKit.Red; rewardsDot = dot.gameObject;
        }

        private static Button Item(RectTransform grid, string text, Color c, string icon, System.Action a)
        {
            var rt = UIKit.Rect(grid, text, Vector2.zero, Vector2.one);
            return UIKit.Button(rt, text, c, a, icon, 38);
        }

        public override void OnShow()
        {
            var p = App.Save.Player;
            hello.text = Loc.T("home.hi", p.username);
            coins.text = p.coins.ToString("N0"); gems.text = p.gems.ToString("N0");
            playLevel.text = Loc.T("levels.level", Mathf.Min(p.currentLevel, App.Content.Content.LastEnabledLevel));
            offline.gameObject.SetActive(!App.Backend.Online);
            var ann = App.Backend.Announcements.FirstOrDefault();
            bannerRoot.SetActive(ann != null || !string.IsNullOrEmpty(App.Content.Runtime.maintenance_message));
            banner.text = !string.IsNullOrEmpty(App.Content.Runtime.maintenance_message) ? App.Content.Runtime.maintenance_message : ann != null ? $"{ann.title}: {ann.message}" : "";
            rewardsDot.SetActive(p.pendingBoxes > 0 || App.Flow.DailyAvailable);
            App.Stage.ShowMenu();
        }

        public override bool OnBack() { Application.Quit(); return true; }
    }

    public class LevelsScreen : UIScreen
    {
        private RectTransform list;
        protected override void Build(RectTransform safe)
        {
            var bg = Root.gameObject.AddComponent<Image>(); bg.color = new Color(0.16f, 0.1f, 0.35f, 0.88f); Root.SetAsFirstSibling();
            Header(safe, Loc.T("levels.title"), () => App.Flow.GoHome());
            var area = UIKit.Rect(safe, "List", Vector2.zero, Vector2.one, new Vector2(30, 30), new Vector2(-30, -210));
            list = UIKit.ScrollList(area, 24);
        }

        public override void OnShow()
        {
            UIKit.Clear(list);
            var c = App.Content.Content; var p = App.Save.Player;
            foreach (var w in c.worlds.OrderBy(w => w.order))
            {
                var levels = c.levels.Where(l => l.world == w.id && l.enabled).OrderBy(l => l.number).ToList();
                var head = UIKit.Rect(list, w.name, Vector2.zero, Vector2.one); UIKit.Size(head, 110);
                var col = Graphics.MaterialLibrary.Hex(w.palette.accent);
                UIKit.Panel(head, "Bg", new Color(col.r, col.g, col.b, w.comingSoon ? 0.35f : 0.9f), 36);
                UIKit.Label(head, w.comingSoon ? $"{w.name} — {Loc.T("levels.comingSoon")}" : $"{w.name}  ·  {w.difficultyLabel}", 52, Color.white);
                if (w.comingSoon || levels.Count == 0) continue;
                var row = UIKit.Rect(list, w.id + "_levels", Vector2.zero, Vector2.one); UIKit.Size(row, Mathf.CeilToInt(levels.Count / 4f) * 262); // 30 levels: up to 6 per world, 4 per row
                var g = row.gameObject.AddComponent<GridLayoutGroup>(); g.cellSize = new Vector2(230, 250); g.spacing = new Vector2(24, 12); g.childAlignment = TextAnchor.UpperCenter;
                g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = 4;
                foreach (var l in levels)
                {
                    var cell = UIKit.Rect(row, "L" + l.number, Vector2.zero, Vector2.one);
                    bool unlocked = p.IsUnlocked(l.number);
                    var prog = p.Level(l.id);
                    int n = l.number;
                    var btn = UIKit.Button(cell, unlocked ? l.number.ToString() : "", unlocked ? UIKit.Blue : UIKit.Grey,
                        () => { if (unlocked) App.Flow.PlayLevel(n); else App.UI.Toast(Loc.T("levels.locked", n - 1)); }, unlocked ? null : "lock", 96);
                    ((RectTransform)btn.transform).offsetMin = new Vector2(0, 70);
                    var stars = UIKit.Rect(cell, "Stars", new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, 64));
                    var sg = stars.gameObject.AddComponent<HorizontalLayoutGroup>(); sg.childAlignment = TextAnchor.MiddleCenter; sg.spacing = 4; sg.childControlWidth = sg.childControlHeight = false;
                    for (int s = 0; s < 3; s++) { var st = UIKit.At(stars, "S" + s, new Vector2(0.5f, 0.5f), new Vector2(60, 60)); UIKit.Image(st, Icons.Get(prog != null && prog.bestStars > s ? "star" : "star_empty")); }
                }
            }
        }

        public override bool OnBack() { App.Flow.GoHome(); return true; }
    }
}
