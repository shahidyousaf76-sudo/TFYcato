using System.Collections.Generic;
using TFY.CatAdventure.Domain;
using TFY.CatAdventure.Graphics;
using UnityEngine;

namespace TFY.CatAdventure.Gameplay
{
    /// <summary>
    /// Builds the cute 3D cat from simple PBR shapes: body, head, ears, eyes with highlights, nose, whiskers, four legs,
    /// a four-segment tail, the cat's costume (pirate bandana, ninja mask, superhero mask, space helmet…) and equipped
    /// accessories (hat/crown, glasses, shoes, backpack, cape, scarf, trail). Replace with a rigged model via Addressables later —
    /// CatAnimator drives an Animator if one is present.
    /// </summary>
    public class CatRig : MonoBehaviour
    {
        public Transform bodyPivot, body, head, earL, earR, eyeL, eyeR, tailRoot, trailAnchor, headTop, faceFront, neck, back;
        public Transform[] legs = new Transform[4]; // FL, FR, BL, BR
        public readonly List<Transform> tail = new List<Transform>();
        public readonly List<Renderer> renderers = new List<Renderer>();
        public TrailRenderer trail;
        public ParticleSystem trailParticles;
        private readonly List<GameObject> accessories = new List<GameObject>();

        public static CatRig Build(Transform parent, ShopItem cat, PlayerData loadout, GameContent content)
        {
            var root = new GameObject("Cat");
            root.transform.SetParent(parent, false);
            var rig = root.AddComponent<CatRig>();
            rig.Create(cat);
            rig.ApplyAccessories(loadout, content);
            return rig;
        }

