using System.Collections.Generic;
using System.IO;
using System.Linq;
using Messingstadt.Core;
using Xunit;

namespace Messingstadt.Core.Tests
{
    /// <summary>Auftragsart Sabotieren: Ziele, die Zeit brauchen, die Uhr, Schotter und die Kurierin.</summary>
    public class SabotageTests
    {
        /// <summary>Freies Feld 30 × 12. Hebelbank bei (10, 5), Weiche bei (20, 5), Zielzone ganz im Westen.</summary>
        private static World Make(string skill = "", int timeLimit = 0, List<UnitData>? guards = null, bool gravel = false, bool planks = false, double agentX = 5.5)
        {
            var mission = new MissionData
            {
                Seed = 7, Item = new[] { 10.5, 5.5 }, Exit = new ZoneData { X0 = 0, Y0 = 0, X1 = 2, Y1 = 12 }, TimeLimit = timeLimit,
                Agents = new List<UnitData> { new() { Name = "Otto", Sprite = "Agent", X = agentX, Y = 5.5, Skill = skill } },
                Guards = guards ?? new List<UnitData>(),
                Sabotage = new List<SabotageData>
                {
                    new() { Name = "Stellwerk", X = 10.5, Y = 5.5, Seconds = 6, Locksmith = true },
                    new() { Name = "Weiche", X = 20.5, Y = 5.5, Seconds = 4 },
                },
            };
            if (gravel)
            {
                mission.Gravel.Add(new ZoneData { X0 = 0, Y0 = 0, X1 = 30, Y1 = 12 });
            }
            if (planks)
            {
                mission.Planks.Add(new ZoneData { X0 = 0, Y0 = 0, X1 = 30, Y1 = 12 });
            }
            return new World(new MapData { Width = 30, Height = 12 }, mission);
        }

        private static int StepUntil(World w, System.Func<bool> done, int max = 2000)
        {
            int n = 0;
            while (n < max && !done() && w.State == MissionState.Running)
            {
                w.Step();
                n++;
            }
            return n;
        }

        private static void Arrive(World w, int target)
        {
            Assert.True(w.OrderSabotage(0, target));
            StepUntil(w, () => w.IsSabotaging(w.Units[0]));
            Assert.True(w.IsSabotaging(w.Units[0]));
        }

        [Fact]
        public void ATargetTakesItsTimeAndThenStaysDone()
        {
            var w = Make();
            Assert.True(w.IsSabotage);
            Arrive(w, 0);
            int ticks = StepUntil(w, () => w.Targets[0].Done);
            Assert.InRange(ticks, 6 * World.TickRate - 2, 6 * World.TickRate + 1);
            Assert.True(w.Targets[0].Done);
            Assert.Equal(-1, w.Units[0].SabotageTarget);
            Assert.False(w.OrderSabotage(0, 0));
        }

        [Fact]
        public void TheLocksmithHalvesTheLeverFrameButNotTheSwitch()
        {
            var w = Make(World.SkillLocksmith);
            Arrive(w, 0);
            int lever = StepUntil(w, () => w.Targets[0].Done);
            Assert.InRange(lever, 3 * World.TickRate - 2, 3 * World.TickRate + 1);
            Arrive(w, 1);
            int points = StepUntil(w, () => w.Targets[1].Done);
            Assert.InRange(points, 4 * World.TickRate - 2, 4 * World.TickRate + 1);
        }

        [Fact]
        public void AnyOtherOrderBreaksOffAndTheWorkStartsOver()
        {
            var w = Make();
            Arrive(w, 0);
            for (int i = 0; i < 60; i++)
            {
                w.Step();
            }
            Assert.True(w.Targets[0].Progress > 40);
            Assert.True(w.OrderMove(0, 6, 5));
            Assert.Equal(-1, w.Units[0].SabotageTarget);
            w.Step();
            Assert.Equal(0, w.Targets[0].Progress);
            Assert.False(w.Targets[0].Done);
        }

        [Fact]
        public void WonOnlyWhenEverythingIsDoneAndSomeoneStandsInTheZone()
        {
            var w = Make();
            Arrive(w, 0);
            StepUntil(w, () => w.Targets[0].Done);
            // Ein Ziel reicht nicht: in der Zone zu stehen gewinnt noch nichts.
            w.OrderMove(0, 1, 5);
            StepUntil(w, () => w.Units[0].Path.Count == 0 && !w.Units[0].Moving);
            for (int i = 0; i < 3 * World.EscapeTicks; i++)
            {
                w.Step();
            }
            Assert.Equal(MissionState.Running, w.State);
            Arrive(w, 1);
            StepUntil(w, () => w.AllSabotaged);
            Assert.True(w.AllSabotaged);
            w.OrderMove(0, 1, 5);
            StepUntil(w, () => false);
            Assert.Equal(MissionState.Won, w.State);
        }

