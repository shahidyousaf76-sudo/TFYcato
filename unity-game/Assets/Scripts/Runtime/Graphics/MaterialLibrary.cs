using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TFY.CatAdventure.Graphics
{
    /// <summary>
    /// Cached PBR materials (URP/Lit) for the procedural art: base colour, metallic, smoothness, optional emission (HDR glow
    /// that blooms) and optional generated normal/occlusion detail. Stylised, bright and child-friendly — but physically based,
    /// so lighting, shadows, reflections and time of day all look right. Final art can replace any of these by name.
    /// </summary>
    public static class MaterialLibrary
    {
        private static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();
        private static Shader lit, unlit, particle;
        public static Texture2D NoiseNormal { get; private set; }
        public static Texture2D SoftDot { get; private set; }

        public static void Init()
        {
            lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            particle = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit");
            if (!NoiseNormal) NoiseNormal = ProceduralTextures.NoiseNormalMap(128, 2.2f, 17);
            if (!SoftDot) SoftDot = ProceduralTextures.SoftDot(64);
        }

        public static Material Lit(Color color, float smoothness = 0.35f, float metallic = 0f, bool detail = false)
        {
            string key = $"lit{ColorUtility.ToHtmlStringRGBA(color)}{smoothness:0.00}{metallic:0.00}{detail}";
            if (cache.TryGetValue(key, out var m) && m) return m;
            m = new Material(lit) { name = key, enableInstancing = true };
            m.SetColor("_BaseColor", color); m.color = color;
            m.SetFloat("_Smoothness", smoothness); m.SetFloat("_Metallic", metallic);
            if (detail && NoiseNormal) { m.SetTexture("_BumpMap", NoiseNormal); m.SetFloat("_BumpScale", 0.6f); m.EnableKeyword("_NORMALMAP"); }
            return cache[key] = m;
        }

        public static Material Lit(string hex, float smoothness = 0.35f, float metallic = 0f, bool detail = false) => Lit(Hex(hex), smoothness, metallic, detail);

        /// <summary>HDR emission (intensity > 1 blooms): coins, gems, lamps, windows, checkpoint lights, power-ups.</summary>
        public static Material Glow(Color color, float intensity, float smoothness = 0.6f, float metallic = 0f)
        {
            string key = $"glow{ColorUtility.ToHtmlStringRGBA(color)}{intensity:0.0}{smoothness:0.00}{metallic:0.00}";
            if (cache.TryGetValue(key, out var m) && m) return m;
            m = new Material(lit) { name = key, enableInstancing = true };
            m.SetColor("_BaseColor", color); m.SetFloat("_Smoothness", smoothness); m.SetFloat("_Metallic", metallic);
            m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", color * intensity);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            return cache[key] = m;
        }

        public static Material Transparent(Color color, float smoothness = 0.9f)
        {
            string key = $"tr{ColorUtility.ToHtmlStringRGBA(color)}{smoothness:0.00}";
            if (cache.TryGetValue(key, out var m) && m) return m;
            m = new Material(lit) { name = key };
            m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha); m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetColor("_BaseColor", color); m.SetFloat("_Smoothness", smoothness);
            return cache[key] = m;
        }

        public static Material Unlit(Color color)
        {
            string key = $"ul{ColorUtility.ToHtmlStringRGBA(color)}";
            if (cache.TryGetValue(key, out var m) && m) return m;
            m = new Material(unlit) { name = key }; m.SetColor("_BaseColor", color); m.color = color;
            return cache[key] = m;
        }

        /// <summary>Soft additive-looking sprite material for particles, fog cards and cloud layers.</summary>
        public static Material Particle(Color tint, Texture tex = null)
        {
            string key = $"pt{ColorUtility.ToHtmlStringRGBA(tint)}{(tex ? tex.name : "dot")}";
            if (cache.TryGetValue(key, out var m) && m) return m;
            m = new Material(particle) { name = key };
            m.SetTexture("_BaseMap", tex ? tex : SoftDot); m.SetColor("_BaseColor", tint);
            m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0);
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha); m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            return cache[key] = m;
        }

        public static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
    }

    public static class ProceduralTextures
    {
        public static Texture2D NoiseNormalMap(int size, float strength, int seed)
        {
            var h = new float[size * size]; var rnd = new System.Random(seed);
            var grid = new float[17 * 17]; for (int i = 0; i < grid.Length; i++) grid[i] = (float)rnd.NextDouble();
            float Sample(float x, float y)
            {
                float gx = x / size * 16, gy = y / size * 16; int ix = (int)gx, iy = (int)gy; float fx = gx - ix, fy = gy - iy;
                float a = grid[iy * 17 + ix], b = grid[iy * 17 + ix + 1], c = grid[(iy + 1) * 17 + ix], d = grid[(iy + 1) * 17 + ix + 1];
                fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
                return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
            }
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) h[y * size + x] = Sample(x, y) + 0.5f * Sample((x * 2) % size, (y * 2) % size);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, true) { name = "noise_normal", wrapMode = TextureWrapMode.Repeat };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float l = h[y * size + (x - 1 + size) % size], r = h[y * size + (x + 1) % size], d = h[((y - 1 + size) % size) * size + x], u = h[((y + 1) % size) * size + x];
                var n = new Vector3((l - r) * strength, (d - u) * strength, 1).normalized;
                px[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1);
            }
            tex.SetPixels(px); tex.Apply(true, true);
            return tex;
        }

        public static Texture2D SoftDot(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "soft_dot", wrapMode = TextureWrapMode.Clamp };
            var px = new Color[size * size]; float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c; float a = Mathf.Clamp01(1 - d); a = a * a * (3 - 2 * a);
                px[y * size + x] = new Color(1, 1, 1, a);
            }
            tex.SetPixels(px); tex.Apply();
            return tex;
        }

        /// <summary>Cracked-wood texture for broken bridges (visible cracks = warning).</summary>
        public static Texture2D Cracks(int size, int seed)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "cracks" };
            var px = new Color[size * size]; for (int i = 0; i < px.Length; i++) px[i] = new Color(1, 1, 1, 1);
            var rnd = new System.Random(seed);
            for (int k = 0; k < 7; k++)
            {
                float x = rnd.Next(size), y = rnd.Next(size / 4); float dx = (float)(rnd.NextDouble() - 0.5) * 1.6f;
                for (int s = 0; s < size * 1.2f; s++)
                {
                    x += dx + (float)(rnd.NextDouble() - 0.5) * 1.4f; y += 1;
                    int ix = Mathf.Clamp((int)x, 0, size - 1), iy = Mathf.Clamp((int)y, 0, size - 1);
                    for (int w = -1; w <= 1; w++) px[iy * size + Mathf.Clamp(ix + w, 0, size - 1)] = new Color(0.25f, 0.15f, 0.08f, 1);
                    if (y >= size - 1) break;
                }
            }
            tex.SetPixels(px); tex.Apply();
            return tex;
        }
    }
}
