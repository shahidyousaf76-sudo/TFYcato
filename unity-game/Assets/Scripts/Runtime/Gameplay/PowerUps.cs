using System.Collections.Generic;
using TFY.CatAdventure.Backend;
using TFY.CatAdventure.Domain;
using TFY.CatAdventure.Graphics;
using UnityEngine;

namespace TFY.CatAdventure.Gameplay
{
    /// <summary>
    /// Coin Magnet (pulls nearby coins), Shield (absorbs one bump), Super Jump (higher jumps), Speed Boost (faster, obstacles
    /// bounce away) and Double Coins (each coin counts twice). Durations come from Remote Config (clamped). Each active
    /// power-up shows a colourful effect on the cat and a timer ring on the HUD.
    /// </summary>
    public class PowerUps
    {
        private readonly Dictionary<string, float> timers = new Dictionary<string, float>();
        private RuntimeConfig runtime;
        private GameObject shieldBubble, magnetRing, jumpGlow, boostGlow, coinsGlow;
        public bool Shield { get; private set; }

        public void Attach(Transform cat, RuntimeConfig rc)
        {
            runtime = rc;
            if (shieldBubble) return;
            shieldBubble = Shapes.Part(cat, PrimitiveType.Sphere, MaterialLibrary.Transparent(new Color(0.3f, 0.8f, 1f, 0.28f)), new Vector3(0, 0.7f, 0), Vector3.one * 2f, default, "ShieldBubble");
            magnetRing = Shapes.Part(cat, Shapes.Torus(), MaterialLibrary.Glow(new Color(1f, 0.3f, 0.45f), 2f), new Vector3(0, 0.08f, 0), new Vector3(2.4f, 1, 2.4f), default, "MagnetRing");
            jumpGlow = Shapes.Part(cat, Shapes.Torus(), MaterialLibrary.Glow(new Color(0.5f, 1f, 0.6f), 2f), new Vector3(0, 0.05f, 0), new Vector3(1.6f, 1, 1.6f), default, "JumpRing");
            boostGlow = Shapes.Part(cat, Shapes.Cone(), MaterialLibrary.Transparent(new Color(1f, 0.75f, 0.2f, 0.3f)), new Vector3(0, 0.6f, 0.9f), new Vector3(1.2f, 1.6f, 1.2f), new Vector3(-90, 0, 0), "BoostGlow");
            coinsGlow = Shapes.Part(cat, PrimitiveType.Sphere, MaterialLibrary.Glow(new Color(1f, 0.85f, 0.2f), 3f), new Vector3(0, 1.9f, 0), Vector3.one * 0.25f, default, "CoinsGlow");
            foreach (var g in new[] { shieldBubble, magnetRing, jumpGlow, boostGlow, coinsGlow }) { g.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; g.SetActive(false); }
        }

        public void Activate(string id)
        {
            if (id == "shield") { Shield = true; GameEvents.RaisePowerUpStarted(id, 0); }
            else { float d = runtime.DurationFor(id); timers[id] = d; GameEvents.RaisePowerUpStarted(id, d); }
            AnalyticsService.PowerUpUsed(id);
        }

        public void UseShield() { Shield = false; GameEvents.RaisePowerUpEnded("shield"); }

        public void Tick(float dt)
        {
            var ended = new List<string>();
            foreach (var k in new List<string>(timers.Keys)) { timers[k] -= dt; if (timers[k] <= 0) ended.Add(k); }
            foreach (var k in ended) { timers.Remove(k); GameEvents.RaisePowerUpEnded(k); }
            if (shieldBubble)
            {
                shieldBubble.SetActive(Shield);
                magnetRing.SetActive(Active("magnet")); magnetRing.transform.Rotate(0, 180 * dt, 0);
                jumpGlow.SetActive(Active("super_jump"));
                boostGlow.SetActive(Active("speed_boost"));
                coinsGlow.SetActive(Active("double_coins"));
            }
        }

        public bool Active(string id) => timers.TryGetValue(id, out var t) && t > 0;
        public float Remaining(string id) => timers.TryGetValue(id, out var t) ? t : 0;
        public float MagnetRadius => Active("magnet") ? 5.5f : 0f;
        public float JumpMultiplier => Active("super_jump") ? 1.45f : 1f;
        public float SpeedMultiplier => Active("speed_boost") ? 1.35f : 1f;
        public bool Invincible => Active("speed_boost");
        public int CoinValue => Active("double_coins") ? 2 : 1;

        public void Clear()
        {
            foreach (var k in timers.Keys) GameEvents.RaisePowerUpEnded(k);
            timers.Clear(); if (Shield) GameEvents.RaisePowerUpEnded("shield"); Shield = false;
            Tick(0);
        }
    }
}
