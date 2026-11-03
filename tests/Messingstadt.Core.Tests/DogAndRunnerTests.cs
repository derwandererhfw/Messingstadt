using System;
using System.Collections.Generic;
using Messingstadt.Core;
using Xunit;

namespace Messingstadt.Core.Tests
{
    public class DogAndRunnerTests
    {
        private const int Size = 40;

        private static UnitData Agent(double x, double y = 5.5, string skill = "") =>
            new() { Name = "A", Sprite = "Agent", X = x, Y = y, Skill = skill };

        private static UnitData Guard(string name, string type, double x, double y = 5.5, string handler = "") =>
            new() { Name = name, Sprite = name, Type = type, X = x, Y = y, Handler = handler, Facing = new[] { 1.0, 0.0 } };

        private static MissionData Mission(UnitData[] agents, params UnitData[] guards) => new()
        {
            Seed = 3, Item = new[] { 39.5, 39.5 }, ItemName = "Buch", Money = 1000,
            Exit = new ZoneData { X0 = 0, Y0 = 38, X1 = 1, Y1 = 39 },
            Agents = new(agents), Guards = new(guards),
        };

        private static World NewWorld(MissionData m, params SolidData[] solids) =>
            new(new MapData { Width = Size, Height = Size, Solids = new(solids) }, m);

        private static World Run(World w, int ticks)
        {
            for (int i = 0; i < ticks && w.State == MissionState.Running; i++)
            {
                w.Step();
            }
            return w;
        }

        private static void RunUntil(World w, Func<bool> done, int maxTicks = 600)
        {
            for (int i = 0; i < maxTicks && !done(); i++)
            {
                w.Step();
            }
            Assert.True(done());
        }

        // ---------- Hund ----------

        [Fact]
        public void DogBitesFromUpCloseNotFromAFieldAway()
        {
            var w = NewWorld(Mission(new[] { Agent(5.5) }, Guard("H", "hund", 2.5)));
            var dog = w.Units[1];
            RunUntil(w, () => dog.LastFiredTick > 0);
            Assert.True(World.Distance(dog, w.Units[0]) <= 800);
            Run(w, 20);
            Assert.False(dog.Moving);
            Assert.True(World.Distance(dog, w.Units[0]) >= 400);
        }

        [Fact]
        public void AgentsOnlyShootADogWhenOrdered()
        {
            var w = NewWorld(Mission(new[] { Agent(5.5) }, Guard("H", "hund", 2.5)));
            Run(w, 45);
            Assert.True(w.Units[0].Hp < w.Units[0].MaxHp);
            Assert.Empty(w.Shots);
            Assert.Equal(MissionRating.Quiet, w.Rating);

            w.OrderAttack(0, 1);
            Run(w, 30);
            Assert.Contains(w.Shots, s => s.ShooterId == 0);
            Assert.Equal(MissionRating.Loud, w.Rating);
        }

        [Fact]
        public void BittenAgentKicksTheDogToDeathQuietlyButAtACost()
        {
            var w = NewWorld(Mission(new[] { Agent(5.5) }, Guard("H", "hund", 2.5)));
            RunUntil(w, () => !w.Units[1].Alive);
            Assert.Empty(w.Shots);
            Assert.Equal(MissionRating.Quiet, w.Rating);
            Assert.True(w.Units[0].Alive);
            Assert.InRange(w.Units[0].Hp, 1, 80);
        }

        [Fact]
        public void AnOrderedShotReplacesTheKick()
        {
            var w = NewWorld(Mission(new[] { Agent(5.5) }, Guard("H", "hund", 2.5)));
            RunUntil(w, () => World.Distance(w.Units[0], w.Units[1]) <= 800);
            w.OrderAttack(0, 1);
            Run(w, 20);
            Assert.Contains(w.Shots, s => s.ShooterId == 0);
        }

        [Fact]
        public void AgentsStillFireAtAlertedGuardsOnTheirOwn()
        {
            var w = NewWorld(Mission(new[] { Agent(5.5) }, Guard("W", "wachmann", 2.5)));
            Run(w, 80);
            Assert.Contains(w.Shots, s => s.ShooterId == 0);
        }

