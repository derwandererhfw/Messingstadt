using System;
using System.Linq;
using Messingstadt.Core;
using Xunit;

namespace Messingstadt.Core.Tests
{
    /// <summary>Der gefälschte Passierschein der Truppführerin.</summary>
    public class PassTests
    {
        private static UnitData Agent(double x, string skill = World.SkillLeader, string name = "Greta", double y = 5.5) =>
            new() { Name = name, Sprite = "Agent", X = x, Y = y, Skill = skill };

        private static UnitData Guard(double x, string type = "", double y = 5.5, double fx = 1, string role = "Posten") =>
            new() { Name = role, Person = "Krüger", Sprite = "Wachmann", Type = type, X = x, Y = y, Facing = new[] { fx, 0.0 } };

        private static MissionData Mission(UnitData[] agents, params UnitData[] guards) => new()
        {
            Seed = 3, Item = new[] { 39.5, 39.5 }, ItemName = "Buch", Money = 1000,
            Exit = new ZoneData { X0 = 0, Y0 = 38, X1 = 1, Y1 = 39 },
            Agents = new(agents), Guards = new(guards),
        };

        private static World NewWorld(MissionData m) => new(new MapData { Width = 40, Height = 40 }, m);

        private static void Run(World w, int ticks)
        {
            for (int i = 0; i < ticks && w.State == MissionState.Running; i++)
            {
                w.Step();
            }
        }

        private static void RunUntil(World w, Func<bool> done, int maxTicks = 900)
        {
            for (int i = 0; i < maxTicks && !done(); i++)
            {
                w.Step();
            }
            Assert.True(done());
        }

        /// <summary>Greta steht hinter der Wache, die von ihr wegschaut.</summary>
        private static World Behind(string skill = World.SkillLeader, string type = "") =>
            NewWorld(Mission(new[] { Agent(8.5, skill) }, Guard(3.5, type, fx: -1)));

        [Fact]
        public void TheGuardReadsThenLooksAwayThenGrowsSuspicious()
        {
            // Die Wache schaut Greta entgegen: Ohne Papier würde sie sie bemerken.
            var w = NewWorld(Mission(new[] { Agent(9.5) }, Guard(3.5)));
            Assert.Equal(PassState.Ready, w.Pass);
            Assert.True(w.PassAvailable);
            Assert.Equal(-1, w.PassGuardId);
            Assert.True(w.OrderShowPass(0, 1));
            RunUntil(w, () => w.Pass == PassState.Showing);
            Assert.Equal(w.Tick, w.PassSince);
            Assert.Equal(1, w.PassGuardId);
            Assert.Equal(0, w.PassAgentId);
            Assert.True(w.PassBearerKnown);
            Assert.True(World.Distance(w.Units[0], w.Units[1]) <= World.PassRange);
            Assert.Equal(World.PassShowTicks, w.PassTicksLeft);
            Assert.Equal(MissionRating.Quiet, w.Rating);

            int shown = w.Tick;
            RunUntil(w, () => w.Pass == PassState.Away);
            Assert.Equal(World.PassShowTicks, w.Tick - shown);
            Assert.Equal(20 * World.TickRate, w.PassTicksLeft);
            Assert.Equal(-1, w.Units[0].BribeTarget);

            var (gx, gy) = (w.Units[1].X, w.Units[1].Y);
            Run(w, 20 * World.TickRate - 1);
            Assert.Equal(PassState.Away, w.Pass);
            Assert.True(w.HeldByPass(w.Units[1]));
            Assert.Equal(GuardState.Patrol, w.Units[1].GuardState);
            Assert.Equal((gx, gy), (w.Units[1].X, w.Units[1].Y));
            Assert.False(w.Units[1].Bribed);
            Assert.Equal(1000, w.Money);
            Assert.Equal(100, w.Units[0].Hp);

            Run(w, 2);
            Assert.Equal(PassState.Spent, w.Pass);
            Assert.False(w.HeldByPass(w.Units[1]));
            Assert.NotEqual(GuardState.Patrol, w.Units[1].GuardState);
            Assert.True(w.Units[1].Alerted);
            Assert.Equal(GuardFate.Alerted, World.FateOf(w.Units[1]));
            // Greta steht noch vor ihr: Jetzt wird geschossen.
            RunUntil(w, () => w.Units[1].GuardState == GuardState.Combat);
        }

        [Fact]
        public void WithoutThePaperTheSameGuardNoticesHer()
        {
            var w = NewWorld(Mission(new[] { Agent(9.5) }, Guard(3.5)));
            w.OrderMove(0, 5, 5);
            RunUntil(w, () => w.Units[1].GuardState == GuardState.Combat);
        }