        [Fact]
        public void WhenTheClockRunsOutTheMissionIsLost()
        {
            var w = Make(timeLimit: 10);
            Assert.Equal(10 * World.TickRate, w.TicksLeft);
            StepUntil(w, () => false);
            Assert.Equal(MissionState.Lost, w.State);
            Assert.True(w.TimeUp);
            Assert.Equal(10 * World.TickRate, w.Tick);
        }

        [Fact]
        public void TheClockStopsOnceEverythingIsDone()
        {
            var w = Make(World.SkillLocksmith, timeLimit: 40);
            Arrive(w, 0);
            StepUntil(w, () => w.Targets[0].Done);
            Arrive(w, 1);
            StepUntil(w, () => w.AllSabotaged);
            Assert.True(w.AllSabotaged);
            Assert.Equal(-1, w.TicksLeft);
            Assert.Equal("", w.ClockLine());
            for (int i = 0; i < 45 * World.TickRate; i++)
            {
                w.Step();
            }
            Assert.Equal(MissionState.Running, w.State);
            Assert.False(w.TimeUp);
        }

        [Fact]
        public void TheStripNamesTargetsAndTheClock()
        {
            var w = Make(timeLimit: 300);
            Assert.Equal("ZIELE LAHMLEGEN STOP DANN ZUM WAGEN STOP", w.SabotageLine());
            Assert.Equal("ZUG IN 5 MINUTEN STOP", w.ClockLine());
            Arrive(w, 0);
            w.Step();
            Assert.Equal("STELLWERK WIRD LAHMGELEGT STOP STEHEN BLEIBEN STOP", w.SabotageLine());
            StepUntil(w, () => w.Targets[0].Done);
            Assert.Equal("STELLWERK STEHT STOP WEICHE FEHLT NOCH STOP", w.SabotageLine());
            // Gerundet auf die nächste Minute, unter einer Minute in Sekunden.
            StepUntil(w, () => w.TicksLeft <= 150 * World.TickRate, 7000);
            Assert.Equal("ZUG IN 3 MINUTEN STOP", w.ClockLine());
            StepUntil(w, () => w.TicksLeft <= 149 * World.TickRate, 7000);
            Assert.Equal("ZUG IN 2 MINUTEN STOP", w.ClockLine());
            StepUntil(w, () => w.TicksLeft <= 89 * World.TickRate, 7000);
            Assert.Equal("ZUG IN EINER MINUTE STOP", w.ClockLine());
            StepUntil(w, () => w.TicksLeft <= 60 * World.TickRate, 7000);
            Assert.Equal("ZUG IN EINER MINUTE STOP", w.ClockLine());
            StepUntil(w, () => w.TicksLeft <= 59 * World.TickRate, 7000);
            Assert.Equal("ZUG IN 59 SEKUNDEN STOP", w.ClockLine());
            StepUntil(w, () => false, 7000);
            Assert.Equal("ZUG IST DA STOP", w.SabotageLine());
        }

        private static List<UnitData> GuardLookingAway() =>
            new() { new UnitData { Name = "W", Sprite = "Wachmann", X = 10.2, Y = 5.5, Facing = new[] { 1.0, 0.0 } } };

        /// <summary>Der Agent geht 2,7 Felder hinter der Wache vorbei, von Norden nach Süden.</summary>
        private static bool HeardBehind(bool gravel, bool planks)
        {
            var w = Make(guards: GuardLookingAway(), gravel: gravel, planks: planks, agentX: 7.5);
            // Kurz genug, dass der Posten sich in der Zeit nicht umsieht.
            w.Units[0].Y = 3 * World.TileUnits + World.TileUnits / 2;
            Assert.True(w.OrderMove(0, 7, 7));
            StepUntil(w, () => w.Units[0].Path.Count == 0 && !w.Units[0].Moving, 400);
            return w.Rating != MissionRating.Unseen;
        }

        [Fact]
        public void StepsOnGravelAreHeardFurtherThanOnPavement()
        {
            Assert.False(HeardBehind(gravel: false, planks: false));
            Assert.True(HeardBehind(gravel: true, planks: false));
        }

        [Fact]
        public void PlanksAcrossTheGravelAreQuiet()
        {
            Assert.False(HeardBehind(gravel: true, planks: true));
        }

