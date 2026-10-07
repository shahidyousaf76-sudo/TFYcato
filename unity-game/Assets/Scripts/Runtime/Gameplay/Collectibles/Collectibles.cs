using System.Collections.Generic;
using TFY.CatAdventure.Domain;
using TFY.CatAdventure.Graphics;
using UnityEngine;

namespace TFY.CatAdventure.Gameplay
{
    /// <summary>A coin, fish, gem, big star, mystery box or power-up bubble: spins, bobs, glows; picked up by proximity; coins fly to the cat with the magnet.</summary>
    public class CollectibleView : MonoBehaviour
    {
        public PlacementKind kind;
        public string id;          // power-up id
        public int lane;
        public float planZ, baseY;
        public bool Magnetised { get; set; }
        public System.Action<CollectibleView> Released;
        private float spin;

        public void Spawn(PlacementKind k, string id, int lane, float planZ, float y)
        {
            kind = k; this.id = id; this.lane = lane; this.planZ = planZ; baseY = y; Magnetised = false;
            spin = kind == PlacementKind.Coin ? 220f : kind == PlacementKind.Fish ? 0f : 90f;
            transform.localPosition = new Vector3((lane - 1) * LevelPlanner.LaneWidth, y, -planZ);
            transform.localRotation = Quaternion.identity;
            gameObject.SetActive(true);
        }

        public void Tick(float dt, Vector3 catLocal, float magnetRadius)
        {
            var p = transform.localPosition;
            if (kind == PlacementKind.Coin && magnetRadius > 0 && !Magnetised && (p - catLocal).magnitude < magnetRadius && p.z < catLocal.z + 1f) Magnetised = true;
            if (Magnetised) { transform.localPosition = Vector3.Lerp(p, catLocal + Vector3.up * 0.7f, 1f - Mathf.Exp(-10f * dt)); return; }
            if (kind == PlacementKind.Fish) transform.localRotation = Quaternion.Euler(0, 90, Mathf.Sin(Time.time * 6f + p.z) * 15f);
            else transform.Rotate(0, spin * dt, 0, Space.Self);
            p.y = baseY + Mathf.Sin(Time.time * 3f + p.z * 0.5f) * 0.1f;
            transform.localPosition = p;
        }

        public bool InReach(Vector3 catLocal, bool sliding)
        {
            var p = transform.localPosition;
            float catCentre = catLocal.y + (sliding ? 0.3f : 0.6f);
            return Mathf.Abs(p.z - catLocal.z) < 0.9f && Mathf.Abs(p.x - catLocal.x) < 1.0f && Mathf.Abs(p.y - catCentre) < 1.1f
                   || Magnetised && (p - (catLocal + Vector3.up * 0.7f)).sqrMagnitude < 0.3f;
        }

        public void Release() { gameObject.SetActive(false); Released?.Invoke(this); }
    }

    /// <summary>Pools and builds collectible models.</summary>
    public class CollectibleFactory
    {
        private readonly Transform container;
        private readonly Dictionary<string, Stack<CollectibleView>> pools = new Dictionary<string, Stack<CollectibleView>>();
        private readonly Data.ContentService contentService;
        private GameContent content => contentService.Content;

        public CollectibleFactory(Transform container, Data.ContentService content) { this.container = container; contentService = content; }

        public CollectibleView Spawn(Placement p)
        {
            string key = p.kind == PlacementKind.PowerUp ? "pu_" + p.id : p.kind.ToString();
            if (!pools.TryGetValue(key, out var stack)) pools[key] = stack = new Stack<CollectibleView>();
            var v = stack.Count > 0 ? stack.Pop() : Build(p.kind, p.id);
            v.Released = x => stack.Push(x);
            v.Spawn(p.kind, p.id, p.lane, p.z, p.y);
            return v;
        }

        public void Prewarm(int coins) { for (int i = 0; i < coins; i++) { var v = Build(PlacementKind.Coin, "coin"); if (!pools.TryGetValue("Coin", out var s)) pools["Coin"] = s = new Stack<CollectibleView>(); s.Push(v); } }

