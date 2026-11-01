using System.Collections.Generic;
using Messingstadt.Core;
using Xunit;

namespace Messingstadt.Core.Tests
{
    public class SkillTests
    {
        private static UnitData Agent(double x, double y, string skill = "") =>
            new() { Name = "A", Sprite = "Agentin", X = x, Y = y, Skill = skill };

        private static UnitData Guard(double x, double y, double fx, double fy) =>
            new() { Name = "W", Sprite = "Wachmann", X = x, Y = y, Facing = new[] { fx, fy } };

        private static MapData WallMap()
        {
            // Durchgehende Wand bei x = 10, nur die Tür bei y = 5 lässt sich öffnen.
            var map = new MapData { Width = 30, Height = 12 };
            map.Solids.Add(new SolidData { X0 = 10, Y0 = 0, X1 = 11, Y1 = 12 });
            return map;
        }

        private static MissionData Mission(List<UnitData> agents, List<UnitData> guards, int money = 1000) => new()
        {
            Seed = 7, Agents = agents, Guards = guards, Item = new[] { 0.5, 0.5 }, ItemName = "Buch", Money = money,
            Exit = new ZoneData { X0 = 28, Y0 = 10, X1 = 30, Y1 = 12 },
            Doors = new() { new DoorData { Name = "Tür", Tiles = new() { new[] { 10.0, 5.0 } } } },
        };

        private static void Run(World w, int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                w.Step();
            }
        }

        [Fact]
        public void ClosedDoorBlocksTheWay()
        {
            var w = new World(WallMap(), Mission(new() { Agent(5.5, 5.5) }, new()));
            Assert.False(w.OrderMove(0, 15, 5));
        }

        [Fact]
        public void LocksmithOpensDoorAndAnyoneCanPass()
        {
            var w = new World(WallMap(), Mission(new() { Agent(8.5, 5.5, "schlosser"), Agent(5.5, 5.5) }, new()));
            Assert.True(w.OrderOpenDoor(0, 0));
            Run(w, 10 + World.DoorTicks);
            Assert.True(w.Doors[0].Open);
            Assert.True(w.OrderMove(1, 15, 5));
        }

        [Fact]
        public void OnlyLocksmithCanOpenDoors()
        {
            var w = new World(WallMap(), Mission(new() { Agent(8.5, 5.5, "buchhalter") }, new()));
            Assert.False(w.OrderOpenDoor(0, 0));
        }

        [Fact]
        public void OpeningTakesTimeAndStopsWhenOrderedAway()
        {
            var w = new World(WallMap(), Mission(new() { Agent(9.5, 5.5, "schlosser") }, new()));
            w.OrderOpenDoor(0, 0);
            Run(w, World.DoorTicks / 2);
            Assert.False(w.Doors[0].Open);
            Assert.True(w.Doors[0].Progress > 0);
            w.OrderMove(0, 2, 2);
            Run(w, World.DoorTicks * 2);
            Assert.False(w.Doors[0].Open);
        }

        [Fact]
        public void OpenDoorStillBlocksSight()
        {
            var w = new World(WallMap(), Mission(new() { Agent(9.5, 5.5, "schlosser") }, new()));
            w.OrderOpenDoor(0, 0);
            Run(w, World.DoorTicks + 2);
            Assert.False(w.Map.HasLineOfSight(9, 5, 11, 5));
        }

        [Fact]
        public void LootCanBeCollected()
        {
            var mission = Mission(new() { Agent(14.5, 5.5) }, new(), 100);
            mission.Loot = new LootData { Name = "Kasse", X = 15.5, Y = 5.5, Value = 300 };
            var w = new World(WallMap(), mission);
            Assert.True(w.OrderMove(0, 15, 5));
            Run(w, 20);
            Assert.True(w.LootTaken);
            Assert.Equal(400, w.Money);
        }

