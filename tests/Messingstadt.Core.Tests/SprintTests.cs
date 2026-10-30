using System.Collections.Generic;
using Messingstadt.Core;
using Xunit;

namespace Messingstadt.Core.Tests
{
    public class SprintTests
    {
        private static MapData OpenMap() => new() { Width = 60, Height = 12 };

        private static UnitData Agent(double x, double y, int legs = 1, int heart = 1) =>
            new() { Name = "A", Sprite = "Agentin", X = x, Y = y, Legs = legs, Heart = heart };

        private static UnitData Guard(double x, double y, double fx, double fy) =>
            new() { Name = "W", Sprite = "Wachmann", X = x, Y = y, Facing = new[] { fx, fy } };

        private static MissionData Mission(List<UnitData> agents, List<UnitData> guards) => new()
        {
            Seed = 7, Agents = agents, Guards = guards, Item = new[] { 0.5, 0.5 }, ItemName = "Buch",
            Exit = new ZoneData { X0 = 58, Y0 = 10, X1 = 60, Y1 = 12 },
        };

        private static void Run(World w, int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                w.Step();
            }
        }

        private static int DistanceAfter(int legs, int heart, bool sprint, int ticks)
        {
            var w = new World(OpenMap(), Mission(new() { Agent(0.5, 5.5, legs, heart) }, new()));
            w.OrderMove(0, 59, 5, sprint);
            Run(w, ticks);
            return w.Units[0].X;
        }

        [Fact]
        public void SprintingIsFasterThanWalking()
        {
            Assert.True(DistanceAfter(1, 1, true, 40) > DistanceAfter(1, 1, false, 40));
        }

        [Fact]
        public void StrongerLegsRunFaster()
        {
            Assert.True(DistanceAfter(2, 1, true, 40) > DistanceAfter(1, 1, true, 40));
            Assert.True(DistanceAfter(3, 1, true, 40) > DistanceAfter(2, 1, true, 40));
        }

        [Fact]
        public void LegsDoNotChangeWalkingSpeed()
        {
            Assert.Equal(DistanceAfter(1, 1, false, 40), DistanceAfter(3, 3, false, 40));
        }

        [Fact]
        public void StrongerHeartRunsLonger()
        {
            int SprintTicks(int heart)
            {
                var w = new World(OpenMap(), Mission(new() { Agent(0.5, 5.5, 1, heart) }, new()));
                w.OrderMove(0, 59, 5, true);
                int t = 0;
                while (w.Units[0].Sprinting && t < 1000)
                {
                    w.Step();
                    t++;
                }
                return t;
            }
            Assert.True(SprintTicks(2) > SprintTicks(1));
            Assert.True(SprintTicks(3) > SprintTicks(2));
        }

        [Fact]
        public void StrongerHeartRecoversFaster()
        {
            int Recovered(int heart)
            {
                var w = new World(OpenMap(), Mission(new() { Agent(0.5, 5.5, 1, heart) }, new()));
                w.Units[0].Stamina = 0;
                Run(w, 60);
                return w.Units[0].Stamina;
            }
            Assert.True(Recovered(3) > Recovered(2));
            Assert.True(Recovered(2) > Recovered(1));
        }

        [Fact]
        public void ExhaustedAgentWalksOnAndCannotSprintAtOnce()
        {
            var w = new World(OpenMap(), Mission(new() { Agent(0.5, 5.5) }, new()));
            w.OrderMove(0, 59, 5, true);
            while (w.Units[0].Sprinting)
            {
                w.Step();
            }
            Assert.True(w.Units[0].Path.Count > 0, "Agent muss weitergehen");
            int before = w.Units[0].X;
            Run(w, 10);
            Assert.Equal(before + 10 * 120, w.Units[0].X);
            w.OrderMove(0, 59, 6, true);
            Assert.False(w.Units[0].Sprinting);
        }

        [Fact]
        public void SprintEndsOnArrival()
        {
            var w = new World(OpenMap(), Mission(new() { Agent(0.5, 5.5) }, new()));
            w.OrderMove(0, 2, 5, true);
            Run(w, 40);
            Assert.False(w.Units[0].Sprinting);
        }

        [Fact]
        public void PlainMoveOrderStopsSprinting()
        {
            var w = new World(OpenMap(), Mission(new() { Agent(0.5, 5.5) }, new()));
            w.OrderMove(0, 30, 5, true);
            Run(w, 5);
            w.OrderMove(0, 30, 5);
            Assert.False(w.Units[0].Sprinting);
        }

        [Fact]
        public void SprintingAgentDoesNotShoot()
        {
            var w = new World(OpenMap(), Mission(new() { Agent(0.5, 5.5, 1, 3) }, new() { Guard(5.5, 8.5, -1, 0) }));
            w.Units[1].Alerted = true;
            w.Units[1].GuardState = GuardState.Combat;
            w.Units[1].Hp = 100000;
            w.Units[0].Hp = 100000;
            w.OrderMove(0, 59, 5, true);
            int shotsByAgent = 0;
            for (int i = 0; i < 40 && w.Units[0].Sprinting; i++)
            {
                w.Step();
                shotsByAgent += w.Units[0].LastFiredTick == w.Tick ? 1 : 0;
            }
            Assert.Equal(0, shotsByAgent);
        }

        [Fact]
        public void SprintingAgentIsHeardFromFurtherAway()
        {
            World Scene(bool sprint)
            {
                // Die Wache schaut weg, der Agent läuft 3 Felder hinter ihr vorbei.
                var w = new World(OpenMap(), Mission(new() { Agent(5.5, 2.5, 3, 3) }, new() { Guard(10.5, 2.5, 1, 0) }));
                w.Units[1].Patrol.Add((10, 2));
                w.OrderMove(0, 7, 2, sprint);
                Run(w, 25);
                return w;
            }
            Assert.Equal(GuardState.Patrol, Scene(false).Units[1].GuardState);
            Assert.NotEqual(GuardState.Patrol, Scene(true).Units[1].GuardState);
        }

        [Fact]
        public void GroupMoveCanSprint()
        {
            var w = new World(OpenMap(), Mission(new() { Agent(0.5, 5.5), Agent(0.5, 6.5) }, new()));
            w.OrderGroupMove(new[] { 0, 1 }, 30, 5, true);
            Assert.True(w.Units[0].Sprinting);
            Assert.True(w.Units[1].Sprinting);
        }

        [Fact]
        public void SameCommandsGiveSameStateWithSprinting()
        {
            ulong Run2()
            {
                var w = new World(OpenMap(), Mission(new() { Agent(0.5, 5.5, 3, 1) }, new()));
                w.OrderMove(0, 59, 5, true);
                Run(w, 100);
                return w.StateHash();
            }
            Assert.Equal(Run2(), Run2());
        }
    }
}
