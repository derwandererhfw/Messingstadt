using System.Collections.Generic;
using System.Linq;
using Messingstadt.Core;
using Xunit;

namespace Messingstadt.Core.Tests
{
    public class LedgerTests
    {
        private static readonly MapData Map = new() { Width = 30, Height = 12 };

        private static UnitData Guard(string name, double x, string type = "") =>
            new() { Name = name, Sprite = "Wachmann", Type = type, X = x, Y = 9.5, Facing = new[] { 1.0, 0.0 } };

        private static MissionData Mission(params UnitData[] guards) => new()
        {
            Seed = 7, Item = new[] { 28.5, 0.5 }, ItemName = "Buch",
            Exit = new ZoneData { X0 = 0, Y0 = 0, X1 = 2, Y1 = 2 },
            Agents = new()
            {
                new UnitData { Name = "Greta", Sprite = "Agent", X = 0.5, Y = 0.5 },
                new UnitData { Name = "Otto", Sprite = "Agent", X = 1.5, Y = 0.5, Skill = World.SkillLocksmith },
                new UnitData { Name = "Emil", Sprite = "Agent", X = 0.5, Y = 1.5 },
            },
            Guards = guards.ToList(),
        };

        private static UnitData[] Watch() => new[] { Guard("Krüger", 20.5), Guard("Behrens", 22.5), Guard("Pieper", 24.5, "offizier"), Guard("Rex", 26.5, "hund") };

        private static World Start(Campaign c, string mission = "mission_01", string district = "viertel_1")
        {
            var m = Mission(Watch());
            c.District = district;
            c.Prepare(m, mission);
            return new World(Map, m);
        }

        private static Unit Unit(World w, string name) => w.Units.First(u => u.Name == name);

        private static World Win(World w)
        {
            w.Units.First(u => u.Faction == Faction.Agent && u.Alive).CarriesItem = true;
            for (int i = 0; i < 40 && w.State == MissionState.Running; i++)
            {
                w.Step();
            }
            Assert.Equal(MissionState.Won, w.State);
            return w;
        }

        [Fact]
        public void DeadGuardsEnterTheBookWithNameMissionAndDistrict()
        {
            var c = new Campaign();
            var w = Start(c);
            Unit(w, "Krüger").Hp = 0;
            Unit(w, "Pieper").Hp = 0;
            c.Finish(Win(w));
            var dead = c.Dead(LedgerSide.Guard);
            Assert.Equal(new[] { "Krüger", "Pieper" }, dead.Select(e => e.Name));
            Assert.All(dead, e => Assert.Equal(("mission_01", "viertel_1", 1), (e.Mission, e.District, e.Night)));
            Assert.Equal("offizier", dead[1].Kind);
            Assert.True(c.IsDead("Krüger"));
            Assert.True(c.IsDead("Krüger", "mission_01"));
            Assert.False(c.IsDead("Krüger", "mission_02"));
            Assert.False(c.IsDead("Behrens"));
            Assert.Empty(c.Dead(LedgerSide.Squad));
        }

        [Fact]
        public void TheBookKnowsWhoWasBoughtAndWhoSurvived()
        {
            var c = new Campaign();
            var w = Start(c);
            Unit(w, "Krüger").Hp = 0;
            Unit(w, "Behrens").Bribed = true;
            c.Finish(Win(w));
            Assert.Equal(GuardFate.Dead, c.FateOf("Krüger"));
            Assert.Equal(GuardFate.Bribed, c.FateOf("Behrens"));
            Assert.True(c.WasBribed("Behrens"));
            Assert.False(c.WasBribed("Pieper"));
            // Pieper hat im ganzen Einsatz nichts gemerkt: Das unterscheidet der Kern jetzt von „bemerkt und überlebt“.
            Assert.Equal(GuardFate.Unaware, c.FateOf("Pieper"));
            Assert.Null(c.FateOf("Niemand"));
            Assert.Equal(new[] { "Behrens", "Pieper" }, c.Survivors("mission_01").Select(g => g.Name));
            Assert.Empty(c.Survivors("mission_02"));
            // Bestochene und Überlebende stehen nicht im Totenbuch, Hunde gar nicht.
            Assert.Single(c.Ledger);
            Assert.Null(c.FateOf("Rex"));
        }