        [Fact]
        public void BookkeeperBribesGuardWhoThenLooksAway()
        {
            var w = new World(new MapData { Width = 30, Height = 12 }, Mission(new() { Agent(2.5, 5.5, "buchhalter") }, new() { Guard(8.5, 5.5, 1, 0) }));
            Assert.True(w.OrderBribe(0, 1));
            Run(w, 60);
            Assert.True(w.Units[1].Bribed);
            Assert.Equal(1000 - World.BribeCost, w.Money);
            // Der Agent geht direkt vor der bestochenen Wache vorbei.
            w.Units[1].FacingX = -1000;
            w.Units[1].FacingY = 0;
            w.OrderMove(0, 7, 5);
            Run(w, 60);
            Assert.Equal(GuardState.Patrol, w.Units[1].GuardState);
            Assert.False(w.Units[1].Alerted);
        }

        [Fact]
        public void OnlyBookkeeperCanBribe()
        {
            var w = new World(new MapData { Width = 30, Height = 12 }, Mission(new() { Agent(2.5, 5.5, "schlosser") }, new() { Guard(8.5, 5.5, 1, 0) }));
            Assert.False(w.OrderBribe(0, 1));
        }

        [Fact]
        public void BribeNeedsMoney()
        {
            var w = new World(new MapData { Width = 30, Height = 12 }, Mission(new() { Agent(2.5, 5.5, "buchhalter") }, new() { Guard(8.5, 5.5, 1, 0) }, World.BribeCost - 1));
            Assert.False(w.OrderBribe(0, 1));
        }

        [Fact]
        public void FightingGuardCannotBeBribed()
        {
            var w = new World(new MapData { Width = 30, Height = 12 }, Mission(new() { Agent(2.5, 5.5, "buchhalter") }, new() { Guard(8.5, 5.5, 1, 0) }));
            w.Units[1].GuardState = GuardState.Combat;
            Assert.False(w.OrderBribe(0, 1));
        }

        [Fact]
        public void BribeFailsWhenGuardStartsFightingOnTheWay()
        {
            var w = new World(new MapData { Width = 30, Height = 12 }, Mission(new() { Agent(2.5, 5.5, "buchhalter") }, new() { Guard(14.5, 5.5, -1, 0) }));
            w.Units[0].Hp = 100000;
            Assert.True(w.OrderBribe(0, 1));
            w.Units[1].GuardState = GuardState.Combat;
            w.Units[1].Alerted = true;
            Run(w, 60);
            Assert.False(w.Units[1].Bribed);
            Assert.Equal(1000, w.Money);
        }

        [Fact]
        public void BribedGuardIgnoresAlarmAndAgentsDoNotShootHim()
        {
            var w = new World(new MapData { Width = 30, Height = 12 }, Mission(new() { Agent(5.5, 5.5, "buchhalter") }, new() { Guard(7.5, 5.5, 1, 0) }));
            w.OrderBribe(0, 1);
            Run(w, 2);
            Assert.True(w.Units[1].Bribed);
            w.RaiseAlarm(7500, 5500);
            Run(w, 60);
            Assert.False(w.Units[1].Alerted);
            Assert.Empty(w.Shots);
        }

        [Fact]
        public void ShootingABribedGuardEndsTheDeal()
        {
            var w = new World(new MapData { Width = 30, Height = 12 }, Mission(new() { Agent(5.5, 5.5, "buchhalter") }, new() { Guard(7.5, 5.5, 1, 0) }));
            w.OrderBribe(0, 1);
            Run(w, 2);
            w.OrderAttack(0, 1);
            Run(w, 20);
            Assert.False(w.Units[1].Bribed);
        }

