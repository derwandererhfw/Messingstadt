using System.Collections.Generic;
using System.IO;
using System.Linq;
using Messingstadt.Core;
using Xunit;

namespace Messingstadt.Core.Tests
{
    public class EquipmentTests
    {
        private static readonly ItemCatalog Catalog = Loader.ParseCatalog(File.ReadAllText(Path.Combine(RepoRoot(), "data", "waffen.json")));

        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "data", "waffen.json")))
            {
                dir = dir.Parent;
            }
            return dir!.FullName;
        }

        private static UnitData Agent(double x, double y, params string[] items) =>
            new() { Name = "A", Sprite = "Agent", X = x, Y = y, Items = items.ToList() };

        private static UnitData Guard(double x, double y, string type = "") =>
            new() { Name = "W", Sprite = "Wachmann", Type = type, X = x, Y = y, Facing = new[] { 1.0, 0.0 } };

        private static World NewWorld(List<UnitData> agents, List<UnitData> guards, MapData? map = null) => new(
            map ?? new MapData { Width = 30, Height = 12 },
            new MissionData
            {
                Seed = 7, Agents = agents, Guards = guards, Item = new[] { 0.5, 0.5 }, ItemName = "Buch",
                Exit = new ZoneData { X0 = 28, Y0 = 10, X1 = 30, Y1 = 12 }, Catalog = Catalog,
            });

        private static void Run(World w, int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                w.Step();
            }
        }

        /// <summary>Ein Agent, der nicht fällt, damit sich die Waffe in Ruhe messen lässt.</summary>
        private static World Range(string weapon, double guardX, string guardType = "werkschutz")
        {
            var w = NewWorld(new() { Agent(2.5, 5.5, weapon) }, new() { Guard(guardX, 5.5, guardType) });
            w.Units[0].Hp = w.Units[0].MaxHp = 100000;
            w.Units[1].Hp = w.Units[1].MaxHp = 100000;
            return w;
        }

        // ---------- Katalog ----------

        [Fact]
        public void CatalogHasEveryKindWithPriceAndText()
        {
            foreach (ItemKind kind in System.Enum.GetValues<ItemKind>())
            {
                Assert.Contains(Catalog.Items, i => i.Kind == kind);
            }
            foreach (var item in Catalog.Items)
            {
                Assert.False(string.IsNullOrWhiteSpace(item.Name), item.Id);
                Assert.False(string.IsNullOrWhiteSpace(item.Text), item.Id);
                Assert.True(item.Price > 0, item.Id);
            }
            Assert.Equal(Catalog.Items.Count, Catalog.Items.Select(i => i.Id).Distinct().Count());
            Assert.All(ItemCatalog.StarterKit, id => Assert.NotNull(Catalog.Get(id)));
        }

        [Fact]
        public void WeaponsHaveWorkingValues()
        {
            foreach (var item in Catalog.Items.Where(i => i.IsWeapon))
            {
                Assert.True(item.Damage > 0 && item.Range > 0 && item.Cooldown > 0, item.Id);
                Assert.True(item.Kind == ItemKind.Melee ? item.Noise == 0 : item.Noise > 0, item.Id);
            }
        }

        [Fact]
        public void SellingReturnsHalfThePrice()
        {
            Assert.Equal(Catalog.Get("schrotflinte")!.Price / 2, Catalog.SellPrice("schrotflinte"));
            Assert.Equal(0, Catalog.SellPrice("gibtsnicht"));
        }

        // ---------- Kampagne ----------

        private static MissionData SquadMission() => new()
        {
            Seed = 7, Item = new[] { 28.5, 0.5 }, ItemName = "Buch", Exit = new ZoneData { X0 = 0, Y0 = 0, X1 = 2, Y1 = 2 },
            Agents = new()
            {
                new UnitData { Name = "Greta", Sprite = "Agent", X = 0.5, Y = 0.5 },
                new UnitData { Name = "Otto", Sprite = "Agent", X = 1.5, Y = 0.5, Skill = World.SkillLocksmith },
            },
            Catalog = Catalog,
        };

        private static Campaign Prepared(int money = 2000)
        {
            var c = new Campaign { Money = money };
            c.Prepare(SquadMission());
            return c;
        }

        [Fact]
        public void NewAgentsCarryThePistol()
        {
            var c = new Campaign();
            var m = SquadMission();
            c.Prepare(m);
            Assert.Equal(new[] { "pistole" }, c.Agent("Greta").Items);
            Assert.Equal(new[] { "pistole" }, m.Agents[0].Items);
            var w = new World(new MapData { Width = 30, Height = 12 }, m);
            Assert.Equal("pistole", w.Units[0].ActiveItem!.Def.Id);
            Assert.Equal(Catalog.Get("pistole")!.Magazine, w.Units[0].ActiveItem!.Ammo);
        }

        [Fact]
        public void BuyingGoesToTheAgentOrTheStash()
        {
            var c = Prepared(1000);
            Assert.True(c.TryBuy(Catalog, "schrotflinte", "Greta"));
            Assert.True(c.TryBuy(Catalog, "rauchtopf"));
            Assert.Equal(1000 - 350 - 60, c.Money);
            Assert.Equal(new[] { "pistole", "schrotflinte" }, c.Agent("Greta").Items);
            Assert.Equal(new[] { "rauchtopf" }, c.Stash);
        }

        [Fact]
        public void BuyingNeedsMoneyAKnownItemAndAFreeSlot()
        {
            var c = Prepared(500);
            Assert.False(c.TryBuy(Catalog, "maschinenpistole"));
            Assert.False(c.TryBuy(Catalog, "kanone"));
            Assert.True(c.TryBuy(Catalog, "verbandszeug", "Otto"));
            Assert.True(c.TryBuy(Catalog, "verbandszeug", "Otto"));
            Assert.True(c.TryBuy(Catalog, "verbandszeug", "Otto"));
            Assert.Equal(Campaign.ItemSlots, c.Agent("Otto").Items.Count);
            Assert.False(c.TryBuy(Catalog, "verbandszeug", "Otto"));
            Assert.Equal(500 - 3 * Catalog.Get("verbandszeug")!.Price, c.Money);
        }

        [Fact]
        public void SellingFromAgentOrStashReturnsHalf()
        {
            var c = Prepared(1000);
            c.TryBuy(Catalog, "dietrich");
            Assert.True(c.TrySell(Catalog, "pistole", "Greta"));
            Assert.True(c.TrySell(Catalog, "dietrich"));
            Assert.False(c.TrySell(Catalog, "dietrich"));
            Assert.Empty(c.Agent("Greta").Items);
            Assert.Equal(1000 - 80 + 50 + 40, c.Money);
        }

        [Fact]
        public void EquipAndUnequipMoveThroughTheStash()
        {
            var c = Prepared(1000);
            c.TryBuy(Catalog, "totschlaeger");
            Assert.True(c.Equip("Greta", "totschlaeger"));
            Assert.False(c.Equip("Greta", "totschlaeger"));
            Assert.True(c.Unequip("Greta", 0));
            Assert.False(c.Unequip("Greta", 5));
            Assert.Equal(new[] { "totschlaeger" }, c.Agent("Greta").Items);
            Assert.Equal(new[] { "pistole" }, c.Stash);
        }

        [Fact]
        public void OldSaveGivesTheLivingAPistol()
        {
            const string old = "{\"Version\":2,\"Money\":420,\"Agents\":[{\"Name\":\"Greta\",\"Slot\":\"Greta\"},{\"Name\":\"Otto\",\"Slot\":\"Otto\",\"Falls\":2,\"Dead\":true}]}";
            var c = Campaign.Load(old, out var status);
            Assert.Equal(SaveStatus.Migrated, status);
            Assert.Equal(new[] { "pistole" }, c.Agent("Greta").Items);
            Assert.Empty(c.Agent("Otto").Items);
            Assert.Empty(c.Stash);
        }

        [Fact]
        public void CurrentSaveKeepsAnEmptyHand()
        {
            var c = Prepared();
            c.TrySell(Catalog, "pistole", "Greta");
            c.TryBuy(Catalog, "rauchtopf");
            var loaded = Campaign.Load(c.ToJson(), out var status);
            Assert.Equal(SaveStatus.Loaded, status);
            Assert.Empty(loaded.Agent("Greta").Items);
            Assert.Equal(new[] { "rauchtopf" }, loaded.Stash);
        }

        [Fact]
        public void TheDeadLoseTheirGearTheInfirmaryKeepsIt()
        {
            var c = Prepared();
            c.Agent("Otto").Falls = 1;
            c.TryBuy(Catalog, "dietrich", "Otto");
            c.TryBuy(Catalog, "schrotflinte", "Greta");
            var m = SquadMission();
            c.Prepare(m);
            var w = new World(new MapData { Width = 30, Height = 12 }, m);
            foreach (var u in w.Units)
            {
                u.Hp = 0;
            }
            w.Step();
            c.Finish(w);
            Assert.True(c.Agent("Otto").Dead);
            Assert.Empty(c.Agent("Otto").Items);
            // Bei nur zwei Plätzen holt die Kampagne Greta gleich wieder aus der Krankenstube, die Sachen bleiben bei ihr.
            Assert.Equal(1, c.Agent("Greta").Falls);
            Assert.Equal(new[] { "pistole", "schrotflinte" }, c.Agent("Greta").Items);
        }

        [Fact]
        public void UsedItemsAreGoneAfterTheMission()
        {
            var c = Prepared();
            c.TryBuy(Catalog, "verbandszeug", "Greta");
            c.TryBuy(Catalog, "rauchtopf", "Greta");
            var m = SquadMission();
            c.Prepare(m);
            var w = new World(new MapData { Width = 30, Height = 12 }, m);
            w.Units[0].Hp = 40;
            Assert.True(w.OrderUseMedkit(0));
            Run(w, Catalog.Get("verbandszeug")!.DurationTicks);
            c.Abandon(w);
            c.Finish(w);
            Assert.Equal(new[] { "pistole", "rauchtopf" }, c.Agent("Greta").Items);
        }

        // ---------- Einsatz ----------

        [Fact]
        public void WithoutCatalogThePistolNeverReloads()
        {
            var w = new World(new MapData { Width = 30, Height = 12 }, new MissionData
            {
                Seed = 7, Agents = new() { new UnitData { Name = "A", X = 2.5, Y = 5.5 } }, Guards = new() { Guard(8.5, 5.5, "werkschutz") },
                Item = new[] { 0.5, 0.5 }, Exit = new ZoneData { X0 = 28, Y0 = 10, X1 = 30, Y1 = 12 },
            });
            w.Units[0].Hp = w.Units[1].Hp = 100000;
            w.OrderAttack(0, 1);
            Run(w, 200);
            Assert.True(w.Shots.Count(s => s.ShooterId == 0) > 10);
            Assert.Equal(0, w.Units[0].ReloadTicks);
        }

        [Fact]
        public void PistolReloadsWhenTheMagazineIsEmpty()
        {
            var w = Range("pistole", 8.5);
            w.OrderAttack(0, 1);
            var ticks = new List<int>();
            for (int i = 0; i < 300; i++)
            {
                int before = w.Shots.Count(s => s.ShooterId == 0);
                w.Step();
                if (w.Shots.Count(s => s.ShooterId == 0) > before)
                {
                    ticks.Add(w.Tick);
                }
            }
            int mag = Catalog.Get("pistole")!.Magazine;
            Assert.True(ticks.Count > mag + 1);
            int pause = ticks[mag] - ticks[mag - 1];
            Assert.True(pause >= Catalog.Get("pistole")!.ReloadTicks, $"Pause {pause}");
            Assert.True(ticks[mag - 1] - ticks[mag - 2] < pause);
        }

        [Fact]
        public void ShotgunOnlyFiresUpCloseAndHitsHard()
        {
            var w = Range("schrotflinte", 9.5);
            w.OrderAttack(0, 1);
            int hp = w.Units[1].Hp;
            for (int i = 0; i < 200 && !w.Shots.Any(s => s.ShooterId == 0); i++)
            {
                w.Step();
            }
            var first = w.Shots.First(s => s.ShooterId == 0);
            Assert.Equal("schrotflinte", first.Weapon);
            int dx = first.ToX - first.FromX, dy = first.ToY - first.FromY;
            Assert.True(dx * dx + dy * dy <= 4000 * 4000, $"aus {dx}");
            Assert.True(!first.Hit || hp - w.Units[1].Hp == Catalog.Get("schrotflinte")!.Damage);
        }

        [Fact]
        public void MachinePistolFiresMoreOftenThanThePistol()
        {
            int Count(string weapon)
            {
                var w = Range(weapon, 6.5);
                w.OrderAttack(0, 1);
                Run(w, 100);
                return w.Shots.Count(s => s.ShooterId == 0);
            }
            Assert.True(Count("maschinenpistole") > 2 * Count("pistole"));
        }

        [Fact]
        public void LouderWeaponsAreHeardFurther()
        {
            // Eine Wache 9 Felder vom Schützen: die Pistole hört sie nicht, die Maschinenpistole schon.
            bool Heard(string weapon)
            {
                var w = NewWorld(new() { Agent(2.5, 5.5, weapon) }, new() { Guard(6.5, 5.5, "werkschutz"), Guard(11.5, 1.5) });
                w.Units[0].Hp = w.Units[1].Hp = 100000;
                w.OrderAttack(0, 1);
                for (int i = 0; i < 40; i++)
                {
                    // Das Ziel schießt nicht zurück, sonst hörte die zweite Wache dessen Schüsse.
                    w.Units[1].Cooldown = 1000;
                    w.Step();
                }
                return w.Units[2].Alerted;
            }
            Assert.False(Heard("pistole"));
            Assert.True(Heard("maschinenpistole"));
        }

        [Fact]
        public void AKnockedOutGuardLivesAndIsNotCountedAsDead()
        {
            var w = NewWorld(new() { Agent(5.5, 5.5, "totschlaeger") }, new() { Guard(6.5, 5.5), Guard(15.5, 5.5) });
            Assert.True(w.OrderAttack(0, 1));
            Run(w, 40);
            var down = w.Units[1];
            Assert.True(down.KnockedOut);
            Assert.False(down.Alive);
            Assert.False(down.Dead);
            Assert.Equal(GuardFate.KnockedOut, World.FateOf(down));
            // Er steht nicht wieder auf, und der Agent schlägt nicht weiter auf ihn ein.
            Assert.Single(w.Strikes);
            Assert.Equal(-1, w.Units[0].TargetId);
            Assert.Equal(MissionRating.Unseen, w.Rating);
            Assert.Equal(0, w.Units.Count(u => u.Faction == Faction.Guard && u.Dead));
        }

        [Fact]
        public void ShootingAKnockedOutGuardKillsHim()
        {
            var w = NewWorld(new() { Agent(5.5, 5.5, "totschlaeger", "pistole") }, new() { Guard(6.5, 5.5), Guard(25.5, 5.5) });
            Assert.True(w.OrderAttack(0, 1));
            Run(w, 10);
            Assert.True(w.Units[1].KnockedOut);
            w.SelectWeapon(0, 1);
            Assert.True(w.OrderAttack(0, 1));
            Run(w, 200);
            Assert.NotEmpty(w.Shots);
            Assert.True(w.Units[1].Dead);
            Assert.False(w.Units[1].KnockedOut);
            Assert.Equal(GuardFate.Dead, World.FateOf(w.Units[1]));
            Assert.Equal(MissionRating.Loud, w.Rating);
        }

        [Fact]
        public void BlackjackKnocksOutAnUnawareGuardWithoutNoise()
        {
            var w = NewWorld(new() { Agent(5.5, 5.5, "totschlaeger") }, new() { Guard(6.5, 5.5), Guard(9.5, 5.5) });
            Assert.True(w.OrderAttack(0, 1));
            w.Step();
            Assert.False(w.Units[1].Alive);
            Assert.Empty(w.Shots);
            Assert.Single(w.Strikes);
            Assert.True(w.Strikes[0].KnockOut);
            Run(w, 20);
            Assert.False(w.Units[2].Alerted);
            Assert.Equal(MissionRating.Unseen, w.Rating);
        }

        [Fact]
        public void ArmourShrugsOffTheBlackjack()
        {
            var w = NewWorld(new() { Agent(5.5, 5.5, "totschlaeger") }, new() { Guard(6.5, 5.5, "werkschutz") });
            w.OrderAttack(0, 1);
            w.Step();
            Assert.True(w.Units[1].Alive);
            Assert.False(w.Strikes[0].KnockOut);
            Assert.Equal(GuardState.Combat, w.Units[1].GuardState);
            Assert.DoesNotContain(w.Shots, s => s.ShooterId == 0);
        }

        [Fact]
        public void SwitchingWeaponsTakesAMoment()
        {
            var w = NewWorld(new() { Agent(5.5, 5.5, "pistole", "verbandszeug", "totschlaeger") }, new());
            var a = w.Units[0];
            Assert.Equal("pistole", a.ActiveItem!.Def.Id);
            Assert.True(w.CycleWeapon(0));
            Assert.Equal("totschlaeger", a.ActiveItem!.Def.Id);
            Assert.True(a.Cooldown >= World.SwitchTicks);
            Assert.True(w.CycleWeapon(0));
            Assert.Equal("pistole", a.ActiveItem!.Def.Id);
            Assert.False(w.SelectWeapon(0, 1));
        }

        [Fact]
        public void SwitchingWithOneWeaponDoesNothing()
        {
            var w = NewWorld(new() { Agent(5.5, 5.5, "pistole", "dietrich") }, new());
            Assert.False(w.CycleWeapon(0));
        }

        [Fact]
        public void EmptyHandsCannotAttack()
        {
            var w = NewWorld(new() { Agent(5.5, 5.5, "dietrich") }, new() { Guard(7.5, 5.5) });
            w.OrderAttack(0, 1);
            Run(w, 40);
            Assert.DoesNotContain(w.Shots, s => s.ShooterId == 0);
            Assert.Empty(w.Strikes);
        }

        [Fact]
        public void SmokeBlocksSightUntilItClears()
        {
            var w = NewWorld(new() { Agent(2.5, 5.5, "rauchtopf") }, new() { Guard(8.5, 5.5) });
            Assert.True(w.CanSee(w.Units[1], w.Units[0], 10000));
            Assert.False(w.OrderThrowSmoke(0, 12, 5));
            Assert.True(w.OrderThrowSmoke(0, 5, 5));
            Assert.Empty(w.Units[0].Items);
            Assert.False(w.CanSee(w.Units[1], w.Units[0], 10000));
            Assert.Null(w.WatcherOf(2, 5));
            Run(w, Catalog.Get("rauchtopf")!.DurationTicks);
            Assert.Empty(w.Smokes);
            Assert.True(w.CanSee(w.Units[1], w.Units[0], 10000));
        }

        [Fact]
        public void BandageHealsOnceAndOnlyTheWounded()
        {
            var w = NewWorld(new() { Agent(2.5, 5.5, "pistole", "verbandszeug") }, new());
            var a = w.Units[0];
            Assert.False(w.OrderUseMedkit(0));
            a.Hp = 30;
            Assert.True(w.OrderUseMedkit(0));
            Run(w, Catalog.Get("verbandszeug")!.DurationTicks - 1);
            Assert.Equal(30, a.Hp);
            w.Step();
            Assert.Equal(70, a.Hp);
            Assert.False(w.OrderUseMedkit(0));
            Assert.Equal("pistole", a.ActiveItem!.Def.Id);
        }

        private static MapData Wall()
        {
            var map = new MapData { Width = 30, Height = 12 };
            map.Solids.Add(new SolidData { X0 = 10, Y0 = 0, X1 = 11, Y1 = 12 });
            return map;
        }

        private static int TicksToOpen(UnitData agent)
        {
            var w = new World(Wall(), new MissionData
            {
                Seed = 7, Agents = new() { agent }, Item = new[] { 0.5, 0.5 }, Catalog = Catalog,
                Exit = new ZoneData { X0 = 28, Y0 = 10, X1 = 30, Y1 = 12 },
                Doors = new() { new DoorData { Name = "Tür", Tiles = new() { new[] { 10.0, 5.0 } } } },
            });
            if (!w.OrderOpenDoor(0, 0))
            {
                return -1;
            }
            for (int i = 1; i < 400; i++)
            {
                w.Step();
                if (w.Doors[0].Open)
                {
                    return i;
                }
            }
            return 999;
        }

        [Fact]
        public void LockpickHelpsEveryoneAtTheDoor()
        {
            int plain = TicksToOpen(Agent(9.5, 5.5));
            int locksmith = TicksToOpen(new UnitData { Name = "O", X = 9.5, Y = 5.5, Skill = World.SkillLocksmith });
            int both = TicksToOpen(new UnitData { Name = "O", X = 9.5, Y = 5.5, Skill = World.SkillLocksmith, Items = new() { "dietrich" } });
            int amateur = TicksToOpen(Agent(9.5, 5.5, "dietrich"));
            Assert.Equal(-1, plain);
            Assert.True(both < locksmith && locksmith < amateur, $"{both} {locksmith} {amateur}");
            Assert.InRange(both, World.DoorTicks / 2, World.DoorTicks / 2 + 2);
            Assert.InRange(amateur, 2 * World.DoorTicks - 2, 2 * World.DoorTicks + 2);
        }

        [Fact]
        public void SameOrdersSameOutcomeWithGear()
        {
            ulong Play()
            {
                var w = Range("maschinenpistole", 7.5, "");
                w.OrderAttack(0, 1);
                w.CycleWeapon(0);
                Run(w, 150);
                return w.StateHash();
            }
            Assert.Equal(Play(), Play());
        }

        [Fact]
        public void MovingBreaksOffTheBandage()
        {
            var w = NewWorld(new() { Agent(2.5, 5.5, "verbandszeug") }, new());
            w.Units[0].Hp = 30;
            Assert.True(w.OrderUseMedkit(0));
            Run(w, 10);
            Assert.True(w.OrderMove(0, 6, 5));
            Run(w, 80);
            Assert.Equal(30, w.Units[0].Hp);
            Assert.Single(w.Units[0].Items);
        }
    }
}
