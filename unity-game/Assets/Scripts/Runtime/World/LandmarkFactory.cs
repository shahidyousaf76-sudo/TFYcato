using System.Collections.Generic;
using TFY.CatAdventure.Graphics;
using UnityEngine;

namespace TFY.CatAdventure.World
{
    /// <summary>
    /// World-famous landmarks, stylised and child-friendly, built from simple PBR shapes (same set, sizes and placement as the
    /// web prototype's landmarks.js). One appears about every 200 m; a banner names it as the cat runs past.
    /// Final art can replace any landmark with a prefab at Resources/Landmarks/{id}.
    /// </summary>
    public static class LandmarkFactory
    {
        public static readonly Dictionary<string, string> Names = new Dictionary<string, string>
        {
            ["eiffel_tower"] = "Eiffel Tower — Paris", ["big_ben"] = "Big Ben — London", ["statue_of_liberty"] = "Statue of Liberty — New York",
            ["leaning_tower_pisa"] = "Leaning Tower of Pisa — Italy", ["taj_mahal"] = "Taj Mahal — India", ["windmill"] = "Windmill — Netherlands",
            ["pagoda"] = "Pagoda — Japan", ["torii_gate"] = "Torii Gate — Japan", ["stonehenge"] = "Stonehenge — England",
            ["great_wall"] = "Great Wall — China", ["christ_the_redeemer"] = "Christ the Redeemer — Brazil",
            ["sydney_opera_house"] = "Sydney Opera House — Australia", ["golden_gate_bridge"] = "Golden Gate Bridge — USA", ["little_mermaid"] = "The Little Mermaid — Copenhagen",
            ["mount_fuji"] = "Mount Fuji — Japan", ["st_basils_cathedral"] = "St. Basil's Cathedral — Moscow", ["santa_village"] = "Santa Claus Village — Finland",
        };

        /// <summary>Distance from the path centre (0 = over the road) and forced side (+1 right / −1 left / 0 alternate).</summary>
        public static (float x, int side) Placement(string id) => id switch
        {
            "eiffel_tower" => (24, 0), "big_ben" => (21, 0), "statue_of_liberty" => (22, 0), "leaning_tower_pisa" => (19, 0), "taj_mahal" => (32, 0),
            "windmill" => (16, 0), "pagoda" => (20, 0), "torii_gate" => (0, 0), "stonehenge" => (22, 0), "great_wall" => (22, -1),
            "christ_the_redeemer" => (42, 0), "sydney_opera_house" => (30, 1), "golden_gate_bridge" => (0, 0), "little_mermaid" => (10, -1),
            "mount_fuji" => (60, 0), "st_basils_cathedral" => (24, 0), "santa_village" => (20, 0),
            _ => (40, 0),
        };

        // ---------------------------------------------------------------- shapes matching three.js conventions (centred, unit size)
        private static Mesh cone, taper, pyramid, torusXY;
        private static Mesh Cone() => cone ? cone : cone = Shapes.Lathe(new[] { new Vector2(0.5f, -0.5f), new Vector2(0.001f, 0.5f) }, 16, "coneC");
        private static Mesh Taper() => taper ? taper : taper = Shapes.Lathe(new[] { new Vector2(0.5f, -0.5f), new Vector2(0.36f, 0.5f) }, 16, "taper");
        private static Mesh Pyramid()
        {
            if (pyramid) return pyramid;
            var v = new List<Vector3>(); var t = new List<int>(); var top = new Vector3(0, 0.5f, 0);
            var c = new[] { new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, 0.5f), new Vector3(-0.5f, -0.5f, 0.5f) };
            for (int i = 0; i < 4; i++) { int s = v.Count; v.Add(top); v.Add(c[(i + 1) % 4]); v.Add(c[i]); t.Add(s); t.Add(s + 1); t.Add(s + 2); }
            int b = v.Count; v.AddRange(c); t.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
            pyramid = new Mesh { name = "pyramid" }; pyramid.SetVertices(v); pyramid.SetTriangles(t, 0); pyramid.RecalculateNormals(); pyramid.RecalculateBounds();
            return pyramid;
        }
        private static Mesh TorusXY()
        {
            if (torusXY) return torusXY;
            var src = Shapes.Torus(); var verts = src.vertices;
            for (int i = 0; i < verts.Length; i++) verts[i] = new Vector3(verts[i].x, verts[i].z, verts[i].y);
            var tris = src.triangles; for (int i = 0; i < tris.Length; i += 3) { int x = tris[i + 1]; tris[i + 1] = tris[i + 2]; tris[i + 2] = x; } // keep facing outwards after the swap
            torusXY = new Mesh { name = "torusXY", vertices = verts, triangles = tris }; torusXY.RecalculateNormals(); torusXY.RecalculateBounds();
            return torusXY;
        }

