using System.Collections.Generic;
using System.Linq;
using Messingstadt.Core;
using Xunit;

namespace Messingstadt.Core.Tests
{
    public class RescueTests
    {
        /// <summary>Zellenblock bei x 15 bis 18. Zelle 1 öffnet nach Westen, Zelle 2 liegt darunter. Der Wagen steht oben links.</summary>
        private static MapData Map()
        {
            var map = new MapData { Width = 30, Height = 12 };
            map.Solids.Add(new SolidData { X0 = 15, Y0 = 3, X1 = 18, Y1 = 9 });
            return map;
        }

        private static UnitData Agent(double x, double y, string skill = "", params string[] items) =>
            new() { Name = "A" + x, Sprite = "Agent", X = x, Y = y, Skill = skill, Items = items.Length > 0 ? items.ToList() : null };

        private static UnitData Guard(double x, double y, string type = "", params string[] keys) =>
            new() { Name = "W" + x, Sprite = "Wachmann", Type = type, X = x, Y = y, Facing = new[] { 1.0, 0.0 }, Keys = keys.ToList() };

        private static MissionData Mission(List<UnitData> agents, List<UnitData> guards, int prisoners = 2, int money = 1000) => new()
        {
            Seed = 7, Agents = agents, Guards = guards, Money = money, Item = new[] { 0.5, 0.5 },
            Exit = new ZoneData { X0 = 0, Y0 = 0, X1 = 3, Y1 = 3 },
            Doors = new()
            {
                new DoorData { Name = "Zelle 1", Tiles = new() { new[] { 15.0, 5.0 }, new[] { 16.0, 5.0 } } },
                new DoorData { Name = "Zelle 2", Tiles = new() { new[] { 15.0, 7.0 }, new[] { 16.0, 7.0 } } },
            },
            Prisoners = new List<PrisonerData>
            {
                new() { Name = "Anton Weiß", Short = "WEISS", Sprite = "Weiss", X = 16.5, Y = 5.5, Door = "Zelle 1" },
                new() { Name = "Paul Roth", Short = "ROTH", Sprite = "Roth", X = 16.5, Y = 7.5, Door = "Zelle 2" },
            }.Take(prisoners).ToList(),
            Reinforcements = new ReinforcementData { X = 28.5, Y = 10.5, Sprite = "Werkschutz", Type = "werkschutz", FirstDelay = 1, Interval = 1, Count = 1 },
        };

        private static void Run(World w, int ticks)
        {
            for (int i = 0; i < ticks && w.State == MissionState.Running; i++)
            {
                w.Step();
            }
        }

        private static Unit Weiss(World w) => w.Prisoners.First(p => p.Name == "Anton Weiß");

        private static Unit Roth(World w) => w.Prisoners.First(p => p.Name == "Paul Roth");

        /// <summary>Der Schlosser öffnet Zelle 1 und bleibt davor stehen, bis Weiß ihm folgt.</summary>
        private static World FreedWeiss(List<UnitData>? guards = null, int prisoners = 2)
        {
            var w = new World(Map(), Mission(new() { Agent(13.5, 5.5, World.SkillLocksmith), Agent(5.5, 5.5) }, guards ?? new(), prisoners));
            Assert.True(w.OrderOpenDoor(0, 0));
            Run(w, 20 + World.DoorTicks + World.FreeTicks + 5);
            Assert.False(Weiss(w).Captive);
            return w;
        }

        [Fact]
        public void PrisonersComeAfterGuardsAndStartCaptive()
        {
            var w = new World(Map(), Mission(new() { Agent(5.5, 5.5) }, new() { Guard(25.5, 1.5) }));
            Assert.True(w.IsRescue);
            Assert.Equal(Faction.Guard, w.Units[1].Faction);
            Assert.Equal(2, w.Prisoners.Count());
            Assert.All(w.Prisoners, p => Assert.True(p.Captive && p.Hp == 60 && p.Items.Count == 0));
        }

