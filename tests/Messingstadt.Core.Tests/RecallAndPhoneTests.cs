using System;
using System.Collections.Generic;
using System.Linq;
using Messingstadt.Core;
using Xunit;

namespace Messingstadt.Core.Tests
{
    public class RecallAndPhoneTests
    {
        private static UnitData Agent(double x, double y = 5.5, string skill = "", string name = "A") =>
            new() { Name = name, Sprite = "Agent", X = x, Y = y, Skill = skill };

        private static UnitData Guard(string role, string person, double x, double y = 5.5, string type = "", double fx = 1, double fy = 0) =>
            new() { Name = role, Person = person, Sprite = "Wachmann", Type = type, X = x, Y = y, Facing = new[] { fx, fy } };

        private static MissionData Mission(UnitData[] agents, params UnitData[] guards) => new()
        {
            Seed = 3, Item = new[] { 39.5, 39.5 }, ItemName = "Buch", Money = 1000,
            Exit = new ZoneData { X0 = 0, Y0 = 38, X1 = 1, Y1 = 39 },
            Agents = new(agents), Guards = new(guards),
        };

        private static GuardMemoryData Memory(CityStance stance, params (string Person, GuardFate Fate)[] known) => new()
        {
            Stance = stance, Known = known.Select(k => new KnownGuardData { Person = k.Person, Fate = k.Fate }).ToList(),
        };

        private static World NewWorld(MissionData m, params SolidData[] solids) =>
            new(new MapData { Width = 40, Height = 40, Solids = new(solids) }, m);

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

        private static int NoticeTicks(MissionData m)
        {
            var w = NewWorld(m);
            Run(w, 1);
            return w.Units[1].NoticeTicks;
        }

        // ---------- Wiedersehen ----------

        [Fact]
        public void WithoutMemoryNothingChanges()
        {
            var w = NewWorld(Mission(new[] { Agent(6.5, skill: World.SkillBookkeeper) }, Guard("Posten", "Krüger", 2.5)));
            Assert.Equal(GuardRecall.None, w.Units[1].Recall);
            Assert.Equal(GuardType.Wachmann.BribeCost, w.BribePrice(w.Units[1], w.Units[0]));
            Assert.True(w.Bribable(w.Units[1]));
        }

        [Fact]
        public void AGuardBoughtBeforeCostsLessAndLooksAway()
        {
            var plain = Mission(new[] { Agent(6.5, skill: World.SkillBookkeeper) }, Guard("Posten", "Krüger", 2.5));
            var met = Mission(new[] { Agent(6.5, skill: World.SkillBookkeeper) }, Guard("Posten", "Krüger", 2.5));
            met.Memory = Memory(CityStance.Helps, ("Krüger", GuardFate.Bribed));
            var w = NewWorld(met);
            Assert.Equal(GuardRecall.OldDeal, w.Units[1].Recall);
            Assert.Equal(GuardType.Wachmann.BribeCost * 75 / 100, w.BribePrice(w.Units[1], w.Units[0]));
            Assert.True(NoticeTicks(met) > NoticeTicks(plain));
            Assert.True(w.OrderBribe(0, 1));
            RunUntil(w, () => w.Units[1].Bribed);
            Assert.Equal(1000 - GuardType.Wachmann.BribeCost * 75 / 100, w.Money);
        }

        [Fact]
        public void DiscountsDoNotStackTheLargerOneCounts()
        {
            var m = Mission(new[] { Agent(6.5, skill: World.SkillBookkeeper), Agent(7.5, skill: World.SkillLeader, name: "Greta") }, Guard("Posten", "Krüger", 2.5));
            m.Memory = Memory(CityStance.Helps, ("Krüger", GuardFate.Bribed));
            var w = NewWorld(m);
            Assert.Equal(50, w.LeaderDiscountPercent);
            Assert.Equal(GuardType.Wachmann.BribeCost / 2, w.BribePrice(w.Units[2], w.Units[0]));
            Assert.Equal(GuardType.Wachmann.BribeCost / 2, w.BribePriceWithLeader(w.Units[2]));
            w.LeaderDiscountPercent = 10;
            Assert.Equal(GuardType.Wachmann.BribeCost * 75 / 100, w.BribePrice(w.Units[2], w.Units[0]));
        }

