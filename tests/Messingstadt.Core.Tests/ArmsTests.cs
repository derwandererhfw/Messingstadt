using System;
using System.IO;
using System.Linq;
using Messingstadt.Core;
using Xunit;

namespace Messingstadt.Core.Tests
{
    /// <summary>Der dritte Ausbau: Arme, die ruhige Hand.</summary>
    public class ArmsTests
    {
        private static readonly MapData Map = new() { Width = 30, Height = 12 };

        private static ItemCatalog Catalog()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "README.md")))
            {
                dir = dir.Parent;
            }
            return Loader.ParseCatalog(File.ReadAllText(Path.Combine(dir!.FullName, "data", "waffen.json")));
        }

        private static MissionData Mission(int arms = 1, bool catalog = false) => new()
        {
            Seed = 7, Item = new[] { 28.5, 0.5 }, ItemName = "Buch",
            Exit = new ZoneData { X0 = 0, Y0 = 0, X1 = 2, Y1 = 2 },
            Catalog = catalog ? Catalog() : null,
            Agents = new()
            {
                new UnitData { Name = "Greta", Sprite = "Agent", X = 0.5, Y = 0.5, Arms = arms, Items = catalog ? new() { "pistole", "maschinenpistole", "totschlaeger" } : null },
                new UnitData { Name = "Otto", Sprite = "Agent", X = 1.5, Y = 0.5, Skill = World.SkillLocksmith },
            },
            Guards = new() { new UnitData { Name = "W", Sprite = "Wachmann", X = 5.5, Y = 0.5, Facing = new[] { 1.0, 0.0 } } },
        };

        private static World Won(Campaign c, Action<World>? before = null)
        {
            var m = Mission();
            c.Prepare(m, "mission_01");
            var w = new World(Map, m);
            before?.Invoke(w);
            w.Units.First(u => u.Faction == Faction.Agent && u.Alive).CarriesItem = true;
            for (int i = 0; i < 40 && w.State == MissionState.Running; i++)
            {
                w.Step();
            }
            Assert.Equal(MissionState.Won, w.State);
            return w;
        }

        [Fact]
        public void EachLevelAimsBetter()
        {
            int[] chance = new[] { 1, 2, 3 }.Select(arms =>
            {
                var w = new World(Map, Mission(arms));
                return w.HitChance(w.Units[0], w.Units[2]);
            }).ToArray();
            Assert.Equal(chance[0] + World.AimPerArm, chance[1]);
            Assert.Equal(chance[0] + 2 * World.AimPerArm, chance[2]);
            Assert.True(World.AimPerArm > 0);
        }

        [Fact]
        public void LevelOneChangesNothingAndTheMissionValueIsClamped()
        {
            var plain = new World(Map, Mission());
            Assert.Equal(1, plain.Units[0].Arms);
            Assert.Equal(0, World.AimBonus(plain.Units[0]));
            Assert.Equal(World.SwitchTicks, World.SwitchTicksFor(plain.Units[0]));
            Assert.Equal(40, World.ReloadTicksFor(plain.Units[0], 40));
            Assert.Equal(3, new World(Map, Mission(9)).Units[0].Arms);
            Assert.Equal(1, new World(Map, Mission(-2)).Units[0].Arms);
            // Wachen haben keine Arme-Stufe.
            Assert.Equal(1, plain.Units[2].Arms);
        }

        [Fact]
        public void HigherLevelsReloadAndSwitchFaster()
        {
            var one = new Unit { Arms = 1 };
            var two = new Unit { Arms = 2 };
            var three = new Unit { Arms = 3 };
            Assert.True(World.ReloadTicksFor(three, 40) < World.ReloadTicksFor(two, 40));
            Assert.True(World.ReloadTicksFor(two, 40) < World.ReloadTicksFor(one, 40));
            Assert.True(World.SwitchTicksFor(three) < World.SwitchTicksFor(one));
            Assert.Equal(1, World.ReloadTicksFor(three, 1));
        }

        [Fact]
        public void SwitchingTheWeaponTakesLessTimeWithBetterArms()
        {
            int[] cooldown = new[] { 1, 3 }.Select(arms =>
            {
                var w = new World(Map, Mission(arms, catalog: true));
                Assert.True(w.SelectWeapon(0, 1));
                return w.Units[0].Cooldown;
            }).ToArray();
            Assert.Equal(World.SwitchTicks, cooldown[0]);
            Assert.True(cooldown[1] < cooldown[0]);
        }

        [Fact]
        public void TheClubDoesNotGetBetter()
        {
            // Der Nahschlag kennt die Arme nicht, damit der leise Weg nicht leichter wird: gleicher Takt, gleiches Ergebnis.
            int[] tick = new[] { 1, 3 }.Select(arms =>
            {
                var w = new World(Map, Mission(arms, catalog: true));
                Assert.True(w.SelectWeapon(0, 2));
                w.Units[0].Cooldown = 0;
                var guard = w.Units[2];
                (w.Units[0].X, w.Units[0].Y) = (guard.X - 800, guard.Y);
                w.OrderAttack(0, guard.Id);
                for (int i = 0; i < 200 && w.Strikes.Count == 0; i++)
                {
                    w.Step();
                }
                Assert.Single(w.Strikes);
                Assert.True(guard.KnockedOut);
                return w.Tick;
            }).ToArray();
            Assert.Equal(tick[0], tick[1]);
        }

        [Fact]
        public void ArmsCostAndRiseLikeLegsAndHeart()
        {
            var c = new Campaign { Money = 5000 };
            c.Prepare(Mission());
            Assert.Equal(1, c.Agent("Otto").Arms);
            Assert.Equal(1, Campaign.Level(c.Agent("Otto"), Upgrade.Arms));
            Assert.True(c.CanUpgrade("Otto", Upgrade.Arms));
            Assert.True(c.TryUpgrade("Otto", Upgrade.Arms));
            Assert.Equal(5000 - Campaign.UpgradeCost(1), c.Money);
            Assert.True(c.TryUpgrade("Otto", Upgrade.Arms));
            Assert.Equal(5000 - Campaign.UpgradeCost(1) - Campaign.UpgradeCost(2), c.Money);
            Assert.Equal(3, c.Agent("Otto").Arms);
            Assert.False(c.CanUpgrade("Otto", Upgrade.Arms));
            Assert.False(c.TryUpgrade("Otto", Upgrade.Arms));
            // Beine und Herz bleiben davon unberührt und gehen weiter über den alten Aufruf.
            Assert.Equal(1, c.Agent("Otto").Legs);
            Assert.True(c.TryUpgrade("Otto", legs: true));
            Assert.Equal(2, Campaign.Level(c.Agent("Otto"), Upgrade.Legs));
            Assert.True(c.TryUpgrade("Otto", Upgrade.Heart));
            Assert.Equal(2, c.Agent("Otto").Heart);
        }

        [Fact]
        public void ArmsNeedMoneyAndALivingAgent()
        {
            var c = new Campaign { Money = 100 };
            c.Prepare(Mission());
            Assert.False(c.CanUpgrade("Otto", Upgrade.Arms));
            c.Money = 5000;
            c.Agent("Otto").Dead = true;
            Assert.False(c.TryUpgrade("Otto", Upgrade.Arms));
        }

        [Fact]
        public void ArmsReachTheNextMissionAndStartFromTheMissionFile()
        {
            var c = new Campaign { Money = 5000 };
            var first = Mission(2);
            c.Prepare(first);
            Assert.Equal(2, c.Agent("Greta").Arms);
            c.TryUpgrade("Greta", Upgrade.Arms);
            var next = Mission();
            c.Prepare(next);
            Assert.Equal(3, new World(Map, next).Units[0].Arms);
        }

        [Fact]
        public void AFallCostsTheArmsLikeLegsAndHeart()
        {
            var c = new Campaign { Money = 5000 };
            c.Prepare(Mission());
            c.TryUpgrade("Otto", Upgrade.Arms);
            c.TryUpgrade("Greta", Upgrade.Arms);
            c.Finish(Won(c, w => w.Units.First(u => u.Name == "Otto").Hp = 0));
            Assert.Equal(1, c.Agent("Otto").Arms);
            Assert.Equal(1, c.Agent("Otto").Falls);
            Assert.Equal(2, c.Agent("Greta").Arms);
        }

        [Fact]
        public void ANewcomerStartsWithArmsAtLevelOne()
        {
            var c = new Campaign { Money = 5000 };
            c.Prepare(Mission());
            c.TryUpgrade("Otto", Upgrade.Arms);
            c.Agent("Otto").Dead = true;
            Assert.True(c.TryRecruit("Otto"));
            Assert.Equal(1, c.Roster()[1].Arms);
        }

        [Fact]
        public void ArmsSurviveSavingAndLoading()
        {
            var c = new Campaign { Money = 5000 };
            c.Prepare(Mission());
            c.TryUpgrade("Otto", Upgrade.Arms);
            var loaded = Campaign.Load(c.ToJson(), out var status);
            Assert.Equal(SaveStatus.Loaded, status);
            Assert.Equal(7, loaded.Version);
            Assert.Equal(2, loaded.Agent("Otto").Arms);
            Assert.Contains("\"Arms\":2", c.ToJson());
        }

        [Theory]
        [InlineData("{\"Version\":6,\"Money\":650,\"Agents\":[{\"Name\":\"Greta\",\"Slot\":\"Greta\",\"Legs\":3,\"Heart\":2},{\"Name\":\"Otto\",\"Slot\":\"Otto\",\"Legs\":1,\"Heart\":3,\"Arms\":3}]}")]
        [InlineData("{\"Version\":2,\"Money\":650,\"Agents\":[{\"Name\":\"Greta\",\"Slot\":\"Greta\",\"Legs\":3,\"Heart\":2},{\"Name\":\"Otto\",\"Slot\":\"Otto\",\"Legs\":1,\"Heart\":3}]}")]
        [InlineData("{\"Money\":650,\"Agents\":[{\"Name\":\"Greta\",\"Legs\":3,\"Heart\":2},{\"Name\":\"Otto\",\"Legs\":1,\"Heart\":3}]}")]
        public void OlderSavesGetArmsAtLevelOneAndKeepEverythingElse(string json)
        {
            var c = Campaign.Load(json, out var status);
            Assert.Equal(SaveStatus.Migrated, status);
            Assert.Equal(Campaign.CurrentVersion, c.Version);
            Assert.All(c.Agents, a => Assert.Equal(1, a.Arms));
            Assert.Equal((3, 2), (c.Agent("Greta").Legs, c.Agent("Greta").Heart));
            Assert.Equal(650, c.Money);
            // Der übernommene Stand lädt danach als aktueller Stand.
            Campaign.Load(c.ToJson(), out var again);
            Assert.Equal(SaveStatus.Loaded, again);
        }

        [Fact]
        public void BrokenArmsValuesAreRepaired()
        {
            var c = Campaign.Load("{\"Version\":7,\"Agents\":[{\"Name\":\"Greta\",\"Arms\":9},{\"Name\":\"Otto\",\"Arms\":-1}]}", out var status);
            Assert.Equal(SaveStatus.Loaded, status);
            Assert.Equal(Campaign.MaxLevel, c.Agent("Greta").Arms);
            Assert.Equal(1, c.Agent("Otto").Arms);
        }

        [Fact]
        public void MissionFilesMayNameArms()
        {
            var m = Loader.ParseMission("{\"agents\":[{\"name\":\"Greta\",\"legs\":2,\"heart\":2,\"arms\":3},{\"name\":\"Otto\"}]}");
            Assert.Equal(3, m.Agents[0].Arms);
            Assert.Equal(1, m.Agents[1].Arms);
        }
    }
}