        [Fact]
        public void BribingDoesNotCountAsShot()
        {
            var w = new World(new MapData { Width = 30, Height = 12 }, Mission(new() { Agent(5.5, 5.5, "buchhalter") }, new() { Guard(7.5, 5.5, 1, 0) }));
            w.OrderBribe(0, 1);
            Run(w, 5);
            Assert.NotEqual(MissionRating.Loud, w.Rating);
        }
    }

    public class CampaignTests
    {
        private static MissionData Mission() => new()
        {
            Seed = 7, Item = new[] { 28.5, 0.5 }, ItemName = "Buch",
            Exit = new ZoneData { X0 = 0, Y0 = 0, X1 = 2, Y1 = 2 },
            Agents = new()
            {
                new UnitData { Name = "Greta", Sprite = "Agentin", X = 0.5, Y = 0.5, Legs = 2, Heart = 2 },
                new UnitData { Name = "Emil", Sprite = "Buchhalter", X = 1.5, Y = 1.5 },
            },
        };

        private static World Won(MissionData mission)
        {
            var w = new World(new MapData { Width = 30, Height = 12 }, mission);
            w.Units[0].CarriesItem = true;
            for (int i = 0; i < 40 && w.State == MissionState.Running; i++)
            {
                w.Step();
            }
            Assert.Equal(MissionState.Won, w.State);
            return w;
        }

        [Fact]
        public void PrepareUsesMissionValuesFirst()
        {
            var c = new Campaign();
            var m = Mission();
            c.Prepare(m);
            Assert.Equal(2, c.Agent("Greta").Legs);
            Assert.Equal(1, c.Agent("Emil").Heart);
            Assert.Equal(c.Money, m.Money);
        }

        [Fact]
        public void UnseenWinPaysMoreThanLoudWin()
        {
            Assert.True(Campaign.Reward(MissionRating.Unseen) > Campaign.Reward(MissionRating.Quiet));
            Assert.True(Campaign.Reward(MissionRating.Quiet) > Campaign.Reward(MissionRating.Loud));
            Assert.True(Campaign.Reward(MissionRating.Loud) > 0);
        }

        [Fact]
        public void WinAddsReward()
        {
            var c = new Campaign();
            var m = Mission();
            c.Prepare(m);
            int before = c.Money;
            c.Finish(Won(m));
            Assert.Equal(before + Campaign.Reward(MissionRating.Unseen), c.Money);
            Assert.Equal(1, c.Missions);
        }

        [Fact]
        public void LossPaysNothingAndLootIsLostButSpentMoneyStaysSpent()
        {
            var c = new Campaign();
            var m = Mission();
            m.Loot = new LootData { Name = "Kasse", X = 0.5, Y = 0.5, Value = 300 };
            m.Guards = new() { new UnitData { Name = "W", Sprite = "Wachmann", X = 2.5, Y = 2.5, Facing = new[] { -1.0, -1.0 } }, new UnitData { Name = "W", Sprite = "Wachmann", X = 3.5, Y = 3.5, Facing = new[] { -1.0, -1.0 } } };
            c.Prepare(m);
            int before = c.Money;
            var w = new World(new MapData { Width = 30, Height = 12 }, m);
            w.Units[2].Hp = 100000;
            w.Units[3].Hp = 100000;
            for (int i = 0; i < 6000 && w.State == MissionState.Running; i++)
            {
                w.Step();
            }
            Assert.True(w.LootTaken);
            Assert.Equal(MissionState.Lost, w.State);
            c.Finish(w);
            Assert.Equal(before, c.Money);
            Assert.Equal(0, c.Missions);
        }

        [Fact]
        public void UpgradeCostsMoneyAndRaisesLevelUpToThree()
        {
            var c = new Campaign { Money = 5000 };
            c.Prepare(Mission());
            int cost = Campaign.UpgradeCost(1);
            Assert.True(c.TryUpgrade("Emil", legs: true));
            Assert.Equal(2, c.Agent("Emil").Legs);
            Assert.Equal(5000 - cost, c.Money);
            Assert.True(c.TryUpgrade("Emil", legs: true));
            Assert.False(c.TryUpgrade("Emil", legs: true));
            Assert.Equal(3, c.Agent("Emil").Legs);
        }

        [Fact]
        public void UpgradeNeedsMoney()
        {
            var c = new Campaign { Money = 0 };
            c.Prepare(Mission());
            Assert.False(c.TryUpgrade("Emil", legs: false));
            Assert.Equal(1, c.Agent("Emil").Heart);
        }

        [Fact]
        public void UpgradesReachTheNextMission()
        {
            var c = new Campaign { Money = 5000 };
            var m = Mission();
            c.Prepare(m);
            c.TryUpgrade("Emil", legs: false);
            var next = Mission();
            c.Prepare(next);
            var w = new World(new MapData { Width = 30, Height = 12 }, next);
            Assert.Equal(2, w.Units[1].Heart);
        }

        [Fact]
        public void FallenAgentLosesUpgradesAfterAWin()
        {
            var c = new Campaign();
            var m = Mission();
            c.Prepare(m);
            var w = new World(new MapData { Width = 30, Height = 12 }, m);
            w.Units[0].CarriesItem = true;
            w.Units[1].Hp = 0;
            c.Agent("Emil").Legs = 3;
            for (int i = 0; i < 40 && w.State == MissionState.Running; i++)
            {
                w.Step();
            }
            c.Finish(w);
            Assert.Equal(1, c.Agent("Emil").Legs);
            Assert.Equal(2, c.Agent("Greta").Legs);
        }

        [Fact]
        public void DefeatTakesTheUpgradesOfTheFallen()
        {
            var c = new Campaign();
            var m = Mission();
            c.Prepare(m);
            var w = new World(new MapData { Width = 30, Height = 12 }, m);
            w.Units[0].Hp = 0;
            w.Units[1].Hp = 0;
            w.Step();
            Assert.Equal(MissionState.Lost, w.State);
            c.Finish(w);
            Assert.Equal(1, c.Agent("Greta").Legs);
            Assert.Equal(1, c.Agent("Greta").Heart);
        }

        [Fact]
        public void AbandoningBeforeTheFirstShotCostsNoUpgrades()
        {
            var c = new Campaign();
            var m = Mission();
            c.Prepare(m);
            var w = new World(new MapData { Width = 30, Height = 12 }, m);
            w.Step();
            Assert.False(Campaign.AbandonCostsSquad(w));
            c.Abandon(w);
            Assert.Equal(2, c.Agent("Greta").Legs);
            Assert.Equal(0, c.Missions);
        }

        [Fact]
        public void AbandoningAfterAShotCountsAsLosingTheSquad()
        {
            var c = new Campaign();
            var m = Mission();
            m.Guards = new() { new UnitData { Name = "W", Sprite = "Wachmann", X = 3.5, Y = 0.5 } };
            c.Prepare(m);
            var w = new World(new MapData { Width = 30, Height = 12 }, m);
            w.OrderAttack(0, 2);
            for (int i = 0; i < 20 && w.Rating != MissionRating.Loud; i++)
            {
                w.Step();
            }
            Assert.True(Campaign.AbandonCostsSquad(w));
            Assert.True(w.Units[0].Alive);
            c.Abandon(w);
            Assert.Equal(1, c.Agent("Greta").Legs);
            Assert.Equal(1, c.Agent("Greta").Heart);
            Assert.Equal(0, c.Missions);
        }

        [Fact]
        public void SpentMoneyStaysSpentWhenAbandoning()
        {
            var c = new Campaign { Money = 500 };
            var m = Mission();
            m.Agents[1].Skill = World.SkillBookkeeper;
            m.Guards = new() { new UnitData { Name = "W", Sprite = "Wachmann", X = 2.5, Y = 2.5, Facing = new[] { 1.0, 1.0 } } };
            c.Prepare(m);
            var w = new World(new MapData { Width = 30, Height = 12 }, m);
            Assert.True(w.OrderBribe(1, 2));
            w.Step();
            w.Step();
            Assert.True(w.Units[2].Bribed);
            c.Abandon(w);
            Assert.Equal(500 - World.BribeCost, c.Money);
        }

        [Fact]
        public void SavingAndLoadingKeepsEverything()
        {
            var c = new Campaign { Money = 777, Missions = 3 };
            c.Prepare(Mission());
            c.Agent("Emil").Heart = 3;
            var loaded = Campaign.FromJson(c.ToJson());
            Assert.Equal(777, loaded.Money);
            Assert.Equal(3, loaded.Missions);
            Assert.Equal(3, loaded.Agent("Emil").Heart);
        }

        [Fact]
        public void BrokenSaveStartsNewCampaign()
        {
            Assert.Equal(new Campaign().Money, Campaign.FromJson("kein json").Money);
        }
    }
}
