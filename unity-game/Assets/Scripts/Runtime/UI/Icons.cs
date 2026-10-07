using System.Collections.Generic;
using UnityEngine;

namespace TFY.CatAdventure.UI
{
    /// <summary>
    /// Icons drawn procedurally with signed-distance shapes (anti-aliased, any colour): coin, star, heart, fish, gem, pause,
    /// gear, cart, trophy, gift, back, play, lock, check, box, flag, music, sound, vibrate, help, parent, cat.
    /// Replace with final art by putting a sprite at Resources/Icons/{name}.
    /// </summary>
    public static class Icons
    {
        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        private const int S = 128;

        public static Sprite Get(string name)
        {
            if (cache.TryGetValue(name, out var s) && s) return s;
            s = Resources.Load<Sprite>("Icons/" + name) ?? Draw(name);
            return cache[name] = s;
        }

        private delegate float Sdf(Vector2 p); // p in -1..1, returns signed distance (negative = inside)

        private static Sprite Draw(string name)
        {
            var layers = Layers(name);
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { name = "icon_" + name, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[S * S];
            float aa = 2.5f / S;
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
            {
                var p = new Vector2((x + 0.5f) / S * 2 - 1, (y + 0.5f) / S * 2 - 1);
                var c = new Color(0, 0, 0, 0);
                foreach (var (sdf, col) in layers)
                {
                    float a = Mathf.Clamp01(0.5f - sdf(p) / aa) * col.a;
                    c = new Color(Mathf.Lerp(c.r, col.r, a), Mathf.Lerp(c.g, col.g, a), Mathf.Lerp(c.b, col.b, a), a + c.a * (1 - a));
                }
                px[y * S + x] = c;
            }
            tex.SetPixels(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100);
        }

        // ---------------------------------------------------------------- SDF primitives
        private static float Circle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;
        private static float Box(Vector2 p, Vector2 c, Vector2 h, float round = 0) { var d = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - h + Vector2.one * round; return new Vector2(Mathf.Max(d.x, 0), Mathf.Max(d.y, 0)).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0) - round; }
        private static float Seg(Vector2 p, Vector2 a, Vector2 b, float r) { var pa = p - a; var ba = b - a; float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba)); return (pa - ba * h).magnitude - r; }
        private static float Union(float a, float b) => Mathf.Min(a, b);
        private static float Sub(float a, float b) => Mathf.Max(a, -b);
        /// <summary>Exact signed distance to a 5-point star polygon (distance to nearest edge, sign from a point-in-polygon test).</summary>
        private static float Star(Vector2 p, float r, float inner)
        {
            var v = new Vector2[10];
            for (int i = 0; i < 10; i++) { float a = Mathf.PI / 2 + i * Mathf.PI / 5; float rr = i % 2 == 0 ? r : r * inner; v[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr; }
            float d = float.MaxValue; bool inside = false;
            for (int i = 0, j = 9; i < 10; j = i++)
            {
                var e = v[j] - v[i]; var w = p - v[i];
                var b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
                d = Mathf.Min(d, b.magnitude);
                if ((v[i].y > p.y) != (v[j].y > p.y) && p.x < (v[j].x - v[i].x) * (p.y - v[i].y) / (v[j].y - v[i].y) + v[i].x) inside = !inside;
            }
            return inside ? -d : d;
        }
        private static float Heart(Vector2 p)
        {
            p = new Vector2(Mathf.Abs(p.x), p.y + 0.35f) / 1.1f;
            if (p.x + p.y > 1f) return (p - new Vector2(0.25f, 0.75f)).magnitude - Mathf.Sqrt(2f) / 4f;
            return Mathf.Min((p - new Vector2(0, 1)).magnitude, (p - 0.5f * Mathf.Max(p.x + p.y, 0) * Vector2.one).magnitude) * Mathf.Sign(p.x - p.y);
        }

        private static List<(Sdf, Color)> Layers(string n)
        {
            var white = Color.white; var dark = new Color(0.16f, 0.12f, 0.25f);
            var gold = new Color(1f, 0.78f, 0.15f); var goldLight = new Color(1f, 0.93f, 0.5f);
            var L = new List<(Sdf, Color)>();
            switch (n)
            {
                case "coin": L.Add((p => Circle(p, Vector2.zero, 0.9f), new Color(0.85f, 0.55f, 0.05f))); L.Add((p => Circle(p, Vector2.zero, 0.75f), gold)); L.Add((p => Star(p, 0.5f, 0.45f), goldLight)); break;
                case "star": L.Add((p => Star(p, 0.95f, 0.48f), new Color(0.95f, 0.6f, 0.05f))); L.Add((p => Star(p * 1.18f, 0.95f, 0.48f), gold)); break;
                case "star_empty": L.Add((p => Star(p, 0.95f, 0.48f), new Color(0.55f, 0.55f, 0.65f))); L.Add((p => Star(p * 1.18f, 0.95f, 0.48f), new Color(0.8f, 0.8f, 0.88f))); break;
                case "heart": L.Add((p => Heart(p * 1.05f), new Color(0.85f, 0.1f, 0.25f))); L.Add((p => Heart(p * 1.25f), new Color(1f, 0.3f, 0.42f))); L.Add((p => Circle(p, new Vector2(-0.3f, 0.3f), 0.12f), new Color(1, 1, 1, 0.8f))); break;
                case "heart_empty": L.Add((p => Heart(p * 1.05f), new Color(0.5f, 0.5f, 0.6f))); L.Add((p => Heart(p * 1.3f), new Color(0.78f, 0.78f, 0.85f))); break;
                case "fish": L.Add((p => Union((new Vector2(p.x * 0.7f, p.y) - new Vector2(0.15f, 0)).magnitude - 0.42f, Sub(Box(p, new Vector2(-0.7f, 0), new Vector2(0.25f, 0.4f)), Circle(p, new Vector2(-1.05f, 0), 0.4f))), new Color(0.3f, 0.7f, 1f))); L.Add((p => Circle(p, new Vector2(0.45f, 0.1f), 0.09f), dark)); break;
                case "gem": L.Add((p => Mathf.Max(Mathf.Abs(p.x) * 1.1f + Mathf.Max(p.y, -p.y * 0.6f) - 0.85f, -p.y - 0.85f), new Color(0.6f, 0.2f, 0.95f))); L.Add((p => Mathf.Abs(p.x) * 1.1f + Mathf.Max(p.y - 0.1f, -(p.y - 0.1f) * 0.6f) - 0.45f, new Color(0.85f, 0.6f, 1f))); break;
                case "pause": L.Add((p => Union(Box(p, new Vector2(-0.3f, 0), new Vector2(0.16f, 0.6f), 0.08f), Box(p, new Vector2(0.3f, 0), new Vector2(0.16f, 0.6f), 0.08f)), white)); break;
                case "play": L.Add((p => Mathf.Max(Mathf.Max(-p.x - 0.45f, p.x * 0.55f + Mathf.Abs(p.y) * 0.95f - 0.45f), -1), white)); break;
                case "back": L.Add((p => Union(Seg(p, new Vector2(0.4f, 0.55f), new Vector2(-0.3f, 0), 0.14f), Seg(p, new Vector2(-0.3f, 0), new Vector2(0.4f, -0.55f), 0.14f)), white)); break;
                case "check": L.Add((p => Union(Seg(p, new Vector2(-0.55f, 0), new Vector2(-0.15f, -0.45f), 0.15f), Seg(p, new Vector2(-0.15f, -0.45f), new Vector2(0.6f, 0.45f), 0.15f)), white)); break;
                case "lock": L.Add((p => Box(p, new Vector2(0, -0.25f), new Vector2(0.55f, 0.45f), 0.12f), white)); L.Add((p => Sub(Circle(p, new Vector2(0, 0.25f), 0.42f), Circle(p, new Vector2(0, 0.25f), 0.25f)) + (p.y < 0.2f ? 1 : 0), white)); L.Add((p => Circle(p, new Vector2(0, -0.2f), 0.12f), dark)); break;
                case "gear": L.Add((p => { float a = Mathf.Atan2(p.y, p.x); float r = 0.65f + 0.15f * Mathf.Clamp(Mathf.Cos(a * 8) * 3, -1, 1); return Sub(p.magnitude - r, Circle(p, Vector2.zero, 0.28f)); }, white)); break;
                case "cart": L.Add((p => Union(Union(Box(p, new Vector2(0.05f, 0.1f), new Vector2(0.6f, 0.32f), 0.08f), Seg(p, new Vector2(-0.85f, 0.6f), new Vector2(-0.55f, 0.4f), 0.08f)), Union(Circle(p, new Vector2(-0.3f, -0.55f), 0.14f), Circle(p, new Vector2(0.4f, -0.55f), 0.14f))), white)); break;
                case "trophy": L.Add((p => Union(Union(Sub(Circle(p, new Vector2(0, 0.3f), 0.55f), Box(p, new Vector2(0, 0.95f), new Vector2(1, 0.4f))), Box(p, new Vector2(0, -0.35f), new Vector2(0.1f, 0.3f))), Box(p, new Vector2(0, -0.7f), new Vector2(0.45f, 0.1f), 0.05f)), gold)); break;
                case "gift": L.Add((p => Box(p, new Vector2(0, -0.2f), new Vector2(0.7f, 0.55f), 0.08f), new Color(1f, 0.35f, 0.5f))); L.Add((p => Union(Box(p, new Vector2(0, -0.2f), new Vector2(0.12f, 0.55f)), Box(p, new Vector2(0, 0.1f), new Vector2(0.7f, 0.1f))), gold)); L.Add((p => Union(Circle(p, new Vector2(-0.22f, 0.5f), 0.2f), Circle(p, new Vector2(0.22f, 0.5f), 0.2f)), gold)); break;
                case "box": L.Add((p => Box(p, Vector2.zero, new Vector2(0.7f, 0.7f), 0.1f), new Color(0.56f, 0.27f, 0.68f))); L.Add((p => Union(Box(p, Vector2.zero, new Vector2(0.1f, 0.7f)), Box(p, Vector2.zero, new Vector2(0.7f, 0.1f))), gold)); L.Add((p => Circle(p, new Vector2(0, 0.02f), 0.2f), white)); break;
                case "flag": L.Add((p => Union(Box(p, new Vector2(-0.5f, 0), new Vector2(0.07f, 0.8f)), Box(p, new Vector2(0.05f, 0.4f), new Vector2(0.5f, 0.3f), 0.05f)), new Color(0.2f, 0.85f, 0.55f))); break;
                case "music": L.Add((p => Union(Union(Circle(p, new Vector2(-0.35f, -0.45f), 0.25f), Box(p, new Vector2(-0.14f, 0.1f), new Vector2(0.07f, 0.55f))), Seg(p, new Vector2(-0.14f, 0.62f), new Vector2(0.45f, 0.35f), 0.1f)), white)); break;
                case "sound": L.Add((p => Union(Box(p, new Vector2(-0.45f, 0), new Vector2(0.18f, 0.25f)), Mathf.Max(Mathf.Abs(p.y) - (p.x + 0.3f) * 0.9f, Mathf.Abs(p.x + 0.05f) - 0.25f)), white)); L.Add((p => Mathf.Abs(Circle(p, new Vector2(0.05f, 0), 0.55f)) - 0.07f + (p.x < 0.3f ? 1 : 0), white)); break;
                case "vibrate": L.Add((p => Sub(Box(p, Vector2.zero, new Vector2(0.32f, 0.6f), 0.1f), Box(p, new Vector2(0, 0.05f), new Vector2(0.2f, 0.4f))), white)); L.Add((p => Union(Seg(p, new Vector2(-0.6f, 0.3f), new Vector2(-0.6f, -0.3f), 0.06f), Seg(p, new Vector2(0.6f, 0.3f), new Vector2(0.6f, -0.3f), 0.06f)), white)); break;
                case "help": L.Add((p => Mathf.Abs(Circle(p, Vector2.zero, 0.8f)) - 0.1f, white)); L.Add((p => Union(Sub(Mathf.Abs(Circle(p, new Vector2(0, 0.2f), 0.28f)) - 0.09f, Box(p, new Vector2(-0.3f, 0.0f), new Vector2(0.3f, 0.25f))), Circle(p, new Vector2(0, -0.45f), 0.1f)), white)); break;
                case "parent": L.Add((p => Union(Circle(p, new Vector2(-0.25f, 0.35f), 0.25f), Box(p, new Vector2(-0.25f, -0.35f), new Vector2(0.3f, 0.35f), 0.15f)), white)); L.Add((p => Union(Circle(p, new Vector2(0.35f, 0.1f), 0.18f), Box(p, new Vector2(0.35f, -0.45f), new Vector2(0.2f, 0.25f), 0.1f)), white)); break;
                case "cat": default:
                    L.Add((p => Union(Circle(p, new Vector2(0, -0.1f), 0.65f), Union(Mathf.Max(Mathf.Abs(p.x + 0.4f) * 1.6f + p.y - 0.95f, -p.y + 0.1f), Mathf.Max(Mathf.Abs(p.x - 0.4f) * 1.6f + p.y - 0.95f, -p.y + 0.1f))), new Color(0.96f, 0.6f, 0.24f)));
                    L.Add((p => Union(Circle(p, new Vector2(-0.25f, -0.02f), 0.1f), Circle(p, new Vector2(0.25f, -0.02f), 0.1f)), dark));
                    L.Add((p => Circle(p, new Vector2(0, -0.25f), 0.07f), new Color(1f, 0.55f, 0.65f))); break;
            }
            return L;
        }

        /// <summary>The official TFY CATO logo (Resources/Branding/tfy_logo.png). Works whether it was imported as a Sprite or a plain texture.</summary>
        public static Sprite Logo()
        {
            if (cache.TryGetValue("__logo", out var s) && s) return s;
            s = Resources.Load<Sprite>("Branding/tfy_logo");
            if (!s) { var tex = Resources.Load<Texture2D>("Branding/tfy_logo"); if (tex) s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100); }
            if (s) cache["__logo"] = s;
            return s;
        }

        /// <summary>Rounded rectangle, 9-sliced — used for all panels and buttons.</summary>
        public static Sprite Rounded(int radius = 28)
        {
            string key = "rounded" + radius;
            if (cache.TryGetValue(key, out var s) && s) return s;
            int size = radius * 2 + 4; var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(0, Mathf.Abs(x + 0.5f - size / 2f) - 2), dy = Mathf.Max(0, Mathf.Abs(y + 0.5f - size / 2f) - 2);
                float d = new Vector2(Mathf.Max(0, dx - (size / 2f - 2 - radius)), Mathf.Max(0, dy - (size / 2f - 2 - radius))).magnitude - radius;
                px[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(0.5f - d));
            }
            tex.SetPixels(px); tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(radius + 1, radius + 1, radius + 1, radius + 1));
            return cache[key] = s;
        }
    }
}
