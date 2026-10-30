using System.Collections.Generic;
using Messingstadt.Core;
using Xunit;

namespace Messingstadt.Core.Tests
{
    public class SquadAndStealthTests
    {
        private static MapData OpenMap() => new() { Width = 30, Height = 12 };

        private static UnitData Agent(double x, double y) => new() { Name = "A", Sprite = "Agentin", X = x, Y = y };

        private static UnitData Guard(double x, double y, double fx, double fy) =>
            new() { Name = "W", Sprite = "Wachmann", X = x, Y = y, Facing = new[] { fx, fy } };

        private static MissionData Mission(List<UnitData> agents, List<UnitData> guards) => new()
        {
            Seed = 7, Agents = agents, Guards = guards, Item = new[] { 3.5, 0.5 }, ItemName = "Buch",
            Exit = new ZoneData { X0 = 28, Y0 = 10, X1 = 30, Y1 = 12 },
        };

        private static List<UnitData> FourAgents() => new() { Agent(0.5, 0.5), Agent(0.5, 1.5), Agent(0.5, 2.5), Agent(0.5, 3.5) };

        private static void Run(World w, int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                w.Step();
            }
        }

        [Fact]
        public void GroupMoveGivesEveryAgentAnOwnSpotOnScreen()
        {
            var w = new World(OpenMap(), Mission(FourAgents(), new()));
            Assert.True(w.OrderGroupMove(new[] { 0, 1, 2, 3 }, 10, 6));
            Run(w, 200);
            var tiles = new HashSet<(int, int)>();
            var columns = new HashSet<int>();
            for (int i = 0; i < 4; i++)
            {
                Assert.True(tiles.Add((w.Units[i].TileX, w.Units[i].TileY)));
                // Gleiches x - y hieße: Im isometrischen Bild steht die eine Figur genau hinter der anderen.
                Assert.True(columns.Add(w.Units[i].TileX - w.Units[i].TileY));
            }
        }

        [Fact]
        public void GroupMoveToBlockedTileIsRejected()
        {
            var map = OpenMap();
            map.Solids.Add(new SolidData { X0 = 10, Y0 = 6, X1 = 11, Y1 = 7 });
            var w = new World(map, Mission(FourAgents(), new()));
            Assert.False(w.OrderGroupMove(new[] { 0, 1 }, 10, 6));
        }

        [Fact]
        public void GroupMoveAvoidsTileOfAgentOutsideTheGroup()
        {
            var w = new World(OpenMap(), Mission(new() { Agent(10.5, 6.5), Agent(0.5, 0.5) }, new()));
            w.OrderGroupMove(new[] { 1 }, 10, 6);
            Run(w, 200);
            Assert.NotEqual((w.Units[0].TileX, w.Units[0].TileY), (w.Units[1].TileX, w.Units[1].TileY));
        }

        [Fact]
        public void CarrierGetsTheClickedTile()
        {
            var w = new World(OpenMap(), Mission(FourAgents(), new()));
            w.Units[2].CarriesItem = true;
            w.OrderGroupMove(new[] { 0, 1, 2, 3 }, 10, 6);
            Assert.Equal((10, 6), w.Units[2].Path[^1]);
        }

        [Fact]
        public void AgentsOnSameTileSpreadOut()
        {
            var w = new World(OpenMap(), Mission(new() { Agent(5.5, 5.5), Agent(5.5, 5.5), Agent(5.5, 5.5) }, new()));
            Run(w, 60);
            var tiles = new HashSet<(int, int)>();
            foreach (var u in w.Units)
            {
                Assert.True(tiles.Add((u.TileX, u.TileY)));
            }
        }

        [Fact]
        public void AttackingAgentsEndOnDifferentTiles()
        {
            var w = new World(OpenMap(), Mission(new() { Agent(0.5, 5.5), Agent(0.5, 5.5), Agent(0.5, 5.5) }, new() { Guard(20.5, 5.5, 1, 0) }));
            for (int i = 0; i < 3; i++)
            {
                w.OrderAttack(i, 3);
            }
            Run(w, 200);
            var tiles = new HashSet<(int, int)>();
            for (int i = 0; i < 3; i++)
            {
                Assert.True(tiles.Add((w.Units[i].TileX, w.Units[i].TileY)));
            }
        }

        [Fact]
        public void GuardDoesNotSeeAgentBehindHim()
        {
            var w = new World(OpenMap(), Mission(new() { Agent(5.5, 5.5) }, new() { Guard(10.5, 5.5, 1, 0) }));
            w.Units[1].Patrol.Add((10, 5));
            Run(w, 30);
            Assert.Equal(GuardState.Patrol, w.Units[1].GuardState);
        }

