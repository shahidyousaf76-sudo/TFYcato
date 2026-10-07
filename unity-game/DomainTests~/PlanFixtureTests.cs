using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TFY.CatAdventure.Domain;
using Xunit;

namespace TFY.CatAdventure.Tests
{
    /// <summary>Writes every level's layout to shared/plan-fixtures.json so the web prototype's JavaScript planner can be checked against it.</summary>
    public class PlanFixtureTests
    {
        [Fact]
        public void Writes_plan_fixtures_for_the_web_port()
        {
            var c = TestContent.Load();
            var plans = c.levels.Select(l =>
            {
                var p = LevelPlanner.Plan(l, c);
                return new { level = l.id, items = p.items.Select(i => $"{KindName(i.kind)}:{i.id}:{i.lane}:{i.z:0.00}:{i.y:0.00}").ToList() };
            }).ToList();
            File.WriteAllText(Path.Combine(TestContent.Root, "shared/plan-fixtures.json"), JsonConvert.SerializeObject(plans, Formatting.Indented));
            Assert.Equal(c.levels.Count, plans.Count);
        }

        private static string KindName(PlacementKind k) => k switch
        {
            PlacementKind.Obstacle => "obstacle", PlacementKind.Coin => "coin", PlacementKind.Fish => "fish", PlacementKind.Gem => "gem",
            PlacementKind.StarToken => "star", PlacementKind.MysteryBox => "box", PlacementKind.PowerUp => "powerup",
            PlacementKind.LongJumpZone => "longjump", PlacementKind.Checkpoint => "checkpoint", _ => "finish",
        };
    }
}
