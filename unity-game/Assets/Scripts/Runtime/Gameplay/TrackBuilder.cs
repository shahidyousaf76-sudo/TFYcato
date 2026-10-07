using System.Collections.Generic;
using System.Linq;
using TFY.CatAdventure.Domain;
using UnityEngine;

namespace TFY.CatAdventure.Gameplay
{
    /// <summary>
    /// Streams the level plan into the scene: spawns placements ~110 m ahead of the cat from pools and recycles them
    /// once they are behind, so the scene only ever holds what is near. Also answers "is there ground here?" for bridge gaps.
    /// </summary>
    public class TrackBuilder
    {
        public const float AheadDistance = 110f, BehindDistance = 14f;
        private readonly ObstacleFactory obstacles;
        private readonly CollectibleFactory collectibles;
        private readonly Transform markerRoot;
        private readonly Data.ContentService contentService;
        private GameContent content => contentService.Content;
        private LevelPlan plan;
        private int next;
        private readonly List<(float start, float end)> gaps = new List<(float, float)>();
        private readonly List<(float start, float end)> longJumpZones = new List<(float, float)>();
        public readonly List<ObstacleView> LiveObstacles = new List<ObstacleView>();
        public readonly List<CollectibleView> LiveCollectibles = new List<CollectibleView>();
        private readonly List<GameObject> markers = new List<GameObject>();
        private readonly HashSet<int> collectedIndices = new HashSet<int>();

        public TrackBuilder(ObstacleFactory o, CollectibleFactory c, Transform markerRoot, Data.ContentService content) { obstacles = o; collectibles = c; this.markerRoot = markerRoot; contentService = content; }

        public LevelPlan Plan => plan;
        public IReadOnlyList<(float start, float end)> Gaps => gaps;

        /// <summary>Starts streaming. `skipCollected` keeps coins/stars already taken before a checkpoint from reappearing.</summary>
        public void Begin(LevelPlan p, float fromDistance, IEnumerable<int> skipCollected)
        {
            Clear();
            plan = p; next = 0;
            collectedIndices.Clear(); if (skipCollected != null) foreach (var i in skipCollected) collectedIndices.Add(i);
            gaps.Clear(); longJumpZones.Clear();
            foreach (var it in p.items)
            {
                if (it.kind == PlacementKind.Obstacle && it.id == "broken_bridge") gaps.Add((it.z, it.z + it.length));
                if (it.kind == PlacementKind.LongJumpZone) longJumpZones.Add((it.z, it.z + it.length));
            }
            markers.Add(TrackPieces.Finish(markerRoot, p.finishZ));
            foreach (var cp in p.checkpoints) markers.Add(TrackPieces.Checkpoint(markerRoot, cp));
            // Skip everything already behind the restart point.
            while (next < plan.items.Count && plan.items[next].z < fromDistance - 2f) next++;
        }

        public void Tick(float catDistance)
        {
            while (next < plan.items.Count && plan.items[next].z < catDistance + AheadDistance)
            {
                var it = plan.items[next];
                switch (it.kind)
                {
                    case PlacementKind.Obstacle:
                        var def = content.Obstacle(it.id);
                        if (def != null) LiveObstacles.Add(obstacles.Spawn(def, it.lane, it.z, it.phase));
                        break;
                    case PlacementKind.LongJumpZone:
                        markers.Add(TrackPieces.LongJumpArrows(markerRoot, it.z, it.length));
                        break;
                    case PlacementKind.Checkpoint: case PlacementKind.Finish: break;
                    default:
                        if (!collectedIndices.Contains(next)) { var v = collectibles.Spawn(it); v.name = next.ToString(); LiveCollectibles.Add(v); }
                        break;
                }
                next++;
            }
            for (int i = LiveObstacles.Count - 1; i >= 0; i--)
            {
                var o = LiveObstacles[i];
                if (!o.gameObject.activeSelf) { LiveObstacles.RemoveAt(i); continue; }
                if (o.planZ + o.def.depth < catDistance - BehindDistance) { o.Release(); LiveObstacles.RemoveAt(i); }
            }
            for (int i = LiveCollectibles.Count - 1; i >= 0; i--)
            {
                var c = LiveCollectibles[i];
                if (c.planZ < catDistance - BehindDistance && !c.Magnetised) { c.Release(); LiveCollectibles.RemoveAt(i); }
            }
        }

        /// <summary>Plan index of a collectible view (its name), so checkpoint restarts can skip already-taken items.</summary>
        public static int IndexOf(CollectibleView v) => int.TryParse(v.name, out var i) ? i : -1;

        public bool HasGround(float distance) { foreach (var g in gaps) if (distance > g.start + 0.3f && distance < g.end - 0.3f) return false; return true; }
        public bool InLongJumpZone(float distance) { foreach (var z in longJumpZones) if (distance >= z.start && distance <= z.end + 0.5f) return true; return false; }
        public float GapEndAfter(float distance) { foreach (var g in gaps) if (distance >= g.start - 1f && distance <= g.end) return g.end; return distance; }

        public void Clear()
        {
            foreach (var o in LiveObstacles) if (o) o.Release();
            foreach (var c in LiveCollectibles) if (c) c.Release();
            foreach (var m in markers) if (m) Object.Destroy(m);
            LiveObstacles.Clear(); LiveCollectibles.Clear(); markers.Clear();
        }
    }
}
