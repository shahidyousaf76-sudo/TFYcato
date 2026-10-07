using TFY.CatAdventure.Backend;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TFY.CatAdventure.Graphics
{
    public enum QualityTier { Low = 0, Medium = 1, High = 2, Ultra = 3 }

    /// <summary>
    /// LOW / MEDIUM / HIGH / ULTRA. Each tier maps to a Quality level with its own URP asset (created by TFY ▸ Setup:
    /// shadows, cascades, MSAA, HDR, render scale, SSAO on HIGH/ULTRA) plus runtime budgets (particles, scenery density,
    /// fog cards, night lights, reflection probe updates, post-processing). "Auto" estimates the device and steps down
    /// if the frame rate stays low. Targets 60 FPS on capable devices, 30+ on older ones.
    /// </summary>
    public class QualityService : MonoBehaviour
    {
        public QualityTier Tier { get; private set; } = QualityTier.Medium;
        public bool Auto { get; private set; } = true;
        public event System.Action<QualityTier> Changed;
        private float fpsAccum; private int fpsFrames; private float lowTime, checkTimer;

        public struct Budget { public float particles, scenery, fog, renderScale; public int nightLights, shadowDistance; public bool bloom, ssao, dofMenus, reflections, fogCards, volumetricClouds; }

        public Budget Current => For(Tier);

        public static Budget For(QualityTier t) => t switch
        {
            QualityTier.Low => new Budget { particles = 0.35f, scenery = 0.5f, fog = 0.7f, renderScale = 0.8f, nightLights = 0, shadowDistance = 0, bloom = false, ssao = false, dofMenus = false, reflections = false, fogCards = false },
            QualityTier.Medium => new Budget { particles = 0.6f, scenery = 0.75f, fog = 1f, renderScale = 0.9f, nightLights = 2, shadowDistance = 35, bloom = true, ssao = false, dofMenus = false, reflections = false, fogCards = false },
            QualityTier.High => new Budget { particles = 1f, scenery = 1f, fog = 1f, renderScale = 1f, nightLights = 4, shadowDistance = 55, bloom = true, ssao = true, dofMenus = true, reflections = true, fogCards = true },
            _ => new Budget { particles = 1f, scenery = 1f, fog = 1f, renderScale = 1f, nightLights = 6, shadowDistance = 80, bloom = true, ssao = true, dofMenus = true, reflections = true, fogCards = true, volumetricClouds = true },
        };

        /// <summary>Rough device estimate: memory, GPU memory, cores and graphics API.</summary>
        public static QualityTier Estimate()
        {
            int ram = SystemInfo.systemMemorySize, vram = SystemInfo.graphicsMemorySize, cores = SystemInfo.processorCount;
            bool vulkanOrMetal = SystemInfo.graphicsDeviceType == GraphicsDeviceType.Vulkan || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Metal;
            if (!Application.isMobilePlatform) return QualityTier.Ultra;
            if (ram >= 8000 && cores >= 8 && vulkanOrMetal && vram >= 2000) return QualityTier.Ultra;
            if (ram >= 6000 && cores >= 8) return QualityTier.High;
            if (ram >= 3500 && cores >= 6) return QualityTier.Medium;
            return QualityTier.Low;
        }

        public void Apply(string setting)
        {
            Auto = setting == "auto" || string.IsNullOrEmpty(setting);
            var tier = Auto ? Estimate() : setting switch { "low" => QualityTier.Low, "medium" => QualityTier.Medium, "high" => QualityTier.High, _ => QualityTier.Ultra };
            SetTier(tier);
            AnalyticsService.GraphicsTier(tier.ToString(), Auto);
        }

        private void SetTier(QualityTier t)
        {
            Tier = t;
            int index = Mathf.Clamp((int)t, 0, QualitySettings.names.Length - 1);
            if (QualitySettings.names.Length >= 4) QualitySettings.SetQualityLevel(index, true);
            var b = For(t);
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.renderScale = b.renderScale;
                urp.shadowDistance = b.shadowDistance;
            }
            QualitySettings.shadows = b.shadowDistance > 0 ? ShadowQuality.All : ShadowQuality.Disable;
            Application.targetFrameRate = t == QualityTier.Low ? 30 : 60;
            Vfx.Budget = b.particles;
            Changed?.Invoke(t);
        }

        /// <summary>Adaptive step-down: if AUTO and the game runs below ~45 FPS for 6 seconds of play, lower one tier.</summary>
        private void Update()
        {
            fpsAccum += Time.unscaledDeltaTime; fpsFrames++; checkTimer += Time.unscaledDeltaTime;
            if (checkTimer < 1f) return;
            float fps = fpsFrames / Mathf.Max(0.001f, fpsAccum); fpsAccum = 0; fpsFrames = 0; checkTimer = 0;
            if (!Auto || Time.timeScale == 0) { lowTime = 0; return; }
            float target = Tier == QualityTier.Low ? 27f : 45f;
            lowTime = fps < target ? lowTime + 1f : 0f;
            if (lowTime >= 6f && Tier > QualityTier.Low) { lowTime = 0; SetTier(Tier - 1); Debug.Log($"[Quality] stepped down to {Tier} (fps {fps:0})"); }
        }
    }
}
