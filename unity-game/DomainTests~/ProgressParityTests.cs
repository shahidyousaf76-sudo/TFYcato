using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TFY.CatAdventure.Domain;
using Xunit;

namespace TFY.CatAdventure.Tests
{
    /// <summary>The C# offline result must equal the server's result to the coin (fixtures generated from the TypeScript code).</summary>
    public class ProgressParityTests
    {
        [Fact]
        public void Csharp_progress_matches_the_server_for_250_random_runs()
        {
            var c = TestContent.Load();
            var steps = JObject.Parse(File.ReadAllText(Path.Combine(TestContent.Root, "shared/parity-fixtures.json")))["steps"]!;
            var p = new PlayerData { uid = "parity", username = "Kitty4821", createdAt = 1000 }.Normalize();
            int i = 0;
            foreach (var step in steps)
            {
                var run = step["run"]!.ToObject<RunReport>();
                float mult = step["run"]!["multiplier"]!.Value<float>();
                var level = c.LevelById(run.levelId);
                var (next, result) = Progress.Apply(p, level, run, c, mult, 2000 + i);
                var exp = step["result"]!;
                Assert.Equal(exp["stars"]!.Value<int>(), result.stars);
                Assert.Equal(exp["score"]!.Value<int>(), result.score);
                Assert.Equal(exp["coinsEarned"]!.Value<int>(), result.coinsEarned);
                Assert.Equal(exp["bonusCoins"]!.Value<int>(), result.bonusCoins);
                Assert.Equal(exp["gemsEarned"]!.Value<int>(), result.gemsEarned);
                Assert.Equal(exp["mysteryBoxesEarned"]!.Value<int>(), result.mysteryBoxesEarned);
                Assert.Equal(exp["firstCompletion"]!.Value<bool>(), result.firstCompletion);
                Assert.Equal(exp["unlockedLevel"]!.Type == JTokenType.Null ? (int?)null : exp["unlockedLevel"]!.Value<int>(), result.unlockedLevel);
                Assert.Equal(exp["achievements"]!.Select(a => a["id"]!.Value<string>()).ToList(), result.achievements.Select(a => a.id).ToList());
                var pe = step["player"]!;
                Assert.Equal(pe["coins"]!.Value<int>(), next.coins);
                Assert.Equal(pe["gems"]!.Value<int>(), next.gems);
                Assert.Equal(pe["fish"]!.Value<int>(), next.fish);
                Assert.Equal(pe["stars"]!.Value<int>(), next.stars);
                Assert.Equal(pe["currentLevel"]!.Value<int>(), next.currentLevel);
                Assert.Equal(pe["highScore"]!.Value<int>(), next.highScore);
                Assert.Equal(pe["pendingBoxes"]!.Value<int>(), next.pendingBoxes);
                Assert.Equal(pe["achievements"]!.Select(a => a.Value<string>()).ToList(), next.achievements.Keys.OrderBy(k => k, System.StringComparer.Ordinal).ToList());
                p = next; i++;
            }
            Assert.Equal(250, i);
        }

        [Fact]
        public void Offline_runs_are_replayed_on_top_of_the_server_profile_and_dropped_when_acknowledged()
        {
            var c = TestContent.Load();
            var s = new SaveState { server = new PlayerData { uid = "u", username = "Kitty4821" }.Normalize() };
            var l1 = c.Level(1); var t = l1.Totals();
            s.Enqueue(new RunReport { runId = "offline_1", levelId = l1.id, coins = 10, coinValue = 10, heartsLeft = 3, durationSec = 60, finishedAt = 1 });
            s.Enqueue(new RunReport { runId = "offline_1", levelId = l1.id, coins = 10, coinValue = 10, heartsLeft = 3, durationSec = 60, finishedAt = 1 }); // duplicate ignored
            var l2 = c.Level(2);
            s.Enqueue(new RunReport { runId = "offline_2", levelId = l2.id, coins = 5, coinValue = 5, heartsLeft = 1, durationSec = 60, finishedAt = 2 });
            var eff = s.Effective(c);
            Assert.Single(s.pending.Where(p => p.runId == "offline_1"));
            Assert.Equal(3, eff.currentLevel);
            Assert.True(eff.coins > 0);
            Assert.Equal(1, s.server.currentLevel); // the confirmed profile is untouched
            s.Acknowledge(new[] { "offline_1", "offline_2" });
            Assert.Empty(s.pending);
            Assert.Equal(1, s.Effective(c).currentLevel);
        }

        [Fact]
        public void Settings_changed_offline_win_until_the_server_has_a_newer_copy()
        {
            var c = TestContent.Load();
            var s = new SaveState { server = new PlayerData { username = "Kitty4821", updatedAt = 100 }.Normalize() };
            s.localSettings = new PlayerSettings { music = false }; s.localSettingsAt = 200;
            Assert.False(s.Effective(c).settings.music);
            s.AcceptServer(new PlayerData { username = "Kitty4821", updatedAt = 300, settings = new PlayerSettings { music = true } });
            Assert.Null(s.localSettings);
            Assert.True(s.Effective(c).settings.music);
        }

        [Fact]
        public void Normalize_repairs_a_damaged_profile()
        {
            var p = JsonConvert.DeserializeObject<PlayerData>("{\"username\":\"Kitty4821\",\"currentLevel\":0,\"selectedCat\":\"space_cat\",\"equipped\":null,\"levels\":null}")!.Normalize();
            Assert.Equal(1, p.currentLevel);
            Assert.Equal("orange_cat", p.selectedCat);
            Assert.Equal(7, p.equipped.Count);
            Assert.NotNull(p.levels);
        }

        [Fact]
        public void Usernames_are_generated_safely()
        {
            var c = TestContent.Load(); var rng = new System.Random(7);
            for (int i = 0; i < 300; i++) Assert.True(Rules.IsValidUsername(Rules.GenerateUsername(c.usernameWords.prefixes, rng), c.usernameWords.prefixes));
            foreach (var bad in new[] { "Emma2015", "Kitty12", "Kitty0123", "Kitty 482", "" }) Assert.False(Rules.IsValidUsername(bad, c.usernameWords.prefixes));
        }

        [Fact]
        public void Remote_config_is_clamped()
        {
            var rc = new RuntimeConfig { run_speed_scale = 9, starting_hearts = 0, powerup_durations = new PowerUpDurations { magnet = 999 } }.Sanitized();
            Assert.Equal(1.2f, rc.run_speed_scale);
            Assert.Equal(1, rc.starting_hearts);
            Assert.Equal(20f, rc.powerup_durations.magnet);
        }
    }
}