        private void Create(ShopItem cat)
        {
            var c = cat?.colors ?? new List<string> { "#F59A3C", "#D9731F", "#FFE3C2", "#3BAA5C" };
            Color fur = MaterialLibrary.Hex(c[0]), marks = MaterialLibrary.Hex(c.Count > 1 ? c[1] : c[0]), belly = MaterialLibrary.Hex(c.Count > 2 ? c[2] : "#FFFFFF"), eyes = MaterialLibrary.Hex(c.Count > 3 ? c[3] : "#3BAA5C");
            var furM = MaterialLibrary.Lit(fur, 0.25f, 0, true); var markM = MaterialLibrary.Lit(marks, 0.25f); var bellyM = MaterialLibrary.Lit(belly, 0.25f);
            var pink = MaterialLibrary.Lit("#FF8FAB", 0.4f); var dark = MaterialLibrary.Lit("#1E1E24", 0.8f); var white = MaterialLibrary.Glow(Color.white, 1.2f, 0.9f);
            var eyeM = MaterialLibrary.Lit(eyes, 0.9f);
            const PrimitiveType S = PrimitiveType.Sphere, C = PrimitiveType.Cylinder, B = PrimitiveType.Cube;

            bodyPivot = Shapes.Group(transform, "BodyPivot", new Vector3(0, 0.55f, 0)).transform;
            body = Shapes.Part(bodyPivot, S, furM, new Vector3(0, 0.05f, 0.05f), new Vector3(0.62f, 0.56f, 0.95f), default, "Body").transform;
            Shapes.Part(body, S, bellyM, new Vector3(0, -0.22f, -0.05f), new Vector3(0.75f, 0.55f, 0.8f), default, "Belly");

            // Head faces -Z (the running direction is -Z in world space).
            head = Shapes.Group(bodyPivot, "Head", new Vector3(0, 0.42f, -0.5f)).transform;
            Shapes.Part(head, S, furM, Vector3.zero, new Vector3(0.62f, 0.54f, 0.54f), default, "Skull");
            Shapes.Part(head, S, bellyM, new Vector3(0, -0.1f, -0.2f), new Vector3(0.34f, 0.22f, 0.22f), default, "Muzzle");
            Shapes.Part(head, S, pink, new Vector3(0, -0.03f, -0.3f), new Vector3(0.08f, 0.06f, 0.06f), default, "Nose");
            earL = Ear(-1, furM, pink); earR = Ear(1, furM, pink);
            eyeL = Eye(-1, eyeM, dark, white); eyeR = Eye(1, eyeM, dark, white);
            for (int s = -1; s <= 1; s += 2) for (int k = 0; k < 3; k++)
                Shapes.Part(head, B, dark, new Vector3(s * 0.22f, -0.06f - k * 0.035f, -0.25f), new Vector3(0.22f, 0.006f, 0.006f), new Vector3(0, s * 12f, s * (k - 1) * 10f), "Whisker");
            Shapes.Part(head, S, MaterialLibrary.Lit("#FF9EB5", 0.3f), new Vector3(-0.2f, -0.1f, -0.2f), new Vector3(0.1f, 0.06f, 0.04f), default, "CheekL");
            Shapes.Part(head, S, MaterialLibrary.Lit("#FF9EB5", 0.3f), new Vector3(0.2f, -0.1f, -0.2f), new Vector3(0.1f, 0.06f, 0.04f), default, "CheekR");

            // Legs: pivot at the hip so rotation swings the leg.
            var hip = new[] { new Vector3(-0.18f, -0.1f, -0.28f), new Vector3(0.18f, -0.1f, -0.28f), new Vector3(-0.18f, -0.1f, 0.32f), new Vector3(0.18f, -0.1f, 0.32f) };
            for (int i = 0; i < 4; i++)
            {
                legs[i] = Shapes.Group(bodyPivot, "Leg" + i, hip[i]).transform;
                Shapes.Part(legs[i], C, furM, new Vector3(0, -0.2f, 0), new Vector3(0.14f, 0.2f, 0.14f), default, "Upper");
                Shapes.Part(legs[i], S, bellyM, new Vector3(0, -0.42f, -0.03f), new Vector3(0.17f, 0.12f, 0.2f), default, "Paw");
            }

            tailRoot = Shapes.Group(bodyPivot, "Tail", new Vector3(0, 0.12f, 0.5f)).transform;
            var prev = tailRoot;
            for (int i = 0; i < 4; i++)
            {
                var seg = Shapes.Group(prev, "TailSeg" + i, i == 0 ? Vector3.zero : new Vector3(0, 0.16f, 0.02f)).transform;
                Shapes.Part(seg, C, i == 3 ? markM : furM, new Vector3(0, 0.08f, 0), new Vector3(0.09f - i * 0.01f, 0.09f, 0.09f - i * 0.01f), default, "TailPart");
                tail.Add(seg); prev = seg;
            }

            // Markings per cat
            string id = cat?.id ?? "orange_cat";
            if (id == "orange_cat" || id == "grey_cat" || id == "pirate_cat")
                for (int k = 0; k < 3; k++) Shapes.Part(body, S, markM, new Vector3(0, 0.33f, -0.2f + k * 0.2f), new Vector3(0.82f, 0.18f, 0.08f), default, "Stripe");
            if (id == "spotted_cat")
            {
                var r = new System.Random(3);
                for (int k = 0; k < 9; k++) Shapes.Part(body, S, markM, new Vector3((float)(r.NextDouble() - 0.5) * 0.9f, 0.1f + (float)r.NextDouble() * 0.35f, (float)(r.NextDouble() - 0.5) * 0.8f), Vector3.one * 0.16f, default, "Spot");
            }
            Costume(id);

            headTop = Shapes.Group(head, "HeadTop", new Vector3(0, 0.27f, 0.02f)).transform;
            faceFront = Shapes.Group(head, "Face", new Vector3(0, 0.06f, -0.27f)).transform;
            neck = Shapes.Group(bodyPivot, "Neck", new Vector3(0, 0.26f, -0.36f)).transform;
            back = Shapes.Group(bodyPivot, "Back", new Vector3(0, 0.33f, 0.12f)).transform;
            trailAnchor = Shapes.Group(transform, "TrailAnchor", new Vector3(0, 0.5f, 0.6f)).transform;

            foreach (var r in GetComponentsInChildren<Renderer>()) renderers.Add(r);
            Shapes.SetLayerAndShadows(gameObject, true);
        }