        [Fact]
        public void TheWatchMapKnowsTheGravel()
        {
            // 2,7 Felder hinter der Wache: auf Pflaster frei, auf Schotter im Lageplan als gehört gezeigt.
            MissionData Yard(bool gravel)
            {
                var m = new MissionData
                {
                    Seed = 7, Item = new[] { 20.5, 5.5 }, Exit = new ZoneData { X0 = 0, Y0 = 0, X1 = 2, Y1 = 12 }, Guards = GuardLookingAway(),
                };
                if (gravel)
                {
                    m.Gravel.Add(new ZoneData { X0 = 0, Y0 = 0, X1 = 30, Y1 = 12 });
                }
                return m;
            }
            var map = new MapData { Width = 30, Height = 12 };
            Assert.False(WatchMap.Measure(map, Yard(false)).IsWatched(7, 5, 0));
            Assert.True(WatchMap.Measure(map, Yard(true)).IsWatched(7, 5, 0));
        }

        [Fact]
        public void WhoStandsStillOnGravelIsAsQuietAsAnywhere()
        {
            var w = Make(guards: GuardLookingAway(), gravel: true, agentX: 7.5);
            for (int i = 0; i < 100; i++)
            {
                w.Step();
            }
            Assert.Equal(MissionRating.Unseen, w.Rating);
        }

        [Fact]
        public void TheCourierCarriesTheItemAtFullSpeed()
        {
            var plain = new Unit { Faction = Faction.Agent };
            var loaded = new Unit { Faction = Faction.Agent, CarriesItem = true };
            var courier = new Unit { Faction = Faction.Agent, CarriesItem = true, Skill = World.SkillCourier };
            Assert.True(World.WalkSpeed(loaded) < World.WalkSpeed(plain));
            Assert.Equal(World.WalkSpeed(plain), World.WalkSpeed(courier));
        }

        [Fact]
        public void WorkersOnlyTurnLoudWhenTheCityIsAgainstTheSquad()
        {
            World Yard(CityStance stance)
            {
                var mission = new MissionData
                {
                    Seed = 7, Stance = stance, Item = new[] { 20.5, 5.5 }, Exit = new ZoneData { X0 = 0, Y0 = 0, X1 = 2, Y1 = 12 },
                    Agents = new List<UnitData> { new() { Name = "Greta", Sprite = "Agent", X = 6.5, Y = 5.5 } },
                    Guards = new List<UnitData> { new() { Name = "W", Sprite = "Wachmann", X = 12.5, Y = 5.5, Facing = new[] { 1.0, 0.0 } } },
                    Residents = new List<ResidentData> { new() { Id = "heizer", X = 5.5, Y = 5.5, OnlyAgainst = true } },
                };
                var w = new World(new MapData { Width = 30, Height = 12 }, mission);
                w.Step();
                return w;
            }
            Assert.Empty(Yard(CityStance.Helps).ResidentCalls);
            Assert.Empty(Yard(CityStance.Silent).ResidentCalls);
            var against = Yard(CityStance.Against);
            Assert.Equal("heizer", Assert.Single(against.ResidentCalls).Resident);
            // Lärm, kein Alarm: Die Wache geht nachsehen, geschossen hat niemand.
            Assert.Equal(GuardState.Investigate, against.Units[1].GuardState);
            Assert.Equal(MissionRating.Quiet, against.Rating);
        }

        [Fact]
        public void TheStokerStillHelpsASilentCityWhilePaulRothLives()
        {
            Campaign With(bool? paul, int debt)
            {
                var c = new Campaign { BloodDebt = debt };
                if (paul is { } alive)
                {
                    c.Records["mission_02"] = new MissionRecord { Won = true, Prisoners = { ["Paul Roth"] = alive } };
                }
                return c;
            }
            Assert.True(With(null, 0).HouseHelps(Campaign.HelperStoker));
            Assert.True(With(true, Campaign.SilentFrom).HouseHelps(Campaign.HelperStoker));
            Assert.False(With(false, Campaign.SilentFrom).HouseHelps(Campaign.HelperStoker));
            Assert.False(With(null, Campaign.SilentFrom).HouseHelps(Campaign.HelperStoker));
            Assert.False(With(true, Campaign.AgainstFrom).HouseHelps(Campaign.HelperStoker));
            // Der Rangierer hat keine solche Ausnahme.
            Assert.False(With(true, Campaign.SilentFrom).HouseHelps("rangierer"));

            // Mit lebendem Roth steht die Pforte der Heizer im schweigenden Gleisviertel offen, die der Rangierer nicht.
            var (map, mission) = Loader.LoadMission(DataDir(), "mission_03");
            With(true, Campaign.SilentFrom).Prepare(mission, "mission_03");
            var w = new World(map, mission);
            Assert.True(w.Doors.First(d => d.Name == "Zaunpforte der Heizer").Open);
            Assert.False(w.Doors.First(d => d.Name == "Zaunpforte der Rangierer").Open);
        }

