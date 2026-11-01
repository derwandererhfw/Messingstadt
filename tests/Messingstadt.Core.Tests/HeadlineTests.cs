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

        [Fact]
        public void UnseenWinTalksAboutTheVanishedBook()
        {
            var (head, _) = Headlines.For(Won(Mission()), false);
            Assert.Contains("spurlos", head);
        }

        [Fact]
        public void LootIsMentionedWhenTaken()
        {
            var m = Mission();
            m.Loot = new LootData { Name = "Lohnkasse", X = 0.5, Y = 0.5, Value = 300 };
            var (_, line) = Headlines.For(Won(m), false);
            Assert.Contains("Lohnkasse", line);
        }

        [Fact]
        public void DefeatAndAbandonHaveTheirOwnHeadlines()
        {
            var lost = NewWorld(Mission());
            lost.Units[0].Hp = 0;
            lost.Step();
            Assert.Equal(MissionState.Lost, lost.State);
            Assert.Contains("zerschlagen", Headlines.For(lost, false).Head);
            Assert.Contains("flüchtig", Headlines.For(NewWorld(Mission()), true).Head);
        }

        [Fact]
        public void DeadGuardsAreCounted()
        {
            var m = Mission();
            m.Guards = new()
            {
                new UnitData { Name = "W", Sprite = "Wachmann", X = 5.5, Y = 5.5 },
                new UnitData { Name = "W", Sprite = "Wachmann", X = 7.5, Y = 5.5 },
            };
            var w = NewWorld(m);
            w.Units[1].Hp = 0;
            w.Units[2].Hp = 0;
            Assert.Contains("2 Wachleute", Headlines.For(w, true).Line);
        }
    }
}
