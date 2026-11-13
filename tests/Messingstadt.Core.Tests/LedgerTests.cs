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
            // Pieper hat etwas gemerkt und lebt. Wer nichts merkt, gilt als ahnungslos.
            Unit(w, "Pieper").Roused = true;
            c.Finish(Win(w));
            Assert.Equal(GuardFate.Dead, c.FateOf("Krüger"));
            Assert.Equal(GuardFate.Bribed, c.FateOf("Behrens"));
            Assert.True(c.WasBribed("Behrens"));
            Assert.False(c.WasBribed("Pieper"));
            Assert.Equal(GuardFate.Survived, c.FateOf("Pieper"));
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

        [Fact]
        public void HousesInTheNordwallHelpAfterABloodlessFirstMission()
        {
            var c = new Campaign();
            Assert.True(c.HouseHelps(Campaign.HelperWidow));
            c.Finish(Win(Start(c)));
            Assert.True(c.HouseHelps(Campaign.HelperWidow));
            Assert.True(c.HouseHelps(Campaign.HelperGravedigger));
            Assert.True(c.HouseHelps(Campaign.HelperLandlady));
        }

        [Fact]
        public void WidowAndGravediggerCloseTheirDoorsAfterBloodButTheLandladyStandsByGreta()
        {
            var c = new Campaign();
            var w = Start(c);
            Unit(w, "Krüger").Hp = 0;
            c.Finish(Win(w));
            Assert.False(c.HouseHelps(Campaign.HelperWidow));
            Assert.False(c.HouseHelps(Campaign.HelperGravedigger));
            Assert.True(c.HouseHelps(Campaign.HelperLandlady));
            // Ein leiser Einsatz danach macht den Toten nicht lebendig.
            c.Finish(Win(Start(c)));
            Assert.False(c.HouseHelps(Campaign.HelperWidow));

            c.Agent("Greta").Falls = 1;
            var last = Start(c);
            Unit(last, "Greta").Hp = 0;
            c.Finish(Win(last));
            Assert.True(c.IsDead("Greta"));
            Assert.False(c.HouseHelps(Campaign.HelperLandlady));
        }

        [Fact]
        public void OtherHousesFollowTheLastNightInTheirDistrict()
        {
            var c = new Campaign();
            Assert.True(c.HouseHelps("baecker", "viertel_3"));
            var w = Start(c, "mission_03", "viertel_3");
            Unit(w, "Krüger").Hp = 0;
            c.Finish(Win(w));
            Assert.False(c.HouseHelps("baecker", "viertel_3"));
            Assert.True(c.HouseHelps("baecker", "viertel_4"));
            c.Finish(Win(Start(c, "mission_03", "viertel_3")));
            Assert.True(c.HouseHelps("baecker", "viertel_3"));
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
            Assert.Equal(5, c.Version);
            Assert.Empty(c.Ledger);
            Assert.Empty(c.Guards);
            Assert.Equal(0, c.BloodTotal());
            Assert.True(c.HouseHelps(Campaign.HelperWidow));
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
            var c = Campaign.Load("{\"Version\":5,\"Nights\":-3,\"Ledger\":[null,{\"Name\":\"\"},{\"Name\":\"Krüger\",\"Side\":1,\"Mission\":null}],\"Guards\":null,\"LastBlood\":{\"viertel_1\":-2}}", out var status);
            Assert.Equal(SaveStatus.Loaded, status);
            Assert.Equal(0, c.Nights);
            Assert.Equal("", Assert.Single(c.Ledger).Mission);
            Assert.Empty(c.Guards);
            Assert.Equal(0, c.BloodLast("viertel_1"));
            Assert.Empty(Campaign.Load("{\"Version\":5,\"Ledger\":null,\"LastBlood\":null}", out _).Ledger);
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