        [Fact]
        public void ASurvivorOfABloodbathTakesNoMoneyAndNoticesFaster()
        {
            var plain = Mission(new[] { Agent(6.5, skill: World.SkillBookkeeper) }, Guard("Posten", "Krüger", 2.5));
            var met = Mission(new[] { Agent(6.5, skill: World.SkillBookkeeper) }, Guard("Posten", "Krüger", 2.5));
            met.Memory = Memory(CityStance.Against, ("Krüger", GuardFate.Alerted));
            var w = NewWorld(met);
            Assert.Equal(GuardRecall.Grudge, w.Units[1].Recall);
            Assert.False(w.Bribable(w.Units[1]));
            Assert.False(w.OrderBribe(0, 1));
            Assert.True(NoticeTicks(met) < NoticeTicks(plain));
        }

        [Fact]
        public void WhileTheCityOnlyKeepsSilentASurvivorHoldsNoGrudge()
        {
            var m = Mission(new[] { Agent(6.5) }, Guard("Posten", "Krüger", 2.5));
            m.Memory = Memory(CityStance.Silent, ("Krüger", GuardFate.Alerted));
            Assert.Equal(GuardRecall.None, NewWorld(m).Units[1].Recall);
        }

        [Fact]
        public void TheCeilingHoldsHoweverMuchBloodWasShed()
        {
            var guards = new List<UnitData>();
            var known = new List<(string, GuardFate)>();
            for (int i = 0; i < 6; i++)
            {
                guards.Add(Guard($"Posten {i}", $"Mann {i}", 20.5 + i, 30.5));
                known.Add(($"Mann {i}", GuardFate.Alerted));
            }
            for (int i = 0; i < 5; i++)
            {
                var partner = Guard($"Zweiter {i}", "", 20.5 + i, 32.5);
                partner.AfterBlood = true;
                guards.Add(partner);
            }
            var m = Mission(new[] { Agent(2.5) }, guards.ToArray());
            m.Memory = Memory(CityStance.Against, known.ToArray());
            var w = NewWorld(m);
            Assert.Equal(World.MaxGrudges, w.Units.Count(u => u.Recall == GuardRecall.Grudge));
            Assert.Equal(6 + World.MaxBloodPartners, w.Units.Count(u => u.Faction == Faction.Guard));
            Assert.Equal(11, m.Guards.Count);
        }

        [Fact]
        public void SecondMenOnlyWalkAfterBlood()
        {
            UnitData Partner()
            {
                var g = Guard("Zweiter", "Behrens", 21.5, 30.5);
                g.AfterBlood = true;
                return g;
            }
            var peace = Mission(new[] { Agent(2.5) }, Guard("Streife", "Krüger", 20.5, 30.5), Partner());
            peace.Memory = Memory(CityStance.Helps);
            var none = Mission(new[] { Agent(2.5) }, Guard("Streife", "Krüger", 20.5, 30.5), Partner());
            var blood = Mission(new[] { Agent(2.5) }, Guard("Streife", "Krüger", 20.5, 30.5), Partner());
            blood.Memory = Memory(CityStance.Against);
            Assert.Equal(1, NewWorld(peace).Units.Count(u => u.Faction == Faction.Guard));
            Assert.Equal(1, NewWorld(none).Units.Count(u => u.Faction == Faction.Guard));
            Assert.Equal(2, NewWorld(blood).Units.Count(u => u.Faction == Faction.Guard));
        }

        [Fact]
        public void DogsKeepTheirHandlerWhenSecondMenStayHome()
        {
            var partner = Guard("Zweiter", "", 21.5, 30.5);
            partner.AfterBlood = true;
            var dog = Guard("Hund", "", 24.5, 30.5, "hund");
            dog.Handler = "Führer";
            var w = NewWorld(Mission(new[] { Agent(2.5) }, partner, Guard("Führer", "Tietz", 25.5, 30.5, "hundefuehrer"), dog));
            var handler = w.Units.Single(u => u.Name == "Führer");
            Assert.Equal(handler.Id, w.Units.Single(u => u.Name == "Hund").LeashedTo);
        }

        [Fact]
        public void WhereNobodyDiedAMarkedPostLooksAway()
        {
            MissionData M(GuardMemoryData? memory)
            {
                var m = Mission(new[] { Agent(6.5) }, Guard("Posten", "Krüger", 2.5));
                m.Guards[0].AtEase = true;
                m.Memory = memory;
                return m;
            }
            Assert.Equal(GuardRecall.AtEase, NewWorld(M(Memory(CityStance.Helps))).Units[1].Recall);
            Assert.True(NoticeTicks(M(Memory(CityStance.Helps))) > NoticeTicks(M(null)));
            Assert.Equal(NoticeTicks(M(null)), NoticeTicks(M(Memory(CityStance.Silent))));
        }