        private Transform Ear(int side, Material fur, Material inner)
        {
            var e = Shapes.Group(head, side < 0 ? "EarL" : "EarR", new Vector3(side * 0.18f, 0.2f, 0.02f)).transform;
            e.localEulerAngles = new Vector3(0, 0, -side * 18f);
            Shapes.Part(e, Shapes.Cone(), fur, Vector3.zero, new Vector3(0.2f, 0.26f, 0.12f), default, "Outer");
            Shapes.Part(e, Shapes.Cone(), inner, new Vector3(0, 0.01f, -0.03f), new Vector3(0.12f, 0.18f, 0.06f), default, "Inner");
            return e;
        }

        private Transform Eye(int side, Material iris, Material pupil, Material shine)
        {
            var e = Shapes.Group(head, side < 0 ? "EyeL" : "EyeR", new Vector3(side * 0.13f, 0.04f, -0.22f)).transform;
            Shapes.Part(e, PrimitiveType.Sphere, iris, Vector3.zero, new Vector3(0.13f, 0.16f, 0.08f), default, "Iris");
            Shapes.Part(e, PrimitiveType.Sphere, pupil, new Vector3(0, 0, -0.025f), new Vector3(0.07f, 0.11f, 0.05f), default, "Pupil");
            Shapes.Part(e, PrimitiveType.Sphere, shine, new Vector3(0.025f, 0.035f, -0.05f), new Vector3(0.035f, 0.035f, 0.02f), default, "Shine");
            return e;
        }

        private void Costume(string id)
        {
            switch (id)
            {
                case "pirate_cat":
                    Shapes.Part(head, PrimitiveType.Sphere, MaterialLibrary.Lit("#D62828", 0.3f), new Vector3(0, 0.12f, 0.02f), new Vector3(0.64f, 0.36f, 0.58f), default, "Bandana");
                    Shapes.Part(head, PrimitiveType.Cylinder, MaterialLibrary.Lit("#111111", 0.5f), new Vector3(0.13f, 0.04f, -0.26f), new Vector3(0.14f, 0.01f, 0.14f), new Vector3(90, 0, 0), "EyePatch");
                    break;
                case "ninja_cat":
                    Shapes.Part(head, PrimitiveType.Cube, MaterialLibrary.Lit("#1B1D2A", 0.4f), new Vector3(0, 0.04f, -0.05f), new Vector3(0.66f, 0.14f, 0.5f), default, "Mask");
                    Shapes.Part(head, PrimitiveType.Cube, MaterialLibrary.Lit("#E63946", 0.4f), new Vector3(0, 0.02f, 0.3f), new Vector3(0.06f, 0.04f, 0.3f), new Vector3(20, 0, 0), "BandTail");
                    break;
                case "superhero_cat":
                    Shapes.Part(head, PrimitiveType.Cube, MaterialLibrary.Lit("#2F80ED", 0.5f), new Vector3(0, 0.05f, -0.2f), new Vector3(0.5f, 0.1f, 0.2f), default, "HeroMask");
                    Shapes.Part(bodyPivot, PrimitiveType.Cube, MaterialLibrary.Lit("#E63946", 0.3f), new Vector3(0, 0.2f, 0.4f), new Vector3(0.7f, 0.04f, 0.7f), new Vector3(-20, 0, 0), "HeroCape");
                    break;
                case "space_cat":
                    Shapes.Part(head, PrimitiveType.Sphere, MaterialLibrary.Transparent(new Color(0.75f, 0.9f, 1f, 0.28f)), new Vector3(0, 0.02f, 0), Vector3.one * 0.85f, default, "Helmet");
                    Shapes.Part(head, PrimitiveType.Sphere, MaterialLibrary.Glow(new Color(0f, 0.85f, 1f), 3f), new Vector3(0, 0.5f, 0), Vector3.one * 0.07f, default, "Antenna");
                    break;
            }
        }

