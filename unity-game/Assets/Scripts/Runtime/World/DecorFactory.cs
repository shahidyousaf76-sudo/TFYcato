using System.Collections.Generic;
using TFY.CatAdventure.Graphics;
using UnityEngine;

namespace TFY.CatAdventure.World
{
    /// <summary>
    /// Stylised scenery built from simple PBR shapes: trees, flowers, benches, lamps, buildings with glowing windows, cars,
    /// mountains with snow caps, waterfalls, palms, boats, sand castles… One template per type is built once and cloned
    /// (GPU-instanced materials, LOD culling for distant pieces). Final art can replace a type by providing a prefab at
    /// Resources/Decor/{type}.
    /// </summary>
    public class DecorFactory
    {
        private readonly Transform templateRoot;
        private readonly Dictionary<string, GameObject> templates = new Dictionary<string, GameObject>();
        public static readonly List<Material> NightEmissive = new List<Material>(); // windows / lamps brighten at night
        public static readonly List<Material> BalloonFlames = new List<Material>(); // hot air balloon burners flicker

        public DecorFactory(Transform root)
        {
            templateRoot = new GameObject("DecorTemplates").transform; templateRoot.SetParent(root, false); templateRoot.gameObject.SetActive(false);
        }

        public GameObject Create(string type, Transform parent, Vector3 pos, float yaw, float scale, System.Random rnd)
        {
            if (!templates.TryGetValue(type, out var t))
            {
                var art = Resources.Load<GameObject>("Decor/" + type);
                t = art ? Object.Instantiate(art, templateRoot) : Build(type);
                t.transform.SetParent(templateRoot, false);
                templates[type] = t;
            }
            var go = Object.Instantiate(t, parent);
            go.transform.localPosition = pos; go.transform.localRotation = Quaternion.Euler(0, yaw, 0); go.transform.localScale = Vector3.one * scale;
            go.SetActive(true);
            return go;
        }

        private static Material M(string hex, float s = 0.3f, float metal = 0, bool detail = false) => MaterialLibrary.Lit(hex, s, metal, detail);

