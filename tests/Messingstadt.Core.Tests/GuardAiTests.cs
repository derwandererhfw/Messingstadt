using System.Collections.Generic;
using Messingstadt.Core;
using Xunit;

namespace Messingstadt.Core.Tests
{
    public class GuardAiTests
    {
        private static MapData OpenMap() => new() { Width = 30, Height = 10 };

        private static UnitData Agent(double x, double y) => new() { Name = "A", Sprite = "Agentin", X = x, Y = y };

        private static UnitData Guard(double x, double y) => new() { Name = "W", Sprite = "Wachmann", X = x, Y = y, Facing = new[] { -1.0, 0.0 } };

        private static MissionData Mission(List<UnitData> agents, List<UnitData> guards) => new()
        {
            Seed = 7, Agents = agents, Guards = guards, Item = new[] { 0.5, 0.5 }, ItemName = "Buch",
            Exit = new ZoneData { X0 = 28, Y0 = 8, X1 = 30, Y1 = 10 },
        };

        [Fact]
        public void GuardNeedsTimeToNotice()
        {
            var w = new World(OpenMap(), Mission(new() { Agent(0.5, 5.5) }, new() { Guard(5.5, 5.5) }));
            for (int i = 0; i < 10; i++)
            {
                w.Step();
            }
            Assert.Equal(GuardState.Noticing, w.Units[1].GuardState);
            Assert.Empty(w.Shots);
            for (int i = 0; i < 40; i++)
            {
                w.Step();
            }
            Assert.Equal(GuardState.Combat, w.Units[1].GuardState);
            Assert.NotEmpty(w.Shots);
        }

        [Fact]
        public void FarGuardNoticesLaterThanNearGuard()
        {
            int Ticks(double guardX)
            {
                var w = new World(OpenMap(), Mission(new() { Agent(0.5, 5.5) }, new() { Guard(guardX, 5.5) }));
                int t = 0;
                while (w.Units[1].GuardState != GuardState.Combat && t < 200)
                {
                    w.Step();
                    t++;
                }
                return t;
            }
            Assert.True(Ticks(2.5) < Ticks(5.0));
        }

        [Fact]
        public void AlarmReachesNearGuardFirstAndStopsAtRadius()
        {
            var w = new World(OpenMap(), Mission(new() { Agent(29.5, 0.5) }, new() { Guard(3.5, 5.5), Guard(7.5, 5.5), Guard(12.5, 5.5) }));
            w.RaiseAlarm(500, 5500);
            bool nearFirst = false;
            for (int i = 0; i < 40; i++)
            {
                w.Step();
                nearFirst |= w.Units[1].Alerted && !w.Units[2].Alerted;
            }
            Assert.True(nearFirst);
            Assert.True(w.Units[2].Alerted);
            Assert.False(w.Units[3].Alerted);
        }

        [Fact]
        public void AlarmedGuardGoesToNoiseNotToHiddenAgent()
        {
            var w = new World(OpenMap(), Mission(new() { Agent(29.5, 0.5) }, new() { Guard(3.5, 5.5) }));
            w.RaiseAlarm(500, 5500);
            for (int i = 0; i < 60; i++)
            {
                w.Step();
            }
            Assert.True(w.Units[1].TileX <= 3, "Wache darf den versteckten Agenten nicht kennen");
        }

        [Fact]
        public void GuardSearchesLastKnownPositionAndReturnsToPatrol()
        {
            var map = OpenMap();
            map.Solids.Add(new SolidData { X0 = 8, Y0 = 0, X1 = 9, Y1 = 10 });
            var w = new World(map, Mission(new() { Agent(6.5, 5.5) }, new() { Guard(2.5, 5.5) }));
            while (w.Units[1].GuardState != GuardState.Combat)
            {
                w.Step();
            }
            // Agent verschwindet hinter der durchgehenden Wand.
            w.Units[0].X = 12500;
            w.Units[0].Y = 2500;
            bool investigated = false;
            for (int i = 0; i < 1000 && w.Units[1].GuardState != GuardState.Patrol; i++)
            {
                w.Step();
                investigated |= w.Units[1].GuardState == GuardState.Investigate;
                Assert.True(w.Units[1].TileX < 8);
            }
            Assert.True(investigated);
            Assert.Equal(GuardState.Patrol, w.Units[1].GuardState);
            Assert.False(w.Units[1].Alerted);
        }