        [Fact]
        public void NobodyIsFreedThroughAClosedDoor()
        {
            var w = new World(Map(), Mission(new() { Agent(14.5, 5.5) }, new()));
            Run(w, 200);
            Assert.True(Weiss(w).Captive);
            Assert.Equal(0, Weiss(w).FreeProgress);
        }

        [Fact]
        public void OpenCellAndTwoSecondsNextToHimFreeThePrisoner()
        {
            var w = new World(Map(), Mission(new() { Agent(13.5, 5.5, World.SkillLocksmith) }, new()));
            w.OrderOpenDoor(0, 0);
            Run(w, 20 + World.DoorTicks);
            Assert.True(w.Doors[0].Open);
            Assert.True(Weiss(w).Captive);
            Run(w, World.FreeTicks + 2);
            Assert.False(Weiss(w).Captive);
            Assert.Equal(0, Weiss(w).FollowId);
            Assert.True(Roth(w).Captive);
        }

        [Fact]
        public void WalkingPastDoesNotFree()
        {
            var w = new World(Map(), Mission(new() { Agent(13.5, 5.5, World.SkillLocksmith) }, new()));
            w.OrderOpenDoor(0, 0);
            Run(w, 20 + World.DoorTicks + World.FreeTicks / 2);
            w.OrderMove(0, 5, 5);
            Run(w, 100);
            Assert.True(Weiss(w).Captive);
        }

        [Fact]
        public void FreedPrisonerFollowsSlowlyAndStopsNearby()
        {
            var w = FreedWeiss();
            Assert.Equal(World.PrisonerSpeed, World.WalkSpeed(Weiss(w)));
            w.OrderMove(0, 4, 9);
            Run(w, 300);
            Assert.True(World.Distance(w.Units[0], Weiss(w)) <= 2 * World.TileUnits);
            Assert.False(Weiss(w).Moving);
        }

        [Fact]
        public void RightClickSwitchesLeaderOrMakesHimWait()
        {
            var w = FreedWeiss();
            int weiss = Weiss(w).Id;
            Assert.True(w.OrderPrisoner(weiss, 1));
            Assert.Equal(1, Weiss(w).FollowId);
            Assert.True(w.OrderPrisoner(weiss, 1));
            Assert.True(Weiss(w).Waiting);
            var (x, y) = (Weiss(w).X, Weiss(w).Y);
            w.OrderMove(1, 3, 10);
            Run(w, 150);
            Assert.Equal((x, y), (Weiss(w).X, Weiss(w).Y));
            Assert.True(w.OrderPrisoner(weiss, 0));
            Assert.False(Weiss(w).Waiting);
            Assert.False(w.OrderPrisoner(Roth(w).Id, 0));
        }

        [Fact]
        public void WhenTheLeaderFallsHeWaitsForTheNextAgent()
        {
            var w = FreedWeiss();
            w.Units[0].Hp = 0;
            Run(w, 60);
            Assert.Equal(-1, Weiss(w).FollowId);
            w.OrderMove(1, 13, 5);
            Run(w, 200);
            Assert.Equal(1, Weiss(w).FollowId);
        }

        [Fact]
        public void GuardsIgnoreCaptivesButNoticeTheFreed()
        {
            // Die Wache steht vor der offenen Zelle und schaut hinein: Solange er sitzt, geschieht nichts.
            var w = new World(Map(), Mission(new() { Agent(27.5, 10.5) }, new() { Guard(13.5, 5.5) }, prisoners: 1));
            w.Doors[0].Open = true;
            foreach (var (x, y) in w.Doors[0].Tiles)
            {
                w.Map.Unblock(x, y);
            }
            Run(w, 100);
            Assert.Equal(MissionRating.Unseen, w.Rating);
            // Sobald er frei vor der Zelle steht, zählt er wie ein Agent.
            Weiss(w).Captive = false;
            Weiss(w).Waiting = true;
            (Weiss(w).X, Weiss(w).Y) = (14500, 5500);
            Run(w, 60);
            Assert.NotEqual(MissionRating.Unseen, w.Rating);
        }

        [Fact]
        public void AgentsNeverAttackPrisoners()
        {
            var w = FreedWeiss();
            Assert.False(w.OrderAttack(0, Weiss(w).Id));
            Assert.False(w.OrderAttack(0, Roth(w).Id));
            Run(w, 100);
            Assert.Empty(w.Shots);
        }

