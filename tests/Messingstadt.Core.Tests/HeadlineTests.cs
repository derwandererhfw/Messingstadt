using System.IO;
using Messingstadt.Core;
using Xunit;

namespace Messingstadt.Core.Tests
{
    public class HeadlineTests
    {
        private static MissionData Mission() => new()
        {
            Seed = 7, Item = new[] { 28.5, 0.5 }, ItemName = "Buch",
            Exit = new ZoneData { X0 = 0, Y0 = 0, X1 = 2, Y1 = 2 },
            Agents = new() { new UnitData { Name = "A", Sprite = "Agent", X = 0.5, Y = 0.5 } },
        };

        private static World NewWorld(MissionData m) => new(new MapData { Width = 30, Height = 12 }, m);

        private static World Won(MissionData m)
        {
            var w = NewWorld(m);
            w.Units[0].CarriesItem = true;
            for (int i = 0; i < 40 && w.State == MissionState.Running; i++)
            {
                w.Step();
            }
            Assert.Equal(MissionState.Won, w.State);
            return w;
        }

        private static World Lost(MissionData m)
        {
            var w = NewWorld(m);
            w.Units[0].Hp = 0;
            w.Step();
            Assert.Equal(MissionState.Lost, w.State);
            return w;
        }

        [Fact]
        public void UnseenWinNamesTheMissingItem()
        {
            var (head, line) = Headlines.For(Won(Mission()), false);
            Assert.Contains("niemand bemerkte", head);
            Assert.Contains("was fehlt: Buch.", line);
        }

        [Fact]
        public void LootIsMentionedWhenTaken()
        {
            var m = Mission();
            m.Loot = new LootData { Name = "Lohnkasse", X = 0.5, Y = 0.5, Value = 300 };
            Assert.EndsWith("Gestohlen wurde außerdem: Lohnkasse.", Headlines.For(Won(m), false).Line);
            m.Headlines = new HeadlineData { Loot = "Auch die {lootName} fehlt." };
            Assert.EndsWith("Auch die Lohnkasse fehlt.", Headlines.For(Won(m), false).Line);
        }

        [Fact]
        public void DefeatAndAbandonHaveTheirOwnHeadlines()
        {
            Assert.Contains("zerschlagen", Headlines.For(Lost(Mission()), false).Head);
            Assert.Contains("flüchtig", Headlines.For(Loud(), true).Head);
        }

        [Fact]
        public void RetreatBeforeAnyShotIsNoShootout()
        {
            var (head, line) = Headlines.For(NewWorld(Mission()), true);
            Assert.Contains("Unruhe", head);
            Assert.DoesNotContain("Gefecht", line);
        }

        [Fact]
        public void DeadGuardsAreCountedButDogsAreNot()
        {
            var m = Mission();
            m.Guards = new()
            {
                new UnitData { Name = "W", Sprite = "Wachmann", X = 5.5, Y = 5.5 },
                new UnitData { Name = "W", Sprite = "Wachmann", X = 7.5, Y = 5.5 },
                new UnitData { Name = "H", Sprite = "Hund", Type = "hund", X = 9.5, Y = 5.5 },
            };
            var w = Lost(m);
            w.Units[1].Hp = 0;
            w.Units[2].Hp = 0;
            w.Units[3].Hp = 0;
            Assert.Contains("2 Wachleute", Headlines.For(w, false).Line);
        }

        [Fact]
        public void DeadRunnerIsNamedApart()
        {
            var m = Mission();
            m.Guards = new() { new UnitData { Name = "L", Sprite = "Laufbursche", Type = "laufbursche", X = 5.5, Y = 5.5 } };
            var w = Lost(m);
            w.Units[1].Hp = 0;
            var line = Headlines.For(w, false).Line;
            Assert.EndsWith("schweigt. Ein Laufbursche, vierzehn Jahre alt, ist unter den Toten.", line);
            Assert.DoesNotContain("Verletzt wurde niemand", line);
        }

        [Fact]
        public void MissionTextsReplaceTheGeneralOnes()
        {
            var m = Mission();
            m.Headlines = new HeadlineData
            {
                Unseen = new HeadlineText { Head = "{item} weg", Line = "Keiner sah was.{loot}{paid}" },
                Lost = new HeadlineText { Head = "Verloren", Line = "Schade.{dead}" },
            };
            Assert.Equal(("Buch weg", "Keiner sah was."), Headlines.For(Won(m), false));
            Assert.Equal(("Verloren", "Schade. Verletzt wurde niemand."), Headlines.For(Lost(m), false));
            Assert.Contains("Unruhe", Headlines.For(NewWorld(m), true).Head);
        }

        [Fact]
        public void FirstMissionHasAllItsHeadlinesWithoutLeftovers()
        {
            var json = File.ReadAllText(Path.Combine(RepoRoot(), "data", "mission_01.json"));
            var h = Loader.ParseMission(json).Headlines;
            Assert.NotNull(h);
            foreach (var t in new[] { h!.Unseen, h.Quiet, h.Loud, h.Lost, h.Abandoned, h.Retreat })
            {
                Assert.NotNull(t);
                Assert.False(string.IsNullOrWhiteSpace(t!.Head));
                Assert.False(string.IsNullOrWhiteSpace(t.Line));
            }
            Assert.Contains("Lohnkasse", h.Loot);
        }

        private static World Loud()
        {
            var m = Mission();
            m.Guards = new() { new UnitData { Name = "W", Sprite = "Wachmann", X = 5.5, Y = 0.5 } };
            var w = NewWorld(m);
            for (int i = 0; i < 80; i++)
            {
                w.Step();
            }
            Assert.Equal(MissionRating.Loud, w.Rating);
            return w;
        }

        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "data")))
            {
                dir = dir.Parent;
            }
            return dir!.FullName;
        }
    }
}
