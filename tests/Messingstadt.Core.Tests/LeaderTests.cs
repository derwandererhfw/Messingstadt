using System.Collections.Generic;
using System.Linq;
using Messingstadt.Core;
using Xunit;

namespace Messingstadt.Core.Tests
{
    /// <summary>Ist die Truppführerin dabei, nehmen Wachen nur den halben Preis.</summary>
    public class LeaderTests
    {
        /// <summary>Freies Feld mit einer Mauer in der Mitte, die die Sicht von Westen nach Osten sperrt. Die Wache steht im Osten und schaut weg.</summary>
        private static World Make(double gretaX, double gretaY, bool withGreta = true, int money = 1000, int guards = 1)
        {
            var map = new MapData { Width = 40, Height = 12 };
            map.Solids.Add(new SolidData { X0 = 14, Y0 = 0, X1 = 15, Y1 = 9 });
            var agents = new List<UnitData> { new() { Name = "Emil", Sprite = "Agent", X = 20.5, Y = 5.5, Skill = World.SkillBookkeeper } };
            if (withGreta)
            {
                agents.Add(new UnitData { Name = "Greta", Sprite = "Agent", X = gretaX, Y = gretaY, Skill = World.SkillLeader });
            }
            var posts = Enumerable.Range(0, guards)
                .Select(i => new UnitData { Name = "W" + i, Sprite = "Wachmann", X = 23.5, Y = 5.5 + 2 * i, Facing = new[] { 1.0, 0.0 } }).ToList();
            return new World(map, new MissionData
            {
                Seed = 7, Agents = agents, Guards = posts, Money = money, Item = new[] { 38.5, 10.5 }, Exit = new ZoneData { X0 = 0, Y0 = 10, X1 = 2, Y1 = 12 },
            });
        }

        private static Unit Guard(World w, int index = 0) => w.Units.Where(u => u.Faction == Faction.Guard).ElementAt(index);

        private static void Bribe(World w, int index = 0)
        {
            Assert.True(w.OrderBribe(0, Guard(w, index).Id));
            for (int i = 0; i < 200 && !Guard(w, index).Bribed; i++)
            {
                w.Step();
            }
            Assert.True(Guard(w, index).Bribed);
        }

        [Fact]
        public void HalfPriceWhenSheStandsInCallingDistance()
        {
            var w = Make(17.5, 5.5);
            Assert.True(w.LeaderBacks(w.Units[0], Guard(w)));
            Assert.Equal(World.BribeCost / 2, w.BribePrice(Guard(w), w.Units[0]));
            Bribe(w);
            Assert.Equal(1000 - World.BribeCost / 2, w.Money);
        }

        [Fact]
        public void FullPriceWhenSheWaitsFarAwayBehindAWall()
        {
            var w = Make(3.5, 5.5);
            Assert.False(w.LeaderBacks(w.Units[0], Guard(w)));
            Bribe(w);
            Assert.Equal(1000 - World.BribeCost, w.Money);
        }

        [Fact]
        public void SeeingTheGuardCountsEvenOutOfCallingDistance()
        {
            // Sechs Felder hinter Emil, aber mit freiem Blick auf die Wache.
            var w = Make(17.5, 10.5);
            Assert.True(World.Distance(w.Units[1], w.Units[0]) > World.LeaderRange);
            Assert.True(w.LeaderBacks(w.Units[0], Guard(w)));
        }

        [Fact]
        public void FullPriceWithoutHerOrWhenSheIsDown()
        {
            var without = Make(0, 0, withGreta: false);
            Assert.Equal(World.BribeCost, without.BribePrice(Guard(without), without.Units[0]));
            var w = Make(17.5, 5.5);
            w.Units[1].Hp = 0;
            Assert.False(w.Units[1].Alive);
            Assert.Equal(World.BribeCost, w.BribePrice(Guard(w), w.Units[0]));
        }

        [Fact]
        public void TheDiscountMakesAGuardAffordable()
        {
            var w = Make(17.5, 5.5, money: World.BribeCost / 2);
            Bribe(w);
            Assert.Equal(0, w.Money);
            var alone = Make(3.5, 5.5, money: World.BribeCost / 2);
            Assert.False(alone.OrderBribe(0, Guard(alone).Id));
        }

        [Fact]
        public void TheSwitchesTurnItOffOrLimitIt()
        {
            var off = Make(17.5, 5.5);
            off.LeaderDiscountPercent = 0;
            Assert.Equal(World.BribeCost, off.BribePrice(Guard(off), off.Units[0]));

            var once = Make(19.5, 6.5, guards: 2);
            once.LeaderDiscountUses = 1;
            Bribe(once, 0);
            Assert.Equal(1, once.LeaderDiscountsUsed);
            Assert.Equal(World.BribeCost, once.BribePrice(Guard(once, 1), once.Units[0]));
        }

        [Fact]
        public void WhoReplacesHerDoesNotKnowTheGuards()
        {
            MissionData Mission() => new()
            {
                Agents = new()
                {
                    new UnitData { Name = "Greta", Sprite = "Agent", X = 1.5, Y = 1.5, Skill = World.SkillLeader, Role = "Truppführerin" },
                    new UnitData { Name = "Emil", Sprite = "Agent", X = 2.5, Y = 1.5, Skill = World.SkillBookkeeper },
                },
            };
            var campaign = new Campaign();
            var first = Mission();
            campaign.Prepare(first);
            Assert.Equal(World.SkillLeader, first.Agents.First(a => a.Name == "Greta").Skill);

            campaign.Agents.First(a => a.Name == "Greta").Dead = true;
            var second = Mission();
            campaign.Prepare(second);
            Assert.DoesNotContain(second.Agents, a => a.Skill == World.SkillLeader);
            Assert.DoesNotContain(campaign.Agents, a => a.Name != "Greta" && a.Skill == World.SkillLeader);
        }
    }
}