        [Fact]
        public void GuardsShootAgentsBeforePrisoners()
        {
            // Der Befreite steht näher an der Wache als der Agent, sie nimmt trotzdem den Agenten.
            var w = FreedWeiss(new() { Guard(8.5, 9.5, "werkschutz") });
            w.Units[2].Cooldown = 10000;
            Weiss(w).Waiting = true;
            (Weiss(w).X, Weiss(w).Y) = (10500, 9500);
            (w.Units[0].X, w.Units[0].Y) = (11500, 9500);
            (w.Units[1].X, w.Units[1].Y) = (28500, 1500);
            w.Units[2].GuardState = GuardState.Combat;
            w.Units[2].Cooldown = 0;
            w.Units[0].Hp = 100000;
            Run(w, 60);
            Assert.Equal(60, Weiss(w).Hp);
            Assert.Contains(w.Shots, s => s.ShooterId == 2);
        }

        [Fact]
        public void WinNeedsEveryLivingPrisonerAndAnAgentInTheWagon()
        {
            var w = FreedWeiss(prisoners: 1);
            (w.Units[0].X, w.Units[0].Y) = (1500, 1500);
            Run(w, 2 * World.EscapeTicks);
            Assert.Equal(MissionState.Running, w.State);
            (Weiss(w).X, Weiss(w).Y) = (500, 1500);
            Weiss(w).Path.Clear();
            Run(w, World.EscapeTicks + 2);
            Assert.Equal(MissionState.Won, w.State);
        }

        [Fact]
        public void HeBoardsWhenHisLeaderStopsBesideTheWagonAndAnotherAgentIsAboard()
        {
            // Der Führer hält ein Feld neben dem Wagen, wie es beim Sammeln eines Trupps vorkommt.
            var w = FreedWeiss(prisoners: 1);
            (w.Units[0].X, w.Units[0].Y) = (4500, 1500);
            w.Units[0].Path.Clear();
            (Weiss(w).X, Weiss(w).Y) = (5500, 1500);
            Weiss(w).Path.Clear();
            Run(w, 200);
            Assert.Equal(MissionState.Running, w.State);
            Assert.True(Weiss(w).X >= 3000);
            (w.Units[1].X, w.Units[1].Y) = (1500, 1500);
            w.Units[1].Path.Clear();
            Run(w, 200);
            Assert.Equal(MissionState.Won, w.State);
        }

        [Fact]
        public void HeStaysOutOfSightUntilHeStepsOntoTheThreshold()
        {
            var w = new World(Map(), Mission(new() { Agent(13.5, 5.5, World.SkillLocksmith) }, new()));
            Assert.True(w.HiddenInCell(Weiss(w)));
            Assert.Equal("WEISS", w.PrisonerShort(Weiss(w)));
            Assert.False(w.HiddenInCell(w.Units[0]));
            Assert.True(w.OrderOpenDoor(0, 0));
            Run(w, 20 + World.DoorTicks + 2);
            // Tür offen, er sitzt noch hinten in der Zelle.
            Assert.True(w.Doors[0].Open);
            Assert.True(w.HiddenInCell(Weiss(w)));
            // Der Schlosser bleibt direkt vor der Tür stehen. Weiß kommt trotzdem heraus, statt auf der Schwelle zu warten.
            Run(w, World.FreeTicks + 80);
            Assert.False(Weiss(w).Captive);
            Assert.False(w.HiddenInCell(Weiss(w)));
            Assert.DoesNotContain((Weiss(w).TileX, Weiss(w).TileY), w.Doors[0].Tiles);
            Assert.True(w.HiddenInCell(Roth(w)));
        }