        [Fact]
        public void TheGuardDoesNotWaitForeverForThePaper()
        {
            // Sie will das Papier zeigen, kommt aber nicht voran: Länger als PassWaitTicks schaut ihr keine Wache zu.
            var w = NewWorld(Mission(new[] { Agent(9.5) }, Guard(3.5)));
            w.Units[0].Hp = 100000;
            Assert.True(w.OrderShowPass(0, 1));
            RunUntil(w, () => w.Units[1].GuardState == GuardState.Noticing);
            int x = w.Units[0].X;
            for (int i = 0; i < 2 * World.PassWaitTicks && w.Units[1].GuardState != GuardState.Combat; i++)
            {
                w.Step();
                w.Units[0].X = x;
            }
            Assert.Equal(GuardState.Combat, w.Units[1].GuardState);
            Assert.True(w.Tick > World.PassWaitTicks);
            Assert.Equal(PassState.Ready, w.Pass);
        }

        [Fact]
        public void ThePaperWorksOncePerMission()
        {
            var w = NewWorld(Mission(new[] { Agent(8.5) }, Guard(3.5, fx: -1), Guard(3.5, y: 20.5, fx: -1, role: "Zweiter")));
            Assert.Equal(PassAnswer.Works, w.PassCheck(w.Units[0], w.Units[2]));
            Assert.True(w.OrderShowPass(0, 1));
            RunUntil(w, () => w.Pass == PassState.Showing);
            Assert.False(w.PassAvailable);
            Assert.Equal(PassAnswer.Used, w.PassCheck(w.Units[0], w.Units[2]));
            Assert.False(w.OrderShowPass(0, 2));
            RunUntil(w, () => w.Pass == PassState.Spent);
            Assert.Equal(PassAnswer.Used, w.PassCheck(w.Units[0], w.Units[2]));
            Assert.False(w.OrderShowPass(0, 2));
            Assert.Equal(0, w.PassTicksLeft);
        }

        [Theory]
        [InlineData("offizier", PassAnswer.Officer)]
        [InlineData("werkschutz", PassAnswer.WrongKind)]
        [InlineData("hund", PassAnswer.WrongKind)]
        [InlineData("laufbursche", PassAnswer.WrongKind)]
        [InlineData("hundefuehrer", PassAnswer.WrongKind)]
        [InlineData("schuetze", PassAnswer.WrongKind)]
        [InlineData("telefonist", PassAnswer.WrongKind)]
        public void OnlyPlainGuardsmenBelieveIt(string type, PassAnswer answer)
        {
            var w = Behind(type: type);
            Assert.Equal(answer, w.PassCheck(w.Units[0], w.Units[1]));
            Assert.False(w.OrderShowPass(0, 1));
            Assert.Equal(PassState.Ready, w.Pass);
        }

        [Fact]
        public void AnAlertedGuardDoesNotRead()
        {
            var w = Behind();
            w.RaiseAlarm(w.Units[1].X, w.Units[1].Y);
            Run(w, 2);
            Assert.Equal(PassAnswer.Alerted, w.PassCheck(w.Units[0], w.Units[1]));
            Assert.False(w.OrderShowPass(0, 1));
        }

        [Fact]
        public void ABoughtGuardNeedsNoPaper()
        {
            var w = NewWorld(Mission(new[] { Agent(8.5), Agent(7.5, World.SkillBookkeeper, "Emil") }, Guard(3.5, fx: -1)));
            Assert.True(w.OrderBribe(1, 2));
            RunUntil(w, () => w.Units[2].Bribed);
            Assert.Equal(PassAnswer.Bribed, w.PassCheck(w.Units[0], w.Units[2]));
            Assert.False(w.OrderShowPass(0, 2));
        }

        [Fact]
        public void WithBloodOnTheSquadNobodyBelievesIt()
        {
            var byMemory = Mission(new[] { Agent(8.5) }, Guard(3.5, fx: -1));
            byMemory.Memory = new GuardMemoryData { Stance = CityStance.Against };
            var byCity = Mission(new[] { Agent(8.5) }, Guard(3.5, fx: -1));
            byCity.Stance = CityStance.Against;
            foreach (var m in new[] { byMemory, byCity })
            {
                var w = NewWorld(m);
                Assert.True(w.PassBlocked);
                Assert.False(w.PassAvailable);
                Assert.Equal(PassAnswer.Blood, w.PassCheck(w.Units[0], w.Units[1]));
                Assert.False(w.OrderShowPass(0, 1));
            }
            var silent = Mission(new[] { Agent(8.5) }, Guard(3.5, fx: -1));
            silent.Memory = new GuardMemoryData { Stance = CityStance.Silent };
            Assert.True(NewWorld(silent).PassAvailable);
        }

