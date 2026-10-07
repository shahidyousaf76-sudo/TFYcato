using TFY.CatAdventure.Domain;
using TFY.CatAdventure.Graphics;
using UnityEngine;
using UnityEngine.Rendering;

namespace TFY.CatAdventure.World
{
    /// <summary>
    /// Day/night lighting: sunrise, morning, day, sunset, evening and night presets per world palette. Controls the sun
    /// (soft real-time shadows), moon, trilight ambient, procedural sky, exponential fog (kept light so obstacles stay visible),
    /// emissive night lights (lamps, windows, headlights) and a small budget of real point lights near the cat at night.
    /// Values are HDR-friendly (post-processing tonemaps them).
    /// </summary>
    public class SkyLighting
    {
        private readonly Light sun, moon;
        private readonly Material sky;
        private readonly Light[] nightLights;
        private readonly Transform follow;
        public string TimeOfDay { get; private set; } = "day";
        public bool IsNight => TimeOfDay == "night" || TimeOfDay == "evening";

        public SkyLighting(Transform parent, Transform follow, int nightLightBudget = 4)
        {
            this.follow = follow;
            sun = new GameObject("Sun").AddComponent<Light>(); sun.transform.SetParent(parent, false);
            sun.type = LightType.Directional; sun.shadows = LightShadows.Soft; sun.shadowStrength = 0.75f;
            moon = new GameObject("Moon").AddComponent<Light>(); moon.transform.SetParent(parent, false);
            moon.type = LightType.Directional; moon.shadows = LightShadows.None; moon.color = new Color(0.62f, 0.72f, 1f);
            var skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader) { sky = new Material(skyShader) { name = "TFY Sky" }; RenderSettings.skybox = sky; }
            RenderSettings.sun = sun;
            nightLights = new Light[nightLightBudget];
            for (int i = 0; i < nightLights.Length; i++)
            {
                var l = new GameObject("NightLight" + i).AddComponent<Light>(); l.transform.SetParent(parent, false);
                l.type = LightType.Point; l.range = 12f; l.intensity = 0; l.color = new Color(1f, 0.85f, 0.6f); l.shadows = LightShadows.None;
                nightLights[i] = l;
            }
        }

        private struct Preset { public float elevation, azimuth, sunIntensity, moonIntensity, exposure, ambient, fogMul, windowGlow; public Color sunColor, skyTint, fogTint; }

        private static Preset For(string tod) => tod switch
        {
            "sunrise" => new Preset { elevation = 8, azimuth = 70, sunIntensity = 1.4f, moonIntensity = 0, exposure = 1.25f, ambient = 0.8f, fogMul = 1.3f, windowGlow = 0.8f, sunColor = new Color(1f, 0.72f, 0.5f), skyTint = new Color(1f, 0.75f, 0.65f), fogTint = new Color(1f, 0.85f, 0.75f) },
            "morning" => new Preset { elevation = 28, azimuth = 100, sunIntensity = 2.2f, exposure = 1.3f, ambient = 1f, fogMul = 1.1f, windowGlow = 0.2f, sunColor = new Color(1f, 0.93f, 0.82f), skyTint = new Color(0.8f, 0.9f, 1f), fogTint = Color.white },
            "sunset" => new Preset { elevation = 10, azimuth = 250, sunIntensity = 1.8f, exposure = 1.2f, ambient = 0.8f, fogMul = 1.2f, windowGlow = 1f, sunColor = new Color(1f, 0.6f, 0.35f), skyTint = new Color(1f, 0.62f, 0.5f), fogTint = new Color(1f, 0.8f, 0.68f) },
            "evening" => new Preset { elevation = -4, azimuth = 270, sunIntensity = 0.25f, moonIntensity = 0.35f, exposure = 1.05f, ambient = 0.55f, fogMul = 1f, windowGlow = 2f, sunColor = new Color(0.9f, 0.55f, 0.6f), skyTint = new Color(0.55f, 0.45f, 0.8f), fogTint = new Color(0.6f, 0.55f, 0.8f) },
            "night" => new Preset { elevation = -25, azimuth = 270, sunIntensity = 0f, moonIntensity = 0.75f, exposure = 1.15f, ambient = 0.45f, fogMul = 0.9f, windowGlow = 3f, sunColor = Color.black, skyTint = new Color(0.25f, 0.3f, 0.6f), fogTint = new Color(0.25f, 0.3f, 0.5f) },
            _ => new Preset { elevation = 48, azimuth = 140, sunIntensity = 2.6f, exposure = 1.3f, ambient = 1f, fogMul = 1f, windowGlow = 0.1f, sunColor = new Color(1f, 0.97f, 0.92f), skyTint = new Color(0.75f, 0.88f, 1f), fogTint = Color.white },
        };