        private CollectibleView Build(PlacementKind kind, string id)
        {
            var go = new GameObject(kind.ToString()); go.transform.SetParent(container, false);
            var v = go.AddComponent<CollectibleView>();
            switch (kind)
            {
                case PlacementKind.Coin:
                    // Metallic, glossy gold with an HDR rim: blooms nicely, reads as "treasure".
                    Shapes.Part(go.transform, PrimitiveType.Cylinder, MaterialLibrary.Glow(new Color(1f, 0.78f, 0.15f), 0.6f, 0.92f, 1f), Vector3.zero, new Vector3(0.55f, 0.05f, 0.55f), new Vector3(90, 0, 0));
                    Shapes.Part(go.transform, Shapes.Star(), MaterialLibrary.Glow(new Color(1f, 0.93f, 0.5f), 1.2f, 0.9f, 1f), new Vector3(0, 0, 0), new Vector3(0.45f, 0.45f, 0.62f));
                    break;
                case PlacementKind.Fish:
                    Shapes.Part(go.transform, Shapes.Fish(), MaterialLibrary.Glow(new Color(0.35f, 0.75f, 1f), 0.8f, 0.85f, 0.2f), Vector3.zero, Vector3.one * 1.1f);
                    Shapes.Part(go.transform, PrimitiveType.Sphere, MaterialLibrary.Lit("#FFFFFF", 0.9f), new Vector3(0.28f, 0.05f, 0.07f), Vector3.one * 0.08f);
                    break;
                case PlacementKind.Gem:
                    Shapes.Part(go.transform, Shapes.Gem(), MaterialLibrary.Glow(new Color(0.75f, 0.3f, 1f), 1.8f, 0.95f, 0.1f), Vector3.zero, Vector3.one * 0.75f);
                    break;
                case PlacementKind.StarToken:
                    Shapes.Part(go.transform, Shapes.Star(), MaterialLibrary.Glow(new Color(1f, 0.85f, 0.2f), 2.2f, 0.9f, 0.6f), Vector3.zero, Vector3.one * 1.3f);
                    break;
                case PlacementKind.MysteryBox:
                    Shapes.Part(go.transform, PrimitiveType.Cube, MaterialLibrary.Lit("#8E44AD", 0.5f), Vector3.zero, Vector3.one * 0.7f);
                    Shapes.Part(go.transform, PrimitiveType.Cube, MaterialLibrary.Glow(new Color(1f, 0.85f, 0.3f), 1.5f), Vector3.zero, new Vector3(0.72f, 0.72f, 0.14f));
                    Shapes.Part(go.transform, PrimitiveType.Cube, MaterialLibrary.Glow(new Color(1f, 0.85f, 0.3f), 1.5f), Vector3.zero, new Vector3(0.14f, 0.72f, 0.72f));
                    Shapes.Part(go.transform, Shapes.Torus(), MaterialLibrary.Glow(new Color(1f, 0.85f, 0.3f), 1.5f), new Vector3(0, 0.42f, 0), new Vector3(0.4f, 0.6f, 0.4f), new Vector3(90, 0, 0));
                    break;
                case PlacementKind.PowerUp:
                    var def = content.PowerUp(id);
                    var c = def != null ? MaterialLibrary.Hex(def.color) : Color.cyan;
                    Shapes.Part(go.transform, PrimitiveType.Sphere, MaterialLibrary.Transparent(new Color(c.r, c.g, c.b, 0.35f)), Vector3.zero, Vector3.one * 1.1f);
                    var icon = MaterialLibrary.Glow(c, 1.8f);
                    switch (id)
                    {
                        case "magnet": Shapes.Part(go.transform, Shapes.Torus(), icon, Vector3.zero, new Vector3(0.8f, 0.9f, 0.8f), new Vector3(90, 0, 0)); break;
                        case "shield": Shapes.Part(go.transform, Shapes.Heart(), icon, Vector3.zero, Vector3.one * 0.7f); break;
                        case "super_jump": Shapes.Part(go.transform, Shapes.Cone(), icon, new Vector3(0, -0.2f, 0), new Vector3(0.45f, 0.5f, 0.45f)); break;
                        case "speed_boost": Shapes.Part(go.transform, PrimitiveType.Cube, icon, Vector3.zero, new Vector3(0.12f, 0.6f, 0.12f), new Vector3(0, 0, 25)); Shapes.Part(go.transform, PrimitiveType.Cube, icon, new Vector3(0.12f, 0.1f, 0), new Vector3(0.12f, 0.4f, 0.12f), new Vector3(0, 0, -35)); break;
                        default: Shapes.Part(go.transform, PrimitiveType.Cylinder, icon, new Vector3(-0.12f, 0, 0), new Vector3(0.4f, 0.04f, 0.4f), new Vector3(90, 0, 0)); Shapes.Part(go.transform, PrimitiveType.Cylinder, icon, new Vector3(0.12f, 0, 0.05f), new Vector3(0.4f, 0.04f, 0.4f), new Vector3(90, 0, 0)); break;
                    }
                    break;
            }
            Shapes.SetLayerAndShadows(go, kind != PlacementKind.Coin, false);
            go.SetActive(false);
            return v;
        }
    }