        // Mauer zwischen x=8 und x=9: Der Agent läuft unten herum auf die andere Seite.
        private static readonly SolidData Wall = new() { X0 = 8, Y0 = 0, X1 = 9, Y1 = 20 };

        private static World WalkBehindWallThenAlert(string type, int waitTicks)
        {
            var w = NewWorld(Mission(new[] { Agent(7.5) }, Guard("G", type, 35.5, 35.5)), Wall);
            Assert.True(w.OrderMove(0, 12, 5));
            RunUntil(w, () => w.Units[0].Path.Count == 0);
            Run(w, waitTicks);
            var g = w.Units[1];
            // Der Spürer beginnt dort, wo der Agent losgegangen ist, und sieht ihn durch die Mauer nicht.
            g.X = 7500;
            g.Y = 5500;
            g.Alerted = true;
            g.LastKnownX = 7500;
            g.LastKnownY = 5500;
            g.GuardState = GuardState.Investigate;
            return w;
        }

        [Fact]
        public void AlertedDogFollowsTheTrailBehindTheWall()
        {
            var w = WalkBehindWallThenAlert("hund", 0);
            Run(w, 300);
            Assert.True(w.Units[0].Hp < w.Units[0].MaxHp);
        }

        [Fact]
        public void AGuardDoesNotFollowTheTrail()
        {
            var w = WalkBehindWallThenAlert("wachmann", 0);
            Run(w, 60);
            Assert.Equal(GuardState.Search, w.Units[1].GuardState);
            Assert.Equal(7, w.Units[1].TileX);
        }

        [Fact]
        public void OldTrailsAreCold()
        {
            var w = WalkBehindWallThenAlert("hund", 500);
            Run(w, 90);
            Assert.Equal(7, w.Units[1].TileX);
            Assert.Equal(5, w.Units[1].TileY);
        }

        [Fact]
        public void DogOnPatrolIgnoresTheTrail()
        {
            var w = NewWorld(Mission(new[] { Agent(7.5) }, Guard("G", "hund", 35.5, 35.5)), Wall);
            Assert.True(w.OrderMove(0, 12, 5));
            RunUntil(w, () => w.Units[0].Path.Count == 0);
            w.Units[1].X = 7500;
            w.Units[1].Y = 5500;
            Run(w, 200);
            Assert.Equal(7, w.Units[1].TileX);
            Assert.Equal(MissionRating.Unseen, w.Rating);
        }

        // ---------- Hundeführer ----------

        private static MissionData Pair(UnitData agent, double handlerX = 2.5)
        {
            var handler = Guard("F", "hundefuehrer", handlerX);
            var dog = Guard("H", "hund", handlerX - 1, handler: "F");
            return Mission(new[] { agent }, handler, dog);
        }

        [Fact]
        public void LeashedDogStaysAtHeelOnPatrol()
        {
            var m = Pair(Agent(35.5, 35.5));
            m.Guards[0].Patrol = new List<double[]> { new[] { 2.0, 5.0 }, new[] { 20.0, 5.0 }, new[] { 20.0, 20.0 } };
            var w = NewWorld(m);
            var handler = w.Units[1];
            var dog = w.Units[2];
            Assert.Equal(handler.Id, dog.LeashedTo);
            int farthest = 0;
            for (int i = 0; i < 400; i++)
            {
                w.Step();
                farthest = Math.Max(farthest, World.Distance(dog, handler));
            }
            Assert.True(handler.TileX > 10);
            Assert.True(farthest <= 3 * World.TileUnits, $"Abstand {farthest}");
            Assert.Equal(GuardState.Patrol, dog.GuardState);
        }

        [Fact]
        public void BribingTheHandlerCalmsTheDog()
        {
            var w = NewWorld(Pair(Agent(4.5, skill: World.SkillBookkeeper)));
            Assert.True(w.OrderBribe(0, 1));
            Run(w, 60);
            Assert.True(w.Units[1].Bribed);
            Assert.True(w.Units[2].Bribed);
            Assert.Equal(1000 - GuardType.Hundefuehrer.BribeCost, w.Money);
            Assert.Equal(GuardState.Patrol, w.Units[2].GuardState);
            Assert.Equal(MissionRating.Unseen, w.Rating);
        }

