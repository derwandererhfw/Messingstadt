using System.Collections.Generic;
using System.IO;
using Messingstadt.Core;
using Xunit;

namespace Messingstadt.Core.Tests
{
    public class MapTests
    {
        private static GameMap WallMap()
        {
            var m = new GameMap(10, 10);
            for (int y = 0; y < 9; y++)
            {
                m.Block(5, y, true);
            }
            return m;
        }

        [Fact]
        public void PathGoesAroundWall()
        {
            var path = WallMap().FindPath(0, 0, 9, 0);
            Assert.NotNull(path);
            Assert.Contains(path!, p => p == (5, 9));
            Assert.Equal((9, 0), path![^1]);
        }

        [Fact]
        public void NoPathToBlockedTile() => Assert.Null(WallMap().FindPath(0, 0, 5, 3));

        [Fact]
        public void NoCornerCutting()
        {
            var m = new GameMap(3, 3);
            m.Block(1, 0, true);
            m.Block(0, 1, true);
            Assert.Null(m.FindPath(0, 0, 1, 1));
        }

        [Fact]
        public void WallBlocksSight()
        {
            var m = WallMap();
            Assert.False(m.HasLineOfSight(2, 3, 8, 3));
            Assert.True(m.HasLineOfSight(2, 9, 8, 9));
        }
    }

    public class WorldTests
    {
        private static MapData OpenMap() => new() { Width = 30, Height = 10 };

        private static UnitData Agent(double x, double y) => new() { Name = "A", Sprite = "Agentin", X = x, Y = y };

        private static UnitData Guard(double x, double y) => new() { Name = "W", Sprite = "Wachmann", X = x, Y = y };

        private static MissionData Mission(List<UnitData> agents, List<UnitData> guards) => new()
        {
            Seed = 7, Agents = agents, Guards = guards, Item = new[] { 3.5, 0.5 }, ItemName = "Buch",
            Exit = new ZoneData { X0 = 0, Y0 = 8, X1 = 2, Y1 = 10 },
        };