        [Fact]
        public void KnockedOutGuardsAreNotInTheBook()
        {
            var c = new Campaign();
            c.Prepare(Mission(Watch()), "mission_01");
            var reports = new List<GuardReport>
            {
                new() { Name = "Krüger", Fate = GuardFate.KnockedOut },
                new() { Name = "Behrens", Fate = GuardFate.Unaware },
            };
            c.RememberNight(reports, "viertel_1");
            Assert.Empty(c.Ledger);
            Assert.Equal(GuardFate.KnockedOut, c.FateOf("Krüger"));
            Assert.Equal(GuardFate.Unaware, c.FateOf("Behrens"));
            Assert.Equal(0, c.BloodLast("viertel_1"));
            Assert.Equal(2, c.Survivors("mission_01").Count);
        }

        [Fact]
        public void FallenAgentsStandInTheSameBookOnlyWhenTheyDie()
        {
            var c = new Campaign();
            var first = Start(c);
            Unit(first, "Otto").Hp = 0;
            c.Finish(Win(first));
            Assert.Empty(c.Dead(LedgerSide.Squad));
            c.Agent("Otto").Rest = 0;
            var second = Start(c);
            Unit(second, "Otto").Hp = 0;
            Unit(second, "Krüger").Hp = 0;
            c.Finish(Win(second));
            var otto = Assert.Single(c.Dead(LedgerSide.Squad));
            Assert.Equal(("Otto", "mission_01", 2), (otto.Name, otto.Mission, otto.Night));
            Assert.True(c.IsDead("Otto"));
            Assert.Equal(new[] { "Otto", "Krüger" }, c.DeadLastNight().Select(e => e.Name));
            Assert.Equal("Tote im Trupp: Otto.   Tote der Wache: Krüger.", CampaignTexts.DeadLine(c.DeadLastNight()));
        }

        [Fact]
        public void DeadStayDeadAndAreNotCountedTwiceWhenTheMissionIsPlayedAgain()
        {
            var c = new Campaign();
            for (int round = 0; round < 2; round++)
            {
                var w = Start(c);
                Unit(w, "Krüger").Hp = 0;
                c.Finish(Win(w));
            }
            Assert.Single(c.Ledger);
            Assert.Equal(2, c.Nights);
            Assert.Equal(1, c.Ledger[0].Night);
            // Im dritten Anlauf lebt er in der Mission wieder, im Buch bleibt er tot.
            c.Finish(Win(Start(c)));
            Assert.Equal(GuardFate.Dead, c.FateOf("Krüger"));
            Assert.Empty(c.DeadLastNight());
        }

        [Fact]
        public void BloodIsCountedPerDistrictForTheLastNightAndInTotal()
        {
            var c = new Campaign();
            var w = Start(c);
            Unit(w, "Krüger").Hp = 0;
            Unit(w, "Behrens").Hp = 0;
            c.Finish(Win(w));
            Assert.Equal(2, c.BloodLast("viertel_1"));
            Assert.Equal(0, c.BloodLast("viertel_2"));
            var north = Start(c, "mission_02", "viertel_2");
            Unit(north, "Pieper").Hp = 0;
            c.Finish(Win(north));
            Assert.Equal(1, c.BloodLast("viertel_2"));
            Assert.Equal(2, c.BloodLast("viertel_1"));
            Assert.Equal(3, c.BloodTotal());
            Assert.Equal(2, c.BloodTotal("viertel_1"));
            Assert.Equal(1, c.BloodIn("mission_02"));
            // Ein leiser Einsatz im Viertel: Zuletzt floss dort kein Blut mehr, die Summe bleibt.
            c.Finish(Win(Start(c)));
            Assert.Equal(0, c.BloodLast("viertel_1"));
            Assert.Equal(2, c.BloodTotal("viertel_1"));
        }