        private enum K { Box, Sphere, Cyl, Cone, Taper, Pyramid, Torus }
        private static void P(Transform parent, K k, Material m, float x, float y, float z, float sx, float sy, float sz, float rx = 0, float ry = 0, float rz = 0)
        {
            Mesh mesh = k switch
            {
                K.Box => Shapes.Primitive(PrimitiveType.Cube), K.Sphere => Shapes.Primitive(PrimitiveType.Sphere), K.Cyl => Shapes.Primitive(PrimitiveType.Cylinder),
                K.Cone => Cone(), K.Taper => Taper(), K.Pyramid => Pyramid(), _ => TorusXY(),
            };
            if (k == K.Cyl) sy *= 0.5f; // Unity's cylinder is 2 units tall
            Shapes.Part(parent, mesh, m, new Vector3(x, y, z), new Vector3(sx, sy, sz), new Vector3(rx, ry, rz) * Mathf.Rad2Deg, k.ToString());
        }
        private static Material M(string hex, float rough = 0.6f, float metal = 0) => MaterialLibrary.Lit(hex, 1f - rough, metal);
        private static Material Glow(string hex, float intensity = 0.4f) { var m = MaterialLibrary.Glow(MaterialLibrary.Hex(hex), intensity); if (!DecorFactory.NightEmissive.Contains(m)) DecorFactory.NightEmissive.Add(m); return m; }

