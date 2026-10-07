using System.Collections.Generic;
using TFY.CatAdventure.Domain;
using TFY.CatAdventure.Graphics;
using UnityEngine;

namespace TFY.CatAdventure.World
{
    /// <summary>
    /// Streams the world around the cat in 40 m chunks: ground, the three-lane path with edges, scenery on both sides
    /// (seeded per chunk, so every run looks the same), big backdrop pieces (mountains, ocean, skyline), cloud layers and
    /// fog cards. Ground tiles inside bridge gaps are removed so the gap is real. Chunks are recycled — no per-frame allocation.
    /// </summary>
    public class EnvironmentBuilder
    {
        public const float ChunkLength = 40f, TileLength = 4f;
        private const int ChunksAhead = 6, ChunksBehind = 1;
        private readonly Transform root;
        private readonly DecorFactory decor;
        private readonly Dictionary<int, GameObject> chunks = new Dictionary<int, GameObject>();
        private readonly List<GameObject> backdrop = new List<GameObject>();
        private readonly List<(Transform t, Vector3 basePos, float phase)> balloons = new List<(Transform, Vector3, float)>();
        private readonly List<(float z, float lead, string name)> landmarkMarks = new List<(float, float, string)>();
        private readonly HashSet<float> landmarksShown = new HashSet<float>();
        private WorldDef world;
        private System.Func<float, bool> hasGround = _ => true;
        private int seed;
        private Transform backdropRoot;
        private Material ground, path, edge, oceanMat;
        public float Quality { get; set; } = 1f; // scenery density 0.5 … 1

        public EnvironmentBuilder(Transform root) { this.root = root; decor = new DecorFactory(root); }

        public WorldDef World => world;

        public void Build(WorldDef w, int levelSeed, System.Func<float, bool> groundAt)
        {
            Clear();
            world = w; seed = levelSeed; hasGround = groundAt ?? (_ => true);
            ground = MaterialLibrary.Lit(w.palette.ground, 0.15f, 0, true);
            path = MaterialLibrary.Lit(w.palette.path, w.id == "city" ? 0.45f : 0.2f, 0, true);
            edge = MaterialLibrary.Lit(w.palette.pathEdge, 0.3f);
            backdropRoot = Shapes.Group(root, "Backdrop").transform;
            BuildBackdrop();
        }

        /// <summary>Keeps chunks around the cat. Called every frame with the cat's distance.</summary>
        public void Tick(float catDistance)
        {
            if (world == null) return;
            int current = Mathf.FloorToInt(catDistance / ChunkLength);
            for (int i = current - ChunksBehind; i <= current + ChunksAhead; i++) if (i >= -1 && !chunks.ContainsKey(i)) chunks[i] = BuildChunk(i);
            var remove = new List<int>();
            foreach (var kv in chunks) if (kv.Key < current - ChunksBehind || kv.Key > current + ChunksAhead) remove.Add(kv.Key);
            foreach (var k in remove) { Object.Destroy(chunks[k]); chunks.Remove(k); }
            if (backdropRoot) backdropRoot.localPosition = new Vector3(0, 0, -catDistance); // far scenery travels with the cat
            if (oceanMat) oceanMat.SetTextureOffset("_BumpMap", new Vector2(Time.time * 0.02f, Time.time * 0.035f));
            AnimateBalloons();
            // Name banner while the landmark is on screen (side ones leave the portrait view ~2.75 × their distance before you reach them).
            if (App.I && App.I.Run && App.I.Run.Playing)
                foreach (var m in landmarkMarks)
                    if (!landmarksShown.Contains(m.z) && catDistance > m.z - m.lead && catDistance < m.z + 10) { landmarksShown.Add(m.z); GameEvents.RaiseLandmark(m.name); }
        }

        /// <summary>Hot air balloons bob and drift gently; burner flames flicker (HDR, so they bloom).</summary>
        private void AnimateBalloons()
        {
            float t = Time.time;
            foreach (var (b, basePos, phase) in balloons)
            {
                if (!b) continue;
                b.localPosition = basePos + new Vector3(Mathf.Sin(t * 0.12f + phase) * 4f, Mathf.Sin(t * 0.45f + phase) * 1.2f, 0);
                b.localRotation = Quaternion.Euler(Mathf.Sin(t * 0.6f + phase) * 2f, t * 4f + phase * 50f, Mathf.Cos(t * 0.5f + phase) * 2f);
            }
            foreach (var m in DecorFactory.BalloonFlames) if (m) m.SetColor("_EmissionColor", new Color(1f, 0.55f, 0.12f) * (2.6f + Mathf.Sin(t * 13f) * 0.5f + Mathf.Sin(t * 29f) * 0.3f));
        }