        [Fact]
        public void GuardsTakeDifferentPositions()
        {
            var w = new World(OpenMap(), Mission(new() { Agent(0.5, 5.5) }, new() { Guard(5.5, 5.5), Guard(5.5, 5.5), Guard(5.5, 5.5) }));
            for (int i = 0; i < 90; i++)
            {
                w.Step();
            }
            var tiles = new HashSet<(int, int)>();
            foreach (var g in w.Units)
            {
                if (g.Faction == Faction.Guard && g.Alive)
                {
                    Assert.True(tiles.Add((g.TileX, g.TileY)), "Zwei Wachen stehen auf demselben Feld");
                }
            }
            Assert.True(tiles.Count >= 2);
        }

        [Fact]
        public void CoverLowersHitChance()
        {
            var open = new World(OpenMap(), Mission(new() { Agent(5.5, 5.5) }, new() { Guard(1.5, 5.5) }));
            var map = OpenMap();
            map.Solids.Add(new SolidData { X0 = 4, Y0 = 5, X1 = 5, Y1 = 6, Opaque = false });
            var covered = new World(map, Mission(new() { Agent(5.5, 5.5) }, new() { Guard(1.5, 5.5) }));
            Assert.Equal(open.HitChance(open.Units[1], open.Units[0]) - 25, covered.HitChance(covered.Units[1], covered.Units[0]));
        }

        [Fact]
        public void CoverOnlyCountsOnTheShootersSide()
        {
            var map = OpenMap();
            map.Solids.Add(new SolidData { X0 = 6, Y0 = 5, X1 = 7, Y1 = 6, Opaque = false });
            var w = new World(map, Mission(new() { Agent(5.5, 5.5) }, new() { Guard(1.5, 5.5) }));
            var open = new World(OpenMap(), Mission(new() { Agent(5.5, 5.5) }, new() { Guard(1.5, 5.5) }));
            Assert.Equal(open.HitChance(open.Units[1], open.Units[0]), w.HitChance(w.Units[1], w.Units[0]));
        }

        [Fact]
        public void WalkingShooterHitsWorse()
        {
            var w = new World(OpenMap(), Mission(new() { Agent(5.5, 5.5) }, new() { Guard(1.5, 5.5) }));
            int standing = w.HitChance(w.Units[1], w.Units[0]);
            w.Units[1].Moving = true;
            Assert.Equal(standing - 15, w.HitChance(w.Units[1], w.Units[0]));
        }

        [Fact]
        public void LineOfSightIsTheSameInBothDirections()
        {
            var map = new MapData { Width = 20, Height = 20 };
            map.Solids.Add(new SolidData { X0 = 8, Y0 = 6, X1 = 9, Y1 = 8 });
            map.Solids.Add(new SolidData { X0 = 4, Y0 = 12, X1 = 7, Y1 = 13 });
            map.Solids.Add(new SolidData { X0 = 12, Y0 = 3, X1 = 15, Y1 = 5 });
            var m = map.Build();
            for (int ax = 0; ax < 20; ax++)
            {
                for (int ay = 0; ay < 20; ay++)
                {
                    for (int bx = 0; bx < 20; bx++)
                    {
                        for (int by = 0; by < 20; by++)
                        {
                            Assert.True(
                                m.HasLineOfSight(ax, ay, bx, by) == m.HasLineOfSight(bx, by, ax, ay),
                                $"Sicht ({ax},{ay}) nach ({bx},{by}) hängt von der Richtung ab");
                        }
                    }
                }
            }
        }

        [Fact]
        public void AttackedGuardFightsBackImmediately()
        {
            var w = new World(OpenMap(), Mission(new() { Agent(0.5, 5.5) }, new() { Guard(5.5, 5.5) }));
            w.OrderAttack(0, 1);
            for (int i = 0; i < 8 && w.Shots.Count == 0; i++)
            {
                w.Step();
            }
            Assert.NotEmpty(w.Shots);
            Assert.Equal(GuardState.Combat, w.Units[1].GuardState);
        }
    }
}