        [Fact]
        public void FullNamesMatchOldSavesThatOnlyKnowTheSurname()
        {
            Assert.True(World.SamePerson("Fritz Bartels", "Bartels"));
            Assert.True(World.SamePerson("Bartels", "Fritz Bartels"));
            Assert.True(World.SamePerson("Fritz Bartels", "Fritz Bartels"));
            Assert.False(World.SamePerson("Fritz Bartels", "Hans Olbrich"));
            Assert.False(World.SamePerson("", ""));
        }

        [Fact]
        public void EveryGuardCarriesAFullNameAndReturnersExistInAnEarlierMission()
        {
            var earlier = new HashSet<string>();
            foreach (string id in new[] { "mission_01", "mission_02", "mission_03" })
            {
                var mission = Loader.LoadMission(DataDir(), id).Mission;
                foreach (var g in mission.Guards)
                {
                    Assert.True(g.Person.Length > 0, $"{id}: {g.Name} ohne Namen");
                    Assert.True(g.Type == "hund" || g.Person.Contains(' '), $"{id}: {g.Name} trägt nur „{g.Person}“");
                    if (g.Returning.Length > 0)
                    {
                        Assert.Contains(g.Returning, earlier);
                    }
                }
                Assert.All(mission.Reinforcements!.Persons, p => Assert.Contains(' ', p));
                earlier.UnionWith(mission.Guards.Select(g => g.Person));
            }
        }

        private static string DataDir()
        {
            var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "data", "stadtplan.json")))
            {
                dir = dir.Parent;
            }
            return Path.Combine(dir!.FullName, "data");
        }

        [Fact]
        public void HannaIsTheCourierInEveryMission()
        {
            foreach (string id in new[] { "mission_01", "mission_02", "mission_03" })
            {
                var mission = Loader.LoadMission(DataDir(), id).Mission;
                Assert.Equal(World.SkillCourier, mission.Agents.First(a => a.Name == "Hanna").Skill);
            }
        }

        [Fact]
        public void TheFreightYardLoadsAsSabotageWithClockGravelAndTwoTargets()
        {
            var (map, mission) = Loader.LoadMission(DataDir(), "mission_03");
            var w = new World(map, mission);
            Assert.True(w.IsSabotage);
            Assert.Equal(new[] { "Stellwerk", "Weiche" }, w.Targets.Select(t => t.Name));
            Assert.Equal(300 * World.TickRate, w.TimeLimitTicks);
            Assert.Equal(9, w.Units.Count(u => u.Faction == Faction.Guard));
            Assert.Equal(2, w.Units.Count(u => u.Faction == Faction.Guard && u.Type == GuardType.Werkschutz));
            int T(double v) => (int)(v * World.TileUnits);
            Assert.True(w.OnGravel(T(10.5), T(12.5)));
            Assert.False(w.OnGravel(T(1.5), T(12.5)));
            Assert.False(w.OnGravel(T(20.5), T(12.5)));
            Assert.False(w.OnGravel(T(10.5), T(16.5)));
            // Die Hebelbank steht hinter der Stellwerkstür: erst die Tür, dann die Arbeit. Die Weiche liegt frei.
            Assert.False(w.OrderSabotage(1, 0));
            Assert.True(w.OrderSabotage(1, 1));
        }

        [Fact]
        public void TheFreightYardCanBeWonWithoutGuardsInsideTheClock()
        {
            var (map, mission) = Loader.LoadMission(DataDir(), "mission_03");
            mission.Guards.Clear();
            mission.Reinforcements = null;
            var w = new World(map, mission);
            int otto = w.Units.FindIndex(u => u.Skill == World.SkillLocksmith);
            int hanna = w.Units.FindIndex(u => u.Skill == World.SkillCourier);
            Assert.True(w.OrderOpenDoor(otto, w.Doors.FindIndex(d => d.Name == "Stellwerk")));
            Assert.True(w.OrderSabotage(hanna, 1));
            StepUntil(w, () => w.Doors.First(d => d.Name == "Stellwerk").Open, 3000);
            Assert.True(w.OrderSabotage(otto, 0));
            StepUntil(w, () => w.AllSabotaged, 3000);
            Assert.True(w.AllSabotaged);
            w.OrderMove(hanna, 1, 7);
            StepUntil(w, () => false, 6000);
            Assert.Equal(MissionState.Won, w.State);
            Assert.True(w.Tick < w.TimeLimitTicks);
        }
    }
}