        [Fact]
        public void LosingAndAbandoningAfterAShotCountToo()
        {
            var c = new Campaign();
            var w = Start(c);
            Unit(w, "Krüger").Hp = 0;
            foreach (var a in w.Units.Where(u => u.Faction == Faction.Agent))
            {
                a.Hp = 0;
            }
            w.Step();
            Assert.Equal(MissionState.Lost, w.State);
            c.Finish(w);
            Assert.True(c.IsDead("Krüger"));
            Assert.Equal(1, c.BloodLast("viertel_1"));
            Assert.Empty(c.Dead(LedgerSide.Squad));

            // Ein Rückzug vor dem ersten Schuss zählt nicht.
            var calm = Start(c);
            calm.Step();
            c.Abandon(calm);
            Assert.Equal(1, c.Nights);
            Assert.Equal(1, c.BloodLast("viertel_1"));
        }

        /// <summary>Eine Nacht, in der so viele Wachen sterben. Die Namen wechseln, damit jede Nacht neue Tote ins Buch kommen.</summary>
        private static void Night(Campaign c, int dead)
        {
            var guards = Enumerable.Range(0, 5).Select(i => Guard($"N{c.Nights}-{i}", 20.5 + i)).ToArray();
            var m = Mission(guards);
            c.District = "viertel_1";
            c.Prepare(m, "mission_01");
            var w = new World(Map, m);
            foreach (var g in w.Units.Where(u => u.Faction == Faction.Guard).Take(dead))
            {
                g.Hp = 0;
            }
            c.Finish(Win(w));
        }

        private static int[] Debts(params int[] deadPerNight)
        {
            var c = new Campaign();
            return deadPerNight.Select(d => { Night(c, d); return c.BloodDebt; }).ToArray();
        }

        [Fact]
        public void BloodDebtFollowsTheApprovedRule()
        {
            Assert.Equal(new[] { 0, 0, 0, 0, 0, 0 }, Debts(0, 0, 0, 0, 0, 0));
            // Einmal laut, dann leise: nach zwei leisen Nächten getilgt.
            Assert.Equal(new[] { 3, 1, 0, 0, 0, 0 }, Debts(5, 0, 0, 0, 0, 0));
            Assert.Equal(new[] { 3, 1, 4, 2, 5, 3 }, Debts(5, 0, 5, 0, 5, 0));
            // Vier oder mehr je Nacht: schlimmer als 6 wird es nicht.
            Assert.Equal(new[] { 3, 6, 6, 6, 6, 6 }, Debts(4, 4, 4, 4, 4, 4));
            Assert.Equal(new[] { 3, 6, 6, 6, 6, 6 }, Debts(5, 5, 5, 5, 5, 5));
        }

        [Fact]
        public void TheFirstDeadGuardOfANightIsFreeEveryFurtherOneCounts()
        {
            Assert.Equal(new[] { 0, 0, 0, 0, 0, 0 }, Debts(1, 1, 1, 1, 1, 1));
            // Zwei je Nacht sind eine Gewohnheit: gegen den Trupp erst ab der vierten Nacht.
            Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, Debts(2, 2, 2, 2, 2, 2));
            Assert.Equal(new[] { 2, 4, 6, 6 }, Debts(3, 3, 3, 3));
            // Nach einer lauten Nacht tilgt ein einzelner Toter nichts, erhöht aber auch nichts.
            Assert.Equal(new[] { 3, 3, 3, 1 }, Debts(5, 1, 1, 0));
            // Keine einzelne Nacht kippt alles: aus dem Stand höchstens "schweigt".
            Assert.Equal(CityStance.Silent, Campaign.StanceFor(Campaign.DebtAfter(0, 50)));
            Assert.All(Debts(1, 1, 1, 1, 1, 1), d => Assert.Equal(CityStance.Helps, Campaign.StanceFor(d)));
        }

        [Theory]
        [InlineData(0, CityStance.Helps)]
        [InlineData(1, CityStance.Helps)]
        [InlineData(2, CityStance.Silent)]
        [InlineData(3, CityStance.Silent)]
        [InlineData(4, CityStance.Against)]
        [InlineData(6, CityStance.Against)]
        public void StanceHasThreeSteps(int debt, CityStance stance)
        {
            Assert.Equal(stance, Campaign.StanceFor(debt));
            Assert.Equal(stance, new Campaign { BloodDebt = debt }.Stance);
        }

