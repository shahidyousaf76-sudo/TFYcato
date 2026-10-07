using System.Collections.Generic;
using System.Linq;
using TFY.CatAdventure.Backend;
using TFY.CatAdventure.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace TFY.CatAdventure.UI
{
    /// <summary>
    /// Cat Shop: CATS, HATS, GLASSES, SHOES, BACKPACKS, CAPES, TRAILS. Tap an item to preview it on the 3D cat before unlocking.
    /// Unlocking asks for confirmation, needs a connection (the server spends the coins) and never uses real money.
    /// </summary>
    public class ShopScreen : UIScreen
    {
        private static readonly string[] Categories = { "CATS", "HATS", "GLASSES", "SHOES", "BACKPACKS", "CAPES_SCARVES", "TRAILS" };
        private string category = "CATS";
        private RectTransform grid, tabs; private Text coins, gems, info; private ShopItem preview; private bool busy;

        protected override void Build(RectTransform safe)
        {
            Header(safe, Loc.T("shop.title"), () => App.Flow.GoHome());
            coins = UIKit.Counter(safe, "coin", new Vector2(0, 1), new Vector2(30, -210));
            gems = UIKit.Counter(safe, "gem", new Vector2(1, 1), new Vector2(-30, -210), 220);
            tabs = UIKit.Rect(safe, "Tabs", new Vector2(0, 0.46f), new Vector2(1, 0.46f), new Vector2(20, 0), new Vector2(-20, 110));
            var h = tabs.gameObject.AddComponent<HorizontalLayoutGroup>(); h.spacing = 8; h.childForceExpandWidth = true; h.childControlWidth = true; h.childControlHeight = true;
            var infoRt = UIKit.Rect(safe, "Info", new Vector2(0, 0.46f), new Vector2(1, 0.46f), new Vector2(40, 120), new Vector2(-40, 200)); info = UIKit.Label(infoRt, "", 44, Color.white);
            var panel = UIKit.Rect(safe, "Items", new Vector2(0, 0), new Vector2(1, 0.46f), new Vector2(20, 20), new Vector2(-20, -10));
            UIKit.Panel(panel, "Bg", new Color(0.16f, 0.1f, 0.35f, 0.85f), 50);
            var area = UIKit.Rect(panel, "Area", Vector2.zero, Vector2.one, new Vector2(10, 10), new Vector2(-10, -10));
            grid = UIKit.ScrollList(area, 18, true, new Vector2(320, 330), 3);
        }

        public override void OnShow()
        {
            preview = null;
            App.Stage.ShowShop();
            BuildTabs(); Refresh();
        }

        public override void OnHide() { App.Stage.ClearPreview(); }

        private void BuildTabs()
        {
            UIKit.Clear(tabs);
            foreach (var c in Categories)
            {
                var rt = UIKit.Rect(tabs, c, Vector2.zero, Vector2.one); var cat = c;
                UIKit.Button(rt, Loc.T("shop.cat." + c), c == category ? UIKit.Pink : UIKit.Purple, () => { category = cat; BuildTabs(); Refresh(); }, null, 30);
            }
        }

        private void Refresh()
        {
            var p = App.Save.Player; var content = App.Content.Content;
            coins.text = p.coins.ToString("N0"); gems.text = p.gems.ToString("N0");
            info.text = preview != null ? $"{preview.name} — {preview.description}" : "";
            UIKit.Clear(grid);
            foreach (var item in content.shop.Where(i => i.enabled && i.category == category))
            {
                var cell = UIKit.Rect(grid, item.id, Vector2.zero, Vector2.one);
                UIKit.Panel(cell, "Bg", item == preview ? UIKit.Yellow : new Color(1, 1, 1, 0.92f), 36);
                var swatch = UIKit.Rect(cell, "Swatch", new Vector2(0.15f, 0.5f), new Vector2(0.85f, 0.95f));
                var sw = swatch.gameObject.AddComponent<Image>(); sw.sprite = Icons.Rounded(40); sw.type = Image.Type.Sliced; sw.color = item.colors.Count > 0 ? Graphics.MaterialLibrary.Hex(item.colors[0]) : Color.white;
                if (item.IsCat) UIKit.Image(UIKit.Rect(swatch, "Cat", new Vector2(0.15f, 0.1f), new Vector2(0.85f, 0.9f)), Icons.Get("cat"));
                var name = UIKit.Rect(cell, "Name", new Vector2(0, 0.33f), new Vector2(1, 0.5f)); UIKit.Label(name, item.name, 34, UIKit.Dark, TextAnchor.MiddleCenter, false);
                var hit = cell.gameObject.AddComponent<Button>(); var it = item;
                hit.onClick.AddListener(() => { preview = it; App.Stage.Preview(it); Refresh(); });
                var action = UIKit.Rect(cell, "Action", new Vector2(0.06f, 0.04f), new Vector2(0.94f, 0.3f));
                ActionButton(action, item, p);
            }
        }

        private void ActionButton(RectTransform rt, ShopItem item, PlayerData p)
        {
            bool owned = p.Owns(item.id);
            if (owned)
            {
                bool wearing = item.IsCat ? p.selectedCat == item.id : p.equipped.TryGetValue(item.slot, out var e) && e == item.id;
                string text = item.IsCat ? Loc.T(wearing ? "shop.selected" : "shop.select") : Loc.T(wearing ? "shop.remove" : "shop.equip");
                UIKit.Button(rt, text, wearing ? UIKit.Grey : UIKit.Green, () => Equip(item, wearing), null, 34);
                return;
            }
            if (item.source != "shop") { UIKit.Label(rt, Loc.T("shop.reward"), 30, UIKit.Purple, TextAnchor.MiddleCenter, false); return; }
            if (item.unlockLevel > 0 && !p.IsCompleted(item.unlockLevel)) { UIKit.Label(rt, Loc.T("shop.needLevel", item.unlockLevel), 30, UIKit.Grey, TextAnchor.MiddleCenter, false); return; }
            bool gemsPrice = item.priceGems > 0;
            UIKit.Button(rt, (gemsPrice ? item.priceGems : item.priceCoins).ToString("N0"), UIKit.Orange, () => Buy(item), gemsPrice ? "gem" : "coin", 36);
        }

        private void Buy(ShopItem item)
        {
            if (busy) return;
            var p = App.Save.Player;
            bool gemsPrice = item.priceGems > 0;
            if (gemsPrice ? p.gems < item.priceGems : p.coins < item.priceCoins) { App.UI.Toast(Loc.T(gemsPrice ? "shop.notEnoughGems" : "shop.notEnough")); return; }
            if (!App.Backend.Online) { App.UI.Toast(Loc.T("shop.offline")); return; }
            App.UI.Dialog($"{Loc.T("shop.buy")} {item.name}?\n{(gemsPrice ? item.priceGems : item.priceCoins)} {(gemsPrice ? "gems" : "coins")}", Loc.T("yes"), async () =>
            {
                busy = true;
                var r = await App.Backend.PurchaseAsync(item);
                busy = false;
                if (r.ok)
                {
                    App.Audio.Sfx("sfx_unlock"); App.Audio.Meow("meow_excited", true);
                    App.UI.Toast(Loc.T("shop.unlocked", item.name));
                    if (item.IsCat) AnalyticsService.CatUnlocked(item.id); else AnalyticsService.ItemUnlocked(item.id);
                    foreach (var a in r.data.achievements) App.UI.Toast(Loc.T("complete.achievement", a.name));
                    Equip(item, false);
                }
                else App.UI.Toast(r.offline ? Loc.T("shop.offline") : r.code == "resource-exhausted" ? Loc.T("shop.notEnough") : Loc.T("error.generic"));
                Refresh();
            }, Loc.T("no"));
        }

        private async void Equip(ShopItem item, bool currentlyWearing)
        {
            Dictionary<string, string> eq = null; string cat = null;
            if (item.IsCat) { if (currentlyWearing) return; cat = item.id; }
            else eq = new Dictionary<string, string> { [item.slot] = currentlyWearing ? "" : item.id };
            var task = App.Backend.EquipAsync(cat, eq);
            App.Stage.RebuildCat();
            Refresh();
            var r = await task;
            if (!r.ok && !r.offline) { App.UI.Toast(Loc.T("error.generic")); await App.Backend.SyncPendingAsync(); App.Stage.RebuildCat(); Refresh(); }
        }

        public override bool OnBack() { App.Flow.GoHome(); return true; }
    }

    public class AchievementsScreen : UIScreen
    {
        private RectTransform list;
        protected override void Build(RectTransform safe)
        {
            var bg = Root.gameObject.AddComponent<Image>(); bg.color = new Color(0.16f, 0.1f, 0.35f, 0.9f); Root.SetAsFirstSibling();
            Header(safe, Loc.T("achievements.title"), () => App.Flow.GoHome());
            list = UIKit.ScrollList(UIKit.Rect(safe, "List", Vector2.zero, Vector2.one, new Vector2(30, 30), new Vector2(-30, -210)), 20);
        }

        public override void OnShow()
        {
            UIKit.Clear(list);
            var p = App.Save.Player;
            foreach (var a in App.Content.Content.achievements)
            {
                bool done = p.achievements.TryGetValue(a.id, out var d) && d;
                int value = Mathf.Min(a.target, Progress.StatValue(p, a.stat));
                var row = UIKit.Rect(list, a.id, Vector2.zero, Vector2.one); UIKit.Size(row, 190);
                UIKit.Panel(row, "Bg", done ? UIKit.Hex("#FFE8A3") : new Color(1, 1, 1, 0.92f), 40);
                UIKit.Image(UIKit.At(row, "Icon", new Vector2(0, 0.5f), new Vector2(140, 140), new Vector2(25, 0)), Icons.Get(done ? "trophy" : "star_empty"));
                var title = UIKit.Rect(row, "Title", new Vector2(0, 0.5f), new Vector2(0.72f, 1), new Vector2(190, 0), new Vector2(0, -15)); UIKit.Label(title, a.name, 46, UIKit.Dark, TextAnchor.MiddleLeft, false);
                var desc = UIKit.Rect(row, "Desc", new Vector2(0, 0.2f), new Vector2(0.72f, 0.5f), new Vector2(190, 0), Vector2.zero); UIKit.Label(desc, a.description, 34, UIKit.Grey, TextAnchor.MiddleLeft, false);
                var track = UIKit.Rect(row, "Track", new Vector2(0, 0.08f), new Vector2(0.72f, 0.18f), new Vector2(190, 0), Vector2.zero); UIKit.Panel(track, "T", new Color(0, 0, 0, 0.12f), 10);
                var fill = UIKit.Rect(track, "Fill", Vector2.zero, new Vector2(Mathf.Clamp01(value / (float)a.target), 1)); var fi = fill.gameObject.AddComponent<Image>(); fi.color = done ? UIKit.Green : UIKit.Blue;
                var right = UIKit.Rect(row, "Right", new Vector2(0.74f, 0), new Vector2(1, 1), Vector2.zero, new Vector2(-20, 0));
                UIKit.Label(right, done ? Loc.T("achievements.done") : $"{value}/{a.target}\n+{a.rewardCoins}{(a.rewardGems > 0 ? $" +{a.rewardGems} gems" : "")}", 36, done ? UIKit.Green : UIKit.Dark, TextAnchor.MiddleCenter, false);
            }
        }

        public override bool OnBack() { App.Flow.GoHome(); return true; }
    }

    /// <summary>Daily Treats (7-day cycle, never resets — no pressure) and Mystery Boxes (opened on the server).</summary>
    public class RewardsScreen : UIScreen
    {
        private RectTransform days; private Button claim, open; private Text boxes, status; private bool busy;
        private DailyResponse daily;

        protected override void Build(RectTransform safe)
        {
            var bg = Root.gameObject.AddComponent<Image>(); bg.color = new Color(0.16f, 0.1f, 0.35f, 0.9f); Root.SetAsFirstSibling();
            Header(safe, Loc.T("rewards.title"), () => App.Flow.GoHome());
            var dt = UIKit.At(safe, "DailyTitle", new Vector2(0.5f, 1), new Vector2(1000, 100), new Vector2(0, -230)); UIKit.Label(dt, Loc.T("rewards.daily"), 64, UIKit.Yellow);
            days = UIKit.Rect(safe, "Days", new Vector2(0, 1), new Vector2(1, 1), new Vector2(30, -800), new Vector2(-30, -340));
            var g = days.gameObject.AddComponent<GridLayoutGroup>(); g.cellSize = new Vector2(230, 210); g.spacing = new Vector2(20, 20); g.childAlignment = TextAnchor.MiddleCenter; g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = 4;
            var st = UIKit.At(safe, "Status", new Vector2(0.5f, 1), new Vector2(1000, 90), new Vector2(0, -820)); status = UIKit.Label(st, "", 44, Color.white);
            claim = UIKit.Button(UIKit.At(safe, "Claim", new Vector2(0.5f, 1), new Vector2(600, 170), new Vector2(0, -920)), Loc.T("rewards.claim"), UIKit.Green, Claim, "gift", 64);

            var bt = UIKit.At(safe, "BoxTitle", new Vector2(0.5f, 0), new Vector2(1000, 100), new Vector2(0, 520)); boxes = UIKit.Label(bt, "", 60, UIKit.Yellow);
            UIKit.Image(UIKit.At(safe, "BoxIcon", new Vector2(0.5f, 0), new Vector2(220, 220), new Vector2(0, 280)), Icons.Get("box"));
            open = UIKit.Button(UIKit.At(safe, "Open", new Vector2(0.5f, 0), new Vector2(600, 170), new Vector2(0, 80)), Loc.T("rewards.openBox"), UIKit.Pink, Open, null, 60);
        }

        public override async void OnShow()
        {
            RefreshBoxes();
            UIKit.Clear(days); status.text = ""; claim.interactable = false;
            if (!App.Backend.Online) { status.text = Loc.T("rewards.offline"); DrawDays(App.Content.Runtime.daily_rewards, 0, false); return; }
            var r = await App.Backend.GetDailyAsync();
            if (!r.ok) { status.text = Loc.T(r.offline ? "rewards.offline" : "error.generic"); return; }
            daily = r.data;
            if (!daily.enabled) { status.text = Loc.T("rewards.off"); return; }
            DrawDays(daily.rewards, daily.status.day, daily.status.claimedToday);
            claim.interactable = daily.status.canClaim;
            status.text = daily.status.claimedToday ? Loc.T("rewards.claimed") : daily.status.canClaim ? "" : Loc.T("rewards.later");
        }

        private void DrawDays(List<DailyRewardDef> rewards, int today, bool claimedToday)
        {
            UIKit.Clear(days);
            foreach (var d in rewards)
            {
                var cell = UIKit.Rect(days, "D" + d.day, Vector2.zero, Vector2.one);
                bool past = d.day < today || (d.day == today && claimedToday);
                UIKit.Panel(cell, "Bg", d.day == today && !claimedToday ? UIKit.Yellow : past ? UIKit.Hex("#B8F2E6") : new Color(1, 1, 1, 0.9f), 36);
                UIKit.Label(UIKit.Rect(cell, "Day", new Vector2(0, 0.72f), Vector2.one), Loc.T("rewards.day", d.day), 32, UIKit.Dark, TextAnchor.MiddleCenter, false);
                UIKit.Image(UIKit.Rect(cell, "Icon", new Vector2(0.25f, 0.3f), new Vector2(0.75f, 0.72f)), Icons.Get(past ? "check" : d.type == "coins" ? "coin" : d.type == "gems" ? "gem" : "gift"), past ? UIKit.Green : Color.white);
                UIKit.Label(UIKit.Rect(cell, "Label", Vector2.zero, new Vector2(1, 0.3f)), d.label, 28, UIKit.Dark, TextAnchor.MiddleCenter, false);
            }
        }

        private async void Claim()
        {
            if (busy) return; busy = true;
            var r = await App.Backend.ClaimDailyAsync();
            busy = false;
            if (r.ok)
            {
                App.Audio.Sfx("sfx_box"); App.Audio.Meow("meow_happy", true);
                Graphics.Vfx.Burst("confetti", Camera.main ? Camera.main.transform.position + Camera.main.transform.forward * 4 : Vector3.zero);
                App.UI.Toast(Loc.T("rewards.got", r.data.claim.label));
                AnalyticsService.DailyRewardClaimed(r.data.claim.day);
            }
            else App.UI.Toast(r.offline ? Loc.T("rewards.offline") : Loc.T("rewards.later"));
            OnShow();
        }

        private async void Open()
        {
            if (busy) return;
            if (App.Save.Player.pendingBoxes <= 0) { App.UI.Toast(Loc.T("rewards.noBoxes")); return; }
            if (!App.Backend.Online) { App.UI.Toast(Loc.T("rewards.offline")); return; }
            busy = true;
            var r = await App.Backend.OpenBoxAsync();
            busy = false;
            if (r.ok)
            {
                App.Audio.Sfx("sfx_box"); App.Audio.Meow("meow_excited", true);
                App.UI.Dialog(Loc.T("rewards.got", r.data.reward.label), Loc.T("ok"), null);
                AnalyticsService.MysteryBoxOpened(r.data.reward.type);
            }
            else App.UI.Toast(r.offline ? Loc.T("rewards.offline") : Loc.T("error.generic"));
            RefreshBoxes();
        }

        private void RefreshBoxes()
        {
            int n = App.Save.Player.pendingBoxes;
            boxes.text = n > 0 ? Loc.T("rewards.boxes", n) : Loc.T("rewards.noBoxes");
            open.interactable = n > 0;
        }

        public override bool OnBack() { App.Flow.GoHome(); return true; }
    }

    /// <summary>Music, sound, vibration, graphics quality, language, calm camera, on-screen buttons, how to play, and a parents' area behind a simple grown-up check.</summary>
    public class SettingsScreen : UIScreen
    {
        private RectTransform list;
        protected override void Build(RectTransform safe)
        {
            var bg = Root.gameObject.AddComponent<Image>(); bg.color = new Color(0.16f, 0.1f, 0.35f, 0.92f); Root.SetAsFirstSibling();
            Header(safe, Loc.T("settings.title"), () => App.Flow.GoHome());
            list = UIKit.ScrollList(UIKit.Rect(safe, "List", Vector2.zero, Vector2.one, new Vector2(40, 30), new Vector2(-40, -210)), 22);
        }

        public override void OnShow()
        {
            UIKit.Clear(list);
            var s = App.Save.Player.settings.Clone();
            void Save() { App.Flow.ApplySettings(s); OnShow(); }
            Toggle("music", Loc.T("settings.music"), s.music, v => { s.music = v; Save(); });
            Toggle("sound", Loc.T("settings.sound"), s.sound, v => { s.sound = v; Save(); });
            Toggle("vibrate", Loc.T("settings.vibration"), s.vibration, v => { s.vibration = v; Save(); });
            Toggle("help", Loc.T("settings.reduceMotion"), s.reduceMotion, v => { s.reduceMotion = v; Save(); });
            Toggle("play", Loc.T("settings.buttons"), App.Flow.OnScreenButtons, v => { App.Flow.OnScreenButtons = v; OnShow(); });
            Segmented(Loc.T("settings.speed"), new[] { "slow", "normal", "fast" }, s.gameSpeed ?? "normal", x => Loc.T("speed." + x), v => { s.gameSpeed = v; Save(); });
            Segmented(Loc.T("settings.graphics"), new[] { "auto", "low", "medium", "high", "ultra" }, s.graphics, x => Loc.T("graphics." + x), v => { s.graphics = v; Save(); });
            Segmented(Loc.T("settings.language"), Loc.Languages, s.language, Loc.LanguageName, v => { s.language = v; Save(); App.UI.Toast(Loc.LanguageName(v)); });
            var help = UIKit.Rect(list, "Help", Vector2.zero, Vector2.one); UIKit.Size(help, 150);
            UIKit.Button(help, Loc.T("settings.help"), UIKit.Blue, () => App.UI.Dialog(Loc.T("help.text"), Loc.T("ok"), null), "help", 48);
            var parents = UIKit.Rect(list, "Parents", Vector2.zero, Vector2.one); UIKit.Size(parents, 150);
            UIKit.Button(parents, Loc.T("settings.parents"), UIKit.Grey, ParentsGate, "parent", 48);
            var ver = UIKit.Rect(list, "Version", Vector2.zero, Vector2.one); UIKit.Size(ver, 80);
            UIKit.Label(ver, $"{App.Save.Player.username} · v{Application.version} · {(App.Backend.Online ? "online" : "offline")}", 32, new Color(1, 1, 1, 0.6f));
        }

        private void Toggle(string icon, string label, bool value, System.Action<bool> set)
        {
            var row = UIKit.Rect(list, label, Vector2.zero, Vector2.one); UIKit.Size(row, 140);
            UIKit.Panel(row, "Bg", new Color(1, 1, 1, 0.12f), 36);
            UIKit.Image(UIKit.At(row, "Icon", new Vector2(0, 0.5f), new Vector2(90, 90), new Vector2(30, 0)), Icons.Get(icon));
            UIKit.Label(UIKit.Rect(row, "Label", Vector2.zero, new Vector2(0.65f, 1), new Vector2(150, 0), Vector2.zero), label, 48, Color.white, TextAnchor.MiddleLeft);
            UIKit.Button(UIKit.Rect(row, "Switch", new Vector2(0.7f, 0.15f), new Vector2(0.97f, 0.85f)), value ? "ON" : "OFF", value ? UIKit.Green : UIKit.Grey, () => set(!value), null, 44);
        }

        private void Segmented(string label, string[] values, string current, System.Func<string, string> name, System.Action<string> set)
        {
            var title = UIKit.Rect(list, label, Vector2.zero, Vector2.one); UIKit.Size(title, 80); UIKit.Label(title, label, 46, UIKit.Yellow, TextAnchor.MiddleLeft);
            var row = UIKit.Rect(list, label + "Row", Vector2.zero, Vector2.one); UIKit.Size(row, 120);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>(); h.spacing = 10; h.childControlWidth = h.childControlHeight = true; h.childForceExpandWidth = true;
            foreach (var v in values) { var val = v; UIKit.Button(UIKit.Rect(row, v, Vector2.zero, Vector2.one), name(v), v == current ? UIKit.Pink : UIKit.Purple, () => set(val), null, 34); }
        }

        /// <summary>A simple grown-up check (multiplication) before privacy details and data deletion.</summary>
        private void ParentsGate()
        {
            int a = Random.Range(6, 10), b = Random.Range(6, 10), answer = a * b;
            var options = new List<int> { answer, answer + Random.Range(1, 6), answer - Random.Range(1, 6) }.OrderBy(_ => Random.value).ToList();
            UIKit.Clear(list);
            var q = UIKit.Rect(list, "Q", Vector2.zero, Vector2.one); UIKit.Size(q, 200); UIKit.Label(q, Loc.T("parents.gate", a, b), 54, Color.white);
            foreach (var o in options)
            {
                var rt = UIKit.Rect(list, "A" + o, Vector2.zero, Vector2.one); UIKit.Size(rt, 140); int opt = o;
                UIKit.Button(rt, o.ToString(), UIKit.Blue, () => { if (opt == answer) ParentsArea(); else OnShow(); }, null, 60);
            }
        }

        private void ParentsArea()
        {
            UIKit.Clear(list);
            var t = UIKit.Rect(list, "Title", Vector2.zero, Vector2.one); UIKit.Size(t, 110); UIKit.Label(t, Loc.T("parents.title"), 64, UIKit.Yellow);
            var p = UIKit.Rect(list, "Privacy", Vector2.zero, Vector2.one); UIKit.Size(p, 620);
            UIKit.Panel(p, "Bg", new Color(1, 1, 1, 0.92f), 40);
            var txt = UIKit.Label(p, Loc.T("parents.privacy"), 40, UIKit.Dark, TextAnchor.MiddleLeft, false); ((RectTransform)txt.transform).offsetMin = new Vector2(40, 30); ((RectTransform)txt.transform).offsetMax = new Vector2(-40, -30);
            var del = UIKit.Rect(list, "Delete", Vector2.zero, Vector2.one); UIKit.Size(del, 150);
            UIKit.Button(del, Loc.T("parents.delete"), UIKit.Red, () => App.UI.Dialog(Loc.T("parents.confirm"), Loc.T("yes"), async () =>
            {
                bool ok = await App.Flow.DeleteAllData();
                App.UI.Toast(ok ? Loc.T("parents.deleted") : Loc.T("parents.deleteOffline"));
            }, Loc.T("no")), null, 46);
            var back = UIKit.Rect(list, "Back", Vector2.zero, Vector2.one); UIKit.Size(back, 140);
            UIKit.Button(back, Loc.T("back"), UIKit.Purple, OnShow, "back", 46);
        }

        public override bool OnBack() { App.Flow.GoHome(); return true; }
    }
}