        private GameObject Build(string type)
        {
            var go = new GameObject(type); var t = go.transform; go.transform.SetParent(templateRoot, false);
            const PrimitiveType S = PrimitiveType.Sphere, C = PrimitiveType.Cylinder, B = PrimitiveType.Cube;
            bool castShadow = true;
            switch (type)
            {
                case "tree_round":
                    Shapes.Part(t, C, M("#8B5A2B", 0.2f, 0, true), new Vector3(0, 1.2f, 0), new Vector3(0.45f, 1.2f, 0.45f));
                    Shapes.Part(t, S, M("#5DBB63", 0.25f, 0, true), new Vector3(0, 3.2f, 0), new Vector3(3f, 2.7f, 3f));
                    Shapes.Part(t, S, M("#6FCF73", 0.25f), new Vector3(0.7f, 3.9f, 0.3f), new Vector3(1.6f, 1.4f, 1.6f)); break;
                case "tree_tall":
                    Shapes.Part(t, C, M("#6E4B2A", 0.2f, 0, true), new Vector3(0, 2.5f, 0), new Vector3(0.6f, 2.5f, 0.6f));
                    for (int i = 0; i < 3; i++) Shapes.Part(t, S, M(i % 2 == 0 ? "#2D6A4F" : "#40916C", 0.25f, 0, true), new Vector3(0, 4.5f + i * 1.3f, 0), Vector3.one * (3.4f - i * 0.8f)); break;
                case "pine":
                    Shapes.Part(t, C, M("#6E4B2A", 0.2f), new Vector3(0, 0.8f, 0), new Vector3(0.35f, 0.8f, 0.35f));
                    for (int i = 0; i < 3; i++) Shapes.Part(t, Shapes.Cone(), M("#1B7F5B", 0.25f, 0, true), new Vector3(0, 1.4f + i * 1.1f, 0), new Vector3(2.6f - i * 0.6f, 1.8f, 2.6f - i * 0.6f)); break;
                case "flowers":
                    castShadow = false;
                    var cols = new[] { "#FF7AB6", "#FFD166", "#F72585", "#FFFFFF", "#B5179E" };
                    for (int i = 0; i < 9; i++) { float a = i * 2.4f, r = 0.2f + (i % 3) * 0.3f; Shapes.Part(t, C, M("#3A7D44"), new Vector3(Mathf.Cos(a) * r, 0.18f, Mathf.Sin(a) * r), new Vector3(0.03f, 0.18f, 0.03f)); Shapes.Part(t, S, M(cols[i % cols.Length], 0.35f), new Vector3(Mathf.Cos(a) * r, 0.4f, Mathf.Sin(a) * r), Vector3.one * 0.18f); }
                    break;
                case "bush": Shapes.Part(t, S, M("#4CAF50", 0.25f, 0, true), new Vector3(0, 0.5f, 0), new Vector3(1.8f, 1.1f, 1.4f)); Shapes.Part(t, S, M("#FF7AB6", 0.4f), new Vector3(0.4f, 0.9f, -0.5f), Vector3.one * 0.18f); break;
                case "bench":
                    Shapes.Part(t, B, M("#B5651D", 0.3f), new Vector3(0, 0.45f, 0), new Vector3(1.6f, 0.08f, 0.5f)); Shapes.Part(t, B, M("#B5651D", 0.3f), new Vector3(0, 0.8f, 0.22f), new Vector3(1.6f, 0.4f, 0.06f));
                    for (int s = -1; s <= 1; s += 2) Shapes.Part(t, B, M("#333333", 0.6f, 0.7f), new Vector3(s * 0.7f, 0.22f, 0), new Vector3(0.08f, 0.45f, 0.45f)); break;
                case "lamp_post":
                case "street_light":
                    Shapes.Part(t, C, M("#3D405B", 0.6f, 0.8f), new Vector3(0, 2f, 0), new Vector3(0.14f, 2f, 0.14f));
                    Shapes.Part(t, B, M("#3D405B", 0.6f, 0.8f), new Vector3(type == "street_light" ? -0.6f : 0, 4f, 0), new Vector3(type == "street_light" ? 1.3f : 0.3f, 0.1f, 0.2f));
                    var lamp = MaterialLibrary.Glow(new Color(1f, 0.88f, 0.6f), 2.5f); NightEmissive.Add(lamp);
                    Shapes.Part(t, S, lamp, new Vector3(type == "street_light" ? -1.15f : 0, 3.85f, 0), Vector3.one * 0.38f); break;
                case "puddle": castShadow = false; Shapes.Part(t, C, MaterialLibrary.Lit("#6EC6FF", 0.97f), new Vector3(0, 0.01f, 0), new Vector3(2f, 0.005f, 1.3f)); break;
                case "small_bridge":
                case "wood_bridge":
                    Shapes.Part(t, C, MaterialLibrary.Lit("#5EC8E8", 0.97f), new Vector3(0, 0.01f, 0), new Vector3(3f, 0.005f, 3f));
                    for (int i = 0; i < 6; i++) Shapes.Part(t, B, M("#A47148", 0.3f), new Vector3(0, 0.25f + Mathf.Sin(i / 5f * Mathf.PI) * 0.35f, -1.25f + i * 0.5f), new Vector3(1.4f, 0.08f, 0.45f)); break;
                case "rock_moss": case "boulder":
                    Shapes.Part(t, S, M("#8D8D8D", 0.2f, 0, true), new Vector3(0, 0.6f, 0), new Vector3(2f, 1.3f, 1.6f));
                    if (type == "rock_moss") Shapes.Part(t, S, M("#6A994E", 0.2f), new Vector3(0, 1.1f, 0), new Vector3(1.4f, 0.4f, 1.1f)); break;
                case "stream": case "river":
                    castShadow = false; Shapes.Part(t, B, MaterialLibrary.Lit("#4FB3E8", 0.97f), new Vector3(0, 0.02f, 0), new Vector3(24f, 0.02f, 3f)); break;
                case "fallen_tree": Shapes.Part(t, C, M("#7A5230", 0.2f, 0, true), new Vector3(0, 0.4f, 0), new Vector3(0.8f, 2.4f, 0.8f), new Vector3(0, 0, 90)); break;
                case "mushroom":
                    Shapes.Part(t, C, M("#FFF3E0", 0.3f), new Vector3(0, 0.3f, 0), new Vector3(0.25f, 0.3f, 0.25f));
                    Shapes.Part(t, S, M("#E63946", 0.4f), new Vector3(0, 0.62f, 0), new Vector3(0.8f, 0.45f, 0.8f));
                    for (int i = 0; i < 4; i++) Shapes.Part(t, S, M("#FFFFFF", 0.4f), new Vector3(Mathf.Cos(i * 1.6f) * 0.22f, 0.78f, Mathf.Sin(i * 1.6f) * 0.22f), Vector3.one * 0.1f); break;
                case "building":
                    var windows = MaterialLibrary.Glow(new Color(1f, 0.9f, 0.6f), 0.4f); NightEmissive.Add(windows);
                    string[] bc = { "#F4A261", "#E76F51", "#2A9D8F", "#8ECAE6", "#FFB4A2", "#CDB4DB" };
                    Shapes.Part(t, B, M(bc[templates.Count % bc.Length], 0.35f, 0, true), new Vector3(0, 6f, 0), new Vector3(7f, 12f, 7f));
                    for (int f = 0; f < 5; f++) for (int w = 0; w < 3; w++) Shapes.Part(t, B, windows, new Vector3(-2.2f + w * 2.2f, 2f + f * 2.2f, -3.52f), new Vector3(1.1f, 1.2f, 0.05f));
                    Shapes.Part(t, B, M("#6D6875", 0.4f), new Vector3(0, 12.2f, 0), new Vector3(7.2f, 0.4f, 7.2f)); break;
                case "car":
                    string[] cc = { "#E63946", "#3A86FF", "#FFBE0B", "#8338EC", "#06D6A0" };
                    var body = M(cc[Random.Range(0, cc.Length)], 0.85f, 0.4f);
                    Shapes.Part(t, B, body, new Vector3(0, 0.6f, 0), new Vector3(1.8f, 0.7f, 3.8f)); Shapes.Part(t, B, body, new Vector3(0, 1.15f, 0.2f), new Vector3(1.6f, 0.55f, 2f));
                    Shapes.Part(t, B, MaterialLibrary.Lit("#BDE0FE", 0.95f, 0.1f), new Vector3(0, 1.15f, 0.2f), new Vector3(1.62f, 0.4f, 1.8f));
                    for (int i = 0; i < 4; i++) Shapes.Part(t, C, M("#222222", 0.5f), new Vector3(i < 2 ? -0.85f : 0.85f, 0.35f, i % 2 == 0 ? -1.2f : 1.2f), new Vector3(0.7f, 0.12f, 0.7f), new Vector3(0, 0, 90));
                    var head = MaterialLibrary.Glow(new Color(1f, 0.95f, 0.7f), 2f); NightEmissive.Add(head);
                    Shapes.Part(t, S, head, new Vector3(-0.6f, 0.65f, -1.92f), Vector3.one * 0.25f); Shapes.Part(t, S, head, new Vector3(0.6f, 0.65f, -1.92f), Vector3.one * 0.25f); break;
                case "traffic_barrier":
                    for (int i = 0; i < 3; i++) Shapes.Part(t, B, M(i % 2 == 0 ? "#E63946" : "#FFFFFF", 0.5f), new Vector3(-0.8f + i * 0.8f, 0.6f, 0), new Vector3(0.8f, 0.3f, 0.1f));
                    Shapes.Part(t, B, M("#555555"), new Vector3(-1.1f, 0.35f, 0), new Vector3(0.1f, 0.7f, 0.4f)); Shapes.Part(t, B, M("#555555"), new Vector3(1.1f, 0.35f, 0), new Vector3(0.1f, 0.7f, 0.4f)); break;
                case "sidewalk": castShadow = false; Shapes.Part(t, B, M("#D8DDE3", 0.3f, 0, true), new Vector3(0, 0.08f, 0), new Vector3(3f, 0.16f, 10f)); break;
                case "park_patch":
                    Shapes.Part(t, B, M("#74C69D", 0.2f, 0, true), new Vector3(0, 0.05f, 0), new Vector3(8f, 0.1f, 8f));
                    Shapes.Part(t, C, M("#8B5A2B", 0.2f), new Vector3(0, 1f, 0), new Vector3(0.35f, 1f, 0.35f)); Shapes.Part(t, S, M("#52B788", 0.25f), new Vector3(0, 2.6f, 0), Vector3.one * 2.4f); break;
                case "tunnel":
                    var wall = M("#9A8C98", 0.3f, 0, true);
                    Shapes.Part(t, B, wall, new Vector3(-4.6f, 3f, 0), new Vector3(1.2f, 6f, 12f)); Shapes.Part(t, B, wall, new Vector3(4.6f, 3f, 0), new Vector3(1.2f, 6f, 12f));
                    Shapes.Part(t, B, wall, new Vector3(0, 6.5f, 0), new Vector3(10.4f, 1f, 12f));
                    var tl = MaterialLibrary.Glow(new Color(1f, 0.85f, 0.5f), 2f); NightEmissive.Add(tl);
                    for (int i = 0; i < 3; i++) Shapes.Part(t, B, tl, new Vector3(0, 5.95f, -4 + i * 4), new Vector3(2f, 0.1f, 0.4f)); break;
                case "mountain":
                    castShadow = false;
                    Shapes.Part(t, Shapes.Cone(), M("#7D8597", 0.2f, 0, true), Vector3.zero, new Vector3(60f, 45f, 60f));
                    Shapes.Part(t, Shapes.Cone(), M("#FFFFFF", 0.5f), new Vector3(0, 30f, 0), new Vector3(20.5f, 15.2f, 20.5f)); break;
                case "cliff": Shapes.Part(t, B, M("#8D99AE", 0.2f, 0, true), new Vector3(0, 5f, 0), new Vector3(8f, 10f, 10f)); Shapes.Part(t, B, M("#FFFFFF", 0.5f), new Vector3(0, 10.1f, 0), new Vector3(8.1f, 0.3f, 10.1f)); break;
                case "waterfall":
                    Shapes.Part(t, B, M("#6C757D", 0.2f, 0, true), new Vector3(0, 6f, 0.6f), new Vector3(8f, 12f, 1f));
                    var water = MaterialLibrary.Glow(new Color(0.6f, 0.85f, 1f), 0.6f, 0.95f); Shapes.Part(t, B, water, new Vector3(0, 6f, 0), new Vector3(2.5f, 12f, 0.3f));
                    Shapes.Part(t, C, MaterialLibrary.Lit("#6CC7F0", 0.97f), new Vector3(0, 0.02f, -2f), new Vector3(6f, 0.02f, 4f)); break;
                case "rope_bridge": Shapes.Part(t, B, M("#A47148", 0.3f), new Vector3(0, 3f, 0), new Vector3(1.6f, 0.1f, 14f)); break;
                case "snow_patch": castShadow = false; Shapes.Part(t, S, M("#FFFFFF", 0.55f), new Vector3(0, 0, 0), new Vector3(4f, 0.4f, 3f)); break;
                case "cloud":
                    castShadow = false; var cloud = M("#FFFFFF", 0.1f);
                    for (int i = 0; i < 4; i++) Shapes.Part(t, S, cloud, new Vector3(-1.5f + i, Mathf.Sin(i) * 0.3f, 0), Vector3.one * (1.6f + (i % 2) * 0.6f)); break;
                case "palm":
                    for (int i = 0; i < 6; i++) Shapes.Part(t, C, M("#A67C52", 0.2f, 0, true), new Vector3(i * 0.12f, 0.5f + i * 0.9f, 0), new Vector3(0.4f - i * 0.03f, 0.48f, 0.4f - i * 0.03f), new Vector3(0, 0, -4 - i * 2));
                    for (int i = 0; i < 6; i++) Shapes.Part(t, S, M("#2A9D8F", 0.3f), new Vector3(0.7f + Mathf.Cos(i) * 1.3f, 5.5f, Mathf.Sin(i) * 1.3f), new Vector3(2.4f, 0.2f, 0.7f), new Vector3(0, i * 60, -20));
                    for (int i = 0; i < 3; i++) Shapes.Part(t, S, M("#6F4E37", 0.3f), new Vector3(0.6f + i * 0.2f, 5.1f, -0.2f + i * 0.2f), Vector3.one * 0.35f); break;
                case "ocean": castShadow = false; break; // built by EnvironmentBuilder (one big animated plane)
                case "boat":
                    Shapes.Part(t, B, M("#FFFFFF", 0.6f), new Vector3(0, 0.4f, 0), new Vector3(2f, 0.8f, 5f)); Shapes.Part(t, B, M("#E63946", 0.5f), new Vector3(0, 0.85f, 0), new Vector3(2.05f, 0.15f, 5.05f));
                    Shapes.Part(t, C, M("#8B5A2B", 0.3f), new Vector3(0, 3f, 0), new Vector3(0.12f, 2.4f, 0.12f)); Shapes.Part(t, B, M("#FFF3B0", 0.3f), new Vector3(0, 3.4f, 0.8f), new Vector3(0.05f, 3f, 1.8f)); break;
                case "crab": Shapes.Part(t, S, M("#FF6F59", 0.4f), new Vector3(0, 0.2f, 0), new Vector3(0.6f, 0.3f, 0.45f)); break;
                case "sand_castle":
                    var sand = M("#E9C98B", 0.2f, 0, true);
                    Shapes.Part(t, B, sand, new Vector3(0, 0.4f, 0), new Vector3(2f, 0.8f, 2f));
                    for (int i = 0; i < 4; i++) { var p = new Vector3(i < 2 ? -0.9f : 0.9f, 0.9f, i % 2 == 0 ? -0.9f : 0.9f); Shapes.Part(t, C, sand, p, new Vector3(0.6f, 0.9f, 0.6f)); Shapes.Part(t, Shapes.Cone(), sand, p + Vector3.up * 0.9f, new Vector3(0.7f, 0.6f, 0.7f)); }
                    Shapes.Part(t, B, M("#FF6F59", 0.3f), new Vector3(0.9f, 2.6f, -0.9f), new Vector3(0.4f, 0.25f, 0.02f)); break;
                case "umbrella":
                    Shapes.Part(t, C, M("#FFFFFF", 0.4f), new Vector3(0, 1.2f, 0), new Vector3(0.08f, 1.2f, 0.08f));
                    Shapes.Part(t, Shapes.Cone(), M("#FF6F59", 0.4f), new Vector3(0, 2.2f, 0), new Vector3(2.8f, 0.7f, 2.8f)); break;
                case "shell": castShadow = false; Shapes.Part(t, S, M("#FFD6E0", 0.6f), new Vector3(0, 0.08f, 0), new Vector3(0.4f, 0.15f, 0.35f)); break;
                case "hot_air_balloon_0": case "hot_air_balloon_1": case "hot_air_balloon_2":
                    castShadow = false;
                    string[][] palettes = { new[] { "#FF595E", "#FFCA3A", "#FFFFFF" }, new[] { "#1982C4", "#8AC926", "#FFFFFF" }, new[] { "#6A4C93", "#FF9F1C", "#FFD6E0" } };
                    var pal = palettes[type[type.Length - 1] - '0'];
                    var e0 = M(pal[0], 0.45f); var e1 = M(pal[1], 0.45f); var e2 = M(pal[2], 0.45f);
                    Shapes.Part(t, S, e0, new Vector3(0, 6f, 0), new Vector3(4f, 4.6f, 4f), default, "Envelope");
                    for (int i = 0; i < 3; i++) Shapes.Part(t, Shapes.Torus(), i % 2 == 0 ? e1 : e2, new Vector3(0, 5.1f + i * 0.9f, 0), new Vector3(4.6f - Mathf.Abs(i - 1) * 0.5f, 2.2f, 4.6f - Mathf.Abs(i - 1) * 0.5f), default, "Band");
                    Shapes.Part(t, S, e1, new Vector3(0, 8.1f, 0), new Vector3(2.2f, 0.9f, 2.2f), default, "Cap");
                    Shapes.Part(t, Shapes.Cone(), e0, new Vector3(0, 4.3f, 0), new Vector3(3.3f, 2.2f, 3.3f), new Vector3(180, 0, 0), "Skirt");
                    var rope = M("#5C4033", 0.6f);
                    for (int i = 0; i < 4; i++) { float a = i * Mathf.PI / 2 + Mathf.PI / 4; Shapes.Part(t, C, rope, new Vector3(Mathf.Cos(a) * 0.45f, 1.55f, Mathf.Sin(a) * 0.45f), new Vector3(0.04f, 0.5f, 0.04f), default, "Rope"); }
                    var basket = M("#A0673A", 0.8f, 0, true);
                    Shapes.Part(t, B, basket, new Vector3(0, 0.45f, 0), new Vector3(1.1f, 0.8f, 1.1f), default, "Basket");
                    Shapes.Part(t, B, M("#6E4B2A", 0.6f), new Vector3(0, 0.88f, 0), new Vector3(1.2f, 0.1f, 1.2f), default, "Rim");
                    var flame = MaterialLibrary.Glow(new Color(1f, 0.55f, 0.12f), 3f); if (!BalloonFlames.Contains(flame)) BalloonFlames.Add(flame);
                    Shapes.Part(t, S, flame, new Vector3(0, 1.75f, 0), new Vector3(0.35f, 0.55f, 0.35f), default, "Flame"); // HDR: blooms
                    break;
                case "snow_pine":
                    Shapes.Part(t, C, M("#6E4B2A", 0.2f), new Vector3(0, 0.8f, 0), new Vector3(0.35f, 0.8f, 0.35f));
                    for (int i = 0; i < 3; i++) { Shapes.Part(t, Shapes.Cone(), M("#1B6F55", 0.25f, 0, true), new Vector3(0, 1.1f + i * 1.1f, 0), new Vector3(2.6f - i * 0.6f, 1.8f, 2.6f - i * 0.6f)); Shapes.Part(t, Shapes.Cone(), M("#FFFFFF", 0.5f), new Vector3(0, 2.0f + i * 1.1f, 0), new Vector3(1.6f - i * 0.4f, 0.9f, 1.6f - i * 0.4f)); }
                    break;
                case "snowman":
                    var snowM = M("#FFFFFF", 0.5f);
                    Shapes.Part(t, S, snowM, new Vector3(0, 0.7f, 0), new Vector3(1.4f, 1.3f, 1.4f)); Shapes.Part(t, S, snowM, new Vector3(0, 1.75f, 0), new Vector3(1f, 0.95f, 1f)); Shapes.Part(t, S, snowM, new Vector3(0, 2.55f, 0), Vector3.one * 0.7f);
                    Shapes.Part(t, Shapes.Cone(), M("#FF8C1A", 0.5f), new Vector3(0, 2.55f, -0.3f), new Vector3(0.12f, 0.4f, 0.12f), new Vector3(-90, 0, 0));
                    for (int s = -1; s <= 1; s += 2) Shapes.Part(t, S, M("#111111", 0.6f), new Vector3(s * 0.13f, 2.68f, -0.3f), Vector3.one * 0.08f);
                    Shapes.Part(t, C, M("#222222", 0.5f), new Vector3(0, 3.0f, 0), new Vector3(0.55f, 0.03f, 0.55f)); Shapes.Part(t, C, M("#222222", 0.5f), new Vector3(0, 3.25f, 0), new Vector3(0.38f, 0.25f, 0.38f));
                    Shapes.Part(t, Shapes.Torus(), M("#E63946", 0.4f), new Vector3(0, 2.2f, 0), new Vector3(0.9f, 1.4f, 0.9f));
                    break;
                case "igloo":
                    var ice = M("#EAF6FF", 0.6f);
                    Shapes.Part(t, S, ice, Vector3.zero, new Vector3(4f, 3.4f, 4f)); Shapes.Part(t, C, ice, new Vector3(0, 0.6f, -1.9f), new Vector3(1.3f, 0.6f, 1.2f), new Vector3(90, 0, 0));
                    Shapes.Part(t, C, M("#2B3A4A", 0.2f), new Vector3(0, 0.55f, -2.5f), new Vector3(0.8f, 0.025f, 0.8f), new Vector3(90, 0, 0));
                    for (int r = 0; r < 3; r++) Shapes.Part(t, Shapes.Torus(), M("#CFE6F7", 0.5f), new Vector3(0, 0.4f + r * 0.45f, 0), new Vector3(4.4f - r * 0.9f, 1.5f, 4.4f - r * 0.9f));
                    break;
                case "frozen_pond": castShadow = false; Shapes.Part(t, C, MaterialLibrary.Lit("#BFE8FA", 0.95f, 0.2f), new Vector3(0, 0.02f, 0), new Vector3(5f, 0.01f, 3.5f)); break;
                case "sled":
                    Shapes.Part(t, B, M("#C0392B", 0.5f), new Vector3(0, 0.45f, 0), new Vector3(0.9f, 0.15f, 1.8f));
                    for (int s = -1; s <= 1; s += 2) Shapes.Part(t, B, M("#8B5A2B", 0.4f), new Vector3(s * 0.4f, 0.2f, 0), new Vector3(0.08f, 0.08f, 2.1f));
                    break;
                case "penguin":
                    Shapes.Part(t, S, M("#1E2230", 0.5f), new Vector3(0, 0.5f, 0), new Vector3(0.6f, 0.9f, 0.55f)); Shapes.Part(t, S, M("#FFFFFF", 0.5f), new Vector3(0, 0.45f, -0.12f), new Vector3(0.42f, 0.7f, 0.4f));
                    Shapes.Part(t, Shapes.Cone(), M("#FFA62B", 0.5f), new Vector3(0, 0.72f, -0.26f), new Vector3(0.14f, 0.22f, 0.14f), new Vector3(-90, 0, 0));
                    break;
                case "butterflies": case "birds": default:
                    castShadow = false; break; // animated by particles / WorldLife
            }
            Shapes.SetLayerAndShadows(go, castShadow);
            AddLod(go);
            go.SetActive(false);
            return go;
        }

        /// <summary>Culls small scenery when it is tiny on screen (cheap LOD for mobile).</summary>
        private static void AddLod(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            var lod = go.AddComponent<LODGroup>();
            lod.SetLODs(new[] { new LOD(0.015f, renderers) });
            lod.RecalculateBounds();
        }
    }
}