        [Fact]
        public void ADoorOfTheCityStandsOpenWhenSupportIsHighEnough()
        {
            MissionData With(int support, params string[] helpers)
            {
                var m = Mission(new() { Agent(13.5, 5.5) }, new());
                m.Doors[0].Helper = "witwe";
                m.Doors[0].OpenAtSupport = 3;
                m.Support = support;
                m.Helpers = helpers.ToList();
                return m;
            }
            var nobody = new World(Map(), With(5));
            Assert.False(nobody.Doors[0].Open);
            Assert.Null(nobody.Map.FindPath(13, 5, 16, 5));

            var helps = new World(Map(), With(3, "witwe"));
            Assert.True(helps.Doors[0].Open && helps.Doors[0].LeftOpen);
            Assert.NotNull(helps.Map.FindPath(13, 5, 16, 5));
            // Die andere Tür hat keinen Helfer und bleibt zu.
            Assert.False(helps.Doors[1].Open);

            // Sie würde helfen, aber der Rückhalt reicht für diese Tür nicht.
            Assert.False(new World(Map(), With(2, "witwe")).Doors[0].Open);
        }

        [Fact]
        public void TheCampaignOpensTheHelpersDoorsOnlyWithoutBlood()
        {
            MissionData Two()
            {
                var m = Mission(new() { Agent(13.5, 5.5) }, new());
                m.Doors[0].Helper = Campaign.HelperWidow;
                m.Doors[1].Helper = Campaign.HelperLandlady;
                return m;
            }
            var clean = new Campaign();
            var first = Two();
            clean.Prepare(first, "mission_02");
            Assert.Contains(Campaign.HelperWidow, first.Helpers);
            Assert.True(new World(Map(), first).Doors[0].Open);

            var bloody = new Campaign();
            bloody.Prepare(Two(), "mission_01");
            // Blutschuld: Der erste Tote einer Nacht ist frei, erst mehrere lassen die Stadt schweigen.
            bloody.RememberNight(new[] { "Krüger", "Bartels", "Henke" }.Select(n => new GuardReport { Name = n, Fate = GuardFate.Dead }).ToArray(), "mission_01");
            Assert.Equal(CityStance.Silent, bloody.Stance);
            var second = Two();
            bloody.Prepare(second, "mission_02");
            Assert.DoesNotContain(Campaign.HelperWidow, second.Helpers);
            Assert.False(new World(Map(), second).Doors[0].Open);
        }

        [Fact]
        public void TheCampaignHandsItsSupportToTheMission()
        {
            var campaign = new Campaign { Support = 4 };
            var m = Mission(new() { Agent(13.5, 5.5) }, new());
            campaign.Prepare(m);
            Assert.Equal(4, m.Support);
        }

        [Fact]
        public void APrisonerStillInHisCellBlocksTheWin()
        {
            var w = FreedWeiss();
            (w.Units[0].X, w.Units[0].Y) = (1500, 1500);
            (Weiss(w).X, Weiss(w).Y) = (500, 1500);
            Weiss(w).Path.Clear();
            Run(w, 3 * World.EscapeTicks);
            Assert.Equal(MissionState.Running, w.State);
            Assert.Equal(0, w.EscapeProgress);
        }

        [Fact]
        public void PrisonersAloneInTheWagonDoNotWin()
        {
            var w = FreedWeiss(prisoners: 1);
            Weiss(w).Waiting = true;
            (Weiss(w).X, Weiss(w).Y) = (500, 1500);
            Run(w, 3 * World.EscapeTicks);
            Assert.Equal(MissionState.Running, w.State);
        }

        [Fact]
        public void OneDeadGoesOnAllDeadIsLost()
        {
            var w = FreedWeiss();
            Roth(w).Hp = 0;
            Run(w, 5);
            Assert.Equal(MissionState.Running, w.State);
            (w.Units[0].X, w.Units[0].Y) = (1500, 1500);
            (Weiss(w).X, Weiss(w).Y) = (500, 1500);
            Weiss(w).Path.Clear();
            Run(w, World.EscapeTicks + 2);
            Assert.Equal(MissionState.Won, w.State);

            var lost = FreedWeiss();
            Roth(lost).Hp = 0;
            Weiss(lost).Hp = 0;
            lost.Step();
            Assert.Equal(MissionState.Lost, lost.State);
        }

