using System.Collections.Generic;
using System.Linq;
using Messingstadt.Core;
using Xunit;

namespace Messingstadt.Core.Tests
{
    /// <summary>Bewohner, die nicht helfen, rufen einmal. Ob sie rufen, hängt an der Haltung der Stadt.</summary>
    public class ResidentTests
    {
        /// <summary>Freies Feld. Der Bewohner steht bei (10, 5), Greta daneben oder weit weg, die Wachen östlich und schauen weg.</summary>
        private static World Make(CityStance stance, double gretaX = 11.5, int guards = 2, double firstGuardX = 16.5)
        {
            var posts = Enumerable.Range(0, guards)
                .Select(i => new UnitData { Name = "W" + i, Sprite = "Wachmann", X = firstGuardX + 2 * i, Y = 5.5, Facing = new[] { 1.0, 0.0 } }).ToList();
            return new World(new MapData { Width = 40, Height = 12 }, new MissionData
            {
                Seed = 7, Stance = stance, Guards = posts, Item = new[] { 38.5, 10.5 }, Exit = new ZoneData { X0 = 0, Y0 = 10, X1 = 2, Y1 = 12 },
                Agents = new List<UnitData> { new() { Name = "Greta", Sprite = "Agent", X = gretaX, Y = 5.5 } },
                Residents = new List<ResidentData> { new() { Id = "gerichtsdiener", X = 10.5, Y = 5.5 } },
            });
        }

        private static List<Unit> Guards(World w) => w.Units.Where(u => u.Faction == Faction.Guard).ToList();

        [Fact]
        public void NobodyCallsWhileTheCityHelps()
        {
            var w = Make(CityStance.Helps);
            for (int i = 0; i < 40; i++)
            {
                w.Step();
            }
            Assert.Empty(w.ResidentCalls);
            Assert.All(Guards(w), g => Assert.Equal(GuardState.Patrol, g.GuardState));
            Assert.Equal(MissionRating.Unseen, w.Rating);
        }

        [Fact]
        public void WhenTheCityIsSilentHeCallsOnceAndTheNearestGuardLooks()
        {
            var w = Make(CityStance.Silent);
            w.Step();
            var call = Assert.Single(w.ResidentCalls);
            Assert.Equal("gerichtsdiener", call.Resident);
            Assert.Equal(Guards(w)[0].Id, call.GuardId);
            Assert.Equal(GuardState.Investigate, Guards(w)[0].GuardState);
            Assert.Equal(GuardState.Patrol, Guards(w)[1].GuardState);
            Assert.Equal(MissionRating.Quiet, w.Rating);
            for (int i = 0; i < 40; i++)
            {
                w.Step();
            }
            Assert.Single(w.ResidentCalls);
        }

        [Fact]
        public void WhenTheCityIsAgainstTheSquadTwoGuardsLook()
        {
            var w = Make(CityStance.Against);
            w.Step();
            Assert.Equal(2, w.ResidentCalls.Count);
            Assert.All(Guards(w), g => Assert.Equal(GuardState.Investigate, g.GuardState));
        }

        [Fact]
        public void HeDoesNotCallForSomeoneFarAway()
        {
            var w = Make(CityStance.Silent, gretaX: 10.5 + 3);
            w.Step();
            Assert.Empty(w.ResidentCalls);
        }

        [Fact]
        public void WithNoGuardInEarshotTheCallFadesButCounts()
        {
            var w = Make(CityStance.Silent, guards: 1, firstGuardX: 30.5);
            w.Step();
            var call = Assert.Single(w.ResidentCalls);
            Assert.Equal(-1, call.GuardId);
            Assert.Equal(GuardState.Patrol, Guards(w)[0].GuardState);
        }

        [Fact]
        public void TheCampaignCarriesItsStanceIntoTheMission()
        {
            var campaign = new Campaign();
            var first = new MissionData();
            campaign.Prepare(first, "mission_02");
            Assert.Equal(CityStance.Helps, first.Stance);
            campaign.BloodDebt = Campaign.SilentFrom;
            var second = new MissionData();
            campaign.Prepare(second, "mission_02");
            Assert.Equal(CityStance.Silent, second.Stance);
            campaign.BloodDebt = Campaign.AgainstFrom;
            var third = new MissionData();
            campaign.Prepare(third, "mission_02");
            Assert.Equal(CityStance.Against, third.Stance);
        }
    }
}