        [Fact]
        public void AMissionCanMarkAGuardAsIncorruptibleOrInattentive()
        {
            var plain = Mission(new[] { Agent(6.5, skill: World.SkillBookkeeper) }, Guard("Posten", "Lindner", 2.5));
            var marked = Mission(new[] { Agent(6.5, skill: World.SkillBookkeeper) }, Guard("Posten", "Lindner", 2.5));
            marked.Guards[0].NoBribe = true;
            marked.Guards[0].NoticeScale = 150;
            var w = NewWorld(marked);
            Assert.False(w.Bribable(w.Units[1]));
            Assert.False(w.OrderBribe(0, 1));
            Assert.True(NoticeTicks(marked) > NoticeTicks(plain));
        }

        [Fact]
        public void AVeteranTakesThePostIfHeIsStillAlive()
        {
            MissionData M(GuardFate fate)
            {
                var m = Mission(new[] { Agent(30.5) }, Guard("Wallstreife 2", "Olbrich", 2.5));
                m.Guards[0].Returning = "Bartels";
                m.Memory = Memory(CityStance.Helps, ("Bartels", fate));
                return m;
            }
            Assert.Equal("Bartels", NewWorld(M(GuardFate.Unaware)).Units[1].Person);
            Assert.Equal("Olbrich", NewWorld(M(GuardFate.Dead)).Units[1].Person);
            var none = Mission(new[] { Agent(30.5) }, Guard("Wallstreife 2", "Olbrich", 2.5));
            none.Guards[0].Returning = "Bartels";
            Assert.Equal("Olbrich", NewWorld(none).Units[1].Person);
        }

        [Fact]
        public void DeadMenDoNotComeAsReinforcements()
        {
            var m = Mission(new[] { Agent(8.5) }, Guard("Posten", "Krüger", 2.5));
            m.Reinforcements = new ReinforcementData { X = 30.5, Y = 30.5, Sprite = "W", FirstDelay = 1, Interval = 1, Count = 2, Persons = new() { "Kasten", "Dreyer", "Hinz" } };
            m.Memory = Memory(CityStance.Silent, ("Kasten", GuardFate.Dead));
            var w = NewWorld(m);
            w.OrderAttack(0, 1);
            w.Units[0].Hp = 100000;
            Run(w, 400);
            Assert.Equal(new[] { "Dreyer", "Hinz" }, w.Units.Where(u => u.Reinforcement).Select(u => u.Person));
        }

        [Fact]
        public void TheCampaignHandsItsBookToTheNextMission()
        {
            var c = new Campaign { Nights = 1 };
            c.Guards.Add(new GuardMemory { Name = "Wiese", Mission = "mission_01", Fate = GuardFate.Bribed, Night = 1 });
            c.Guards.Add(new GuardMemory { Name = "Krüger", Mission = "mission_01", Fate = GuardFate.Dead, Night = 1 });
            c.Ledger.Add(new LedgerEntry { Name = "Krüger", Side = LedgerSide.Guard, Mission = "mission_01", Night = 1 });
            var memory = GuardMemoryData.From(c);
            Assert.Equal(c.Stance, memory.Stance);
            Assert.Equal(GuardFate.Bribed, memory.Known.Single(k => k.Person == "Wiese").Fate);
            var m = Mission(new[] { Agent(6.5, skill: World.SkillBookkeeper) }, Guard("Hundeführer", "Wiese", 2.5, type: "hundefuehrer"));
            c.Prepare(m, "mission_02");
            Assert.NotNull(m.Memory);
            var fresh = Mission(new[] { Agent(6.5) }, Guard("Posten", "Krüger", 2.5));
            new Campaign().Prepare(fresh, "mission_01");
            Assert.Null(fresh.Memory);
        }

        // ---------- Telefonist und Kaserne ----------

        private static MissionData Station(double agentX = 30.5, int count = 4, int beds = 0, bool wire = false)
        {
            var m = Mission(new[] { Agent(agentX, 5.5, World.SkillBookkeeper) },
                Guard("Posten", "Krüger", 34.5, 5.5, fx: -1),
                Guard("Telefonist", "Behrens", 10.5, 20.5, "telefonist", fx: 0, fy: 1));
            m.Reinforcements = new ReinforcementData
            {
                X = 20.5, Y = 30.5, Sprite = "W", FirstDelay = 8, Interval = 2, Count = count, Phone = "Telefonist", Beds = beds,
            };
            if (wire)
            {
                m.Doors.Add(new DoorData { Name = "Telefondraht", Tiles = new() { new[] { 29.0, 7.0 } }, Wire = true });
            }
            return m;
        }

        private static int Came(World w) => w.Units.Count(u => u.Reinforcement);

