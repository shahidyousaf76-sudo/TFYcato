using System;
using System.Collections.Generic;
using System.Linq;

namespace TFY.CatAdventure.Domain
{
    public enum PlacementKind { Obstacle, Coin, Fish, Gem, StarToken, MysteryBox, PowerUp, LongJumpZone, Checkpoint, Finish }

    /// <summary>One thing placed on the track. z is metres from the start line (forward positive); lane 0..2, -1 = all lanes.</summary>
    [Serializable]
    public struct Placement
    {
        public PlacementKind kind;
        public string id;
        public int lane;
        public float z, y, length, phase;
        public int section;
        public override string ToString() => $"{kind}:{id} lane={lane} z={z:0.0} y={y:0.0}";
    }

    /// <summary>A row of obstacles across the three lanes.</summary>
    [Serializable]
    public class Row
    {
        public float z, depth;
        public bool[] blocked = new bool[3];
        public string obstacleId;
        public bool fullWidth, jumpable;
        public int section;
        public int OpenLane(int preferred)
        {
            if (!blocked[preferred]) return preferred;
            for (int d = 1; d < 3; d++) { if (preferred - d >= 0 && !blocked[preferred - d]) return preferred - d; if (preferred + d < 3 && !blocked[preferred + d]) return preferred + d; }
            return 1; // full-width jumpable row: stay in the middle
        }
        public float End => z + depth;
    }

    public class LevelPlan
    {
        public LevelDef level;
        public float length, finishZ;
        public List<Placement> items = new List<Placement>();
        public List<Row> rows = new List<Row>();
        public List<float> checkpoints = new List<float>();
        public List<float> sectionStarts = new List<float>();
        public int Count(PlacementKind k) => items.Count(i => i.kind == k);
    }

    /// <summary>Deterministic 32-bit PRNG (mulberry32) — identical layouts on every device and in the web prototype.</summary>
    public class Mulberry32
    {
        private uint state;
        public Mulberry32(int seed) { state = unchecked((uint)seed); }
        public double Next()
        {
            unchecked
            {
                state += 0x6D2B79F5u;
                uint t = state;
                t = (t ^ (t >> 15)) * (t | 1u);
                t ^= t + (t ^ (t >> 7)) * (t | 61u);
                return ((t ^ (t >> 14)) >> 0) / 4294967296.0;
            }
        }
        public int Range(int minInclusive, int maxExclusive) => minInclusive + (int)(Next() * (maxExclusive - minInclusive));
        public T Pick<T>(IList<T> list) => list[Range(0, list.Count)];
    }

    /// <summary>
    /// Turns a LevelDef (sections + budgets) into concrete placements. Fairness rules — never impossible:
    ///  1. A row never blocks all three lanes unless every lane can be jumped (logs, branches, a bridge gap with a long-jump zone).
    ///  2. Rows are spaced by speed: at least ~1.35 s of running between rows (more on easy levels), so there is time to react.
    ///  3. Coin lines lead into a lane that is open at the next row; coin arcs sit over obstacles to show where to jump.
    ///  4. Checkpoints and the finish have clear space around them.
    ///  5. Exactly the section budgets of coins, fish, gems, big stars, mystery boxes and power-ups are placed (the server validates against them).
    /// </summary>
    public static class LevelPlanner
    {
        public const float LaneWidth = 2.2f;
        private const float LineSpacing = 2.0f;

        private class Region { public float start, end; public int lane; public int section; public bool usedByItem; public int coins; public float itemZ; }
        private class Arc { public Row row; public int lane; public int coins; public int section; public bool hasItem; }

