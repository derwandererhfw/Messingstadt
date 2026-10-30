using System.Collections.Generic;
using Messingstadt.Core;
using Xunit;

namespace Messingstadt.Core.Tests
{
    public class ReinforcementTests
    {
        private static MapData OpenMap() => new() { Width = 30, Height = 12 };

        private static UnitData Agent(double x, double y) => new() { Name = "A", Sprite = "Agentin", X = x, Y = y };

        private static UnitData Guard(double x, double y) =>
            new() { Name = "W", Sprite = "Wachmann", X = x, Y = y, Facing = new[] { -1.0, 0.0 } };

        private static MissionData Mission(List<UnitData> agents, List<UnitData> guards) => new()
        {
            Seed = 7, Agents = agents, Guards = guards, Item = new[] { 3.5, 0.5 }, ItemName = "Buch",
            Exit = new ZoneData { X0 = 0, Y0 = 10, X1 = 2, Y1 = 12 },
            Reinforcements = new ReinforcementData { X = 28.5, Y = 5.5, Sprite = "Wachmann", FirstDelay = 5, Interval = 3, Count = 2 },
        };

        private static void Run(World w, int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                w.Step();
            }
        }

        private static int Guards(World w) => w.Units.FindAll(u => u.Faction == Faction.Guard).Count;

        [Fact]
        public void QuietMissionGetsNoReinforcements()
        {
            var w = new World(OpenMap(), Mission(new() { Agent(0.5, 0.5) }, new() { Guard(25.5, 5.5) }));
            Run(w, 20 * 60);
            Assert.Equal(1, Guards(w));
            Assert.Equal(-1, w.TicksUntilReinforcement);
        }

        [Fact]
        public void FirstShotStartsTheClockAndGuardsArriveInWaves()
        {
            var w = new World(OpenMap(), Mission(new() { Agent(20.5, 5.5) }, new() { Guard(25.5, 5.5) }));
            w.OrderAttack(0, 1);
            Run(w, 5);
            Assert.True(w.TicksUntilReinforcement > 0);
            Assert.Equal(1, Guards(w));
            Run(w, 20 * 5 + 1);
            Assert.Equal(2, Guards(w));
            Run(w, 20 * 3);
            Assert.Equal(3, Guards(w));
            Run(w, 20 * 30);
            Assert.Equal(3, Guards(w));
            Assert.Equal(-1, w.TicksUntilReinforcement);
        }

        [Fact]
        public void ReinforcementSecuresTheExitAndIsAlert()
        {
            var w = new World(OpenMap(), Mission(new() { Agent(5.5, 5.5) }, new() { Guard(10.5, 5.5) }));
            w.OrderAttack(0, 1);
            Run(w, 20 * 5 + 5);
            var r = w.Units[2];
            Assert.Equal(Faction.Guard, r.Faction);
            Assert.True(r.Alerted);
            Assert.Equal(GuardState.Investigate, r.GuardState);
            var (x, y) = r.Path[^1];
            Assert.Equal(0, x);
            Assert.InRange(y, 10, 11);
        }

        [Fact]
        public void SameCommandsGiveSameStateWithReinforcements()
        {
            ulong Run2()
            {
                var w = new World(OpenMap(), Mission(new() { Agent(5.5, 5.5) }, new() { Guard(10.5, 5.5) }));
                w.OrderAttack(0, 1);
                Run(w, 20 * 15);
                return w.StateHash();
            }
            Assert.Equal(Run2(), Run2());
        }

        [Fact]
        public void UnnoticedRunIsRatedUnseen()
        {
            var w = new World(OpenMap(), Mission(new() { Agent(0.5, 0.5) }, new() { Guard(25.5, 5.5) }));
            Run(w, 20);
            Assert.Equal(MissionRating.Unseen, w.Rating);
        }

        [Fact]
        public void BeingNoticedWithoutShotsIsRatedQuiet()
        {
            var w = new World(OpenMap(), Mission(new() { Agent(5.5, 5.5) }, new() { Guard(10.5, 5.5) }));
            Run(w, 3);
            Assert.Equal(GuardState.Noticing, w.Units[1].GuardState);
            Assert.Equal(MissionRating.Quiet, w.Rating);
        }

        [Fact]
        public void AnyShotIsRatedLoud()
        {
            var w = new World(OpenMap(), Mission(new() { Agent(20.5, 5.5) }, new() { Guard(25.5, 5.5) }));
            w.OrderAttack(0, 1);
            Run(w, 3);
            Assert.Equal(MissionRating.Loud, w.Rating);
        }
    }
}