        [Fact]
        public void WithACallTheBarracksComeAtOnceAndInFull()
        {
            var w = NewWorld(Station());
            Assert.Equal(PhoneState.Idle, w.Phone);
            w.OrderAttack(0, 1);
            RunUntil(w, () => w.Phone == PhoneState.Dialing);
            int alarm = w.Tick;
            RunUntil(w, () => Came(w) > 0);
            Assert.Equal(PhoneState.Called, w.Phone);
            Assert.Equal(alarm + World.PhoneTicks + World.TickRate, w.Tick);
            w.Units[0].Hp = 100000;
            Run(w, 400);
            Assert.Equal(4, Came(w));
        }

        [Fact]
        public void WithoutACallTheyComeLateAndFew()
        {
            var m = Station();
            var w = NewWorld(m);
            w.Units[2].Hp = 0;
            w.OrderAttack(0, 1);
            RunUntil(w, () => w.Shots.Count > 0);
            int alarm = w.Tick;
            Assert.Equal(PhoneState.Silent, w.Phone);
            w.Units[0].Hp = 100000;
            RunUntil(w, () => Came(w) > 0);
            Assert.Equal(alarm + 16 * World.TickRate, w.Tick);
            Run(w, 600);
            Assert.Equal(2, Came(w));
        }

        [Fact]
        public void SoundAlarmStartsTheCallWithoutAShot()
        {
            var w = NewWorld(Station());
            w.SoundAlarm();
            Assert.Empty(w.Shots);
            Assert.NotEqual(PhoneState.Idle, w.Phone);
            RunUntil(w, () => w.Phone == PhoneState.Called);
        }

        [Fact]
        public void SoundAlarmWithoutAManAtThePhoneLeavesTheLineSilent()
        {
            var w = NewWorld(Station());
            w.Units[2].Hp = 0;
            w.SoundAlarm();
            Assert.Equal(PhoneState.Silent, w.Phone);
        }

        [Fact]
        public void LateValuesCanBeSetInTheMission()
        {
            var m = Station();
            m.Reinforcements!.LateDelay = 12;
            m.Reinforcements.LateCount = 1;
            var w = NewWorld(m);
            w.Units[2].Hp = 0;
            w.OrderAttack(0, 1);
            RunUntil(w, () => w.Shots.Count > 0);
            int alarm = w.Tick;
            w.Units[0].Hp = 100000;
            RunUntil(w, () => Came(w) > 0);
            Assert.Equal(alarm + 12 * World.TickRate, w.Tick);
            Run(w, 600);
            Assert.Equal(1, Came(w));
        }

        [Fact]
        public void StoppingTheOperatorWhileHeDialsStillHelps()
        {
            var w = NewWorld(Station());
            w.OrderAttack(0, 1);
            RunUntil(w, () => w.Phone == PhoneState.Dialing);
            w.Units[2].Hp = 0;
            Run(w, 2);
            Assert.Equal(PhoneState.Silent, w.Phone);
            w.Units[0].Hp = 100000;
            Run(w, 900);
            Assert.Equal(2, Came(w));
        }

        [Fact]
        public void ABoughtOperatorDoesNotCall()
        {
            var m = Station(agentX: 10.5);
            m.Agents[0].Y = 23.5;
            var w = NewWorld(m);
            Assert.True(w.OrderBribe(0, 2));
            RunUntil(w, () => w.Units[2].Bribed);
            Assert.Equal(1000 - GuardType.Telefonist.BribeCost, w.Money);
            Assert.False(w.PhoneWorks);
            w.RaiseAlarm(10500, 20500);
            Run(w, 100);
            Assert.Equal(PhoneState.Idle, w.Phone);
            Assert.Equal(-1, w.TicksUntilReinforcement);
        }

        [Fact]
        public void AnyoneCanCutTheWireAndTheFieldStaysBlocked()
        {
            var m = Station(wire: true);
            m.Agents[0].Skill = "";
            m.Guards[0].X = 37.5;
            m.Guards[0].Facing = new[] { 1.0, 0.0 };
            var w = NewWorld(m);
            Assert.True(w.OrderOpenDoor(0, 0));
            RunUntil(w, () => w.WireCut);
            Assert.True(w.Map.IsBlocked(29, 7));
            Assert.False(w.PhoneWorks);
            Assert.Equal(MissionRating.Unseen, w.Rating);
            w.OrderAttack(0, 1);
            RunUntil(w, () => w.Shots.Count > 0);
            Assert.Equal(PhoneState.Silent, w.Phone);
        }