        public static LevelPlan Plan(LevelDef level, GameContent content, RuntimeConfig runtime = null)
        {
            runtime = runtime ?? new RuntimeConfig();
            var rng = new Mulberry32(level.seed);
            var plan = new LevelPlan { level = level };
            float S = 0, prevEnd = -1f; // prevEnd: where the next row may start after the previous section's last obstacle
            bool easy = level.difficulty == "easy";

            for (int si = 0; si < level.sections.Count; si++)
            {
                var sec = level.sections[si];
                float E = S + sec.length;
                plan.sectionStarts.Add(S);
                var rows = BuildRows(level, content, runtime, sec, si, S, E, easy, rng, prevEnd);
                plan.rows.AddRange(rows);
                var lastObstacle = rows.LastOrDefault(r => r.obstacleId != null);
                if (lastObstacle != null) prevEnd = NextRowStart(level, runtime, lastObstacle, easy);
                foreach (var r in rows)
                {
                    if (r.obstacleId == null) continue;
                    if (r.fullWidth)
                    {
                        plan.items.Add(new Placement { kind = PlacementKind.Obstacle, id = r.obstacleId, lane = -1, z = r.z, length = r.depth, section = si });
                        if (r.obstacleId == "broken_bridge")
                            plan.items.Add(new Placement { kind = PlacementKind.LongJumpZone, id = "long_jump", lane = -1, z = r.z - 7f, length = 7f, section = si });
                    }
                    else for (int l = 0; l < 3; l++) if (r.blocked[l])
                        plan.items.Add(new Placement { kind = PlacementKind.Obstacle, id = r.obstacleId, lane = l, z = r.z, length = r.depth, phase = (float)rng.Next(), section = si });
                }
                if (sec.checkpointAfter) { plan.checkpoints.Add(E - 5f); plan.items.Add(new Placement { kind = PlacementKind.Checkpoint, id = "checkpoint", lane = -1, z = E - 5f, section = si }); }
                S = E;
            }
            plan.length = S;
            plan.finishZ = S;
            plan.items.Add(new Placement { kind = PlacementKind.Finish, id = "finish", lane = -1, z = S, section = level.sections.Count - 1 });

            PlaceCollectibles(plan, level, rng);
            // Deterministic order (z, then kind, lane, height) — identical on every platform and in the web port.
            plan.items.Sort((a, b) => a.z != b.z ? a.z.CompareTo(b.z) : a.kind != b.kind ? a.kind.CompareTo(b.kind) : a.lane != b.lane ? a.lane.CompareTo(b.lane) : a.y.CompareTo(b.y));
            return plan;
        }

        // ------------------------------------------------------------------ rows
        /// <summary>Earliest start of the next row after row r (speed-based reaction gap, extra room after bridges and walls).</summary>
        private static float NextRowStart(LevelDef level, RuntimeConfig rt, Row r, bool easy)
        {
            float speed = Rules.SpeedAt(level, r.z, rt.run_speed_scale);
            float gap = Math.Max(11f, speed * 1.35f) + (easy ? 3f : 0f);
            return r.End + gap + (r.obstacleId == "broken_bridge" ? 4f : 0f) + (r.obstacleId == "wall" ? 2f : 0f);
        }

        private static List<Row> BuildRows(LevelDef level, GameContent content, RuntimeConfig rt, SectionDef sec, int si, float S, float E, bool easy, Mulberry32 rng, float prevEnd)
        {
            var rows = new List<Row>();
            bool noObstacles = sec.kind == SectionKinds.Start || sec.kind == SectionKinds.Finish || sec.obstacles == null || sec.obstacles.Count == 0;
            float marginIn = si == 0 ? 24f : 9f;
            float marginOut = sec.checkpointAfter ? 12f : 5f;
            float density = Math.Min(0.95f, sec.density * rt.difficulty_density_scale);
            bool isChallenge = sec.kind == SectionKinds.Challenge;
            int cycle = 0, weave = rng.Range(0, 3);
            float z = Math.Max(S + marginIn, prevEnd); // sections never squeeze rows closer than the reaction gap

            while (true)
            {
                float speed = Rules.SpeedAt(level, z, rt.run_speed_scale);
                float gap = Math.Max(11f, speed * 1.35f) + (easy ? 3f : 0f);
                var row = new Row { z = z, depth = 1f, section = si };
                bool hasObstacle = !noObstacles && (isChallenge || rng.Next() < density);
                if (hasObstacle)
                {
                    var id = isChallenge ? sec.obstacles[cycle++ % sec.obstacles.Count] : rng.Pick(sec.obstacles);
                    var def = content.Obstacle(id);
                    if (def == null) { hasObstacle = false; }
                    else
                    {
                        row.obstacleId = id; row.depth = def.depth;
                        if (id == "broken_bridge") { row.z = z + 8f; row.fullWidth = true; row.jumpable = true; for (int l = 0; l < 3; l++) row.blocked[l] = true; } // 8 m run-up for the long-jump zone
                        else ChooseLanes(row, def, sec, isChallenge, rng, ref weave);
                    }
                }
                if (row.End + marginOut > E) break;
                rows.Add(row);
                z = row.obstacleId != null ? NextRowStart(level, rt, row, easy) : row.End + gap;
            }
            return rows;
        }