        [Fact]
        public void GuardSeesAgentInFront()
        {
            var w = new World(OpenMap(), Mission(new() { Agent(5.5, 5.5) }, new() { Guard(10.5, 5.5, -1, 0) }));
            Run(w, 5);
            Assert.Equal(GuardState.Noticing, w.Units[1].GuardState);
        }

        [Fact]
        public void GuardNoticesAgentCloseBehindHim()
        {
            var w = new World(OpenMap(), Mission(new() { Agent(9.5, 5.5) }, new() { Guard(10.5, 5.5, 1, 0) }));
            Run(w, 5);
            Assert.Equal(GuardState.Noticing, w.Units[1].GuardState);
        }

        [Fact]
        public void PostedGuardLooksAround()
        {
            // Posten ohne Rundgang drehen sich und entdecken den Agenten irgendwann.
            var w = new World(OpenMap(), Mission(new() { Agent(5.5, 5.5) }, new() { Guard(10.5, 5.5, 1, 0) }));
            Run(w, 300);
            Assert.NotEqual(GuardState.Patrol, w.Units[1].GuardState);
        }

        [Fact]
        public void AlertedGuardSeesAllAround()
        {
            var w = new World(OpenMap(), Mission(new() { Agent(5.5, 5.5) }, new() { Guard(10.5, 5.5, 1, 0) }));
            w.Units[1].GuardState = GuardState.Search;
            w.Units[1].SearchUntilTick = 1000;
            w.Units[1].Alerted = true;
            Run(w, 2);
            Assert.NotEqual(GuardState.Search, w.Units[1].GuardState);
        }

        [Fact]
        public void PickingUpTheItemTakesTime()
        {
            var w = new World(OpenMap(), Mission(new() { Agent(3.5, 0.5) }, new()));
            Run(w, World.PickupTicks - 2);
            Assert.False(w.ItemTaken);
            Assert.True(w.ItemProgress > 0);
            Run(w, 4);
            Assert.True(w.ItemTaken);
            Assert.True(w.Units[0].CarriesItem);
        }

        [Fact]
        public void ItemInTileCornerCanBePickedUpFromTileCentre()
        {
            // So liegt das Buch, wenn der Träger nah an einer Feldecke fällt.
            var mission = Mission(new() { Agent(10.5, 8.5) }, new());
            mission.Item = new[] { 10.004, 8.004 };
            var w = new World(OpenMap(), mission);
            Run(w, World.PickupTicks + 2);
            Assert.True(w.ItemTaken);
        }

        [Fact]
        public void StandingAgentTakesNearbyCoverUnderFire()
        {
            var map = OpenMap();
            map.Solids.Add(new SolidData { X0 = 9, Y0 = 3, X1 = 10, Y1 = 4, Opaque = false });
            var w = new World(map, Mission(new() { Agent(8.5, 5.5) }, new() { Guard(14.5, 3.5, -1, 0) }));
            w.Units[1].Alerted = true;
            w.Units[1].GuardState = GuardState.Combat;
            Run(w, 25);
            Assert.True(w.HasCoverFrom(w.Units[0], w.Units[1]), $"Agent steht auf {w.Units[0].TileX},{w.Units[0].TileY}");
        }

        [Fact]
        public void AgentPickingUpTheItemStaysUnderFire()
        {
            var map = OpenMap();
            map.Solids.Add(new SolidData { X0 = 4, Y0 = 2, X1 = 5, Y1 = 3, Opaque = false });
            var w = new World(map, Mission(new() { Agent(3.5, 0.5) }, new() { Guard(8.5, 3.5, -1, 0) }));
            w.Units[1].Alerted = true;
            w.Units[1].GuardState = GuardState.Combat;
            w.Units[0].Hp = 10000;
            Run(w, World.PickupTicks + 2);
            Assert.True(w.ItemTaken);
        }

        [Fact]
        public void CarrierBoardingStaysUnderFire()
        {
            var map = OpenMap();
            map.Solids.Add(new SolidData { X0 = 27, Y0 = 9, X1 = 28, Y1 = 10, Opaque = false });
            var w = new World(map, Mission(new() { Agent(28.5, 10.5) }, new() { Guard(23.5, 8.5, 1, 0) }));
            w.Units[0].CarriesItem = true;
            w.Units[0].Hp = 10000;
            w.Units[1].Alerted = true;
            w.Units[1].GuardState = GuardState.Combat;
            Run(w, World.EscapeTicks + 2);
            Assert.Equal(MissionState.Won, w.State);
        }