        [Fact]
        public void ShootingTheDogBreaksTheDealWithBoth()
        {
            var w = NewWorld(Pair(Agent(4.5, skill: World.SkillBookkeeper)));
            w.OrderBribe(0, 1);
            Run(w, 2);
            w.OrderAttack(0, 2);
            Run(w, 20);
            Assert.False(w.Units[1].Bribed);
            Assert.False(w.Units[2].Bribed);
        }

        [Fact]
        public void KillingTheHandlerSetsTheDogOnTheNearestAgent()
        {
            var w = NewWorld(Pair(Agent(8.5)));
            w.Units[1].Hp = 1;
            w.OrderAttack(0, 1);
            RunUntil(w, () => !w.Units[1].Alive);
            RunUntil(w, () => w.Units[2].LeashedTo == -1);
            RunUntil(w, () => w.Units[0].Hp < w.Units[0].MaxHp);
        }

        [Fact]
        public void HandlerInCombatLetsTheDogGo()
        {
            var w = NewWorld(Pair(Agent(6.5)));
            RunUntil(w, () => w.Units[1].GuardState == GuardState.Combat || w.Units[2].GuardState == GuardState.Combat);
            Run(w, 2);
            Assert.Equal(-1, w.Units[2].LeashedTo);
            Assert.NotEqual(GuardState.Patrol, w.Units[2].GuardState);
        }

        [Fact]
        public void ShootingTheDogAlertsTheHandler()
        {
            var w = NewWorld(Pair(Agent(8.5)));
            w.OrderAttack(0, 2);
            RunUntil(w, () => w.Shots.Count > 0);
            Run(w, 20);
            Assert.NotEqual(GuardState.Patrol, w.Units[1].GuardState);
        }

        [Fact]
        public void UnknownHandlerFails()
        {
            Assert.Throws<ArgumentException>(() => NewWorld(Mission(new[] { Agent(5.5) }, Guard("H", "hund", 2.5, handler: "Niemand"))));
        }

        // ---------- Laufbursche ----------

        private static World RunnerSees(UnitData agent, double guardX = 25.5)
        {
            var m = Mission(new[] { agent }, Guard("L", "laufbursche", 5.5), Guard("W", "wachmann", guardX, 5.5));
            m.Guards[1].Facing = new[] { 1.0, 0.0 };
            m.Reinforcements = new ReinforcementData { X = 0.5, Y = 38.5, Sprite = "W", FirstDelay = 6, Interval = 4, Count = 3 };
            return NewWorld(m);
        }

        [Fact]
        public void RunnerIsUnarmedAndFasterThanWalkingSlowerThanRunning()
        {
            var w = RunnerSees(Agent(8.5));
            int walk = World.WalkSpeed(w.Units[0]);
            Assert.True(GuardType.Laufbursche.AlertSpeed > walk);
            Assert.True(GuardType.Laufbursche.AlertSpeed < walk + 25);
            RunUntil(w, () => w.Units[1].GuardState == GuardState.Report);
            Run(w, 40);
            Assert.DoesNotContain(w.Shots, s => s.ShooterId == 1);
        }

        [Fact]
        public void RunnerFetchesTheNearestGuardAndBringsReinforcements()
        {
            var w = RunnerSees(Agent(8.5));
            RunUntil(w, () => w.Units[1].GuardState == GuardState.Report);
            Assert.Equal(GuardState.Patrol, w.Units[2].GuardState);
            Assert.Equal(-1, w.TicksUntilReinforcement);
            RunUntil(w, () => w.Units[1].GuardState == GuardState.Search, 300);
            Assert.NotEqual(GuardState.Patrol, w.Units[2].GuardState);
            Assert.Equal(8, w.Units[2].LastKnownX / World.TileUnits);
            Assert.True(w.TicksUntilReinforcement > 0);
            Assert.NotEqual(MissionRating.Loud, w.Rating);
        }

