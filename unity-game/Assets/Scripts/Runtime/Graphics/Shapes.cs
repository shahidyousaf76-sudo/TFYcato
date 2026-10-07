using System.Collections.Generic;
using UnityEngine;

namespace TFY.CatAdventure.Graphics
{
    /// <summary>Primitive and procedural mesh helpers for the stylised art (no colliders — collision is lane-based).</summary>
    public static class Shapes
    {
        private static readonly Dictionary<PrimitiveType, Mesh> primitive = new Dictionary<PrimitiveType, Mesh>();
        private static Mesh star, gem, fish, heart, cone, roundedBox, torus;

        public static Mesh Primitive(PrimitiveType t)
        {
            if (primitive.TryGetValue(t, out var m) && m) return m;
            var go = GameObject.CreatePrimitive(t); m = go.GetComponent<MeshFilter>().sharedMesh; Object.Destroy(go);
            return primitive[t] = m;
        }

        public static GameObject Part(Transform parent, PrimitiveType t, Material mat, Vector3 pos, Vector3 scale, Vector3 euler = default, string name = null)
            => Part(parent, Primitive(t), mat, pos, scale, euler, name ?? t.ToString());

        public static GameObject Part(Transform parent, Mesh mesh, Material mat, Vector3 pos, Vector3 scale, Vector3 euler = default, string name = "part")
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localEulerAngles = euler; go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat;
            return go;
        }

