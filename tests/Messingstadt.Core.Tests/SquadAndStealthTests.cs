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
