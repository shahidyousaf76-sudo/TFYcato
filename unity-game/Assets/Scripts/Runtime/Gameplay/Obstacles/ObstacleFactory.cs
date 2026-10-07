using System.Collections.Generic;
using TFY.CatAdventure.Domain;
using TFY.CatAdventure.Graphics;
using UnityEngine;

namespace TFY.CatAdventure.Gameplay
{
    /// <summary>Object pool per obstacle id + world — nothing is instantiated or destroyed during a run after warm-up.</summary>
    public class ObstaclePool
    {
        private readonly System.Func<ObstacleView> create;
        private readonly Stack<ObstacleView> free = new Stack<ObstacleView>();
        public ObstaclePool(System.Func<ObstacleView> create, int prewarm) { this.create = create; for (int i = 0; i < prewarm; i++) { var o = Create(); o.gameObject.SetActive(false); free.Push(o); } }
        private ObstacleView Create() { var o = create(); o.pool = this; return o; }
        public ObstacleView Get() => free.Count > 0 ? free.Pop() : Create();
        public void Return(ObstacleView o) { o.gameObject.SetActive(false); free.Push(o); }
    }

    /// <summary>
    /// Builds child-friendly, colourful obstacle models, themed per world (e.g. the moving obstacle is a bouncy ball in the park,
    /// a hedgehog in the forest, a toy car in the city, a snowball in the mountains and a crab at the beach). Never scary.
    /// </summary>
    public class ObstacleFactory
    {
        private readonly Transform container;
        private readonly Dictionary<string, ObstaclePool> pools = new Dictionary<string, ObstaclePool>();
        private string world = "park";

        public ObstacleFactory(Transform container) { this.container = container; }

        public void SetWorld(string worldId) { world = worldId; }

        public ObstacleView Spawn(ObstacleDef def, int lane, float planZ, float phase)
        {
            string key = def.id + "@" + world;
            if (!pools.TryGetValue(key, out var pool)) { var w = world; pools[key] = pool = new ObstaclePool(() => Build(def, w), 3); }
            var o = pool.Get(); o.Spawn(def, lane, planZ, phase); return o;
        }

        private ObstacleView Build(ObstacleDef def, string w)
        {
            var go = new GameObject($"{def.id}_{w}");
            go.transform.SetParent(container, false);
            ObstacleView view;
            switch (def.id)
            {
                case "log": view = go.AddComponent<ObstacleView>(); Log(go.transform, w); break;
                case "rock": view = go.AddComponent<ObstacleView>(); Rock(go.transform, w, 0.8f); break;
                case "branch": view = go.AddComponent<ObstacleView>(); Branch(go.transform, w); break;
                case "barrel": view = go.AddComponent<ObstacleView>(); Barrel(go.transform, w); break;
                case "wall": view = go.AddComponent<ObstacleView>(); Wall(go.transform, w); break;
                case "broken_bridge": view = go.AddComponent<BrokenBridgeView>(); Bridge(go.transform, w, def.depth); break;
                case "falling_rocks": view = FallingRock(go, w); break;
                case "moving_critter": view = Critter(go, w); break;
                default: view = go.AddComponent<ObstacleView>(); Rock(go.transform, w, 0.8f); break;
            }
            view.def = def;
            Shapes.SetLayerAndShadows(go, true);
            go.SetActive(false);
            return view;
        }

        // ------------------------------------------------------------------ models
        private static Material M(string hex, float s = 0.3f, float metal = 0) => MaterialLibrary.Lit(hex, s, metal, true);