        /// <summary>Re-dresses the cat: removes old accessories and attaches the equipped ones (shop preview uses this too).</summary>
        public void ApplyAccessories(PlayerData p, GameContent content)
        {
            foreach (var a in accessories) if (a) Destroy(a);
            accessories.Clear();
            if (trail) { Destroy(trail); trail = null; }
            if (trailParticles) { Destroy(trailParticles.gameObject); trailParticles = null; }
            if (p?.equipped == null) return;
            foreach (var kv in p.equipped)
            {
                if (string.IsNullOrEmpty(kv.Value)) continue;
                var item = content.Item(kv.Value); if (item == null) continue;
                var col = item.colors.Count > 0 ? MaterialLibrary.Hex(item.colors[0]) : Color.white;
                var col2 = item.colors.Count > 1 ? MaterialLibrary.Hex(item.colors[1]) : col;
                var m = MaterialLibrary.Lit(col, 0.45f); var m2 = MaterialLibrary.Lit(col2, 0.45f);
                switch (item.slot)
                {
                    case "hat": accessories.Add(Hat(item.id, m, m2)); break;
                    case "glasses": accessories.Add(Glasses(item.id, m)); break;
                    case "shoes": for (int i = 0; i < 4; i++) accessories.Add(Shapes.Part(legs[i], PrimitiveType.Sphere, item.id == "gold_boots" ? MaterialLibrary.Lit(col, 0.85f, 1f) : m, new Vector3(0, -0.42f, -0.04f), new Vector3(0.2f, 0.14f, 0.24f), default, "Shoe")); break;
                    case "backpack":
                        var bp = Shapes.Part(back, PrimitiveType.Cube, m, new Vector3(0, 0.08f, 0.05f), new Vector3(0.4f, 0.3f, 0.3f), default, "Backpack");
                        Shapes.Part(bp.transform, PrimitiveType.Cube, m2, new Vector3(0, -0.2f, 0), new Vector3(0.9f, 0.3f, 1.05f), default, "Pocket");
                        if (item.id == "rocket_backpack") Shapes.Part(bp.transform, Shapes.Cone(), MaterialLibrary.Glow(new Color(1f, 0.45f, 0.1f), 2.5f), new Vector3(0, -0.8f, 0.3f), new Vector3(0.5f, -0.6f, 0.5f), default, "Flame");
                        accessories.Add(bp); break;
                    case "cape": accessories.Add(Shapes.Part(back, PrimitiveType.Cube, m, new Vector3(0, -0.05f, 0.25f), new Vector3(0.62f, 0.03f, 0.7f), new Vector3(-25, 0, 0), "Cape")); break;
                    case "scarf":
                        var sc = Shapes.Part(neck, Shapes.Torus(), m, Vector3.zero, new Vector3(0.9f, 1.4f, 0.9f), new Vector3(70, 0, 0), "Scarf");
                        Shapes.Part(neck, PrimitiveType.Cube, m2, new Vector3(0.12f, -0.12f, 0.05f), new Vector3(0.1f, 0.25f, 0.04f), new Vector3(0, 0, 15), "ScarfEnd");
                        accessories.Add(sc); break;
                    case "trail": AddTrail(item); break;
                }
            }
            renderers.Clear(); foreach (var r in GetComponentsInChildren<Renderer>()) if (!(r is TrailRenderer) && !(r is ParticleSystemRenderer)) renderers.Add(r);
            Shapes.SetLayerAndShadows(gameObject, true);
        }

