using System.Collections.Generic;
using UnityEngine;

namespace TFY.CatAdventure.Graphics
{
    /// <summary>
    /// Particle effects built in code (pooled bursts + looping weather). Budgets scale with the quality tier and the
    /// "reduce motion" setting, and nothing flashes rapidly.
    /// </summary>
    public static class Vfx
    {
        public static float Budget = 1f; // 0.35 LOW … 1.0 HIGH/ULTRA, set by QualityService
        private static readonly Dictionary<string, Stack<ParticleSystem>> pools = new Dictionary<string, Stack<ParticleSystem>>();
        private static Transform poolRoot;

        public static ParticleSystem Make(Transform parent, string name, Color color, float size, float speed, float life, int burst, float gravity = 0, bool loop = false, float rate = 0, ParticleSystemShapeType shape = ParticleSystemShapeType.Sphere, float radius = 0.3f)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = loop; main.playOnAwake = false; main.duration = loop ? 5f : 0.5f;
            main.startColor = color; main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed); main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.7f, life);
            main.gravityModifier = gravity; main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = Mathf.Max(8, (int)(Mathf.Max(burst, rate * life) * 1.5f));
            var em = ps.emission; em.rateOverTime = rate * Budget;
            if (burst > 0) em.SetBursts(new[] { new ParticleSystem.Burst(0, (short)Mathf.Max(1, burst * Budget)) });
            var sh = ps.shape; sh.shapeType = shape; sh.radius = radius;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            col.color = g;
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, 1, 1, 0.3f));
            var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = MaterialLibrary.Particle(Color.white); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return ps;
        }

        public static ParticleSystem DustPuff(Transform parent, Vector3 local, Color c)
        {
            var ps = Make(parent, "Dust", c, 0.5f, 1.2f, 1.1f, 14, 0.4f); ps.transform.localPosition = local; return ps;
        }

        /// <summary>Fire-and-forget pooled burst at a world position.</summary>
        public static void Burst(string kind, Vector3 pos)
        {
            if (!poolRoot) poolRoot = new GameObject("VfxPool").transform;
            if (!pools.TryGetValue(kind, out var stack)) pools[kind] = stack = new Stack<ParticleSystem>();
            var ps = stack.Count > 0 ? stack.Pop() : Create(kind);
            if (!ps) return;
            ps.transform.position = pos;
            ps.Play(true);
            PoolReturner.Schedule(ps, 2.5f, () => stack.Push(ps));
        }

        private static ParticleSystem Create(string kind)
        {
            switch (kind)
            {
                case "coin": return Make(poolRoot, kind, new Color(1f, 0.9f, 0.35f), 0.18f, 3f, 0.4f, 8);
                case "fish": return Make(poolRoot, kind, new Color(0.5f, 0.85f, 1f), 0.25f, 3f, 0.5f, 10);
                case "gem": return Make(poolRoot, kind, new Color(0.9f, 0.4f, 1f), 0.25f, 4f, 0.6f, 16);
                case "star": return Make(poolRoot, kind, new Color(1f, 0.95f, 0.5f), 0.35f, 5f, 0.8f, 22, 0.5f);
                case "powerup": return Make(poolRoot, kind, new Color(0.6f, 1f, 0.8f), 0.3f, 4f, 0.6f, 18);
                case "landing": return Make(poolRoot, kind, new Color(0.85f, 0.8f, 0.7f, 0.8f), 0.35f, 1.5f, 0.45f, 8, 0.2f, false, 0, ParticleSystemShapeType.Circle, 0.4f);
                case "bump": return Make(poolRoot, kind, new Color(1f, 1f, 0.7f), 0.25f, 4f, 0.5f, 12, 0.6f);
                case "doublejump": return Make(poolRoot, kind, new Color(0.7f, 0.9f, 1f), 0.22f, 2.5f, 0.5f, 12);
                case "checkpoint": return Make(poolRoot, kind, new Color(0.5f, 1f, 0.6f), 0.3f, 6f, 1.2f, 40, 0.8f);
                case "confetti":
                    var c = Make(poolRoot, kind, Color.white, 0.25f, 9f, 2.2f, 90, 0.9f);
                    var main = c.main; main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.4f, 0.6f), new Color(0.3f, 0.8f, 1f));
                    return c;
                default: return Make(poolRoot, kind, Color.white, 0.2f, 3f, 0.5f, 10);
            }
        }

        /// <summary>Looping world weather: petals (park), leaves (forest), snow (mountains), sparkles/sea breeze (beach), birds, mist.</summary>
        public static ParticleSystem Weather(Transform follow, string weather, float budget)
        {
            if (weather == "clear" || budget <= 0) return null;
            ParticleSystem ps;
            switch (weather)
            {
                case "petals": ps = Make(follow, "Petals", new Color(1f, 0.75f, 0.85f), 0.14f, 0.6f, 6f, 0, 0.03f, true, 14 * budget, ParticleSystemShapeType.Box); break;
                case "leaves": ps = Make(follow, "Leaves", new Color(1f, 0.7f, 0.25f), 0.18f, 0.5f, 7f, 0, 0.04f, true, 18 * budget, ParticleSystemShapeType.Box); break;
                case "snow": ps = Make(follow, "Snow", Color.white, 0.12f, 0.4f, 8f, 0, 0.05f, true, 45 * budget, ParticleSystemShapeType.Box); break;
                case "sea_breeze": ps = Make(follow, "Sparkles", new Color(1f, 1f, 0.85f), 0.08f, 0.4f, 3f, 0, -0.01f, true, 16 * budget, ParticleSystemShapeType.Box); break;
                case "mist": ps = Make(follow, "Mist", new Color(1f, 1f, 1f, 0.25f), 3f, 0.2f, 8f, 0, 0, true, 5 * budget, ParticleSystemShapeType.Box); break;
                default: return null;
            }
            var sh = ps.shape; sh.scale = new Vector3(30, 1, 40); ps.transform.localPosition = new Vector3(0, 9, -20);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.x = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f); vel.y = new ParticleSystem.MinMaxCurve(-1.2f, -0.6f); vel.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
            ps.Play();
            return ps;
        }
    }

    /// <summary>Returns pooled particle systems after they finish.</summary>
    public class PoolReturner : MonoBehaviour
    {
        private static PoolReturner instance;
        private readonly List<(float at, System.Action done)> jobs = new List<(float, System.Action)>();
        public static void Schedule(ParticleSystem ps, float after, System.Action done)
        {
            if (!instance) instance = new GameObject("PoolReturner").AddComponent<PoolReturner>();
            instance.jobs.Add((Time.time + after, done));
        }
        private void Update()
        {
            for (int i = jobs.Count - 1; i >= 0; i--) if (Time.time >= jobs[i].at) { jobs[i].done(); jobs.RemoveAt(i); }
        }
    }
}
