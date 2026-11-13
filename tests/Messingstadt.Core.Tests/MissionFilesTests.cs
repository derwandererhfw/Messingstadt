using System.Collections.Generic;
using System.IO;
using System.Linq;
using Messingstadt.Core;
using Xunit;

namespace Messingstadt.Core.Tests
{
    /// <summary>Jeder Einsatz, den der Stadtplan nennt, muss sich laden lassen und lösbar gebaut sein.</summary>
    public class MissionFilesTests
    {
        private static string DataDir()
        {
            var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "data", "stadtplan.json")))
            {
                dir = dir.Parent;
            }
            return Path.Combine(dir!.FullName, "data");
        }

        public static IEnumerable<object[]> Missions() =>
            CityMap.Parse(File.ReadAllText(Path.Combine(DataDir(), "stadtplan.json"))).Districts
                .Where(d => d.Mission.Length > 0).Select(d => new object[] { d.Mission });

        private static (MapData Map, MissionData Mission) Load(string id) =>
            Loader.LoadMission(DataDir(), id, Loader.ParseCatalog(File.ReadAllText(Path.Combine(DataDir(), "waffen.json"))));

        /// <summary>Jeder Bewohner des Nordwalls steht so, dass er jemanden auf dem Feld vor sich sieht und dann ruft.</summary>
        [Fact]
        public void EveryResidentOfNordwallCanSeeTheStreetInFrontOfHim()
        {
            var ids = Load("mission_02").Mission.Residents.Select(r => r.Id).ToList();
            Assert.Equal(new[] { "gerichtsdiener", "frau_des_wachmanns", "kasernenkoch" }, ids);
            foreach (var id in ids)
            {
                var (map, mission) = Load("mission_02");
                var r = mission.Residents.First(x => x.Id == id);
                var grid = new World(map, Load("mission_02").Mission).Map;
                var spot = (from dx in new[] { 0, -1, 1 } from dy in new[] { 0, 1, -1 }
                            let x = (int)r.X + dx let y = (int)r.Y + dy
                            where !grid.IsBlocked(x, y) select (x, y)).First();
                mission.Stance = CityStance.Silent;
                mission.Agents[0].X = spot.x + 0.5;
                mission.Agents[0].Y = spot.y + 0.5;
                var w = new World(map, mission);
                w.Step();
                Assert.Contains(w.ResidentCalls, c => c.Resident == id);
            }
        }

        [Fact]
        public void TheCityPlanNamesBothMissions()
        {
            var ids = Missions().Select(m => (string)m[0]).ToList();
            Assert.Contains("mission_01", ids);
            Assert.Contains("mission_02", ids);
        }

        [Theory]
        [MemberData(nameof(Missions))]
        public void MissionLoadsWithMapAndLight(string id)
        {
            var (map, mission) = Load(id);
            Assert.True(map.Width > 0 && map.Height > 0);
            Assert.NotNull(mission.Light);
            Assert.Equal(mission.Light!.Size * mission.Light.Size, mission.Light.Levels.Length);
            Assert.False(string.IsNullOrWhiteSpace(mission.Title));
            var w = new World(map, mission);
            Assert.Equal(mission.Agents.Count + mission.Guards.Count + mission.Prisoners.Count, w.Units.Count);
        }

        [Theory]
        [MemberData(nameof(Missions))]
        public void NobodyStartsInsideAWallAndEveryRoundCanBeWalked(string id)
        {
            var (map, mission) = Load(id);
            var w = new World(map, mission);
            foreach (var u in w.Units.Where(u => !u.IsPrisoner))
            {
                Assert.False(w.Map.IsBlocked(u.TileX, u.TileY), $"{u.Name} steht in einem gesperrten Feld");
                foreach (var (x, y) in u.Patrol)
                {
                    Assert.NotNull(w.Map.FindPath(u.TileX, u.TileY, x, y));
                }
            }
        }

        private static bool ReachesExit(World w, Unit from)
        {
            for (int y = w.Exit.Y0 / World.TileUnits; y < w.Exit.Y1 / World.TileUnits; y++)
            {
                for (int x = w.Exit.X0 / World.TileUnits; x < w.Exit.X1 / World.TileUnits; x++)
                {
                    if (!w.Map.IsBlocked(x, y) && w.Map.FindPath(from.TileX, from.TileY, x, y) != null)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        [Theory]
        [MemberData(nameof(Missions))]
        public void EveryDoorCanBeReachedAndTheWayOutIsOpen(string id)
        {
            var (map, mission) = Load(id);
            var w = new World(map, mission);
            Assert.True(ReachesExit(w, w.Units[0]));
            // Der Schlosser kommt an jede Tür, auch wenn dafür erst eine andere offen sein muss.
            int locksmith = w.Units.FindIndex(u => u.Skill == World.SkillLocksmith);
            var closed = Enumerable.Range(0, w.Doors.Count).ToList();
            while (closed.Count > 0)
            {
                int next = closed.FindIndex(d => w.OrderOpenDoor(locksmith, d));
                Assert.True(next >= 0, "Nicht erreichbar: " + string.Join(", ", closed.Select(d => w.Doors[d].Name)));
                w.Doors[closed[next]].Open = true;
                foreach (var (x, y) in w.Doors[closed[next]].Tiles)
                {
                    w.Map.Unblock(x, y);
                }
                closed.RemoveAt(next);
            }
            // Sind die Türen offen, führt von jedem Gefangenen ein Weg zum Wagen.
            Assert.All(w.Prisoners, p => Assert.True(ReachesExit(w, p), p.Name));
        }

        [Fact]
        public void MissionTwoIsARescueWithKeysAtTheKeeper()
        {
            var (map, mission) = Load("mission_02");
            var w = new World(map, mission);
            Assert.True(w.IsRescue);
            Assert.Equal(new[] { "Anton Weiß", "Paul Roth" }, w.Prisoners.Select(p => p.Name));
            var keeper = w.Units.First(u => u.Name == "Schließer");
            Assert.Equal(2, keeper.Keys.Count);
            Assert.All(keeper.Keys, k => Assert.StartsWith("Zelle", w.Doors[k].Name));
            // Die Verstärkung ist Stadtwache aus der Kaserne. Der Wachhabende ruft sie vom Apparat an seinem Posten, drei Betten.
            Assert.Equal("", mission.Reinforcements!.Type);
            Assert.Equal("Wachhabender", mission.Reinforcements.Phone);
            Assert.Equal(3, mission.Reinforcements.Beds);
            Assert.Contains(mission.Doors, d => d.Wire && d.Name == "Telefondraht");
            Assert.Equal("nordwall", mission.Scene);
            Assert.NotEmpty(mission.Chatter!.Guards);
        }

        /// <summary>Stellt den Agenten zwei Felder hinter die Wache und besticht sie. Das Anschleichen ist Sache der Messung, hier zählt die Kette der Regeln.</summary>
        private static void BribeFromBehind(World w, string guard)
        {
            var g = w.Units.First(u => u.Name == guard);
            g.Path.Clear();
            (g.FacingX, g.FacingY) = (1000, 0);
            (w.Units[0].X, w.Units[0].Y) = (g.X - 1500, g.Y);
            w.Units[0].Path.Clear();
            Assert.True(w.OrderBribe(0, g.Id), guard);
            for (int i = 0; i < 20 && !g.Bribed; i++)
            {
                w.Step();
            }
            Assert.True(g.Bribed, guard);
        }

        private static void OpenAndWait(World w, string door)
        {
            int id = w.Doors.FindIndex(d => d.Name == door);
            Assert.True(w.OrderOpenDoor(0, id), door);
            for (int i = 0; i < 600 && (!w.Doors[id].Open || w.Prisoners.Any(p => p.CellDoor == id && p.Captive)); i++)
            {
                w.Step();
            }
            Assert.True(w.Doors[id].Open, door);
        }

        [Fact]
        public void MissionTwoCanBeSolvedWithMoneyAlone()
        {
            // Ohne Schlosser und Dietrich: Emil kauft den Türposten und bekommt den Schlüssel zur Pforte,
            // kauft im Hof den Schließer, schließt beide Zellen auf und führt beide zum Wagen.
            // Die übrigen Wachen fehlen, hier geht es um den Weg und die Regeln.
            var (map, mission) = Load("mission_02");
            mission.Agents.RemoveAll(a => a.Skill != World.SkillBookkeeper);
            mission.Guards.RemoveAll(g => g.Name != "Schließer" && g.Name != "Türposten");
            mission.Reinforcements!.Phone = "";
            mission.Money = 400;
            var w = new World(map, mission);
            Assert.False(w.OrderOpenDoor(0, 0));
            BribeFromBehind(w, "Türposten");
            OpenAndWait(w, "Pforte zur Gerichtsgasse");
            w.OrderMove(0, 8, 8);
            for (int i = 0; i < 200 && w.Units[0].Path.Count > 0; i++)
            {
                w.Step();
            }
            BribeFromBehind(w, "Schließer");
            Assert.Equal(0, w.Money);
            OpenAndWait(w, "Zelle 1");
            OpenAndWait(w, "Zelle 2");
            Assert.All(w.Prisoners, p => Assert.False(p.Captive));
            foreach (var p in w.Prisoners)
            {
                w.OrderPrisoner(p.Id, 0);
                Assert.Equal(0, p.FollowId);
                p.Waiting = false;
            }
            w.OrderMove(0, 28, 23);
            for (int i = 0; i < 3000 && w.State == MissionState.Running; i++)
            {
                w.Step();
            }
            Assert.Equal(MissionState.Won, w.State);
            Assert.NotEqual(MissionRating.Loud, w.Rating);
        }
    }
}