        private static void ChooseLanes(Row row, ObstacleDef def, SectionDef sec, bool isChallenge, Mulberry32 rng, ref int weave)
        {
            string id = def.id;
            bool canFullWidth = id == "log" || id == "branch"; // everyone can jump (or slide under) these
            double fullChance = isChallenge && (sec.challenge == "log_hop" || sec.challenge == "branch_duck") ? 0.75 : 0.12;
            row.jumpable = def.clearance != "wall";
            if (canFullWidth && rng.Next() < fullChance) { row.fullWidth = true; for (int l = 0; l < 3; l++) row.blocked[l] = true; return; }

            bool moving = id == "moving_critter";
            int blockedCount = !moving && rng.Next() < sec.twoLaneChance ? 2 : 1;
            int open;
            if (isChallenge && (sec.challenge == "wall_weave" || sec.challenge == "barrel_alley" || sec.challenge == "combo_wall_critter"))
            { weave = (weave + 1 + rng.Range(0, 2)) % 3; open = weave; blockedCount = 2; } // weave: the open lane moves every row
            else open = rng.Range(0, 3);

            if (blockedCount == 2) { for (int l = 0; l < 3; l++) row.blocked[l] = l != open; }
            else { int b = (open + 1 + rng.Range(0, 2)) % 3; row.blocked[b] = true; }
        }

        // ------------------------------------------------------------------ collectibles
        private static void PlaceCollectibles(LevelPlan plan, LevelDef level, Mulberry32 rng)
        {
            var sections = level.sections;
            for (int si = 0; si < sections.Count; si++)
            {
                var sec = sections[si];
                float S = plan.sectionStarts[si], E = S + sec.length;
                var rows = plan.rows.Where(r => r.section == si).OrderBy(r => r.z).ToList();
                var regions = BuildRegions(plan, rows, si, S, E);
                var arcs = rows.Where(r => r.obstacleId != null && r.jumpable).Select(r => new Arc { row = r, lane = ArcLane(r, rng), section = si }).ToList();

                // Special items first: each takes a region (or sits high above a jumpable obstacle).
                var specials = new List<(PlacementKind kind, string id)>();
                for (int i = 0; i < sec.starTokens; i++) specials.Add((PlacementKind.StarToken, "star"));
                for (int i = 0; i < sec.gems; i++) specials.Add((PlacementKind.Gem, "gem"));
                for (int i = 0; i < sec.mysteryBoxes; i++) specials.Add((PlacementKind.MysteryBox, "mystery_box"));
                for (int i = 0; i < sec.fish; i++) specials.Add((PlacementKind.Fish, "fish"));
                for (int i = 0; i < sec.powerUps; i++) specials.Add((PlacementKind.PowerUp, PickPowerUp(level.number, rng)));

                var itemRegions = Spread(regions, specials.Count);
                for (int i = 0; i < specials.Count; i++)
                {
                    var (kind, id) = specials[i];
                    // Big stars in challenge sections and gems prefer the top of a jump arc: you have to jump for them.
                    var arc = (kind == PlacementKind.Gem || (kind == PlacementKind.StarToken && sec.kind == SectionKinds.Challenge)) ? arcs.FirstOrDefault(a => !a.hasItem && a.row.obstacleId != "broken_bridge") : null;
                    if (arc != null)
                    {
                        arc.hasItem = true;
                        plan.items.Add(new Placement { kind = kind, id = id, lane = arc.lane, z = arc.row.z + arc.row.depth * 0.5f, y = TopOfArc(arc.row) + 0.5f, section = si });
                        continue;
                    }
                    var reg = itemRegions.Count > i ? itemRegions[i] : null;
                    if (reg != null)
                    {
                        reg.usedByItem = true;
                        reg.itemZ = (reg.start + reg.end) * 0.5f;
                        plan.items.Add(new Placement { kind = kind, id = id, lane = reg.lane, z = reg.itemZ, y = 1.0f, section = si });
                    }
                    else
                    {
                        // Fallback (very short sections): high above the middle of the section — reachable with a jump.
                        plan.items.Add(new Placement { kind = kind, id = id, lane = 1, z = S + sec.length * (i + 1f) / (specials.Count + 1f), y = 2.0f, section = si });
                    }
                }

                // Coins: arcs over every other jumpable obstacle (shows where to jump), then lines that lead into an open lane.
                int remaining = sec.coins;
                for (int i = 0; i < arcs.Count && remaining > 0; i += 2)
                {
                    if (arcs[i].hasItem) continue;
                    arcs[i].coins = Math.Min(5, remaining); remaining -= arcs[i].coins;
                }
                foreach (var reg in regions)
                {
                    if (remaining <= 0) break;
                    int cap = Capacity(reg);
                    reg.coins = Math.Min(Math.Min(5, cap), remaining); remaining -= reg.coins;
                }
                foreach (var reg in regions)
                {
                    if (remaining <= 0) break;
                    int extra = Math.Min(Capacity(reg) - reg.coins, remaining); reg.coins += extra; remaining -= extra;
                }
                for (int i = 1; i < arcs.Count && remaining > 0; i += 2)
                {
                    if (arcs[i].hasItem || arcs[i].coins > 0) continue;
                    arcs[i].coins = Math.Min(5, remaining); remaining -= arcs[i].coins;
                }
                if (remaining > 0)
                {
                    // Last resort: a high line above the section start (jump to collect). Keeps the budget exact.
                    float z0 = S + 3f;
                    for (int i = 0; i < remaining; i++) plan.items.Add(new Placement { kind = PlacementKind.Coin, id = "coin", lane = 1, z = z0 + i * 1.2f, y = 2.2f, section = si });
                    remaining = 0;
                }

                foreach (var a in arcs) EmitArc(plan, a);
                foreach (var reg in regions) EmitLine(plan, reg);
            }
        }