        [Fact]
        public void TheItemPlaysNoPartInARescue()
        {
            var w = new World(Map(), Mission(new() { Agent(0.5, 0.5) }, new()));
            Run(w, 3 * World.PickupTicks);
            Assert.False(w.ItemTaken);
            Assert.Equal(MissionState.Running, w.State);
        }

        [Fact]
        public void ABribedKeeperHandsOverHisKeys()
        {
            var w = new World(Map(), Mission(new() { Agent(11.5, 5.5, World.SkillBookkeeper) }, new() { Guard(13.5, 2.5, "", "Zelle 1", "Zelle 2") }));
            Assert.False(w.OrderOpenDoor(0, 0));
            Assert.True(w.OrderBribe(0, 1));
            Run(w, 40);
            Assert.True(w.Units[1].Bribed);
            Assert.Equal(new[] { 0, 1 }, w.Units[0].Keys);
            Assert.True(w.OrderOpenDoor(0, 1));
            int before = w.Tick;
            while (!w.Doors[1].Open && w.Tick < before + 400)
            {
                w.Step();
            }
            Assert.True(w.Doors[1].Open);
            Assert.NotEqual(MissionRating.Loud, w.Rating);
        }

        [Fact]
        public void KeysOpenFasterThanALocksmith()
        {
            var w = new World(Map(), Mission(new() { Agent(14.5, 5.5) }, new()));
            w.Units[0].Keys.Add(0);
            Assert.True(w.OrderOpenDoor(0, 0));
            Run(w, World.DoorTicks / World.KeyDoorStep + 1);
            Assert.True(w.Doors[0].Open);
        }

        [Fact]
        public void AKnockedOutKeeperDropsHisKeysForWhoeverComes()
        {
            var w = new World(Map(), Mission(new() { Agent(12.5, 2.5, "", "totschlaeger"), Agent(3.5, 9.5) }, new() { Guard(13.5, 2.5, "", "Zelle 1") }));
            w.Units[2].Hp = 0;
            w.Units[2].Keys.Clear();
            w.KeysOnGround.Add(new DroppedKeys { X = 13500, Y = 2500, Doors = new() { 0 } });
            Assert.Empty(w.Units[0].Keys);
            w.OrderMove(0, 13, 2);
            Run(w, 40);
            Assert.Equal(new[] { 0 }, w.Units[0].Keys);
            Assert.Empty(w.KeysOnGround);
        }

        [Fact]
        public void AGuardWhoFallsDropsHisKeys()
        {
            var w = new World(Map(), Mission(new() { Agent(10.5, 2.5) }, new() { Guard(13.5, 2.5, "", "Zelle 1", "Zelle 2") }));
            w.Units[0].Hp = 100000;
            w.OrderAttack(0, 1);
            for (int i = 0; i < 2000 && w.Units[1].Alive; i++)
            {
                w.Step();
            }
            Assert.False(w.Units[1].Alive);
            Assert.Single(w.KeysOnGround);
            Assert.Equal(2, w.KeysOnGround[0].Doors.Count);
        }

        [Fact]
        public void ReinforcementsCanBeAnotherKind()
        {
            var w = new World(Map(), Mission(new() { Agent(5.5, 5.5) }, new() { Guard(9.5, 5.5, "werkschutz") }));
            w.Units[0].Hp = 100000;
            w.Units[1].Hp = 100000;
            w.OrderAttack(0, 1);
            Run(w, 100);
            var extra = w.Units.Last(u => u.Faction == Faction.Guard);
            Assert.StartsWith("Verstärkung", extra.Name);
            Assert.Equal(GuardType.Werkschutz, extra.Type);
        }

        [Fact]
        public void TelegramFollowsTheRescue()
        {
            var texts = new RescueTextData();
            var w = new World(Map(), Mission(new() { Agent(13.5, 5.5, World.SkillLocksmith) }, new()));
            Assert.Equal(texts.Start, w.RescueLine());
            w.OrderOpenDoor(0, 0);
            Run(w, 20);
            Assert.Equal(texts.Opening, w.RescueLine());
            Run(w, World.DoorTicks + World.FreeTicks + 5);
            Assert.Equal("WEISS FREI STOP DER ZWEITE FEHLT NOCH STOP", w.RescueLine());
            Roth(w).Captive = false;
            Assert.Equal(texts.AllFree, w.RescueLine());
            Roth(w).Hp = 0;
            Assert.Equal("ROTH TOT STOP DEN ANDEREN RAUSBRINGEN STOP", w.RescueLine());
        }