    /// <summary>Checkpoint flags, the finish arch and the long-jump arrows before bridges.</summary>
    public static class TrackPieces
    {
        public static GameObject Checkpoint(Transform parent, float planZ)
        {
            var g = Shapes.Group(parent, "Checkpoint", new Vector3(0, 0, -planZ));
            for (int s = -1; s <= 1; s += 2)
            {
                Shapes.Part(g.transform, PrimitiveType.Cylinder, MaterialLibrary.Lit("#FFFFFF", 0.6f, 0.5f), new Vector3(s * 3.8f, 1.6f, 0), new Vector3(0.12f, 1.6f, 0.12f));
                Shapes.Part(g.transform, PrimitiveType.Cube, MaterialLibrary.Lit("#06D6A0", 0.4f), new Vector3(s * 3.8f + s * 0.45f, 2.8f, 0), new Vector3(0.8f, 0.5f, 0.04f), new Vector3(0, 0, 0), "Flag");
                Shapes.Part(g.transform, PrimitiveType.Sphere, MaterialLibrary.Glow(new Color(0.3f, 1f, 0.6f), 2.5f), new Vector3(s * 3.8f, 3.3f, 0), Vector3.one * 0.25f);
            }
            Shapes.Part(g.transform, PrimitiveType.Cube, MaterialLibrary.Glow(new Color(0.3f, 1f, 0.6f), 1.2f), new Vector3(0, 0.02f, 0), new Vector3(7f, 0.02f, 0.3f));
            Shapes.SetLayerAndShadows(g, true);
            return g;
        }

        public static GameObject Finish(Transform parent, float planZ)
        {
            var g = Shapes.Group(parent, "Finish", new Vector3(0, 0, -planZ));
            var red = MaterialLibrary.Lit("#FF4D6D", 0.5f); var white = MaterialLibrary.Lit("#FFFFFF", 0.5f);
            for (int s = -1; s <= 1; s += 2) Shapes.Part(g.transform, PrimitiveType.Cylinder, red, new Vector3(s * 4f, 2.5f, 0), new Vector3(0.4f, 2.5f, 0.4f));
            Shapes.Part(g.transform, PrimitiveType.Cube, red, new Vector3(0, 5f, 0), new Vector3(8.4f, 0.9f, 0.4f));
            for (int i = 0; i < 12; i++) for (int r = 0; r < 2; r++)
                Shapes.Part(g.transform, PrimitiveType.Cube, (i + r) % 2 == 0 ? white : MaterialLibrary.Lit("#222222", 0.4f), new Vector3(-3.3f + i * 0.6f, 0.02f, -0.3f + r * 0.6f), new Vector3(0.6f, 0.02f, 0.6f));
            for (int i = 0; i < 7; i++) Shapes.Part(g.transform, PrimitiveType.Sphere, MaterialLibrary.Glow(new Color(1f, 0.85f, 0.3f), 3f), new Vector3(-3f + i, 5f, -0.25f), Vector3.one * 0.22f);
            Shapes.SetLayerAndShadows(g, true);
            return g;
        }

        public static GameObject LongJumpArrows(Transform parent, float planZ, float length)
        {
            var g = Shapes.Group(parent, "LongJumpZone", new Vector3(0, 0.03f, -planZ));
            var glow = MaterialLibrary.Glow(new Color(1f, 0.85f, 0.2f), 1.8f);
            for (int i = 0; i < 3; i++) for (int l = 0; l < 3; l++)
            {
                float z = -(length * (i + 1) / 4f); float x = (l - 1) * LevelPlanner.LaneWidth;
                Shapes.Part(g.transform, PrimitiveType.Cube, glow, new Vector3(x - 0.25f, 0, z), new Vector3(0.14f, 0.02f, 0.8f), new Vector3(0, 40, 0));
                Shapes.Part(g.transform, PrimitiveType.Cube, glow, new Vector3(x + 0.25f, 0, z), new Vector3(0.14f, 0.02f, 0.8f), new Vector3(0, -40, 0));
            }
            Shapes.SetLayerAndShadows(g, false);
            return g;
        }
    }
}