        [Fact]
        public void KnockedOutAndBribedGuardsLeaveNoDebt()
        {
            var c = new Campaign();
            var w = Start(c);
            Unit(w, "Krüger").Hp = 0;
            Unit(w, "Krüger").KnockedOut = true;
            Unit(w, "Behrens").Hp = 0;
            Unit(w, "Behrens").KnockedOut = true;
            Unit(w, "Pieper").Bribed = true;
            c.Finish(Win(w));
            Assert.Equal(0, c.BloodDebt);
            Assert.Empty(c.Ledger);
            Assert.Equal(GuardFate.KnockedOut, c.FateOf("Krüger"));
        }

        [Fact]
        public void KnockedOutGuardsAreNotCountedAsDeadAnywhere()
        {
            var c = new Campaign();
            var w = Start(c);
            foreach (string name in new[] { "Krüger", "Behrens", "Pieper" })
            {
                Unit(w, name).Hp = 0;
                Unit(w, name).KnockedOut = true;
            }
            c.Finish(Win(w));
            // Drei am Boden wären sonst eine blutige Nacht: Fahndung, Merker und Blut im Viertel bleiben bei null.
            Assert.Equal(0, c.Heat);
            Assert.Equal(0, c.Records["mission_01"].DeadGuards);
            Assert.Equal(0, c.BloodLast("viertel_1"));
            Assert.Equal(0, c.BloodTotal());
            Assert.Empty(c.DeadLastNight());

            var bloody = Start(c);
            foreach (string name in new[] { "Krüger", "Behrens", "Pieper" })
            {
                Unit(bloody, name).Hp = 0;
            }
            c.Finish(Win(bloody));
            Assert.Equal(3, c.Records["mission_01"].DeadGuards);
            Assert.Equal(3, c.BloodLast("viertel_1"));
        }

        [Fact]
        public void EveryHouseHelpsWhileTheCityHelps()
        {
            var c = new Campaign();
            Assert.True(c.HouseHelps(Campaign.HelperWidow));
            Assert.True(c.HouseHelps(Campaign.HelperGravedigger));
            Assert.True(c.HouseHelps(Campaign.HelperLandlady));
            Assert.True(c.HouseHelps("baecker", "viertel_3"));
            // Ein einzelner Toter ändert daran nichts.
            Night(c, 1);
            Assert.True(c.HouseHelps(Campaign.HelperWidow));
        }

        [Fact]
        public void WhenTheCityIsSilentOnlyTheLandladyHelpsAndOnlyWhileGretaLives()
        {
            var c = new Campaign();
            Night(c, 4);
            Assert.Equal(CityStance.Silent, c.Stance);
            Assert.False(c.HouseHelps(Campaign.HelperWidow));
            Assert.False(c.HouseHelps(Campaign.HelperGravedigger));
            Assert.False(c.HouseHelps("baecker"));
            Assert.True(c.HouseHelps(Campaign.HelperLandlady));

            var dead = new Campaign { BloodDebt = 3 };
            dead.Ledger.Add(new LedgerEntry { Name = "Greta", Side = LedgerSide.Squad });
            Assert.False(dead.HouseHelps(Campaign.HelperLandlady));
            // Zwei leise Nächte bringen die Türen zurück, auch ohne Greta.
            dead.BloodDebt = Campaign.DebtAfter(Campaign.DebtAfter(3, 0), 0);
            Assert.True(dead.HouseHelps(Campaign.HelperLandlady));
            Assert.True(dead.HouseHelps(Campaign.HelperWidow));
        }

        [Fact]
        public void WhenTheCityIsAgainstTheSquadNoHouseHelps()
        {
            var c = new Campaign { BloodDebt = 4 };
            Assert.Equal(CityStance.Against, c.Stance);
            Assert.False(c.HouseHelps(Campaign.HelperLandlady));
            Assert.False(c.HouseHelps(Campaign.HelperWidow));
        }