        [Fact]
        public void TheCampaignRemembersWhoCameOutAlive()
        {
            var c = new Campaign();
            var m = Mission(new() { Agent(13.5, 5.5, World.SkillLocksmith) }, new());
            c.Prepare(m, "mission_02");
            Assert.Null(c.PrisonerAlive("Anton Weiß"));
            var w = new World(Map(), m);
            w.OrderOpenDoor(0, 0);
            Run(w, 20 + World.DoorTicks + World.FreeTicks + 5);
            Roth(w).Hp = 0;
            (w.Units[0].X, w.Units[0].Y) = (1500, 1500);
            (Weiss(w).X, Weiss(w).Y) = (500, 1500);
            Weiss(w).Path.Clear();
            Run(w, World.EscapeTicks + 2);
            Assert.Equal(MissionState.Won, w.State);
            c.Finish(w);
            Assert.True(c.Records["mission_02"].Prisoners["Anton Weiß"]);
            Assert.False(c.Records["mission_02"].Prisoners["Paul Roth"]);
            var loaded = Campaign.FromJson(c.ToJson());
            Assert.True(loaded.PrisonerAlive("Anton Weiß"));
            Assert.False(loaded.PrisonerAlive("Paul Roth"));
            Assert.Equal("Befreit: Anton Weiß. Nicht überlebt: Paul Roth.", CampaignTexts.Rescue(loaded.LastPrisoners));
        }

        [Fact]
        public void TheFirstRescueStandsWhenTheDistrictIsPlayedAgain()
        {
            var c = new Campaign();
            for (int round = 0; round < 2; round++)
            {
                var m = Mission(new() { Agent(13.5, 5.5, World.SkillLocksmith) }, new());
                c.Prepare(m, "mission_02");
                var w = new World(Map(), m);
                w.OrderOpenDoor(0, 0);
                Run(w, 20 + World.DoorTicks + World.FreeTicks + 5);
                // Beim ersten Mal stirbt Roth, beim zweiten Mal Weiß.
                (round == 0 ? Roth(w) : Weiss(w)).Hp = 0;
                var living = round == 0 ? Weiss(w) : Roth(w);
                living.Captive = false;
                (w.Units[0].X, w.Units[0].Y) = (1500, 1500);
                (living.X, living.Y) = (500, 1500);
                living.Path.Clear();
                Run(w, World.EscapeTicks + 2);
                Assert.Equal(MissionState.Won, w.State);
                c.Finish(w);
            }
            Assert.Equal(2, c.Records["mission_02"].Attempts);
            Assert.True(c.PrisonerAlive("Anton Weiß"));
            Assert.False(c.PrisonerAlive("Paul Roth"));
        }

        /// <summary>Befreit Weiß, Roth stirbt dabei, der Einsatz ist gewonnen.</summary>
        private static World RescueWithRothDead(Campaign c, string district = "viertel_2")
        {
            var m = Mission(new() { Agent(13.5, 5.5, World.SkillLocksmith) }, new());
            c.District = district;
            c.Prepare(m, "mission_02");
            var w = new World(Map(), m);
            w.OrderOpenDoor(0, 0);
            Run(w, 20 + World.DoorTicks + World.FreeTicks + 5);
            Roth(w).Hp = 0;
            (w.Units[0].X, w.Units[0].Y) = (1500, 1500);
            (Weiss(w).X, Weiss(w).Y) = (500, 1500);
            Weiss(w).Captive = false;
            Weiss(w).Path.Clear();
            Run(w, World.EscapeTicks + 2);
            Assert.Equal(MissionState.Won, w.State);
            return w;
        }