        private GameObject BuildChunk(int index)
        {
            float z0 = index * ChunkLength;
            var chunk = Shapes.Group(root, "Chunk" + index, new Vector3(0, 0, -z0));
            var rnd = new System.Random(seed * 31 + index * 7919 + world.id.GetHashCode());

            bool chunkHasGap = false;
            for (float z = 0; z <= ChunkLength; z += 1f) if (!hasGround(z0 + z)) { chunkHasGap = true; break; }
            // No gap: one long piece each (fewer draw calls). With a gap: 4 m tiles, and tiles inside the gap are left out
            // (the bridge obstacle brings its own water/chasm below).
            float step = chunkHasGap ? TileLength : ChunkLength;
            for (float z = 0; z < ChunkLength; z += step)
            {
                if (chunkHasGap && (!hasGround(z0 + z + step * 0.5f) || !hasGround(z0 + z + 0.5f) || !hasGround(z0 + z + step - 0.5f))) continue;
                Shapes.Part(chunk.transform, PrimitiveType.Cube, ground, new Vector3(0, -0.26f, -z - step * 0.5f), new Vector3(90f, 0.5f, step), default, "Ground");
                Shapes.Part(chunk.transform, PrimitiveType.Cube, path, new Vector3(0, -0.01f, -z - step * 0.5f), new Vector3(7.2f, 0.04f, step), default, "Path");
                for (int s = -1; s <= 1; s += 2) Shapes.Part(chunk.transform, PrimitiveType.Cube, edge, new Vector3(s * 3.75f, 0.05f, -z - step * 0.5f), new Vector3(0.3f, 0.14f, step), default, "Edge");
            }
            if (world.id == "city") for (float z = 1f; z < ChunkLength; z += TileLength) if (hasGround(z0 + z))
                for (int l = -1; l <= 1; l += 2) Shapes.Part(chunk.transform, PrimitiveType.Cube, MaterialLibrary.Lit("#FFFFFF", 0.5f), new Vector3(l * LevelPlanner.LaneWidth * 0.5f, 0.015f, -z - 1f), new Vector3(0.12f, 0.01f, 1.6f), default, "LaneMark");

            // World-famous landmark about every 200 m (chunks 2, 7, 12 …); that side of the road stays clear of other scenery.
            var lms = world.landmarks;
            bool isLm = lms != null && lms.Count > 0 && index >= 2 && index % 5 == 2;
            int lmN = index / 5; string lmId = isLm ? lms[(lmN + seed) % lms.Count] : null;
            (float x, int side) lmPlace = lmId != null ? LandmarkFactory.Placement(lmId) : (0f, 0);
            int lmSide = lmId == null ? 0 : lmPlace.side != 0 ? lmPlace.side : (lmN % 2 == 1 ? 1 : -1);

            // Scenery on both sides, never within 5 m of the path (keeps the view of obstacles clear).
            int count = Mathf.RoundToInt((world.decor.Count > 0 ? 9 : 0) * Quality);
            for (int i = 0; i < count && world.decor.Count > 0; i++)
            {
                string type = world.decor[rnd.Next(world.decor.Count)];
                if (type == "ocean" || type == "mountain" || type == "cloud" || type == "butterflies" || type == "birds") continue; // backdrop / particles
                float side = rnd.Next(2) == 0 ? -1 : 1;
                if (world.id == "beach" && side > 0 && type != "boat") side = -1; // ocean on the right
                float x = side * (NearDistance(type) + (float)rnd.NextDouble() * 18f);
                if (type == "boat") x = 20f + (float)rnd.NextDouble() * 25f;
                float z = (float)rnd.NextDouble() * ChunkLength;
                if (!hasGround(z0 + z)) continue;
                if (lmId != null && lmPlace.x > 0 && Mathf.Sign(x) == lmSide) continue;
                decor.Create(type, chunk.transform, new Vector3(x, type == "boat" ? -0.3f : 0, -z), (float)rnd.NextDouble() * 360f, ScaleFor(type, rnd), rnd);
            }
            if (lmId != null)
            {
                var lm = LandmarkFactory.Build(lmId, chunk.transform);
                if (lm)
                {
                    lm.transform.localPosition = new Vector3(lmSide * lmPlace.x, 0, -20f);
                    float yaw = lmId == "golden_gate_bridge" ? 90f : lmId == "great_wall" || lmId == "torii_gate" ? 0f : lmSide > 0 ? -90f : 90f;
                    lm.transform.localRotation = Quaternion.Euler(0, yaw, 0);
                    float lead = lmPlace.x == 0 ? 35f : 2.75f * lmPlace.x + 30f;
                    float markZ = z0 + 20f;
                    if (!landmarkMarks.Exists(m => m.z == markZ)) landmarkMarks.Add((markZ, lead, LandmarkFactory.Names.TryGetValue(lmId, out var nm) ? nm : lmId));
                }
            }
            if (world.id == "city" && index % 6 == 3 && hasGround(z0 + 10) && hasGround(z0 + 30)) decor.Create("tunnel", chunk.transform, new Vector3(0, 0, -20f), 0, 1, rnd);
            if (world.id == "city") for (int s = -1; s <= 1; s += 2) for (int k = 0; k < 4; k++) decor.Create("sidewalk", chunk.transform, new Vector3(s * 5.4f, 0, -5f - k * 10f), 0, 1, rnd);
            if (world.id == "city") for (int s = -1; s <= 1; s += 2) decor.Create("street_light", chunk.transform, new Vector3(s * 4.6f, 0, -rnd.Next(5, 35)), s > 0 ? 180 : 0, 1, rnd);
            if (world.id == "park") decor.Create("lamp_post", chunk.transform, new Vector3(4.8f * (index % 2 == 0 ? 1 : -1), 0, -20f), 0, 1, rnd);
            return chunk;
        }