        private static void Log(Transform t, string w)
        {
            if (w == "city")
            {   // low striped barrier pole on two feet
                var red = M("#E63946", 0.5f); var white = M("#F1FAEE", 0.5f);
                for (int i = 0; i < 6; i++) Shapes.Part(t, PrimitiveType.Cylinder, i % 2 == 0 ? red : white, new Vector3(-0.9f + i * 0.36f, 0.5f, 0), new Vector3(0.22f, 0.18f, 0.22f), new Vector3(0, 0, 90));
                Shapes.Part(t, PrimitiveType.Cube, M("#FFB703"), new Vector3(-1f, 0.25f, 0), new Vector3(0.2f, 0.5f, 0.5f)); Shapes.Part(t, PrimitiveType.Cube, M("#FFB703"), new Vector3(1f, 0.25f, 0), new Vector3(0.2f, 0.5f, 0.5f));
                return;
            }
            var bark = w == "beach" ? M("#C8B79E", 0.2f) : M("#8B5A2B", 0.2f);
            Shapes.Part(t, PrimitiveType.Cylinder, bark, new Vector3(0, 0.33f, 0), new Vector3(0.66f, 1f, 0.66f), new Vector3(0, 0, 90));
            Shapes.Part(t, PrimitiveType.Cylinder, M("#E9C98B", 0.4f), new Vector3(1.0f, 0.33f, 0), new Vector3(0.56f, 0.02f, 0.56f), new Vector3(0, 0, 90));
            Shapes.Part(t, PrimitiveType.Cylinder, M("#E9C98B", 0.4f), new Vector3(-1.0f, 0.33f, 0), new Vector3(0.56f, 0.02f, 0.56f), new Vector3(0, 0, 90));
            Shapes.Part(t, PrimitiveType.Sphere, M(w == "snow" ? "#FFFFFF" : "#7BD389", w == "snow" ? 0.5f : 0.3f), new Vector3(0.3f, 0.66f, 0), w == "snow" ? new Vector3(1.6f, 0.14f, 0.5f) : new Vector3(0.25f, 0.12f, 0.25f)); // moss tuft, or snow on top
        }

        private static void Rock(Transform t, string w, float h)
        {
            string col = w == "beach" ? "#D9B98B" : w == "mountains" ? "#8D99AE" : w == "snow" ? "#B8C4D0" : w == "city" ? "#A8A8A8" : "#9A8F85";
            Shapes.Part(t, PrimitiveType.Sphere, M(col, 0.25f), new Vector3(0, h * 0.5f, 0), new Vector3(1.2f, h * 1.1f, 1.0f));
            Shapes.Part(t, PrimitiveType.Sphere, M(col, 0.25f), new Vector3(0.35f, h * 0.35f, 0.15f), new Vector3(0.6f, h * 0.7f, 0.6f));
            if (w == "mountains" || w == "snow") Shapes.Part(t, PrimitiveType.Sphere, M("#FFFFFF", 0.6f), new Vector3(0, h * 0.95f, 0), new Vector3(0.9f, 0.2f, 0.7f));
        }

        private static void Branch(Transform t, string w)
        {
            // Two posts holding a leafy bar: underside at 1.0 m (slide under), top at 1.35 m (jump over).
            var post = w == "city" ? M("#FFB703", 0.5f) : M("#6E4B2A", 0.25f);
            Shapes.Part(t, PrimitiveType.Cylinder, post, new Vector3(-1.05f, 0.6f, 0), new Vector3(0.14f, 0.6f, 0.14f));
            Shapes.Part(t, PrimitiveType.Cylinder, post, new Vector3(1.05f, 0.6f, 0), new Vector3(0.14f, 0.6f, 0.14f));
            if (w == "city")
            {
                for (int i = 0; i < 5; i++) Shapes.Part(t, PrimitiveType.Cube, i % 2 == 0 ? M("#E63946", 0.5f) : M("#FFFFFF", 0.5f), new Vector3(-0.85f + i * 0.42f, 1.17f, 0), new Vector3(0.42f, 0.3f, 0.12f));
                return;
            }
            Shapes.Part(t, PrimitiveType.Cylinder, M(w == "beach" ? "#A0673A" : "#7A5230", 0.2f), new Vector3(0, 1.17f, 0), new Vector3(0.28f, 1.15f, 0.28f), new Vector3(0, 0, 90));
            var leaf = M(w == "beach" ? "#2A9D8F" : w == "snow" ? "#F4FAFF" : "#52B788", 0.3f);
            for (int i = 0; i < 5; i++) Shapes.Part(t, PrimitiveType.Sphere, leaf, new Vector3(-0.9f + i * 0.45f, 1.3f, 0), new Vector3(0.45f, 0.18f, 0.35f));
        }