        private static string DataPath(string file)
        {
            var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "README.md")))
            {
                dir = dir.Parent;
            }
            return Path.Combine(dir!.FullName, "data", file);
        }

        [Fact]
        public void AgentReachesTarget()
        {
            var w = new World(OpenMap(), Mission(new() { Agent(0.5, 0.5) }, new()));
            Assert.True(w.OrderMove(0, 2, 0));
            for (int i = 0; i < 40; i++)
            {
                w.Step();
            }
            Assert.Equal(2, w.Units[0].TileX);
            Assert.False(w.Units[0].Moving);
        }

        [Fact]
        public void MissionWonWithItemInExit()
        {
            var w = new World(OpenMap(), Mission(new() { Agent(0.5, 0.5) }, new()));
            w.OrderMove(0, 3, 0);
            for (int i = 0; i < 60; i++)
            {
                w.Step();
            }
            Assert.True(w.ItemTaken);
            Assert.True(w.Units[0].CarriesItem);
            w.OrderMove(0, 1, 9);
            for (int i = 0; i < 200 && w.State == MissionState.Running; i++)
            {
                w.Step();
            }
            Assert.Equal(MissionState.Won, w.State);
        }

        [Fact]
        public void ExitWithoutItemDoesNotWin()
        {
            var w = new World(OpenMap(), Mission(new() { Agent(5.5, 5.5) }, new()));
            w.OrderMove(0, 1, 9);
            for (int i = 0; i < 200; i++)
            {
                w.Step();
            }
            Assert.Equal(MissionState.Running, w.State);
        }

        [Fact]
        public void GuardSeesAgentAndFightEnds()
        {
            var w = new World(OpenMap(), Mission(new() { Agent(0.5, 5.5) }, new() { Guard(5.5, 5.5) }));
            for (int i = 0; i < 2000 && w.Units[0].Alive && w.Units[1].Alive; i++)
            {
                w.Step();
            }
            Assert.True(w.Units[1].Alerted);
            Assert.True(!w.Units[0].Alive || !w.Units[1].Alive);
        }

        [Fact]
        public void GuardBehindWallStaysCalm()
        {
            var map = OpenMap();
            map.Solids.Add(new SolidData { X0 = 3, Y0 = 0, X1 = 4, Y1 = 10 });
            var w = new World(map, Mission(new() { Agent(0.5, 5.5) }, new() { Guard(5.5, 5.5) }));
            for (int i = 0; i < 100; i++)
            {
                w.Step();
            }
            Assert.False(w.Units[1].Alerted);
            Assert.Equal(100, w.Units[0].Hp);
        }

        [Fact]
        public void AttackOrderOnDistantGuardApproachesAndShoots()
        {
            // Wache steht weit außerhalb von Sicht und Reichweite.
            var w = new World(OpenMap(), Mission(new() { Agent(0.5, 5.5) }, new() { Guard(25.5, 5.5) }));
            w.OrderAttack(0, 1);
            for (int i = 0; i < 400 && w.Units[1].Hp == w.Units[1].MaxHp; i++)
            {
                w.Step();
            }
            Assert.True(w.Units[1].Hp < w.Units[1].MaxHp, "Agent hat die befohlene Wache nie getroffen");
        }

        [Fact]
        public void MoveOrderCancelsAttackOrder()
        {
            var w = new World(OpenMap(), Mission(new() { Agent(0.5, 5.5) }, new() { Guard(25.5, 5.5) }));
            w.OrderAttack(0, 1);
            w.OrderMove(0, 0, 0);
            Assert.Equal(-1, w.Units[0].TargetId);
        }

        [Fact]
        public void SpottedOnlyWithLineOfSight()
        {
            var map = OpenMap();
            map.Solids.Add(new SolidData { X0 = 3, Y0 = 0, X1 = 4, Y1 = 10 });
            var hidden = new World(map, Mission(new() { Agent(0.5, 5.5) }, new() { Guard(5.5, 5.5) }));
            Assert.False(hidden.IsSpottedByAgents(hidden.Units[1]));
            var open = new World(OpenMap(), Mission(new() { Agent(0.5, 5.5) }, new() { Guard(5.5, 5.5) }));
            Assert.True(open.IsSpottedByAgents(open.Units[1]));
        }

        [Fact]
        public void AllAgentsDeadLosesMission()
        {
            var guards = new List<UnitData> { Guard(3.5, 5.5), Guard(3.5, 4.5), Guard(3.5, 6.5) };
            var w = new World(OpenMap(), Mission(new() { Agent(0.5, 5.5) }, guards));
            for (int i = 0; i < 3000 && w.State == MissionState.Running; i++)
            {
                w.Step();
            }
            Assert.Equal(MissionState.Lost, w.State);
        }

        [Fact]
        public void SameCommandsGiveSameState()
        {
            ulong Run()
            {
                var w = new World(OpenMap(), Mission(new() { Agent(0.5, 5.5), Agent(1.5, 5.5) }, new() { Guard(6.5, 5.5), Guard(7.5, 4.5) }));
                w.OrderMove(0, 4, 2);
                for (int i = 0; i < 400; i++)
                {
                    w.Step();
                }
                return w.StateHash();
            }
            Assert.Equal(Run(), Run());
        }

        [Fact]
        public void RealMissionIsWinnableAndLoads()
        {
            var map = Loader.ParseMap(File.ReadAllText(DataPath("map_01.json")));
            var mission = Loader.ParseMission(File.ReadAllText(DataPath("mission_01.json")));
            var w = new World(map, mission);
            foreach (var u in w.Units)
            {
                Assert.False(w.Map.IsBlocked(u.TileX, u.TileY), $"{u.Name} steht auf gesperrtem Feld");
                foreach (var (x, y) in u.Patrol)
                {
                    Assert.False(w.Map.IsBlocked(x, y), $"Rundgang {u.Name} {x},{y} gesperrt");
                }
            }
            int ix = (int)mission.Item[0], iy = (int)mission.Item[1];
            Assert.NotNull(w.Map.FindPath(10, 18, ix, iy));
            Assert.NotNull(w.Map.FindPath(ix, iy, 18, 11));

            // Niemand darf den Trupp schon beim Start sehen.
            for (int i = 0; i < 20 * 3; i++)
            {
                w.Step();
            }
            foreach (var u in w.Units)
            {
                Assert.False(u.Alerted, $"{u.Name} ist direkt nach dem Start alarmiert");
            }

            // Ablauf: alle Agenten zum Buch, dann zum Ausgang. Der Einsatz muss gewinnbar sein.
            for (int a = 0; a < 4; a++)
            {
                w.OrderMove(a, ix, iy);
            }
            bool toExit = false;
            for (int i = 0; i < 20 * 300 && w.State == MissionState.Running; i++)
            {
                if (w.ItemTaken && !toExit)
                {
                    toExit = true;
                    foreach (var u in w.Units)
                    {
                        if (u.Faction == Faction.Agent && u.Alive)
                        {
                            w.OrderMove(u.Id, 18, 11);
                        }
                    }
                }
                w.Step();
            }
            Assert.Equal(MissionState.Won, w.State);
        }
    }
}