        public static GameObject Group(Transform parent, string name, Vector3 pos = default)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = pos; return go;
        }

        /// <summary>Five-pointed star, extruded (big collectible stars, UI stars in 3D).</summary>
        public static Mesh Star() => star ? star : star = Extrude(StarOutline(5, 0.5f, 0.22f), 0.16f, "star");
        public static Mesh Heart() => heart ? heart : heart = Extrude(HeartOutline(), 0.14f, "heart");
        public static Mesh Fish() => fish ? fish : fish = Extrude(FishOutline(), 0.12f, "fish");
        public static Mesh Cone() => cone ? cone : cone = Lathe(new[] { new Vector2(0.5f, 0), new Vector2(0.001f, 1) }, 16, "cone");
        public static Mesh RoundedBox() => roundedBox ? roundedBox : roundedBox = Primitive(PrimitiveType.Cube);
        public static Mesh Torus() => torus ? torus : torus = BuildTorus(0.4f, 0.1f, 24, 10);

        /// <summary>Faceted gem (octahedron-style brilliant).</summary>
        public static Mesh Gem()
        {
            if (gem) return gem;
            var v = new List<Vector3>(); var tris = new List<int>(); int n = 8;
            var top = new Vector3(0, 0.5f, 0); var bottom = new Vector3(0, -0.45f, 0);
            for (int i = 0; i < n; i++)
            {
                float a0 = i * Mathf.PI * 2 / n, a1 = (i + 1) * Mathf.PI * 2 / n;
                var p0 = new Vector3(Mathf.Cos(a0) * 0.42f, 0.12f, Mathf.Sin(a0) * 0.42f); var p1 = new Vector3(Mathf.Cos(a1) * 0.42f, 0.12f, Mathf.Sin(a1) * 0.42f);
                AddTri(v, tris, top, p1, p0); AddTri(v, tris, bottom, p0, p1);
            }
            gem = new Mesh { name = "gem" }; gem.SetVertices(v); gem.SetTriangles(tris, 0); gem.RecalculateNormals(); gem.RecalculateBounds();
            return gem;
        }

        private static void AddTri(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c) { int i = v.Count; v.Add(a); v.Add(b); v.Add(c); t.Add(i); t.Add(i + 1); t.Add(i + 2); }

        private static List<Vector2> StarOutline(int points, float r1, float r2)
        {
            var list = new List<Vector2>();
            for (int i = 0; i < points * 2; i++) { float a = Mathf.PI / 2 + i * Mathf.PI / points; float r = i % 2 == 0 ? r1 : r2; list.Add(new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r)); }
            return list;
        }

        private static List<Vector2> HeartOutline()
        {
            var list = new List<Vector2>();
            for (int i = 0; i < 32; i++) { float t = i / 32f * Mathf.PI * 2; list.Add(new Vector2(16 * Mathf.Pow(Mathf.Sin(t), 3), 13 * Mathf.Cos(t) - 5 * Mathf.Cos(2 * t) - 2 * Mathf.Cos(3 * t) - Mathf.Cos(4 * t)) / 34f); }
            return list;
        }

        private static List<Vector2> FishOutline()
        {
            var list = new List<Vector2>();
            for (int i = 0; i <= 16; i++) { float t = i / 16f * Mathf.PI; list.Add(new Vector2(Mathf.Cos(t) * 0.42f + 0.05f, Mathf.Sin(t) * 0.2f)); }
            list.Add(new Vector2(-0.4f, 0.0f)); list.Add(new Vector2(-0.58f, 0.2f)); list.Add(new Vector2(-0.55f, 0f)); list.Add(new Vector2(-0.58f, -0.2f)); list.Add(new Vector2(-0.4f, 0.0f));
            for (int i = 16; i >= 0; i--) { float t = i / 16f * Mathf.PI; list.Add(new Vector2(Mathf.Cos(t) * 0.42f + 0.05f, -Mathf.Sin(t) * 0.2f)); }
            // de-duplicate consecutive points
            var clean = new List<Vector2>(); foreach (var p in list) if (clean.Count == 0 || (clean[clean.Count - 1] - p).sqrMagnitude > 1e-6f) clean.Add(p);
            return clean;
        }

        /// <summary>Extrudes a star-shaped (from the centroid) outline into a closed mesh.</summary>
        public static Mesh Extrude(List<Vector2> outline, float depth, string name)
        {
            var v = new List<Vector3>(); var t = new List<int>();
            Vector2 c = Vector2.zero; foreach (var p in outline) c += p; c /= outline.Count;
            float h = depth / 2; int n = outline.Count;
            for (int side = 0; side < 2; side++)
            {
                float z = side == 0 ? -h : h;
                int ci = v.Count; v.Add(new Vector3(c.x, c.y, z));
                for (int i = 0; i < n; i++) v.Add(new Vector3(outline[i].x, outline[i].y, z));
                for (int i = 0; i < n; i++) { int a = ci + 1 + i, b = ci + 1 + (i + 1) % n; if (side == 0) { t.Add(ci); t.Add(b); t.Add(a); } else { t.Add(ci); t.Add(a); t.Add(b); } }
            }
            for (int i = 0; i < n; i++)
            {
                var a = outline[i]; var b = outline[(i + 1) % n]; int s = v.Count;
                v.Add(new Vector3(a.x, a.y, -h)); v.Add(new Vector3(b.x, b.y, -h)); v.Add(new Vector3(b.x, b.y, h)); v.Add(new Vector3(a.x, a.y, h));
                t.Add(s); t.Add(s + 1); t.Add(s + 2); t.Add(s); t.Add(s + 2); t.Add(s + 3);
            }
            var m = new Mesh { name = name }; m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        public static Mesh Lathe(Vector2[] profile, int segments, string name)
        {
            var v = new List<Vector3>(); var t = new List<int>();
            for (int s = 0; s <= segments; s++)
            {
                float a = s * Mathf.PI * 2 / segments;
                foreach (var p in profile) v.Add(new Vector3(Mathf.Cos(a) * p.x, p.y, Mathf.Sin(a) * p.x));
            }
            int rows = profile.Length;
            for (int s = 0; s < segments; s++) for (int r = 0; r < rows - 1; r++)
            {
                int i0 = s * rows + r, i1 = (s + 1) * rows + r;
                t.Add(i0); t.Add(i0 + 1); t.Add(i1); t.Add(i1); t.Add(i0 + 1); t.Add(i1 + 1);
            }
            var m = new Mesh { name = name }; m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        private static Mesh BuildTorus(float R, float r, int seg, int side)
        {
            var v = new List<Vector3>(); var t = new List<int>();
            for (int i = 0; i <= seg; i++) for (int j = 0; j <= side; j++)
            {
                float u = i * Mathf.PI * 2 / seg, w = j * Mathf.PI * 2 / side;
                v.Add(new Vector3((R + r * Mathf.Cos(w)) * Mathf.Cos(u), r * Mathf.Sin(w), (R + r * Mathf.Cos(w)) * Mathf.Sin(u)));
            }
            for (int i = 0; i < seg; i++) for (int j = 0; j < side; j++)
            {
                int a = i * (side + 1) + j, b = (i + 1) * (side + 1) + j;
                t.Add(a); t.Add(b); t.Add(a + 1); t.Add(b); t.Add(b + 1); t.Add(a + 1);
            }
            var m = new Mesh { name = "torus" }; m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        public static void SetLayerAndShadows(GameObject root, bool cast, bool receive = true)
        {
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
            {
                r.shadowCastingMode = cast ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = receive;
            }
        }
    }
}