        private static void Barrel(Transform t, string w)
        {
            bool city = w == "city";
            if (w == "snow") Shapes.Part(t, PrimitiveType.Sphere, M("#FFFFFF", 0.5f), new Vector3(0, 1.33f, 0), new Vector3(0.95f, 0.25f, 0.95f)); // snow cap
            var main = city ? M("#FB8500", 0.45f) : M("#9C6644", 0.25f);
            var band = city ? M("#FFFFFF", 0.45f) : M("#6C757D", 0.7f, 0.8f);
            Shapes.Part(t, PrimitiveType.Cylinder, main, new Vector3(0, 0.65f, 0), new Vector3(0.9f, 0.65f, 0.9f));
            Shapes.Part(t, PrimitiveType.Cylinder, band, new Vector3(0, 0.35f, 0), new Vector3(0.93f, 0.05f, 0.93f));
            Shapes.Part(t, PrimitiveType.Cylinder, band, new Vector3(0, 0.95f, 0), new Vector3(0.93f, 0.05f, 0.93f));
            // A soft glowing ring makes barrels easy to spot from far away.
            Shapes.Part(t, Shapes.Torus(), MaterialLibrary.Glow(new Color(1f, 0.85f, 0.3f), 1.6f), new Vector3(0, 1.32f, 0), new Vector3(1.1f, 1f, 1.1f));
        }

        private static void Wall(Transform t, string w)
        {
            // Tall (2.8 m) and bright so it is visible from a distance. One lane wide — there is always a way around.
            string[] cols = w == "park" ? new[] { "#2D6A4F", "#40916C" } : w == "forest" ? new[] { "#7F4F24", "#936639" } :
                            w == "city" ? new[] { "#E76F51", "#F4A261" } : w == "beach" ? new[] { "#E9C46A", "#F4D58D" } : w == "snow" ? new[] { "#A9DCF7", "#D6F0FF" } : new[] { "#6C757D", "#ADB5BD" }; // snow: ice blocks
            for (int row = 0; row < 4; row++) for (int col = 0; col < 3; col++)
            {
                float off = row % 2 == 0 ? 0 : 0.33f;
                float x = -0.66f + col * 0.66f + off; if (x > 0.8f) x -= 1.98f;
                Shapes.Part(t, PrimitiveType.Cube, M(cols[(row + col) % 2], 0.25f), new Vector3(x, 0.35f + row * 0.7f, 0), new Vector3(0.64f, 0.66f, 0.8f));
            }
            Shapes.Part(t, PrimitiveType.Cube, MaterialLibrary.Glow(new Color(1f, 0.95f, 0.5f), 1.4f), new Vector3(0, 2.85f, 0), new Vector3(2f, 0.1f, 0.85f)); // bright top edge
        }

        private static void Bridge(Transform t, string w, float depth)
        {
            // Planks up to the gap, broken dangling boards, cracks, and rope rails. The gap itself is empty.
            var wood = new Material(M("#A47148", 0.25f)) { mainTexture = ProceduralTextures.Cracks(64, 5) };
            wood.SetTexture("_BaseMap", wood.mainTexture);
            var rail = M("#D4A373", 0.3f);
            for (int i = 0; i < 3; i++) Shapes.Part(t, PrimitiveType.Cube, wood, new Vector3(0, -0.05f, depth * 0.5f + 0.4f + i * 0.9f), new Vector3(6.8f, 0.1f, 0.8f), new Vector3(i * 2f, 0, 0));
            for (int i = 0; i < 3; i++) Shapes.Part(t, PrimitiveType.Cube, wood, new Vector3(-2.2f + i * 2.2f, -0.6f, depth * 0.5f - 0.3f), new Vector3(1.4f, 0.1f, 0.9f), new Vector3(55 + i * 10, 0, 0));
            for (int i = 0; i < 2; i++) Shapes.Part(t, PrimitiveType.Cube, wood, new Vector3(-1.5f + i * 3f, -0.5f, -depth * 0.5f + 0.3f), new Vector3(1.5f, 0.1f, 0.9f), new Vector3(-50, 0, 0));
            for (int s = -1; s <= 1; s += 2)
            {
                Shapes.Part(t, PrimitiveType.Cylinder, rail, new Vector3(s * 3.6f, 0.6f, depth * 0.5f + 1f), new Vector3(0.12f, 0.6f, 0.12f));
                Shapes.Part(t, PrimitiveType.Cylinder, rail, new Vector3(s * 3.6f, 0.6f, -depth * 0.5f - 1f), new Vector3(0.12f, 0.6f, 0.12f));
                Shapes.Part(t, PrimitiveType.Cylinder, M("#F4E3C1", 0.2f), new Vector3(s * 3.6f, 0.9f, 0), new Vector3(0.04f, depth * 0.5f + 1f, 0.04f), new Vector3(90, 0, 0));
            }
            // Water / chasm below so the gap reads clearly.
            string below = w == "mountains" || w == "forest" ? "#5DADE2" : w == "beach" ? "#1FB5D6" : w == "snow" ? "#9ADCF5" : "#4895EF";
            Shapes.Part(t, PrimitiveType.Cube, MaterialLibrary.Lit(below, 0.95f), new Vector3(0, -3f, 0), new Vector3(9f, 0.1f, depth));
        }