        [Fact]
        public void ThePaperBelongsToTheJobNotTheName()
        {
            var plain = Behind(skill: "");
            Assert.Equal(PassState.None, plain.Pass);
            Assert.False(plain.PassAvailable);
            Assert.Equal(PassAnswer.NoPapers, plain.PassCheck(plain.Units[0], plain.Units[1]));
            Assert.False(plain.OrderShowPass(0, 1));
            Assert.Equal(PassAnswer.NoPapers, Behind(World.SkillBookkeeper).PassCheck(plain.Units[0], plain.Units[1]));

            var w = NewWorld(Mission(new[] { Agent(8.5, World.SkillDeputy, "Die Neue"), Agent(7.5, World.SkillBookkeeper, "Emil") }, Guard(3.5, fx: -1)));
            // Die Neue hat das Papier, aber den halben Preis gibt ihr keine Wache.
            Assert.False(w.LeaderBacks(w.Units[1], w.Units[2]));
            Assert.Equal(GuardType.Wachmann.BribeCost, w.BribePrice(w.Units[2], w.Units[1]));
            Assert.True(w.OrderShowPass(0, 2));
            RunUntil(w, () => w.Pass == PassState.Away);
            Assert.False(w.PassBearerKnown);
        }

        [Fact]
        public void HalfPriceStaysAsItWas()
        {
            var w = NewWorld(Mission(new[] { Agent(8.5), Agent(7.5, World.SkillBookkeeper, "Emil") }, Guard(3.5, fx: -1)));
            Assert.True(w.LeaderBacks(w.Units[1], w.Units[2]));
            Assert.Equal(GuardType.Wachmann.BribeCost / 2, w.BribePrice(w.Units[2], w.Units[1]));
        }

        [Fact]
        public void PullingThePaperBackMakesTheGuardSuspiciousAtOnce()
        {
            var w = Behind();
            w.OrderShowPass(0, 1);
            RunUntil(w, () => w.Pass == PassState.Showing);
            Run(w, 5);
            Assert.True(w.OrderMove(0, 20, 5));
            Run(w, 2);
            Assert.Equal(PassState.Spent, w.Pass);
            Assert.True(w.Units[1].Alerted);
            Assert.False(w.Units[0].ShowsPass);
        }

        [Fact]
        public void AnotherOrderOnTheWayCostsNothing()
        {
            var w = NewWorld(Mission(new[] { Agent(30.5) }, Guard(3.5, fx: -1)));
            w.OrderShowPass(0, 1);
            Run(w, 10);
            Assert.True(w.OrderMove(0, 32, 5));
            Run(w, 60);
            Assert.Equal(PassState.Ready, w.Pass);
            Assert.True(w.PassAvailable);
            Assert.Equal(GuardState.Patrol, w.Units[1].GuardState);
            Assert.Equal(MissionRating.Unseen, w.Rating);
        }

        [Fact]
        public void AnAlarmEndsTheLookingAway()
        {
            var w = Behind();
            w.OrderShowPass(0, 1);
            RunUntil(w, () => w.Pass == PassState.Away);
            Run(w, 20);
            w.RaiseAlarm(w.Units[1].X, w.Units[1].Y);
            Run(w, 2);
            Assert.Equal(PassState.Spent, w.Pass);
            Assert.NotEqual(GuardState.Patrol, w.Units[1].GuardState);
        }

        [Fact]
        public void StrikingTheGuardEndsIt()
        {
            var w = Behind();
            w.OrderShowPass(0, 1);
            RunUntil(w, () => w.Pass == PassState.Away);
            w.Units[1].Hp = 100000;
            Assert.True(w.OrderAttack(0, 1));
            RunUntil(w, () => w.Pass == PassState.Spent);
        }

        [Fact]
        public void ADeadBearerLeavesNoPaper()
        {
            var w = Behind();
            w.Units[0].Hp = 0;
            Assert.Equal(PassState.None, w.Pass);
        }

        [Fact]
        public void APatrolStopsWhileItHolds()
        {
            var guard = Guard(3.5, fx: -1);
            guard.Patrol.Add(new double[] { 3, 5 });
            guard.Patrol.Add(new double[] { 3, 25 });
            var w = NewWorld(Mission(new[] { Agent(8.5) }, guard));
            w.OrderShowPass(0, 1);
            RunUntil(w, () => w.Pass == PassState.Away);
            var at = (w.Units[1].X, w.Units[1].Y);
            Run(w, 100);
            Assert.Equal(at, (w.Units[1].X, w.Units[1].Y));
        }

        [Fact]
        public void WhoReplacesTheLeaderCarriesThePaper()
        {
            MissionData M() => new()
            {
                Agents = new()
                {
                    new UnitData { Name = "Greta", Sprite = "Agent", X = 1.5, Y = 1.5, Skill = World.SkillLeader, Role = "Truppführerin" },
                    new UnitData { Name = "Emil", Sprite = "Agent", X = 2.5, Y = 1.5, Skill = World.SkillBookkeeper },
                },
            };
            var campaign = new Campaign();
            campaign.Prepare(M());
            campaign.Agents.First(a => a.Name == "Greta").Dead = true;
            var second = M();
            campaign.Prepare(second);
            Assert.DoesNotContain(second.Agents, a => a.Skill == World.SkillLeader);
            Assert.Contains(second.Agents, a => a.Skill == World.SkillDeputy);
        }
    }
}