        /// <summary>Free stretches between rows. The lane is the one open at the NEXT row, so following coins leads to safety.</summary>
        private static List<Region> BuildRegions(LevelPlan plan, List<Row> rows, int si, float S, float E)
        {
            var regions = new List<Region>();
            float cursor = S + (si == 0 ? 8f : 3f);
            int prevOpen = 1;
            foreach (var r in rows)
            {
                if (r.obstacleId == null) continue;
                float end = r.z - (r.obstacleId == "broken_bridge" ? 9f : 4.5f);
                int lane = r.OpenLane(prevOpen);
                if (end - cursor >= 4f) regions.Add(new Region { start = cursor, end = end, lane = lane, section = si });
                cursor = r.End + (r.obstacleId == "broken_bridge" ? 5f : 3.5f);
                prevOpen = lane;
            }
            float tailEnd = E - 3f;
            foreach (var cp in plan.checkpoints) if (cp > cursor && cp < tailEnd) tailEnd = Math.Min(tailEnd, cp - 2f);
            if (tailEnd - cursor >= 4f) regions.Add(new Region { start = cursor, end = tailEnd, lane = prevOpen, section = si });
            return regions;
        }

        private static int Capacity(Region r) => LinePositions(r, 8).Count;

        /// <summary>Evenly spaced coin positions inside the region, stepping around a special item if one sits there.</summary>
        private static List<float> LinePositions(Region r, int count)
        {
            var list = new List<float>();
            float z = r.start;
            if (!r.usedByItem) z += Math.Max(0, (r.end - r.start - (count - 1) * LineSpacing) * 0.5f); // centre the line
            while (list.Count < count && z <= r.end + 0.001f)
            {
                if (r.usedByItem && Math.Abs(z - r.itemZ) < 1.5f) { z = r.itemZ + 1.5f; continue; }
                list.Add(z); z += LineSpacing;
            }
            return list;
        }

        private static List<Region> Spread(List<Region> regions, int n)
        {
            var result = new List<Region>();
            if (n == 0 || regions.Count == 0) return result;
            // Items take regions spread evenly through the section; with more items than regions, some share (handled by fallback).
            for (int i = 0; i < n && i < regions.Count; i++) result.Add(regions[(int)Math.Floor((i + 0.5) * regions.Count / Math.Min(n, regions.Count))]);
            return result.Distinct().ToList();
        }

        private static int ArcLane(Row r, Mulberry32 rng)
        {
            if (r.fullWidth) return 1;
            var lanes = new List<int>(); for (int l = 0; l < 3; l++) if (r.blocked[l]) lanes.Add(l);
            return lanes.Count > 0 ? rng.Pick(lanes) : 1;
        }

        private static float TopOfArc(Row r) => r.obstacleId == "barrel" ? 2.8f : r.obstacleId == "broken_bridge" ? 3.0f : 2.4f;

        private static void EmitArc(LevelPlan plan, Arc a)
        {
            if (a.coins <= 0) return;
            float mid = a.row.z + a.row.depth * 0.5f, top = TopOfArc(a.row);
            float span = a.row.obstacleId == "broken_bridge" ? 5f : 2.6f;
            for (int i = 0; i < a.coins; i++)
            {
                float k = a.coins == 1 ? 0 : (i / (a.coins - 1f)) * 2f - 1f; // -1..1
                plan.items.Add(new Placement { kind = PlacementKind.Coin, id = "coin", lane = a.lane, z = mid + k * span, y = 0.9f + (top - 0.9f) * (1f - k * k), section = a.section });
            }
        }

        private static void EmitLine(LevelPlan plan, Region r)
        {
            if (r.coins <= 0) return;
            foreach (var z in LinePositions(r, r.coins))
                plan.items.Add(new Placement { kind = PlacementKind.Coin, id = "coin", lane = r.lane, z = z, y = 0.8f, section = r.section });
        }

        private static string PickPowerUp(int levelNumber, Mulberry32 rng)
        {
            var pool = levelNumber < 4 ? new[] { "magnet", "shield", "double_coins" } : new[] { "magnet", "shield", "super_jump", "speed_boost", "double_coins" };
            return rng.Pick(pool);
        }
    }
}