        private GameObject Hat(string id, Material m, Material m2)
        {
            var g = Shapes.Group(headTop, "Hat");
            switch (id)
            {
                case "party_hat": Shapes.Part(g.transform, Shapes.Cone(), m, Vector3.zero, new Vector3(0.28f, 0.38f, 0.28f), default, "Cone"); Shapes.Part(g.transform, PrimitiveType.Sphere, m2, new Vector3(0, 0.38f, 0), Vector3.one * 0.08f); break;
                case "cowboy_hat": Shapes.Part(g.transform, PrimitiveType.Cylinder, m, Vector3.zero, new Vector3(0.7f, 0.02f, 0.6f)); Shapes.Part(g.transform, PrimitiveType.Cylinder, m, new Vector3(0, 0.1f, 0), new Vector3(0.32f, 0.1f, 0.3f)); break;
                case "gold_crown":
                    var gold = MaterialLibrary.Lit(new Color(1f, 0.82f, 0.2f), 0.9f, 1f);
                    Shapes.Part(g.transform, PrimitiveType.Cylinder, gold, new Vector3(0, 0.04f, 0), new Vector3(0.34f, 0.06f, 0.34f));
                    for (int i = 0; i < 5; i++) { float a = i * Mathf.PI * 2 / 5; Shapes.Part(g.transform, Shapes.Cone(), gold, new Vector3(Mathf.Cos(a) * 0.14f, 0.09f, Mathf.Sin(a) * 0.14f), new Vector3(0.08f, 0.12f, 0.08f)); }
                    Shapes.Part(g.transform, Shapes.Gem(), MaterialLibrary.Glow(new Color(1f, 0.2f, 0.4f), 1.5f), new Vector3(0, 0.06f, -0.17f), Vector3.one * 0.08f);
                    break;
                case "flower_crown":
                    for (int i = 0; i < 7; i++) { float a = i * Mathf.PI * 2 / 7; Shapes.Part(g.transform, PrimitiveType.Sphere, i % 2 == 0 ? m : MaterialLibrary.Lit(Color.white, 0.3f), new Vector3(Mathf.Cos(a) * 0.17f, 0.02f, Mathf.Sin(a) * 0.17f), Vector3.one * 0.09f); }
                    break;
                default: // red cap
                    Shapes.Part(g.transform, PrimitiveType.Sphere, m, new Vector3(0, 0.02f, 0), new Vector3(0.44f, 0.24f, 0.44f));
                    Shapes.Part(g.transform, PrimitiveType.Cylinder, m, new Vector3(0, 0f, -0.2f), new Vector3(0.3f, 0.01f, 0.22f)); break;
            }
            return g;
        }

        private GameObject Glasses(string id, Material m)
        {
            var g = Shapes.Group(faceFront, "Glasses");
            var lens = id == "sunglasses" ? MaterialLibrary.Lit("#101010", 0.95f) : MaterialLibrary.Transparent(new Color(0.8f, 0.95f, 1f, 0.35f));
            for (int s = -1; s <= 1; s += 2)
            {
                if (id == "star_glasses") Shapes.Part(g.transform, Shapes.Star(), m, new Vector3(s * 0.13f, 0, 0), Vector3.one * 0.3f, default, "StarLens");
                else
                {
                    Shapes.Part(g.transform, Shapes.Torus(), m, new Vector3(s * 0.13f, 0, 0), new Vector3(0.24f, 0.24f, 0.24f), new Vector3(90, 0, 0), "Rim");
                    Shapes.Part(g.transform, PrimitiveType.Cylinder, lens, new Vector3(s * 0.13f, 0, 0.005f), new Vector3(0.18f, 0.005f, 0.18f), new Vector3(90, 0, 0), "Lens");
                }
            }
            Shapes.Part(g.transform, PrimitiveType.Cube, m, new Vector3(0, 0.01f, 0), new Vector3(0.08f, 0.015f, 0.015f), default, "Bridge");
            return g;
        }

        private void AddTrail(ShopItem item)
        {
            trail = trailAnchor.gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.45f; trail.minVertexDistance = 0.15f; trail.startWidth = 0.35f; trail.endWidth = 0.02f;
            trail.material = MaterialLibrary.Particle(Color.white);
            var g = new Gradient();
            var cols = item.colors.Count > 0 ? item.colors : new List<string> { "#FFFFFF" };
            var keys = new GradientColorKey[Mathf.Min(8, cols.Count)];
            for (int i = 0; i < keys.Length; i++) keys[i] = new GradientColorKey(MaterialLibrary.Hex(cols[i]), keys.Length == 1 ? 0 : i / (keys.Length - 1f));
            g.SetKeys(keys, new[] { new GradientAlphaKey(0.9f, 0), new GradientAlphaKey(0, 1) });
            trail.colorGradient = g;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        public void SetVisible(bool v) { foreach (var r in renderers) if (r) r.enabled = v; }
    }
}