        private static float NearDistance(string type) => type == "building" ? 10f : type == "mountain" ? 40f : type == "cliff" ? 12f : type == "flowers" || type == "shell" || type == "puddle" ? 5f : 6.5f;

        private static float ScaleFor(string type, System.Random r) => type switch
        {
            "building" => 0.8f + (float)r.NextDouble() * 0.8f,
            "tree_round" or "tree_tall" or "pine" or "palm" => 0.8f + (float)r.NextDouble() * 0.5f,
            _ => 0.8f + (float)r.NextDouble() * 0.4f,
        };

        private void BuildBackdrop()
        {
            var rnd = new System.Random(seed);
            // Mountains on the horizon (mountains world: everywhere; others: soft distant hills).
            int hills = world.id == "city" ? 0 : world.id == "mountains" || world.id == "snow" ? 10 : 6;
            for (int i = 0; i < hills; i++)
            {
                float side = i % 2 == 0 ? -1 : 1; if (world.id == "beach") side = -1;
                var m = decor.Create("mountain", backdropRoot, new Vector3(side * (70 + rnd.Next(0, 60)), -2, -60 - rnd.Next(0, 200)), rnd.Next(360), world.id == "mountains" ? 1.2f : 0.6f, rnd);
                if (world.id != "mountains" && world.id != "snow") foreach (var r in m.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = MaterialLibrary.Lit(world.palette.ground, 0.1f);
                backdrop.Add(m);
            }
            if (world.id == "city") for (int i = 0; i < 14; i++)
                backdrop.Add(decor.Create("building", backdropRoot, new Vector3((i % 2 == 0 ? -1 : 1) * (45 + rnd.Next(0, 40)), 0, -40 - rnd.Next(0, 220)), 0, 2f + (float)rnd.NextDouble() * 1.5f, rnd));
            if (world.id == "beach")
            {
                oceanMat = new Material(MaterialLibrary.Lit(world.palette.water ?? "#1FB5D6", 0.96f)) { name = "Ocean" };
                oceanMat.SetTexture("_BumpMap", MaterialLibrary.NoiseNormal); oceanMat.EnableKeyword("_NORMALMAP"); oceanMat.SetTextureScale("_BumpMap", new Vector2(20, 20));
                var ocean = Shapes.Part(backdropRoot, PrimitiveType.Cube, oceanMat, new Vector3(260f, -0.35f, -100f), new Vector3(500f, 0.1f, 700f), default, "Ocean");
                ocean.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var foam = Shapes.Part(backdropRoot, PrimitiveType.Cube, MaterialLibrary.Lit("#FFFFFF", 0.6f), new Vector3(10.2f, -0.28f, -100f), new Vector3(0.6f, 0.1f, 700f), default, "Foam");
                foam.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                backdrop.Add(ocean); backdrop.Add(foam);
            }
            // Hot air balloons drifting in the sky of every world (more on higher quality).
            int balloonCount = Mathf.Max(2, Mathf.RoundToInt(5 * Quality));
            for (int i = 0; i < balloonCount; i++)
            {
                float side = i % 2 == 0 ? -1 : 1; if (world.id == "beach" && i % 3 == 0) side = 1;
                var pos = new Vector3(side * (22 + rnd.Next(0, 60)), 16 + rnd.Next(0, 22), -40 - rnd.Next(0, 220));
                var b = decor.Create("hot_air_balloon_" + (i % 3), backdropRoot, pos, rnd.Next(360), 1.2f + (float)rnd.NextDouble() * 0.7f, rnd);
                backdrop.Add(b); balloons.Add((b.transform, pos, (float)rnd.NextDouble() * 6.28f));
            }
            // Cloud layers (more layers on higher quality = richer "volumetric" sky).
            int clouds = Mathf.RoundToInt(10 * Quality);
            for (int i = 0; i < clouds; i++)
                backdrop.Add(decor.Create("cloud", backdropRoot, new Vector3(rnd.Next(-90, 90), 28 + rnd.Next(0, 18), -rnd.Next(0, 260)), rnd.Next(360), 3f + (float)rnd.NextDouble() * 3f, rnd));
        }

        public void Clear()
        {
            foreach (var c in chunks.Values) if (c) Object.Destroy(c);
            chunks.Clear();
            foreach (var b in backdrop) if (b) Object.Destroy(b);
            backdrop.Clear(); balloons.Clear(); landmarkMarks.Clear(); landmarksShown.Clear();
            if (backdropRoot) Object.Destroy(backdropRoot.gameObject);
            oceanMat = null;
        }
    }
}