        [Fact]
        public void RankAndAgeGoIntoTheBookAndGiveTheTitle()
        {
            var c = new Campaign();
            var guards = new[]
            {
                Guard("Posten 1", 20.5), Guard("Posten 2", 21.5, "offizier"), Guard("Posten 3", 22.5, "werkschutz"),
                Guard("Posten 4", 23.5, "laufbursche"), Guard("Posten 5", 24.5, "offizier"), Guard("Posten 6", 25.5, "laufbursche"),
                Guard("Posten 7", 26.5, "hund"),
            };
            string[] persons = { "Krüger", "Behrens", "Rademacher", "Lüders", "Vogt", "Pagel", "Rex" };
            for (int i = 0; i < guards.Length; i++)
            {
                guards[i].Person = persons[i];
            }
            guards[3].Age = 14;
            guards[4].Rank = "Oberwachtmeister";
            guards[5].Rank = "Bote";
            guards[5].Age = 13;
            var m = Mission(guards);
            c.District = "viertel_2";
            c.Prepare(m, "mission_02");
            var w = new World(Map, m);
            foreach (var g in w.Units.Where(u => u.Faction == Faction.Guard))
            {
                g.Hp = 0;
            }
            var reports = GuardReport.From(w);
            Assert.Equal(new[] { "Wachmann", "Wachtmeister", "Werkschutzmann", "Laufbursche", "Oberwachtmeister", "Bote" }, reports.Select(r => r.Title));
            Assert.Equal(new[] { 0, 0, 0, 14, 0, 13 }, reports.Select(r => r.Age));
            c.Finish(Win(w));
            var book = Campaign.Load(c.ToJson(), out _).Dead(LedgerSide.Guard);
            Assert.Equal(new[] { "Krüger", "Behrens", "Rademacher", "Lüders", "Vogt", "Pagel" }, book.Select(e => e.Name));
            Assert.Equal(new[] { "Wachmann", "Wachtmeister", "Werkschutzmann", "Laufbursche", "Oberwachtmeister", "Bote" }, book.Select(e => e.Title));
            Assert.Equal(13, book[5].Age);
            Assert.Equal("", book[0].Rank);
            Assert.DoesNotContain(book, e => e.Name == "Rex");
        }

        [Fact]
        public void OwnDeadStandInTheBookWithTheirJob()
        {
            var c = new Campaign();
            c.Prepare(Mission(Watch()), "mission_01");
            Assert.Equal("Schlosser", c.Agent("Otto").Role);
            Assert.Equal("Agent", c.Agent("Greta").Role);
            c.Agent("Otto").Falls = 1;
            var w = Start(c);
            Unit(w, "Otto").Hp = 0;
            c.Finish(Win(w));
            var otto = Assert.Single(c.Dead(LedgerSide.Squad));
            Assert.Equal("Schlosser", otto.Title);
            Assert.True(c.TryRecruit("Otto"));
            Assert.Equal("Schlosser", c.Roster()[1].Job);
            c.Prepare(Mission(Watch()), "mission_01");
            Assert.Equal("Schlosser", c.Roster()[1].Role);
        }

        [Fact]
        public void NamelessReinforcementsAreCountedOneByOne()
        {
            var c = new Campaign();
            var m = Mission(Guard("Wachmann", 20.5), Guard("Wachmann", 21.5), Guard("Wachmann", 22.5));
            c.District = "viertel_1";
            c.Prepare(m, "mission_01");
            var w = new World(Map, m);
            foreach (var g in w.Units.Where(u => u.Faction == Faction.Guard))
            {
                g.Hp = 0;
            }
            c.Finish(Win(w));
            Assert.Equal(new[] { "Wachmann", "Wachmann (2)", "Wachmann (3)" }, c.Dead(LedgerSide.Guard).Select(e => e.Name));
            Assert.Equal(2, c.BloodDebt);
        }

        [Fact]
        public void OlderBookGetsItsDebtFromItsNights()
        {
            const string old = "{\"Version\":5,\"Nights\":3,\"Ledger\":[{\"Name\":\"A\",\"Side\":1,\"Night\":1},{\"Name\":\"B\",\"Side\":1,\"Night\":1},{\"Name\":\"C\",\"Side\":1,\"Night\":1},{\"Name\":\"Otto\",\"Side\":0,\"Night\":2},{\"Name\":\"D\",\"Side\":1,\"Night\":3},{\"Name\":\"E\",\"Side\":1,\"Night\":3}]}";
            var c = Campaign.Load(old, out var status);
            Assert.Equal(SaveStatus.Migrated, status);
            // Nacht 1: drei Tote, 2. Nacht 2: keine tote Wache, 0. Nacht 3: zwei Tote, 1.
            Assert.Equal(1, c.BloodDebt);
            Assert.Equal(1, Campaign.Load(c.ToJson(), out _).BloodDebt);
            Assert.Equal(Campaign.MaxBloodDebt, Campaign.Load("{\"Version\":8,\"BloodDebt\":40}", out _).BloodDebt);
            Assert.Equal(0, Campaign.Load("{\"Version\":8,\"BloodDebt\":-4}", out _).BloodDebt);
        }

