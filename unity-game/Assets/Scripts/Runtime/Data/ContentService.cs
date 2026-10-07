using System;
using System.Collections.Generic;
using System.Linq;
using TFY.CatAdventure.Domain;
using UnityEngine;

namespace TFY.CatAdventure.Data
{
    /// <summary>
    /// Game content (levels, worlds, shop, achievements…) and Remote Config values.
    /// Order of trust: bundled defaults (always present, offline) → last cached online copy → fresh online copy.
    /// A broken or partial online copy is ignored field by field, never crashing the game.
    /// </summary>
    public class ContentService
    {
        private const string CacheFile = "content_cache.json";
        private const string RuntimeCacheFile = "runtime_cache.json";

        public GameContent Content { get; private set; }
        public RuntimeConfig Runtime { get; private set; } = new RuntimeConfig().Sanitized();
        public event Action Changed;

        public void LoadBundledAndCached()
        {
            var bundled = Resources.Load<TextAsset>("default_content");
            Content = bundled ? Json.Read<GameContent>(bundled.text) : new GameContent();
            var bundledRuntime = Resources.Load<TextAsset>("default_runtime");
            if (bundledRuntime) Runtime = Json.Read<RuntimeConfig>(bundledRuntime.text).Sanitized();

            var cached = LocalFile.Read(CacheFile, out _);
            if (cached != null)
            {
                try { var c = Json.Read<GameContent>(cached); if (c != null && c.version >= Content.version) Merge(c); }
                catch (Exception e) { Debug.LogWarning("[Content] ignoring cached content: " + e.Message); }
            }
            var cachedRt = LocalFile.Read(RuntimeCacheFile, out _);
            if (cachedRt != null) { try { Runtime = Json.Read<RuntimeConfig>(cachedRt).Sanitized(); } catch { /* keep defaults */ } }
            Changed?.Invoke();
        }

        /// <summary>Applies online content. Only well-formed levels/items are taken; everything else keeps the bundled value.</summary>
        public void ApplyOnline(List<LevelDef> levels, List<WorldDef> worlds, List<ShopItem> shop, List<AchievementDef> achievements)
        {
            var incoming = new GameContent { version = Content.version, levels = levels ?? new List<LevelDef>(), worlds = worlds ?? new List<WorldDef>(), shop = shop ?? new List<ShopItem>(), achievements = achievements ?? new List<AchievementDef>() };
            Merge(incoming);
            try { LocalFile.Write(CacheFile, Json.Write(Content)); } catch (Exception e) { Debug.LogWarning("[Content] cache write failed: " + e.Message); }
            Changed?.Invoke();
        }

        public void ApplyRuntime(RuntimeConfig rc)
        {
            if (rc == null) return;
            Runtime = rc.Sanitized();
            if (Runtime.daily_rewards == null || Runtime.daily_rewards.Count == 0) Runtime.daily_rewards = Content.dailyRewards;
            try { LocalFile.Write(RuntimeCacheFile, Json.Write(Runtime)); } catch { /* not critical */ }
            Changed?.Invoke();
        }

        private void Merge(GameContent c)
        {
            bool LevelOk(LevelDef l) => l != null && l.number > 0 && l.sections != null && l.sections.Count >= 3 && Content.World(l.world) != null
                                        && l.sections.All(s => s != null && s.length > 0 && (s.obstacles ?? new List<string>()).All(o => Content.Obstacle(o) != null));
            foreach (var w in c.worlds.Where(w => w != null && !string.IsNullOrEmpty(w.id) && w.palette != null)) Upsert(Content.worlds, w, x => x.id == w.id);
            foreach (var l in c.levels.Where(LevelOk)) Upsert(Content.levels, l, x => x.id == l.id);
            foreach (var i in c.shop.Where(i => i != null && !string.IsNullOrEmpty(i.id) && !string.IsNullOrEmpty(i.slot))) Upsert(Content.shop, i, x => x.id == i.id);
            foreach (var a in c.achievements.Where(a => a != null && !string.IsNullOrEmpty(a.id))) Upsert(Content.achievements, a, x => x.id == a.id);
            Content.levels.Sort((a, b) => a.number.CompareTo(b.number));
            Content.worlds.Sort((a, b) => a.order.CompareTo(b.order));
        }

        private static void Upsert<T>(List<T> list, T item, Predicate<T> match)
        {
            int i = list.FindIndex(match);
            if (i >= 0) list[i] = item; else list.Add(item);
        }

        public List<LevelDef> PlayableLevels => Content.levels.Where(l => l.enabled && Content.World(l.world)?.enabled == true).ToList();
        public string RandomTip() => Content.tips.Count > 0 ? Content.tips[UnityEngine.Random.Range(0, Content.tips.Count)] : "";
    }
}
