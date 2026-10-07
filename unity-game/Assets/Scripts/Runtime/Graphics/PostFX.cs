using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TFY.CatAdventure.Graphics
{
    /// <summary>
    /// Global post-processing volume: ACES tonemapping (HDR → screen), bloom (glowing coins, gems, lamps), colour grading for a
    /// bright cartoon look, a soft vignette, and depth of field on menus only (never during play, and off with "Calm camera").
    /// Strength depends on the quality tier. SSAO is a URP renderer feature enabled on the HIGH/ULTRA renderer (TFY ▸ Setup).
    /// </summary>
    public class PostFX
    {
        private readonly Volume volume;
        private readonly Bloom bloom; private readonly Tonemapping tone; private readonly ColorAdjustments color;
        private readonly Vignette vignette; private readonly DepthOfField dof;
        private bool dofAllowed;

        public PostFX(Transform parent)
        {
            var go = new GameObject("PostFX"); go.transform.SetParent(parent, false);
            volume = go.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 10;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            tone = profile.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.ACES);
            bloom = profile.Add<Bloom>(true); bloom.threshold.Override(1.05f); bloom.intensity.Override(0.7f); bloom.scatter.Override(0.65f);
            color = profile.Add<ColorAdjustments>(true); color.saturation.Override(14f); color.contrast.Override(8f); color.postExposure.Override(0.15f);
            vignette = profile.Add<Vignette>(true); vignette.intensity.Override(0.18f); vignette.smoothness.Override(0.5f);
            dof = profile.Add<DepthOfField>(true); dof.mode.Override(DepthOfFieldMode.Gaussian); dof.gaussianStart.Override(4f); dof.gaussianEnd.Override(18f); dof.active = false;
            volume.profile = profile;
        }

        public void ApplyTier(QualityService.Budget b, bool reduceMotion)
        {
            bloom.active = b.bloom; bloom.intensity.Override(b.bloom ? 0.75f : 0f);
            vignette.active = b.bloom;
            dofAllowed = b.dofMenus && !reduceMotion;
            if (!dofAllowed) dof.active = false;
        }

        public void SetMenu(bool menu) { dof.active = menu && dofAllowed; }

        public void SetNight(bool night) { color.postExposure.Override(night ? 0.35f : 0.15f); color.saturation.Override(night ? 8f : 14f); }

        /// <summary>A short, gentle warm pulse when a power-up starts (never flashing).</summary>
        public void Pulse(float amount) { bloom.intensity.Override(Mathf.Clamp(bloom.intensity.value + amount, 0, 1.4f)); }
        public void Relax(float dt, bool bloomOn) { if (bloomOn) bloom.intensity.Override(Mathf.MoveTowards(bloom.intensity.value, 0.75f, dt)); }
    }
}