        public void Apply(WorldDef w, string timeOfDay, float fogQuality)
        {
            TimeOfDay = timeOfDay ?? "day";
            var p = For(TimeOfDay);
            sun.transform.rotation = Quaternion.Euler(Mathf.Max(p.elevation, 2f), p.azimuth, 0);
            sun.intensity = p.sunIntensity; sun.color = p.sunColor; sun.enabled = p.sunIntensity > 0.01f;
            moon.transform.rotation = Quaternion.Euler(40, p.azimuth + 160, 0); moon.intensity = p.moonIntensity; moon.enabled = p.moonIntensity > 0.01f;

            Color top = MaterialLibrary.Hex(w.palette.skyTop), horizon = MaterialLibrary.Hex(w.palette.skyHorizon), groundCol = MaterialLibrary.Hex(w.palette.ground);
            if (sky)
            {
                sky.SetColor("_SkyTint", Color.Lerp(top, p.skyTint, 0.5f));
                sky.SetColor("_GroundColor", Color.Lerp(groundCol, Color.black, 0.3f));
                sky.SetFloat("_Exposure", p.exposure);
                sky.SetFloat("_AtmosphereThickness", TimeOfDay == "sunset" || TimeOfDay == "sunrise" ? 1.6f : 0.9f);
                sky.SetFloat("_SunSize", 0.05f);
            }
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Color.Lerp(top, p.skyTint, 0.5f) * p.ambient;
            RenderSettings.ambientEquatorColor = Color.Lerp(horizon, p.fogTint, 0.5f) * p.ambient * 0.9f;
            RenderSettings.ambientGroundColor = groundCol * p.ambient * 0.45f;

            // Fog: exponential, tinted, and capped so obstacles are always visible well before the cat reaches them.
            RenderSettings.fog = fogQuality > 0;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = Color.Lerp(MaterialLibrary.Hex(w.palette.fog), p.fogTint, 0.5f) * Mathf.Max(0.35f, p.ambient);
            RenderSettings.fogDensity = Mathf.Min(0.012f, w.fogDensity * p.fogMul * fogQuality);

            foreach (var m in DecorFactory.NightEmissive)
                if (m) m.SetColor("_EmissionColor", m.GetColor("_BaseColor") * p.windowGlow);
            DynamicGI.UpdateEnvironment();
        }

        /// <summary>At night a few real point lights follow the cat (budget per quality tier) so the path glows warmly.</summary>
        public void Tick(float catDistance, int budget)
        {
            for (int i = 0; i < nightLights.Length; i++)
            {
                var l = nightLights[i];
                bool on = IsNight && i < budget;
                l.enabled = on;
                if (!on) continue;
                float spacing = 18f, z = Mathf.Floor(catDistance / spacing) * spacing + i * spacing;
                l.transform.position = new Vector3((i % 2 == 0 ? -1 : 1) * 4.6f, 3.7f, -z);
                l.intensity = TimeOfDay == "night" ? 3f : 1.6f;
            }
        }
    }
}