        private static ObstacleView FallingRock(GameObject go, string w)
        {
            var v = go.AddComponent<FallingRockView>();
            bool beach = w == "beach";
            var rock = Shapes.Group(go.transform, "Rock").transform;
            if (beach) { Shapes.Part(rock, PrimitiveType.Sphere, M("#6F4E37", 0.3f), Vector3.zero, Vector3.one * 0.95f); Shapes.Part(rock, PrimitiveType.Sphere, M("#3E2723", 0.3f), new Vector3(0, 0.3f, -0.3f), Vector3.one * 0.18f); }
            else if (w == "snow") Shapes.Part(rock, PrimitiveType.Sphere, M("#FFFFFF", 0.55f), Vector3.zero, Vector3.one * 1.1f); // a big snowball
            else Rock(rock, w, 1.0f);
            v.rock = rock;
            // Overhang (cliff edge or palm) above the lane — the source of the rock is always visible.
            var cliff = Shapes.Group(go.transform, "Source", new Vector3(0, 7.8f, 0)).transform;
            if (beach) { Shapes.Part(cliff, PrimitiveType.Sphere, M("#2A9D8F"), Vector3.zero, new Vector3(2.4f, 0.4f, 1.6f)); }
            else Shapes.Part(cliff, PrimitiveType.Cube, M(w == "mountains" ? "#6C757D" : "#8D6E63", 0.2f), Vector3.zero, new Vector3(2.2f, 0.5f, 1.6f));
            var shadow = Shapes.Part(go.transform, PrimitiveType.Cylinder, MaterialLibrary.Particle(new Color(0, 0, 0, 0.45f)), new Vector3(0, 0.02f, 0), new Vector3(0.5f, 0.005f, 0.5f), default, "Shadow");
            shadow.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            v.shadow = shadow.transform;
            v.dust = Vfx.DustPuff(go.transform, new Vector3(0, 7.4f, 0), beach ? new Color(0.35f, 0.7f, 0.3f) : new Color(0.75f, 0.7f, 0.62f));
            return v;
        }