        [Fact]
        public void TheOperatorCallsWhenHeSeesSomeoneAndStaysAtHisDesk()
        {
            var m = Station(agentX: 10.5);
            m.Agents[0].Y = 24.5;
            var w = NewWorld(m);
            RunUntil(w, () => w.Phone == PhoneState.Dialing);
            Assert.Equal(GuardState.Report, w.Units[2].GuardState);
            Run(w, World.PhoneTicks + 5);
            Assert.Equal(PhoneState.Called, w.Phone);
            Assert.Equal(10, w.Units[2].TileX);
            Assert.Equal(20, w.Units[2].TileY);
            Assert.Empty(w.Shots);
            Assert.Equal(MissionRating.Quiet, w.Rating);
        }

        [Fact]
        public void AnOfficerOnDutyWalksToThePhoneFirst()
        {
            var m = Station();
            m.Guards[1] = Guard("Wachhabender", "Seidel", 10.5, 20.5, "offizier", fx: 0, fy: 1);
            m.Reinforcements!.Phone = "Wachhabender";
            m.Reinforcements.PhoneAt = new[] { 20.5, 20.5 };
            var w = NewWorld(m);
            w.OrderAttack(0, 1);
            RunUntil(w, () => w.Phone == PhoneState.Fetching);
            int alarm = w.Tick;
            w.Units[0].Hp = 100000;
            RunUntil(w, () => w.Phone == PhoneState.Dialing);
            Assert.Equal(w.Tick, w.PhoneSince);
            Assert.Equal((20500, 20500), w.PhoneSpot);
            Assert.True(World.Distance(w.Units[2], new Unit { X = 20500, Y = 20500 }) <= 1500);
            Assert.True(w.Tick - alarm > 3 * World.TickRate, "Der Weg zum Apparat kostet Zeit");
            RunUntil(w, () => w.Phone == PhoneState.Called);
            Run(w, 600);
            Assert.Equal(4, Came(w));
        }

        [Fact]
        public void StoppingTheOfficerOnHisWayKeepsTheBarracksAsleep()
        {
            var m = Station();
            m.Guards[1] = Guard("Wachhabender", "Seidel", 10.5, 20.5, "offizier", fx: 0, fy: 1);
            m.Reinforcements!.Phone = "Wachhabender";
            m.Reinforcements.PhoneAt = new[] { 20.5, 20.5 };
            var w = NewWorld(m);
            w.OrderAttack(0, 1);
            RunUntil(w, () => w.Phone == PhoneState.Fetching);
            Run(w, 20);
            w.Units[2].Hp = 0;
            Run(w, 2);
            Assert.Equal(PhoneState.Silent, w.Phone);
            w.Units[0].Hp = 100000;
            Run(w, 900);
            Assert.Equal(2, Came(w));
        }

        [Fact]
        public void BedsCapTheBarracksEvenWithAManhunt()
        {
            var m = Station(count: 4 + 2, beds: 4);
            var w = NewWorld(m);
            w.OrderAttack(0, 1);
            RunUntil(w, () => w.Phone == PhoneState.Called);
            w.Units[0].Hp = 100000;
            Run(w, 900);
            Assert.Equal(4, Came(w));
            Assert.Equal(4, w.ReinforcementLimit);
        }

        [Fact]
        public void RunnerAndPhoneShareOneBarracks()
        {
            var m = Station(agentX: 8.5);
            m.Guards.Add(Guard("Bote", "Wolff", 5.5, 5.5, "laufbursche"));
            m.Guards[0].X = 25.5;
            var w = NewWorld(m);
            RunUntil(w, () => w.Phone == PhoneState.Called, 900);
            w.Units[0].Hp = 100000;
            Run(w, 1200);
            Assert.Equal(4, Came(w));
        }

        [Fact]
        public void WithoutAPhoneReinforcementsComeAsBefore()
        {
            var m = Station();
            m.Reinforcements!.Phone = "";
            var w = NewWorld(m);
            Assert.Equal(PhoneState.None, w.Phone);
            w.OrderAttack(0, 1);
            RunUntil(w, () => w.Shots.Count > 0);
            int alarm = w.Tick;
            w.Units[0].Hp = 100000;
            RunUntil(w, () => Came(w) > 0);
            Assert.Equal(alarm + 8 * World.TickRate, w.Tick);
            Run(w, 600);
            Assert.Equal(4, Came(w));
        }

        [Fact]
        public void UnknownOperatorFails()
        {
            var m = Station();
            m.Reinforcements!.Phone = "Niemand";
            Assert.Throws<ArgumentException>(() => NewWorld(m));
        }
    }
}