        [Fact]
        public void APrisonerWhoDiesStandsOnOurSideOfTheBookButIsNoSquadMember()
        {
            var c = new Campaign();
            c.Finish(RescueWithRothDead(c));
            var roth = Assert.Single(c.Dead(LedgerSide.Prisoner));
            Assert.Equal(("Paul Roth", "mission_02", "viertel_2", 1), (roth.Name, roth.Mission, roth.District, roth.Night));
            Assert.Equal(Campaign.PrisonerKind, roth.Kind);
            Assert.Equal("Gefangener", roth.Title);
            Assert.True(roth.Ours);
            Assert.True(c.IsDead("Paul Roth"));
            Assert.False(c.IsDead("Anton Weiß"));
            // Links im Buch und in der Nacht steht er, beim Trupp nicht.
            Assert.Equal(new[] { "Paul Roth" }, c.DeadOurs().Select(e => e.Name));
            Assert.Equal(new[] { "Paul Roth" }, c.DeadLastNight().Select(e => e.Name));
            Assert.Empty(c.Dead(LedgerSide.Squad));
            Assert.Empty(c.LastDeaths);
            Assert.DoesNotContain(c.Agents, a => a.Name == "Paul Roth");
            // Kein Blut, keine Schuld, niemand rückt nach.
            Assert.Equal(0, c.BloodLast("viertel_2"));
            Assert.Equal(0, c.BloodTotal());
            Assert.Equal(0, c.BloodDebt);
            Assert.Empty(c.LastNewcomers);
            Assert.Equal("", CampaignTexts.DeadLine(c.DeadLastNight()));
            Assert.Equal("Gefangener Paul Roth", SequenceTexts.Ledger(c, LedgerSide.Squad));
            Assert.Equal("", SequenceTexts.Ledger(c, LedgerSide.Guard));
        }

        [Fact]
        public void ADeadPrisonerIsWrittenOnceAndSurvivesSaving()
        {
            var c = new Campaign();
            c.Finish(RescueWithRothDead(c));
            c.Finish(RescueWithRothDead(c));
            Assert.Single(c.Dead(LedgerSide.Prisoner));
            // In der zweiten Nacht ist er kein Toter dieser Nacht mehr.
            Assert.Empty(c.DeadLastNight());
            var loaded = Campaign.Load(c.ToJson(), out var status);
            Assert.Equal(SaveStatus.Loaded, status);
            Assert.Equal("Paul Roth", Assert.Single(loaded.DeadOurs()).Name);
            Assert.Equal(LedgerSide.Prisoner, loaded.DeadOurs()[0].Side);
        }

        [Fact]
        public void ALostRescueWritesNoPrisonerIntoTheBook()
        {
            // Nach einer Niederlage sitzen beide wieder in der Zelle, der neue Versuch beginnt mit Lebenden.
            var c = new Campaign();
            var m = Mission(new() { Agent(13.5, 5.5, World.SkillLocksmith) }, new());
            c.Prepare(m, "mission_02");
            var w = new World(Map(), m);
            Roth(w).Hp = 0;
            Weiss(w).Hp = 0;
            w.Step();
            Assert.Equal(MissionState.Lost, w.State);
            c.Finish(w);
            Assert.Empty(c.DeadOurs());
            Assert.Null(c.PrisonerAlive("Paul Roth"));
        }

        [Fact]
        public void OlderSaveMovesItsDeadPrisonerIntoTheBook()
        {
            const string old = "{\"Version\":7,\"Nights\":2,\"Records\":{\"mission_02\":{\"Attempts\":1,\"Won\":true,\"Prisoners\":{\"Anton Weiß\":true,\"Paul Roth\":false}}}}";
            var c = Campaign.Load(old, out var status);
            Assert.Equal(SaveStatus.Migrated, status);
            var roth = Assert.Single(c.DeadOurs());
            Assert.Equal(("Paul Roth", LedgerSide.Prisoner, "mission_02", 0), (roth.Name, roth.Side, roth.Mission, roth.Night));
            Assert.Empty(c.DeadLastNight());
            Assert.Equal(0, c.BloodDebt);
            Assert.Single(Campaign.Load(c.ToJson(), out _).DeadOurs());
        }

