using System;
using System.IO;
using System.Linq;
using Messingstadt.Core;
using Xunit;

namespace Messingstadt.Core.Tests
{
    public class CityMapTests
    {
        private static readonly MapData Map = new() { Width = 30, Height = 12 };

        private static CityMapData City() => new()
        {
            Districts = new()
            {
                new DistrictData { Id = "viertel_1", Name = "Eins", Mission = "mission_01", Reward = 300 },
                new DistrictData { Id = "viertel_2", Name = "Zwei", Mission = "mission_02", Reward = 450, Requires = new() { "viertel_1" } },
                new DistrictData { Id = "viertel_3", Name = "Drei", Requires = new() { "viertel_2" } },
            },
        };

        private static MissionData Mission() => new()
        {
            Seed = 7, Item = new[] { 28.5, 0.5 }, ItemName = "Buch",
            Exit = new ZoneData { X0 = 0, Y0 = 0, X1 = 2, Y1 = 2 },
            Agents = new()
            {
                new UnitData { Name = "Greta", Sprite = "Agent", X = 0.5, Y = 0.5 },
                new UnitData { Name = "Otto", Sprite = "Agent", X = 1.5, Y = 0.5 },
            },
        };

        private static World Play(Campaign c, bool win)
        {
            var m = Mission();
            c.Prepare(m, "mission_01");
            var w = new World(Map, m);
            if (win)
            {
                w.Units[0].CarriesItem = true;
            }
            else
            {
                w.Units[0].Hp = 0;
                w.Units[1].Hp = 0;
            }
            for (int i = 0; i < 40 && w.State == MissionState.Running; i++)
            {
                w.Step();
            }
            Assert.Equal(win ? MissionState.Won : MissionState.Lost, w.State);
            return w;
        }

        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "README.md")))
            {
                dir = dir.Parent;
            }
            return dir!.FullName;
        }

        [Fact]
        public void NewCampaignStartsWithSixHundredAndAllDistrictsBelongToBrandt()
        {
            var c = new Campaign();
            Assert.Equal(600, c.Money);
            Assert.Equal(Campaign.StartMoney, c.Money);
            Assert.All(City().Districts, d => Assert.Equal(DistrictOwner.Brandt, c.Owner(d.Id)));
        }

        [Fact]
        public void OnlyTheFirstDistrictIsOpenAtTheStart()
        {
            var c = new Campaign();
            var city = City();
            Assert.Equal(DistrictState.Open, c.State(city, "viertel_1"));
            Assert.Equal(DistrictState.Locked, c.State(city, "viertel_2"));
            Assert.Equal(DistrictState.NoMission, c.State(city, "viertel_3"));
            Assert.Equal(DistrictState.NoMission, c.State(city, "gibt_es_nicht"));
            Assert.True(c.CanSelect(city, "viertel_1"));
            Assert.False(c.TrySelect(city, "viertel_2"));
            Assert.False(c.TrySelect(city, "viertel_3"));
            Assert.Equal("viertel_1", c.Selected(city)!.Id);
            Assert.Equal("mission_01", c.Selected(city)!.Mission);
        }

        [Fact]
        public void WinningTakesTheDistrictAndOpensTheNext()
        {
            var c = new Campaign();
            var city = City();
            var w = Play(c, win: true);
            c.Finish(w, c.Selected(city));
            Assert.Equal(DistrictOwner.RoterHammer, c.Owner("viertel_1"));
            Assert.Equal(DistrictState.Taken, c.State(city, "viertel_1"));
            Assert.Equal(DistrictState.Open, c.State(city, "viertel_2"));
            Assert.Equal(new[] { "viertel_1" }, c.Conquered);
            // Ein erobertes Viertel bleibt spielbar, wird aber nicht doppelt gezählt.
            Assert.True(c.TrySelect(city, "viertel_1"));
            c.Finish(Play(c, win: true), c.Selected(city));
            Assert.Single(c.Conquered);
        }

        [Fact]
        public void LosingOrAbandoningKeepsTheOwner()
        {
            var c = new Campaign();
            var city = City();
            c.Finish(Play(c, win: false), c.Selected(city));
            Assert.Equal(DistrictOwner.Brandt, c.Owner("viertel_1"));
            c.Abandon(Play(c, win: false));
            Assert.Empty(c.Conquered);
        }

        [Fact]
        public void DistrictWithoutMissionStaysLockedEvenWhenItsTurnComes()
        {
            var c = new Campaign { Conquered = new() { "viertel_1", "viertel_2" } };
            var city = City();
            Assert.Equal(DistrictState.NoMission, c.State(city, "viertel_3"));
            Assert.False(c.CanSelect(city, "viertel_3"));
            // Nichts Offenes mehr: Die Wahl fällt auf das erste spielbare Viertel zurück.
            Assert.Equal("viertel_1", c.Selected(city)!.Id);
        }

        [Fact]
        public void SelectionPrefersTheChosenThenTheFirstOpenDistrict()
        {
            var c = new Campaign { Conquered = new() { "viertel_1" } };
            var city = City();
            Assert.Equal("viertel_2", c.Selected(city)!.Id);
            Assert.True(c.TrySelect(city, "viertel_1"));
            Assert.Equal("viertel_1", c.Selected(city)!.Id);
            c.District = "viertel_3";
            Assert.Equal("viertel_2", c.Selected(city)!.Id);
            Assert.Null(new Campaign().Selected(new CityMapData()));
        }

        [Fact]
        public void DistrictRewardIsTheBaseWage()
        {
            var c = new Campaign { Conquered = new() { "viertel_1" } };
            var city = City();
            Assert.True(c.TrySelect(city, "viertel_2"));
            int before = c.Money;
            c.Finish(Play(c, win: true), c.Selected(city));
            Assert.Equal(before + Campaign.Reward(MissionRating.Unseen, 450) + c.Dues, c.Money);
            Assert.Equal(Campaign.Reward(MissionRating.Loud), Campaign.Reward(MissionRating.Loud, Campaign.BaseReward));
        }

        [Fact]
        public void FinishWithoutDistrictWorksAsBefore()
        {
            var c = new Campaign();
            int before = c.Money;
            c.Finish(Play(c, win: true));
            Assert.Equal(before + Campaign.Reward(MissionRating.Unseen) + Campaign.DuesPerSupport, c.Money);
            Assert.Empty(c.Conquered);
        }

        [Fact]
        public void OwnersAndChoiceSurviveSaving()
        {
            var c = new Campaign { Conquered = new() { "viertel_1" }, District = "viertel_2" };
            var loaded = Campaign.Load(c.ToJson(), out var status);
            Assert.Equal(SaveStatus.Loaded, status);
            Assert.Equal(Campaign.CurrentVersion, loaded.Version);
            Assert.Equal(DistrictOwner.RoterHammer, loaded.Owner("viertel_1"));
            Assert.Equal("viertel_2", loaded.District);
        }

        [Theory]
        [InlineData("{\"Version\":3,\"Money\":650,\"Missions\":1,\"Records\":{\"mission_01\":{\"Attempts\":1,\"Won\":true}}}", true)]
        [InlineData("{\"Version\":2,\"Money\":650,\"Missions\":0,\"Records\":{\"mission_01\":{\"Attempts\":2,\"Won\":true}}}", true)]
        [InlineData("{\"Money\":900,\"Missions\":2}", true)]
        [InlineData("{\"Version\":3,\"Money\":300,\"Missions\":0,\"Records\":{\"mission_01\":{\"Attempts\":1,\"Won\":false}}}", false)]
        [InlineData("{\"Version\":3,\"Money\":300}", false)]
        public void OlderSaveGetsTheFirstDistrictOnlyAfterAWin(string json, bool taken)
        {
            var c = Campaign.Load(json, out var status);
            Assert.Equal(SaveStatus.Migrated, status);
            Assert.Equal(Campaign.CurrentVersion, c.Version);
            Assert.Equal(taken ? DistrictOwner.RoterHammer : DistrictOwner.Brandt, c.Owner(Campaign.FirstDistrict));
            Assert.Equal("", c.District);
        }

        [Fact]
        public void OlderSaveKeepsItsMoney()
        {
            Assert.Equal(300, Campaign.Load("{\"Version\":3,\"Money\":300}", out _).Money);
        }

        [Fact]
        public void BrokenDistrictValuesAreRepaired()
        {
            var c = Campaign.Load("{\"Version\":8,\"Conquered\":[\"viertel_1\",\"\",\"viertel_1\",null],\"District\":null}", out var status);
            Assert.Equal(SaveStatus.Loaded, status);
            Assert.Equal(new[] { "viertel_1" }, c.Conquered);
            Assert.Equal("", c.District);
            Assert.Empty(Campaign.Load("{\"Version\":8,\"Missions\":3,\"Conquered\":null}", out _).Conquered);
        }

        [Fact]
        public void ShippedChainOpensTheNordwallAndStopsAtTheGleisviertel()
        {
            var city = CityMap.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "data", "stadtplan.json")));
            var c = new Campaign();
            Assert.Equal(DistrictState.Locked, c.State(city, "viertel_2"));
            c.Finish(Play(c, win: true), c.Selected(city));
            Assert.Equal(DistrictState.Open, c.State(city, "viertel_2"));
            Assert.True(c.TrySelect(city, "viertel_2"));
            Assert.Equal("mission_02", c.Selected(city)!.Mission);
            int before = c.Money;
            c.Finish(Play(c, win: true), c.Selected(city));
            Assert.Equal(before + Campaign.Reward(MissionRating.Unseen, city.Get("viertel_2")!.Reward) + c.Dues, c.Money);
            Assert.Equal(new[] { "viertel_1", "viertel_2" }, c.Conquered);
            // Das Gleisviertel hat noch keinen Einsatz: sichtbar, aber nicht wählbar. Die eroberten bleiben spielbar.
            Assert.Equal(DistrictState.NoMission, c.State(city, "viertel_3"));
            Assert.False(c.TrySelect(city, "viertel_3"));
            Assert.Equal(DistrictState.NoMission, c.State(city, "viertel_4"));
            Assert.True(c.CanSelect(city, "viertel_1"));
            Assert.True(c.CanSelect(city, "viertel_2"));
            Assert.Equal("viertel_2", c.Selected(city)!.Id);
            Assert.True(c.TrySelect(city, "viertel_1"));
            Assert.Equal("viertel_1", Campaign.FromJson(c.ToJson()).Selected(city)!.Id);
        }

        [Fact]
        public void ShippedCityMapHasFiveDistrictsAndTheFirstTwoHaveAMission()
        {
            var city = CityMap.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "data", "stadtplan.json")));
            Assert.Equal(new[] { "viertel_1", "viertel_2", "viertel_3", "viertel_4", "viertel_5" }, city.Districts.Select(d => d.Id));
            Assert.Equal(Campaign.FirstDistrict, city.Districts[0].Id);
            Assert.Equal("mission_01", city.Districts[0].Mission);
            Assert.True(File.Exists(Path.Combine(RepoRoot(), "data", city.Districts[0].Mission + ".json")));
            Assert.Equal("mission_02", city.Districts[1].Mission);
            Assert.True(File.Exists(Path.Combine(RepoRoot(), "data", city.Districts[1].Mission + ".json")));
            Assert.All(city.Districts.Skip(2), d => Assert.Equal("", d.Mission));
            Assert.All(city.Districts, d => Assert.False(string.IsNullOrWhiteSpace(d.Name) || string.IsNullOrWhiteSpace(d.Kind) || string.IsNullOrWhiteSpace(d.Briefing)));
            Assert.All(city.Districts, d => Assert.True(d.Reward > 0));
            Assert.Equal(new[] { "Kesselviertel", "Nordwall", "Gleisviertel", "Kontorviertel", "Altstadt" }, city.Districts.Select(d => d.Name));
            Assert.All(city.Districts, d => Assert.False(string.IsNullOrWhiteSpace(d.Title) || string.IsNullOrWhiteSpace(d.Time) || string.IsNullOrWhiteSpace(d.Character) || string.IsNullOrWhiteSpace(d.Stakes)));
            // Jedes Viertel mit Voraussetzung bringt seinen Satz für „gesperrt“ mit, das erste braucht keinen.
            Assert.All(city.Districts, d => Assert.Equal(d.Requires.Count > 0, d.Locked.Length > 0));
            Assert.Equal("Kein Weg hinein. Erst den Nordwall halten.", city.Get("viertel_3")!.Locked);
            // Jedes Viertel sperrt das nächste.
            for (int i = 1; i < city.Districts.Count; i++)
            {
                Assert.Equal(new[] { city.Districts[i - 1].Id }, city.Districts[i].Requires);
            }
            Assert.All(city.Districts, d => Assert.All(d.Requires, r => Assert.NotNull(city.Get(r))));

            var c = new Campaign();
            Assert.Equal(new[] { "viertel_1" }, city.Districts.Where(d => c.CanSelect(city, d.Id)).Select(d => d.Id));
            Assert.Equal(Campaign.BaseReward, c.Selected(city)!.Reward);
        }
    }
}