        [Fact]
        public void TheBookSurvivesSavingAndLoading()
        {
            var c = new Campaign();
            var w = Start(c);
            Unit(w, "Krüger").Hp = 0;
            Unit(w, "Behrens").Bribed = true;
            c.Finish(Win(w));
            var loaded = Campaign.Load(c.ToJson(), out var status);
            Assert.Equal(SaveStatus.Loaded, status);
            Assert.Equal(Campaign.CurrentVersion, loaded.Version);
            Assert.True(loaded.IsDead("Krüger"));
            Assert.True(loaded.WasBribed("Behrens"));
            Assert.Equal(1, loaded.BloodLast("viertel_1"));
            Assert.Equal(1, loaded.Nights);
            Assert.Single(loaded.DeadLastNight());
        }

        [Theory]
        [InlineData("{\"Version\":4,\"Money\":650,\"Missions\":1,\"Records\":{\"mission_01\":{\"Attempts\":1,\"Won\":true,\"DeadGuards\":13}}}")]
        [InlineData("{\"Version\":3,\"Money\":300}")]
        [InlineData("{\"Money\":900,\"Missions\":2}")]
        public void OlderSavesHaveAnEmptyBook(string json)
        {
            var c = Campaign.Load(json, out var status);
            Assert.Equal(SaveStatus.Migrated, status);
            Assert.Equal(Campaign.CurrentVersion, c.Version);
            Assert.Empty(c.Ledger);
            Assert.Empty(c.Guards);
            Assert.Equal(0, c.BloodTotal());
            Assert.True(c.HouseHelps(Campaign.HelperWidow));
            Assert.Equal(0, c.BloodDebt);
        }

        [Fact]
        public void OlderSaveKeepsItsOwnDeadInTheBook()
        {
            const string old = "{\"Version\":4,\"Agents\":[{\"Name\":\"Greta\",\"Slot\":\"Greta\"},{\"Name\":\"Otto\",\"Slot\":\"Otto\",\"Falls\":2,\"Dead\":true},{\"Name\":\"Karl Hoffmann\",\"Slot\":\"Otto\",\"Job\":\"Schlosser\"}]}";
            var c = Campaign.Load(old, out _);
            var otto = Assert.Single(c.Ledger);
            Assert.Equal(("Otto", LedgerSide.Squad, "", 0), (otto.Name, otto.Side, otto.Mission, otto.Night));
            Assert.Empty(c.DeadLastNight());
            // Ein zweites Laden trägt ihn nicht noch einmal ein.
            Assert.Single(Campaign.Load(c.ToJson(), out _).Ledger);
        }

        [Fact]
        public void BrokenBookValuesAreRepaired()
        {
            var c = Campaign.Load("{\"Version\":8,\"Nights\":-3,\"Ledger\":[null,{\"Name\":\"\"},{\"Name\":\"Krüger\",\"Side\":1,\"Mission\":null}],\"Guards\":null,\"LastBlood\":{\"viertel_1\":-2}}", out var status);
            Assert.Equal(SaveStatus.Loaded, status);
            Assert.Equal(0, c.Nights);
            Assert.Equal("", Assert.Single(c.Ledger).Mission);
            Assert.Empty(c.Guards);
            Assert.Equal(0, c.BloodLast("viertel_1"));
            Assert.Empty(Campaign.Load("{\"Version\":8,\"Ledger\":null,\"LastBlood\":null}", out _).Ledger);
        }

        [Fact]
        public void DeadLineNamesAFewAndCountsTheRest()
        {
            var dead = Enumerable.Range(1, 6).Select(i => new LedgerEntry { Name = $"W{i}", Side = LedgerSide.Guard }).ToList();
            Assert.Equal("Tote der Wache: W1, W2, W3, W4 und 2 weitere.", CampaignTexts.DeadLine(dead));
            Assert.Equal("", CampaignTexts.DeadLine(new List<LedgerEntry>()));
        }
    }
}
