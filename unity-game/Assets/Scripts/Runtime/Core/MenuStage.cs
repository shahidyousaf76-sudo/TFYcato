using System.Linq;
using TFY.CatAdventure.Domain;
using TFY.CatAdventure.Gameplay;
using UnityEngine;

namespace TFY.CatAdventure
{
    /// <summary>
    /// The 3D cat behind the menus: idle/happy on the Home screen (in the world of the child's current level, soft depth of
    /// field), and a close-up in the Cat Shop where items can be previewed before unlocking.
    /// </summary>
    public class MenuStage
    {
        private readonly App app;
        private CatRig rig;
        private ShopItem previewItem;
        private string builtKey;

        public MenuStage(App app) { this.app = app; }

        public CatRig Rig => rig;

        public void RebuildCat() => Build(app.Save.Player, force: true);

        private void Build(PlayerData p, bool force = false)
        {
            string key = p.selectedCat + string.Join(",", p.equipped.Values);
            if (!force && rig && key == builtKey) return;
            if (rig) Object.Destroy(rig.gameObject);
            var cat = app.Content.Content.Item(p.selectedCat) ?? app.Content.Content.Item("orange_cat");
            rig = CatRig.Build(app.Cat.transform, cat, p, app.Content.Content);
            app.Cat.anim.rig = rig;
            builtKey = key;
        }

        public void ShowMenu()
        {
            var p = app.Save.Player;
            var level = app.Content.Content.Level(Mathf.Min(p.currentLevel, app.Content.Content.LastEnabledLevel)) ?? app.Content.Content.levels.First();
            var world = app.Content.Content.World(level.world);
            app.Run.Stop();
            app.Environment.Build(world, level.seed, _ => true);
            app.Environment.Tick(0);
            app.Sky.Apply(world, level.timeOfDay == "night" ? "evening" : "morning", app.Quality.Current.fog);
            app.Post.SetMenu(true);
            Build(p);
            app.Cat.ResetTo(0, 1);
            app.Cat.transform.localPosition = Vector3.zero;
            app.Cat.SetMenuPose(Random.value < 0.5f ? CatAnim.Idle : CatAnim.Happy);
            app.CameraRig.SetMode(CameraMode.Menu);
        }

        public void ShowShop()
        {
            ShowMenu();
            app.CameraRig.SetMode(CameraMode.Shop);
            app.Cat.SetMenuPose(CatAnim.Happy);
        }

        /// <summary>Shows an item on the cat without owning it (try before you unlock).</summary>
        public void Preview(ShopItem item)
        {
            previewItem = item;
            var p = app.Save.Player.Clone();
            if (item.IsCat) p.selectedCat = item.id; else p.equipped[item.slot] = item.id;
            p.unlockedCats[p.selectedCat] = true; // preview only
            Build(p, force: true);
            app.Cat.SetMenuPose(CatAnim.Dancing);
            app.Audio.Meow("meow_happy");
        }

        public void ClearPreview()
        {
            if (previewItem == null) return;
            previewItem = null; Build(app.Save.Player, force: true);
        }
    }
}
