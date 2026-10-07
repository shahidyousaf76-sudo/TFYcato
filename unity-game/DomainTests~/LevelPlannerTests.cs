using System;
using System.Collections.Generic;
using System.Linq;
using TFY.CatAdventure.Domain;
using Xunit;
using Xunit.Abstractions;

namespace TFY.CatAdventure.Tests
{
    public class LevelPlannerTests
    {
        private readonly ITestOutputHelper output;
        public LevelPlannerTests(ITestOutputHelper output) { this.output = output; }

        public static IEnumerable<object[]> Levels() => TestContent.Load().levels.Select(l => new object[] { l.number });
        private static (LevelDef level, LevelPlan plan) PlanFor(int n, RuntimeConfig rc = null)
        {
            var c = TestContent.Load(); var l = c.Level(n);
            return (l, LevelPlanner.Plan(l, c, rc));
        }

        [Theory, MemberData(nameof(Levels))]
        public void Places_exactly_the_budgets_the_server_validates(int n)
        {
            var (l, plan) = PlanFor(n);
            var t = l.Totals();
            Assert.Equal(t.coins, plan.Count(PlacementKind.Coin));
            Assert.Equal(t.fish, plan.Count(PlacementKind.Fish));
            Assert.Equal(t.gems, plan.Count(PlacementKind.Gem));
            Assert.Equal(t.starTokens, plan.Count(PlacementKind.StarToken));
            Assert.Equal(t.mysteryBoxes, plan.Count(PlacementKind.MysteryBox));
            Assert.Equal(t.powerUps, plan.Count(PlacementKind.PowerUp));
            Assert.Equal(l.checkpointCount, plan.checkpoints.Count);
            Assert.Equal(t.length, plan.length);
            output.WriteLine($"level {n}: {plan.rows.Count(r => r.obstacleId != null)} obstacle rows, {t.coins} coins, length {t.length}");
        }

        [Theory, MemberData(nameof(Levels))]
        public void Is_deterministic(int n)
        {
            var a = PlanFor(n).plan.items.Select(i => i.ToString()).ToList();
            var b = PlanFor(n).plan.items.Select(i => i.ToString()).ToList();
            Assert.Equal(a, b);
        }

        [Theory, MemberData(nameof(Levels))]
        public void Never_blocks_every_lane_unless_everything_can_be_jumped(int n)
        {
            foreach (var r in PlanFor(n).plan.rows.Where(r => r.obstacleId != null))
            {
                bool allBlocked = r.blocked.All(b => b);
                if (allBlocked) Assert.True(r.jumpable && (r.obstacleId == "log" || r.obstacleId == "branch" || r.obstacleId == "broken_bridge"), $"row {r.obstacleId} at {r.z} blocks all lanes");
                if (r.obstacleId == "wall") Assert.Contains(false, r.blocked);
            }
        }

        [Theory, MemberData(nameof(Levels))]
        public void Leaves_time_to_react_between_rows(int n)
        {
            var (l, plan) = PlanFor(n, new RuntimeConfig { run_speed_scale = 1.2f, difficulty_density_scale = 1.2f }.Sanitized()); // hardest remote tuning
            var rows = plan.rows.Where(r => r.obstacleId != null).OrderBy(r => r.z).ToList();
            for (int i = 1; i < rows.Count; i++)
            {
                float speed = Rules.SpeedAt(l, rows[i].z, 1.2f);
                float free = rows[i].z - rows[i - 1].End;
                Assert.True(free >= speed * 1.0f, $"level {n}: only {free:0.0} m (<1 s at {speed:0.0} m/s) between {rows[i - 1].obstacleId}@{rows[i - 1].z:0} and {rows[i].obstacleId}@{rows[i].z:0}");
            }
        }

        [Theory, MemberData(nameof(Levels))]
        public void Bridges_always_have_a_long_jump_zone_right_before_them(int n)
        {
            var plan = PlanFor(n).plan;
            foreach (var b in plan.items.Where(i => i.kind == PlacementKind.Obstacle && i.id == "broken_bridge"))
                Assert.Contains(plan.items, z => z.kind == PlacementKind.LongJumpZone && Math.Abs(z.z + z.length - b.z) < 0.01f);
        }

        [Theory, MemberData(nameof(Levels))]
        public void Collectibles_never_sit_inside_an_obstacle(int n)
        {
            var (_, plan) = PlanFor(n);
            var obstacles = plan.items.Where(i => i.kind == PlacementKind.Obstacle).ToList();
            var content = TestContent.Load();
            foreach (var c in plan.items.Where(i => i.kind >= PlacementKind.Coin && i.kind <= PlacementKind.PowerUp))
            foreach (var o in obstacles)
            {
                bool sameLane = o.lane == -1 || o.lane == c.lane;
                bool overlapsZ = c.z > o.z - 0.6f && c.z < o.z + o.length + 0.6f;
                if (!sameLane || !overlapsZ) continue;
                var def = content.Obstacle(o.id);
                float top = o.id == "broken_bridge" ? 0 : Math.Max(def.height, def.topHeight);
                Assert.True(c.y >= top + 0.3f, $"level {n}: {c} is inside {o}");
            }
        }

        [Theory, MemberData(nameof(Levels))]
        public void Checkpoints_and_finish_have_clear_space(int n)
        {
            var (_, plan) = PlanFor(n);
            foreach (var cp in plan.checkpoints.Concat(new[] { plan.finishZ }))
                Assert.DoesNotContain(plan.rows.Where(r => r.obstacleId != null), r => r.z < cp + 6f && r.End > cp - 6f);
        }

        [Fact]
        public void Level_one_starts_with_a_long_safe_runway()
        {
            var (_, plan) = PlanFor(1);
            Assert.True(plan.rows.Where(r => r.obstacleId != null).Min(r => r.z) >= 24f);
        }

        [Fact]
        public void Mulberry32_matches_the_javascript_reference()
        {
            // Values from the JS implementation used by the web prototype: mulberry32(42) first three outputs.
            var r = new Mulberry32(42);
            Assert.Equal(0.6011037519201636, r.Next(), 12);
            Assert.Equal(0.44829055899754167, r.Next(), 12);
            Assert.Equal(0.8524657934904099, r.Next(), 12);
        }
    }
}