        [Fact]
        public void AgentsLetTheRunnerGoUntilOrdered()
        {
            var w = RunnerSees(Agent(8.5));
            RunUntil(w, () => w.Units[1].GuardState == GuardState.Report);
            Run(w, 30);
            Assert.Empty(w.Shots);
        }

        [Fact]
        public void OneHitStopsTheRunnerButTheShotIsLoud()
        {
            var w = RunnerSees(Agent(8.5));
            RunUntil(w, () => w.Units[1].GuardState == GuardState.Report);
            w.OrderAttack(0, 1);
            RunUntil(w, () => !w.Units[1].Alive);
            Assert.Single(w.Shots.FindAll(s => s.Hit));
            Assert.Equal(MissionRating.Loud, w.Rating);
        }

        [Fact]
        public void RunnerCanBeBribedOnTheRunIfTheBookkeeperSprints()
        {
            var w = RunnerSees(Agent(8.5, skill: World.SkillBookkeeper));
            RunUntil(w, () => w.Units[1].GuardState == GuardState.Report);
            Assert.True(w.OrderBribe(0, 1));
            Assert.True(w.Units[0].Sprinting);
            RunUntil(w, () => w.Units[1].Bribed, 120);
            Assert.Equal(1000 - GuardType.Laufbursche.BribeCost, w.Money);
            Run(w, 100);
            Assert.Equal(GuardState.Patrol, w.Units[2].GuardState);
            Assert.Equal(-1, w.TicksUntilReinforcement);
        }

        [Fact]
        public void RunnerWithoutGuardsToTellStaysPut()
        {
            var w = NewWorld(Mission(new[] { Agent(8.5) }, Guard("L", "laufbursche", 5.5)));
            RunUntil(w, () => w.Units[1].GuardState == GuardState.Report);
            Run(w, 100);
            Assert.Equal(GuardState.Report, w.Units[1].GuardState);
            Assert.False(w.Units[1].Moving);
            Assert.Equal(-1, w.TicksUntilReinforcement);
        }

        [Fact]
        public void RunnerDoesNotReportTwiceInARow()
        {
            var w = RunnerSees(Agent(25.5, 8.5));
            w.Units[1].X = 24500;
            w.Units[1].Y = 8500;
            RunUntil(w, () => w.Units[1].GuardState == GuardState.Search, 100);
            Run(w, 50);
            Assert.Equal(GuardState.Search, w.Units[1].GuardState);
        }

        [Fact]
        public void RunnerDoesNotRunTowardsTheNoise()
        {
            var w = RunnerSees(Agent(35.5, 35.5));
            w.RaiseAlarm(5500, 12500);
            Run(w, 60);
            Assert.Equal(GuardState.Patrol, w.Units[1].GuardState);
        }

        [Fact]
        public void ReportAfterAShotDoesNotPostponeReinforcements()
        {
            var w = RunnerSees(Agent(8.5));
            var runner = w.Units[1];
            RunUntil(w, () => runner.GuardState == GuardState.Report);
            w.Units[2].GuardState = GuardState.Combat;
            w.Units[2].Alerted = true;
            RunUntil(w, () => w.TicksUntilReinforcement >= 0, 300);
            int spawnAt = w.Tick + w.TicksUntilReinforcement;
            runner.GuardState = GuardState.Report;
            w.Units[2].Hp = 60;
            for (int i = 0; i < 60 && w.TicksUntilReinforcement >= 0; i++)
            {
                Assert.Equal(spawnAt, w.Tick + w.TicksUntilReinforcement);
                w.Step();
            }
        }

        [Fact]
        public void DogsAndRunnersStayDeterministic()
        {
            static ulong Play()
            {
                var m = Pair(Agent(8.5));
                m.Guards.Add(Guard("L", "laufbursche", 12.5, 12.5));
                var w = NewWorld(m);
                w.OrderMove(0, 20, 20);
                Run(w, 400);
                return w.StateHash();
            }
            Assert.Equal(Play(), Play());
        }
    }
}