        private static ObstacleView Critter(GameObject go, string w)
        {
            var v = go.AddComponent<MovingCritterView>();
            var body = Shapes.Group(go.transform, "Critter").transform; v.body = body;
            var eyeW = MaterialLibrary.Lit("#FFFFFF", 0.8f); var eyeB = MaterialLibrary.Lit("#111111", 0.9f);
            void Eyes(float y, float z, float spread) { for (int s = -1; s <= 1; s += 2) { Shapes.Part(body, PrimitiveType.Sphere, eyeW, new Vector3(s * spread, y, z), Vector3.one * 0.16f); Shapes.Part(body, PrimitiveType.Sphere, eyeB, new Vector3(s * spread, y, z - 0.06f), Vector3.one * 0.08f); } }
            switch (w)
            {
                case "beach": // crab
                    Shapes.Part(body, PrimitiveType.Sphere, M("#FF6F59", 0.4f), new Vector3(0, 0.35f, 0), new Vector3(0.8f, 0.4f, 0.6f));
                    for (int s = -1; s <= 1; s += 2) { Shapes.Part(body, PrimitiveType.Sphere, M("#FF6F59", 0.4f), new Vector3(s * 0.55f, 0.5f, -0.2f), new Vector3(0.3f, 0.22f, 0.22f)); for (int k = 0; k < 3; k++) Shapes.Part(body, PrimitiveType.Cylinder, M("#E85D4A"), new Vector3(s * 0.4f, 0.15f, -0.15f + k * 0.15f), new Vector3(0.05f, 0.15f, 0.05f), new Vector3(0, 0, s * 50)); }
                    Eyes(0.65f, -0.2f, 0.12f); break;
                case "forest": // hedgehog
                    Shapes.Part(body, PrimitiveType.Sphere, M("#8D6E63", 0.2f), new Vector3(0, 0.4f, 0), new Vector3(0.75f, 0.6f, 0.9f));
                    for (int k = 0; k < 10; k++) Shapes.Part(body, Shapes.Cone(), M("#5D4037", 0.2f), new Vector3(Mathf.Cos(k) * 0.25f, 0.6f + Mathf.Sin(k * 1.3f) * 0.1f, -0.1f + (k % 5) * 0.1f), new Vector3(0.1f, 0.25f, 0.1f), new Vector3(-20 + k * 4, 0, Mathf.Cos(k) * 30));
                    Shapes.Part(body, PrimitiveType.Sphere, M("#FFCCBC"), new Vector3(0, 0.35f, -0.45f), new Vector3(0.35f, 0.3f, 0.3f)); Eyes(0.45f, -0.55f, 0.08f); break;
                case "city": // toy car
                    Shapes.Part(body, PrimitiveType.Cube, M("#3A86FF", 0.7f, 0.3f), new Vector3(0, 0.35f, 0), new Vector3(0.7f, 0.35f, 1.0f));
                    Shapes.Part(body, PrimitiveType.Cube, MaterialLibrary.Transparent(new Color(0.8f, 0.95f, 1f, 0.6f)), new Vector3(0, 0.62f, 0.05f), new Vector3(0.6f, 0.25f, 0.55f));
                    for (int i = 0; i < 4; i++) Shapes.Part(body, PrimitiveType.Cylinder, M("#222222", 0.5f), new Vector3(i < 2 ? -0.38f : 0.38f, 0.16f, i % 2 == 0 ? -0.32f : 0.32f), new Vector3(0.3f, 0.06f, 0.3f), new Vector3(0, 0, 90));
                    Shapes.Part(body, PrimitiveType.Sphere, MaterialLibrary.Glow(new Color(1f, 0.95f, 0.6f), 2f), new Vector3(0, 0.4f, -0.52f), Vector3.one * 0.12f); break;
                case "snow": // penguin
                    Shapes.Part(body, PrimitiveType.Sphere, M("#1E2230", 0.5f), new Vector3(0, 0.5f, 0), new Vector3(0.62f, 0.9f, 0.55f)); Shapes.Part(body, PrimitiveType.Sphere, M("#FFFFFF", 0.5f), new Vector3(0, 0.45f, -0.12f), new Vector3(0.45f, 0.7f, 0.4f));
                    Shapes.Part(body, Shapes.Cone(), M("#FFA62B", 0.5f), new Vector3(0, 0.72f, -0.26f), new Vector3(0.14f, 0.22f, 0.14f), new Vector3(-90, 0, 0));
                    for (int s = -1; s <= 1; s += 2) { Shapes.Part(body, PrimitiveType.Sphere, M("#FFA62B", 0.5f), new Vector3(s * 0.15f, 0.05f, -0.1f), new Vector3(0.22f, 0.08f, 0.3f)); Shapes.Part(body, PrimitiveType.Sphere, M("#1E2230", 0.5f), new Vector3(s * 0.34f, 0.5f, 0), new Vector3(0.12f, 0.5f, 0.25f), new Vector3(0, 0, s * 23)); }
                    Eyes(0.8f, -0.25f, 0.1f); break;
                case "mountains": // rolling snowball with a smile
                    Shapes.Part(body, PrimitiveType.Sphere, M("#F8F9FA", 0.5f), new Vector3(0, 0.45f, 0), Vector3.one * 0.85f); Eyes(0.6f, -0.38f, 0.14f); break;
                default: // park: bouncy ball
                    Shapes.Part(body, PrimitiveType.Sphere, M("#FF4D6D", 0.6f), new Vector3(0, 0.42f, 0), Vector3.one * 0.8f);
                    Shapes.Part(body, Shapes.Torus(), M("#FFD166", 0.6f), new Vector3(0, 0.42f, 0), new Vector3(1.0f, 1f, 1.0f), new Vector3(90, 0, 0)); break;
            }
            // Dotted path across all three lanes shows exactly where it walks.
            var dot = MaterialLibrary.Lit("#FFFFFF", 0.3f);
            for (int i = -6; i <= 6; i++) Shapes.Part(go.transform, PrimitiveType.Cylinder, dot, new Vector3(i * 0.45f, 0.02f, 0), new Vector3(0.18f, 0.005f, 0.18f), default, "PathDot");
            return v;
        }
    }
}