        [Fact]
        public void RescueLineNamesTheFreedAndTheDead()
        {
            Assert.Equal("", CampaignTexts.Rescue(new Dictionary<string, bool>()));
            Assert.Equal("Befreit: Anton Weiß, Paul Roth.", CampaignTexts.Rescue(new Dictionary<string, bool> { ["Anton Weiß"] = true, ["Paul Roth"] = true }));
            Assert.Equal("Nicht überlebt: Paul Roth.", CampaignTexts.Rescue(new Dictionary<string, bool> { ["Paul Roth"] = false }));
            Assert.Empty(new Campaign().LastPrisoners);
        }

        [Theory]
        [InlineData("{\"Version\":3,\"Money\":650,\"Missions\":1,\"Records\":{\"mission_01\":{\"Attempts\":1,\"Won\":true}}}", SaveStatus.Migrated)]
        [InlineData("{\"Version\":8,\"Current\":\"mission_02\",\"Records\":{\"mission_02\":{\"Attempts\":1,\"Won\":true,\"Prisoners\":null},\"kaputt\":null}}", SaveStatus.Loaded)]
        [InlineData("{\"Money\":900,\"Missions\":2}", SaveStatus.Migrated)]
        public void SavesWithoutPrisonersLoadCleanly(string json, SaveStatus expected)
        {
            var c = Campaign.Load(json, out var status);
            Assert.Equal(expected, status);
            Assert.Null(c.PrisonerAlive("Anton Weiß"));
            Assert.Empty(c.LastPrisoners);
            Assert.All(c.Records.Values, r => Assert.NotNull(r.Prisoners));
            Assert.DoesNotContain("kaputt", c.Records.Keys);
            // Der Stand lässt sich danach wieder speichern und laden.
            Assert.Equal(SaveStatus.Loaded, Load(c.ToJson()));
        }

        private static SaveStatus Load(string json)
        {
            Campaign.Load(json, out var status);
            return status;
        }

        [Fact]
        public void TheHeadlineMentionsADeadPrisoner()
        {
            World Won(bool rothDies)
            {
                var m = Mission(new() { Agent(13.5, 5.5, World.SkillLocksmith) }, new());
                m.Headlines = new HeadlineData { Unseen = new HeadlineText { Head = "Arrest leer", Line = "Zwei fehlen.{prisoners}" } };
                var w = new World(Map(), m);
                w.OrderOpenDoor(0, 0);
                Run(w, 20 + World.DoorTicks + World.FreeTicks + 5);
                if (rothDies)
                {
                    Roth(w).Hp = 0;
                }
                else
                {
                    Roth(w).Captive = false;
                    (Roth(w).X, Roth(w).Y) = (500, 500);
                    Roth(w).Waiting = true;
                }
                (w.Units[0].X, w.Units[0].Y) = (1500, 1500);
                (Weiss(w).X, Weiss(w).Y) = (500, 1500);
                Weiss(w).Path.Clear();
                Run(w, World.EscapeTicks + 2);
                Assert.Equal(MissionState.Won, w.State);
                return w;
            }
            Assert.Equal("Zwei fehlen.", Headlines.For(Won(false), false).Line);
            Assert.Equal("Zwei fehlen. Einer der Gefangenen kam dabei ums Leben.", Headlines.For(Won(true), false).Line);
        }

        [Fact]
        public void ALostRescueLeavesThemInTheirCells()
        {
            var c = new Campaign();
            var m = Mission(new() { Agent(13.5, 5.5) }, new());
            c.Prepare(m, "mission_02");
            var w = new World(Map(), m);
            w.Units[0].Hp = 0;
            w.Step();
            c.Finish(w);
            Assert.Null(c.PrisonerAlive("Anton Weiß"));
            Assert.Empty(c.Records["mission_02"].Prisoners);
        }

        [Fact]
        public void SameOrdersSameOutcome()
        {
            ulong Play()
            {
                var w = FreedWeiss(new() { Guard(8.5, 9.5) });
                w.OrderMove(0, 2, 2);
                Run(w, 300);
                return w.StateHash();
            }
            Assert.Equal(Play(), Play());
        }
    }
}