        [Fact]
        public void StandingSquadDoesNotScatterUnderFire()
        {
            var map = new MapData { Width = 30, Height = 30 };
            for (int y = 2; y < 28; y += 3)
            {
                for (int x = 2; x < 28; x += 3)
                {
                    map.Solids.Add(new SolidData { X0 = x, Y0 = y, X1 = x + 1, Y1 = y + 1, Opaque = false });
                }
            }
            var w = new World(map, new MissionData
            {
                Seed = 7, Item = new[] { 0.5, 0.5 }, ItemName = "Buch", Exit = new ZoneData { X0 = 28, Y0 = 28, X1 = 30, Y1 = 30 },
                Agents = new() { Agent(14.5, 14.5) },
                Guards = new() { Guard(8.5, 14.5, 1, 0), Guard(20.5, 14.5, -1, 0), Guard(14.5, 8.5, 0, 1), Guard(14.5, 20.5, 0, -1) },
            });
            w.Units[0].Hp = 100000;
            for (int i = 0; i < 400; i++)
            {
                w.Step();
                Assert.InRange(w.Units[0].TileX, 12, 16);
                Assert.InRange(w.Units[0].TileY, 12, 16);
            }
        }

        [Fact]
        public void AgentOrderedToWalkDoesNotStopForCover()
        {
            var map = OpenMap();
            map.Solids.Add(new SolidData { X0 = 9, Y0 = 3, X1 = 10, Y1 = 4, Opaque = false });
            var w = new World(map, Mission(new() { Agent(8.5, 5.5) }, new() { Guard(14.5, 3.5, -1, 0) }));
            w.Units[1].Alerted = true;
            w.Units[1].GuardState = GuardState.Combat;
            w.OrderMove(0, 8, 11);
            Run(w, 40);
            Assert.Equal(8, w.Units[0].TileX);
            Assert.True(w.Units[0].TileY >= 8);
        }

        [Fact]
        public void ShooterRemembersAimWhileWalkingAway()
        {
            var w = new World(OpenMap(), Mission(new() { Agent(5.5, 5.5) }, new() { Guard(10.5, 5.5, -1, 0) }));
            w.Units[0].Hp = 100000;
            w.Units[1].Hp = 100000;
            w.OrderAttack(0, 1);
            Run(w, 2);
            Assert.Equal(w.Tick - 1, w.Units[0].LastFiredTick);
            Assert.True(w.Units[0].AimX > 0);
            w.OrderMove(0, 0, 5);
            Run(w, 3);
            Assert.True(w.Units[0].FacingX < 0, "Geht nach links");
            Assert.True(w.Units[0].AimX > 0, "Die Waffe zeigt weiter auf die Wache");
        }

        [Fact]
        public void BoardingTheCarTakesTime()
        {
            var w = new World(OpenMap(), Mission(new() { Agent(28.5, 10.5) }, new()));
            w.Units[0].CarriesItem = true;
            Run(w, World.EscapeTicks - 2);
            Assert.Equal(MissionState.Running, w.State);
            Assert.True(w.EscapeProgress > 0);
            Run(w, 4);
            Assert.Equal(MissionState.Won, w.State);
        }

        [Fact]
        public void WalkingThroughTheExitDoesNotWin()
        {
            var w = new World(OpenMap(), Mission(new() { Agent(27.5, 10.5) }, new()));
            w.Units[0].CarriesItem = true;
            w.OrderMove(0, 25, 10);
            w.Units[0].Path.Insert(0, (28, 10));
            Run(w, 60);
            Assert.Equal(MissionState.Running, w.State);
            Assert.Equal(0, w.EscapeProgress);
        }

        [Fact]
        public void WalkingAwayResetsPickup()
        {
            var w = new World(OpenMap(), Mission(new() { Agent(3.5, 0.5) }, new()));
            Run(w, 30);
            w.OrderMove(0, 10, 0);
            Run(w, 2);
            Assert.Equal(0, w.ItemProgress);
            Run(w, 200);
            Assert.False(w.ItemTaken);
        }

        [Fact]
        public void UnitsKeepTheirSpeedAcrossTiles()
        {
            var w = new World(OpenMap(), Mission(new() { Agent(0.5, 8.5) }, new()));
            w.OrderMove(0, 25, 8);
            Run(w, 100);
            Assert.Equal(500 + 100 * 120, w.Units[0].X);
        }

        [Fact]
        public void CarrierIsSlowerThanAlertedGuard()
        {
            int Distance(bool carrying)
            {
                var w = new World(OpenMap(), Mission(new() { Agent(0.5, 8.5) }, new()));
                w.Units[0].CarriesItem = carrying;
                w.OrderMove(0, 25, 8);
                Run(w, 40);
                return w.Units[0].X;
            }
            Assert.True(Distance(true) < Distance(false));
            var guard = new World(OpenMap(), Mission(new() { Agent(29.5, 0.5) }, new() { Guard(0.5, 8.5, 1, 0) }));
            guard.Units[1].Alerted = true;
            guard.OrderMove(1, 25, 8);
            Run(guard, 40);
            Assert.True(Distance(true) < guard.Units[1].X);
        }
    }
}