        /// <summary>Builds a landmark (base at y = 0). Returns null for an unknown id.</summary>
        public static GameObject Build(string id, Transform parent)
        {
            var art = Resources.Load<GameObject>("Landmarks/" + id);
            if (art) return Object.Instantiate(art, parent);
            var go = new GameObject("Landmark_" + id); go.transform.SetParent(parent, false); var g = go.transform;
            switch (id)
            {
                case "eiffel_tower":
                {
                    var iron = M("#8C6A4A", 0.55f, 0.6f); var lamp = Glow("#FFD27A");
                    for (int sx = -1; sx <= 1; sx += 2) for (int sz = -1; sz <= 1; sz += 2)
                    {
                        P(g, K.Cyl, iron, sx * 4.4f, 5, sz * 4.4f, 1, 11.5f, 1, -sz * 0.33f, 0, sx * 0.33f);
                        P(g, K.Cyl, iron, sx * 2.25f, 15, sz * 2.25f, 0.6f, 10.5f, 0.6f, -sz * 0.13f, 0, sx * 0.13f);
                    }
                    for (int s = -1; s <= 1; s += 2) { P(g, K.Torus, iron, s * 4.3f, 6.2f, 0, 2.4f, 2.4f, 1.2f, 0, Mathf.PI / 2); P(g, K.Torus, iron, 0, 6.2f, s * 4.3f, 2.4f, 2.4f, 1.2f); }
                    P(g, K.Box, iron, 0, 10, 0, 9.5f, 0.9f, 9.5f); P(g, K.Box, iron, 0, 20, 0, 4.8f, 0.7f, 4.8f);
                    P(g, K.Pyramid, iron, 0, 26.5f, 0, 3.4f, 13, 3.4f); P(g, K.Box, iron, 0, 31, 0, 1.4f, 1, 1.4f); P(g, K.Cyl, iron, 0, 33.5f, 0, 0.18f, 4, 0.18f);
                    foreach (var y in new[] { 10.6f, 20.5f, 31.6f }) for (int i = 0; i < 8; i++) { float a = i / 8f * Mathf.PI * 2, r = y < 15 ? 4.6f : y < 25 ? 2.4f : 0.8f; P(g, K.Sphere, lamp, Mathf.Cos(a) * r, y, Mathf.Sin(a) * r, 0.35f, 0.35f, 0.35f); }
                    break;
                }
                case "big_ben":
                {
                    var stone = M("#D8C38A", 0.8f); var dark = M("#3E5F55"); var face = Glow("#FFF6D8", 0.3f); var hand = M("#222222", 0.4f); var gold = M("#FFD54A", 0.3f, 0.9f);
                    P(g, K.Box, stone, 0, 9, 0, 5, 18, 5);
                    foreach (var y in new[] { 4f, 8f, 12f, 16f }) P(g, K.Box, M("#C2AC70", 0.8f), 0, y, 0, 5.3f, 0.4f, 5.3f);
                    P(g, K.Box, stone, 0, 20.5f, 0, 5.8f, 5, 5.8f);
                    for (int s = 0; s < 4; s++)
                    {
                        var f = Shapes.Group(g, "Clock", new Vector3(0, 20.5f, 0)).transform; f.localRotation = Quaternion.Euler(0, s * 90, 0);
                        P(f, K.Cyl, face, 0, 0, -2.95f, 4, 0.12f, 4, Mathf.PI / 2); P(f, K.Box, hand, 0.5f, 0.3f, -3.05f, 0.2f, 1.5f, 0.05f, 0, 0, -0.8f); P(f, K.Box, hand, -0.2f, 0.6f, -3.05f, 0.2f, 1.1f, 0.05f, 0, 0, 0.3f);
                    }
                    P(g, K.Box, stone, 0, 24.5f, 0, 4.6f, 3, 4.6f); P(g, K.Pyramid, dark, 0, 29.5f, 0, 5.2f, 7, 5.2f); P(g, K.Sphere, gold, 0, 33.4f, 0, 0.6f, 0.9f, 0.6f);
                    break;
                }
                case "statue_of_liberty":
                {
                    var stone = M("#B9A88C", 0.85f); var copper = M("#7FC4AE", 0.6f, 0.3f); var flame = Glow("#FFB13B", 2.2f);
                    P(g, K.Box, stone, 0, 1, 0, 9, 2, 9); P(g, K.Box, stone, 0, 7, 0, 6, 10, 6); P(g, K.Box, stone, 0, 12.4f, 0, 6.6f, 0.8f, 6.6f);
                    P(g, K.Cone, copper, 0, 16.8f, 0, 4.2f, 9.6f, 4.2f); P(g, K.Cyl, copper, 0, 21.8f, 0, 1.6f, 3, 1.6f); P(g, K.Sphere, copper, 0, 24.1f, 0, 1.7f, 1.9f, 1.7f);
                    for (int i = 0; i < 7; i++) { float a = -Mathf.PI / 2 + (i - 3) * 0.4f; P(g, K.Cone, copper, Mathf.Cos(a) * 0.9f, 25.3f + Mathf.Sin(a + Mathf.PI / 2) * 0.2f, Mathf.Sin(a) * 0.9f, 0.35f, 1.3f, 0.35f, Mathf.Sin(a) * 0.9f, 0, -Mathf.Cos(a) * 0.9f); }
                    P(g, K.Cyl, copper, 1.5f, 26, 0, 0.55f, 5, 0.55f, 0, 0, -0.25f); P(g, K.Cyl, M("#D4AF37", 0.3f, 0.9f), 2.1f, 28.7f, 0, 0.6f, 1, 0.6f); P(g, K.Sphere, flame, 2.1f, 29.6f, 0, 0.9f, 1.3f, 0.9f);
                    P(g, K.Box, copper, -1.4f, 21, -0.7f, 1.3f, 2.2f, 0.4f, 0, 0, 0.3f);
                    break;
                }
                case "leaning_tower_pisa":
                {
                    var tower = Shapes.Group(g, "Tower").transform; tower.localRotation = Quaternion.Euler(0, 0, 0.09f * Mathf.Rad2Deg);
                    var marble = M("#F2EDE2"); var ring = M("#E2DACB");
                    for (int i = 0; i < 7; i++) { P(tower, K.Cyl, marble, 0, 1.5f + i * 3, 0, 6, 3, 6); P(tower, K.Torus, ring, 0, 3 + i * 3, 0, 7.6f, 7.6f, 1.2f, Mathf.PI / 2); }
                    P(tower, K.Cyl, marble, 0, 22.5f, 0, 4.4f, 3, 4.4f); P(tower, K.Sphere, marble, 0, 24.2f, 0, 4.4f, 1.4f, 4.4f);
                    break;
                }
                case "taj_mahal":
                {
                    var white = M("#FBF8F2", 0.5f); var shade = M("#D9D2C5", 0.7f); var gold = M("#E8C35A", 0.3f, 0.9f);
                    P(g, K.Box, white, 0, 0.75f, 0, 26, 1.5f, 26); P(g, K.Box, white, 0, 6.5f, 0, 13, 10, 13);
                    for (int s = 0; s < 4; s++) { var f = Shapes.Group(g, "Arch").transform; f.localRotation = Quaternion.Euler(0, s * 90, 0); P(f, K.Box, shade, 0, 5.5f, -6.55f, 4, 6.5f, 0.2f); }
                    P(g, K.Cyl, white, 0, 12.3f, 0, 6, 1.6f, 6); P(g, K.Sphere, white, 0, 16, 0, 8, 9, 8); P(g, K.Cone, white, 0, 21.3f, 0, 1.4f, 2.4f, 1.4f); P(g, K.Sphere, gold, 0, 22.8f, 0, 0.5f, 0.8f, 0.5f);
                    for (int sx = -1; sx <= 1; sx += 2) for (int sz = -1; sz <= 1; sz += 2)
                    {
                        P(g, K.Sphere, white, sx * 4.5f, 13, sz * 4.5f, 2.6f, 2.8f, 2.6f);
                        P(g, K.Cyl, white, sx * 11, 9.5f, sz * 11, 1, 17, 1); P(g, K.Sphere, white, sx * 11, 18.6f, sz * 11, 1.6f, 1.8f, 1.6f);
                    }
                    break;
                }
                case "windmill":
                {
                    P(g, K.Taper, M("#E9DCC9", 0.8f), 0, 5, 0, 5, 10, 5); P(g, K.Cone, M("#7A2E2E", 0.7f), 0, 11.5f, 0, 4.2f, 3, 4.2f);
                    P(g, K.Box, M("#5C3A21", 0.7f), 0, 1.5f, -2.45f, 1.3f, 2.4f, 0.2f);
                    var hub = Shapes.Group(g, "Hub", new Vector3(0, 10.2f, -2.6f)).transform; hub.gameObject.AddComponent<Spinner>();
                    P(hub, K.Sphere, M("#5C3A21"), 0, 0, 0, 0.8f, 0.8f, 0.8f);
                    for (int i = 0; i < 4; i++) { var bl = Shapes.Group(hub, "Blade").transform; bl.localRotation = Quaternion.Euler(0, 0, i * 90); P(bl, K.Box, M("#F5F0E6", 0.7f), 0, 3.6f, 0, 1.3f, 6.4f, 0.15f); P(bl, K.Box, M("#8B5A2B", 0.7f), 0, 3.6f, -0.1f, 0.18f, 7, 0.15f); }
                    break;
                }
                case "pagoda":
                {
                    var red = M("#C0392B"); var roof = M("#3B2F2F"); var gold = M("#E8C35A", 0.3f, 0.9f);
                    for (int i = 0; i < 5; i++) { float s = 7 - i * 1.1f, y = i * 3.6f; P(g, K.Box, red, 0, y + 1.5f, 0, s, 3, s); P(g, K.Pyramid, roof, 0, y + 3.4f, 0, s * 1.6f, 1.2f, s * 1.6f); }
                    P(g, K.Cyl, gold, 0, 20.5f, 0, 0.3f, 4, 0.3f); for (int i = 0; i < 4; i++) P(g, K.Torus, gold, 0, 19 + i * 0.7f, 0, 0.6f, 0.6f, 0.6f, Mathf.PI / 2);
                    break;
                }
                case "torii_gate":
                {
                    var red = M("#D7263D", 0.5f); var black = M("#1E1E24", 0.5f);
                    foreach (var x in new[] { -5.2f, 5.2f }) { P(g, K.Cyl, red, x, 4.6f, 0, 0.9f, 9.2f, 0.9f); P(g, K.Cyl, black, x, 0.4f, 0, 1.1f, 0.8f, 1.1f); }
                    P(g, K.Box, red, 0, 7.6f, 0, 11.6f, 0.6f, 0.7f); P(g, K.Box, red, 0, 9.4f, 0, 14, 0.8f, 1.1f); P(g, K.Box, black, 0, 9.95f, 0, 14.6f, 0.35f, 1.3f); P(g, K.Box, red, 0, 8.5f, 0, 0.6f, 1.8f, 0.5f);
                    break;
                }
                case "stonehenge":
                {
                    var stone = M("#9A968C", 0.95f); var dark = M("#85817A", 0.95f);
                    for (int i = 0; i < 14; i++) { float a = i / 14f * Mathf.PI * 2; P(g, K.Box, stone, Mathf.Cos(a) * 9, 2.1f, Mathf.Sin(a) * 9, 1.4f, 4.2f, 0.9f, 0, -a); if (i % 2 == 0) P(g, K.Box, dark, Mathf.Cos(a + 0.22f) * 9, 4.5f, Mathf.Sin(a + 0.22f) * 9, 1, 0.7f, 4.2f, 0, -a - 0.22f); }
                    for (int i = 0; i < 5; i++) { float a = Mathf.PI * 0.2f + i * Mathf.PI * 0.4f, x = Mathf.Cos(a) * 4.5f, z = Mathf.Sin(a) * 4.5f; P(g, K.Box, stone, x - 0.9f, 3, z, 1.4f, 6, 1.1f); P(g, K.Box, stone, x + 0.9f, 3, z, 1.4f, 6, 1.1f); P(g, K.Box, dark, x, 6.4f, z, 3.4f, 0.9f, 1.3f); }
                    break;
                }
                case "great_wall":
                {
                    var brick = M("#B59B7A", 0.9f); var top = M("#9C8263", 0.9f); var roof = M("#5A3E2B", 0.7f);
                    for (int i = 0; i < 12; i++)
                    {
                        float z = -i * 10, y = Mathf.Sin(i * 0.7f) * 1.5f;
                        P(g, K.Box, brick, 0, 2.5f + y, z, 3.5f, 5, 10.2f);
                        for (int k = 0; k < 4; k++) for (int s = -1; s <= 1; s += 2) P(g, K.Box, top, s * 1.6f, 5.4f + y, z - 4 + k * 2.6f, 0.4f, 0.8f, 1.2f);
                        if (i % 4 == 0) { P(g, K.Box, brick, 0, 4.5f + y, z, 6, 9, 6); P(g, K.Pyramid, roof, 0, 10 + y, z, 7.5f, 2.2f, 7.5f); }
                    }
                    break;
                }
                case "christ_the_redeemer":
                {
                    var rock = M("#6E7F5E", 0.95f); var white = M("#F2F0EA", 0.5f);
                    P(g, K.Cone, rock, 0, 13, 0, 34, 26, 34); P(g, K.Box, white, 0, 27.5f, 0, 2.4f, 3, 2.4f);
                    P(g, K.Taper, white, 0, 32, 0, 2.2f, 6, 2.2f); P(g, K.Box, white, 0, 34.2f, 0, 10, 0.9f, 1); P(g, K.Sphere, white, 0, 35.8f, 0, 1.1f, 1.3f, 1.1f);
                    break;
                }
                case "sydney_opera_house":
                {
                    var deck = M("#D9C8A9", 0.85f); var shell = M("#FFFFFF", 0.25f, 0.1f);
                    P(g, K.Box, deck, 0, 1, 0, 28, 2, 16);
                    foreach (var (x, z, s, r) in new[] { (-8f, 2f, 1f, -0.5f), (-3f, 2f, 1.15f, -0.55f), (3f, 2f, 1.25f, -0.6f), (-6f, -4f, 0.75f, -0.5f), (-1.5f, -4f, 0.85f, -0.55f), (9f, -1f, 0.7f, -0.45f) })
                        P(g, K.Sphere, shell, x, 2, z, 6 * s, 11 * s, 4.5f * s, r);
                    break;
                }
                case "golden_gate_bridge":
                {
                    var red = M("#C0362C", 0.55f, 0.2f); var road = M("#4A4A4A", 0.7f);
                    P(g, K.Box, red, 0, 8, 0, 9, 1.2f, 100); P(g, K.Box, road, 0, 8.7f, 0, 7.6f, 0.2f, 100);
                    foreach (var tz in new[] { -26f, 26f })
                    {
                        foreach (var x in new[] { -4f, 4f }) { P(g, K.Box, red, x, 12, tz, 1.4f, 26, 1.8f); P(g, K.Cyl, red, x, 0, tz, 2.2f, 6, 2.2f); }
                        foreach (var y in new[] { 10f, 17f, 23.5f }) P(g, K.Box, red, 0, y, tz, 8, 1, 1.2f);
                    }
                    foreach (var x in new[] { -4.2f, 4.2f })
                        for (float z = -50; z <= 50.01f; z += 1.6f)
                        {
                            float y = Mathf.Abs(z) <= 26 ? 24.5f - 14 * (1 - (z / 26) * (z / 26)) : 24.5f - (Mathf.Abs(z) - 26) / 24 * 15;
                            P(g, K.Sphere, red, x, y, z, 0.5f, 0.5f, 0.5f);
                            if (Mathf.RoundToInt(z * 10) % 48 == 0 && y > 9.5f) P(g, K.Box, red, x, (y + 9) / 2, z, 0.12f, y - 9, 0.12f);
                        }
                    break;
                }
                case "little_mermaid":
                {
                    var rock = M("#8C8A85", 0.95f); var bronze = M("#5E8C6A", 0.45f, 0.6f);
                    P(g, K.Sphere, rock, 0, 0.8f, 0, 4.5f, 2.4f, 3.5f); P(g, K.Sphere, rock, 1.4f, 1.4f, 0.6f, 2.2f, 1.8f, 2);
                    P(g, K.Cyl, bronze, 0.3f, 2.9f, 0, 0.6f, 1.5f, 0.5f, 0, 0, 0.25f); P(g, K.Sphere, bronze, 0.5f, 3.95f, 0, 0.6f, 0.65f, 0.6f);
                    P(g, K.Cone, bronze, -0.7f, 2.25f, 0.2f, 0.55f, 2, 0.55f, 0, 0, 1.35f); P(g, K.Box, bronze, -1.8f, 2, 0.2f, 0.9f, 0.12f, 0.6f, 0, 0, 0.3f);
                    break;
                }
                case "mount_fuji":
                {
                    var snowM = M("#FFFFFF", 0.5f);
                    P(g, K.Cone, M("#8FA6D6", 0.95f), 0, 17, 0, 70, 34, 70); P(g, K.Cone, snowM, 0, 28.5f, 0, 25, 11.2f, 25);
                    for (int i = 0; i < 8; i++) { float a = i / 8f * Mathf.PI * 2; P(g, K.Cone, snowM, Mathf.Cos(a) * 11, 20.5f, Mathf.Sin(a) * 11, 4, 6, 4, Mathf.Sin(a) * 0.5f, 0, -Mathf.Cos(a) * 0.5f); }
                    P(g, K.Cyl, M("#DDE8F5", 0.9f), 0, 34.2f, 0, 6, 0.6f, 6);
                    break;
                }
                case "st_basils_cathedral":
                {
                    var wall = M("#B03A2E", 0.7f); var white = M("#F4EFE6"); var gold = M("#FFD54A", 0.3f, 0.9f);
                    P(g, K.Box, wall, 0, 4, 0, 14, 8, 14); P(g, K.Box, white, 0, 8.3f, 0, 14.6f, 0.6f, 14.6f);
                    string[][] domes = { new[] { "#2E86DE", "#FFD54A" }, new[] { "#27AE60", "#E74C3C" }, new[] { "#E67E22", "#2E86DE" }, new[] { "#E74C3C", "#27AE60" }, new[] { "#F1C40F", "#8E44AD" } };
                    var spots = new[] { (0f, 0f, 1.4f), (-5f, -5f, 1f), (5f, -5f, 1f), (-5f, 5f, 1f), (5f, 5f, 1f) };
                    for (int i = 0; i < spots.Length; i++)
                    {
                        var (x, z, s) = spots[i]; float h = 8 + (s > 1 ? 10 : 6);
                        P(g, K.Cyl, wall, x, 8 + (h - 8) / 2, z, 2.6f * s, h - 8, 2.6f * s);
                        var dome = M(domes[i][0], 0.5f); var stripe = M(domes[i][1], 0.5f);
                        P(g, K.Sphere, dome, x, h + 1.5f * s, z, 3.6f * s, 3.6f * s, 3.6f * s);
                        for (int k = 0; k < 3; k++) P(g, K.Torus, stripe, x, h + (0.5f + k * 1.1f) * s, z, 3.4f * s * (1 - k * 0.22f), 3.4f * s * (1 - k * 0.22f), 0.6f, Mathf.PI / 2);
                        P(g, K.Cone, dome, x, h + 3.6f * s, z, 1.6f * s, 2.6f * s, 1.6f * s); P(g, K.Cyl, gold, x, h + 5.4f * s, z, 0.15f, 1.6f, 0.15f); P(g, K.Box, gold, x, h + 5.6f * s, z, 0.9f, 0.12f, 0.12f);
                    }
                    P(g, K.Pyramid, M("#2E7D5B"), 8.5f, 11, 0, 3, 6, 3); P(g, K.Box, wall, 8.5f, 4, 0, 3, 8, 3);
                    break;
                }
                case "santa_village":
                {
                    var wood = M("#8B4A2B", 0.8f); var snowM = M("#FFFFFF", 0.5f); var red = M("#D62828"); var win = Glow("#FFD27A", 0.6f);
                    foreach (var (x, z, s) in new[] { (0f, 0f, 1.3f), (-8f, 4f, 1f), (7f, 5f, 0.9f) })
                    {
                        P(g, K.Box, wood, x, 2 * s, z, 6 * s, 4 * s, 5 * s); P(g, K.Pyramid, snowM, x, 4 * s + 1.6f * s, z, 8.2f * s, 3.2f * s, 7 * s);
                        P(g, K.Box, win, x - 1.5f * s, 2.2f * s, z - 2.52f * s, 1.1f * s, 1.1f * s, 0.05f); P(g, K.Box, win, x + 1.5f * s, 2.2f * s, z - 2.52f * s, 1.1f * s, 1.1f * s, 0.05f);
                        P(g, K.Box, M("#5C3A21", 0.7f), x, 1.2f * s, z - 2.52f * s, 1.1f * s, 2.4f * s, 0.05f);
                    }
                    P(g, K.Cyl, M("#FFFFFF", 0.4f), 3.5f, 3, -6, 0.25f, 6, 0.25f); P(g, K.Sphere, red, 3.5f, 6.2f, -6, 0.6f, 0.6f, 0.6f); // the Arctic Circle pole
                    for (int i = 0; i < 5; i++) P(g, K.Cyl, i % 2 == 1 ? red : M("#FFFFFF", 0.4f), 3.5f, 0.6f + i * 1.1f, -6, 0.27f, 0.5f, 0.27f);
                    P(g, K.Box, red, -2, 0.6f, -6, 2.4f, 0.7f, 1.2f); for (int k = -1; k <= 1; k += 2) P(g, K.Box, M("#FFD54A", 0.3f, 0.9f), -2, 0.15f, -6 + k * 0.5f, 2.8f, 0.1f, 0.1f); // Santa's sleigh
                    for (int i = 0; i < 6; i++) { float a = i / 6f * Mathf.PI * 2; P(g, K.Cone, M("#1B6F55", 0.8f), Mathf.Cos(a) * 13, 3, Mathf.Sin(a) * 9, 3, 6, 3); P(g, K.Cone, snowM, Mathf.Cos(a) * 13, 4.2f, Mathf.Sin(a) * 9, 1.8f, 2.4f, 1.8f); }
                    break;
                }
                default: Object.Destroy(go); return null;
            }
            Shapes.SetLayerAndShadows(go, false);
            var lod = go.AddComponent<LODGroup>(); lod.SetLODs(new[] { new LOD(0.004f, go.GetComponentsInChildren<Renderer>()) }); lod.RecalculateBounds();
            return go;
        }
    }

    /// <summary>Turns windmill sails slowly.</summary>
    public class Spinner : MonoBehaviour
    {
        public float degreesPerSecond = 40f;
        private void Update() => transform.Rotate(0, 0, degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
