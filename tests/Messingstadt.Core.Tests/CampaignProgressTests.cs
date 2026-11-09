using System.Collections.Generic;
using System.Linq;
using Messingstadt.Core;
using Xunit;

namespace Messingstadt.Core.Tests
{
    public class CampaignProgressTests
    {
        private static readonly MapData Map = new() { Width = 30, Height = 12 };

        /// <summary>Vier Agenten am Wagen, Wachen nach Bedarf. Jede Mission wird frisch gebaut, Prepare verändert sie.</summary>
        private static MissionData Mission(params UnitData[] guards) => new()
        {
            Seed = 7, Item = new[] { 28.5, 0.5 }, ItemName = "Buch",
            Exit = new ZoneData { X0 = 0, Y0 = 0, X1 = 2, Y1 = 2 },
            Reinforcements = new ReinforcementData { X = 20.5, Y = 10.5, Sprite = "Wachmann", FirstDelay = 6, Interval = 4, Count = 3 },
            Agents = new()
            {
                new UnitData { Name = "Greta", Sprite = "Agent", X = 0.5, Y = 0.5, Legs = 2, Heart = 2 },
                new UnitData { Name = "Otto", Sprite = "Agent", X = 1.5, Y = 0.5, Skill = World.SkillLocksmith },
                new UnitData { Name = "Emil", Sprite = "Agent", X = 0.5, Y = 1.5, Skill = World.SkillBookkeeper },
                new UnitData { Name = "Hanna", Sprite = "Agent", X = 1.5, Y = 1.5, Legs = 3 },
            },
            Guards = guards.ToList(),
        };

        private static UnitData Guard(double x, double y) => new() { Name = "W", Sprite = "Wachmann", X = x, Y = y, Facing = new[] { 1.0, 0.0 } };

        private static World Start(Campaign c, MissionData m, string id = "m1")
        {
            c.Prepare(m, id);
            return new World(Map, m);
        }

        private static Unit Agent(World w, string name) => w.Units.First(u => u.Faction == Faction.Agent && u.Name == name);

        /// <summary>Der erste Agent trägt das Buch und steht schon im Wagen.</summary>
        private static void Win(World w)
        {
            w.Units.First(u => u.Faction == Faction.Agent && u.Alive).CarriesItem = true;
            for (int i = 0; i < 40 && w.State == MissionState.Running; i++)
            {
                w.Step();
            }
            Assert.Equal(MissionState.Won, w.State);
        }

        private static void Lose(World w)
        {
            foreach (var u in w.Units.Where(u => u.Faction == Faction.Agent))
            {
                u.Hp = 0;
            }
            w.Step();
            Assert.Equal(MissionState.Lost, w.State);
        }

        /// <summary>Ein Agent schießt auf eine Wache, bis der Einsatz laut ist.</summary>
        private static void MakeLoud(World w)
        {
            var guard = w.Units.First(u => u.Faction == Faction.Guard);
            guard.Hp = 100000;
            w.OrderAttack(0, guard.Id);
            for (int i = 0; i < 40 && w.Rating != MissionRating.Loud; i++)
            {
                w.Step();
            }
            Assert.Equal(MissionRating.Loud, w.Rating);
            guard.Hp = 1;
        }

        [Fact]
        public void FirstFallSendsToTheInfirmaryAndTheAgentSitsOutOneMission()
        {
            var c = new Campaign();
            var w = Start(c, Mission());
            Agent(w, "Emil").Hp = 0;
            Win(w);
            c.Finish(w);
            var emil = c.Agent("Emil");
            Assert.False(emil.Dead);
            Assert.Equal(1, emil.Falls);
            Assert.True(emil.Infirmary);
            Assert.Equal(1, emil.Legs);

            var next = Mission();
            c.Prepare(next, "m2");
            Assert.DoesNotContain(next.Agents, a => a.Name == "Emil");
            Assert.Equal(3, next.Agents.Count);
            var w2 = new World(Map, next);
            Win(w2);
            c.Finish(w2);
            Assert.True(c.Agent("Emil").Ready);
        }

        [Fact]
        public void SecondFallKills()
        {
            var c = new Campaign();
            for (int round = 0; round < 2; round++)
            {
                if (round > 0)
                {
                    c.Agent("Emil").Rest = 0;
                }
                var w = Start(c, Mission());
                Agent(w, "Emil").Hp = 0;
                Win(w);
                c.Finish(w);
            }
            Assert.True(c.Agent("Emil").Dead);
            Assert.Equal("Emil", c.Roster()[2].Name);
            var next = Mission();
            c.Prepare(next, "m3");
            Assert.DoesNotContain(next.Agents, a => a.Name == "Emil");
        }

        [Fact]
        public void DefeatOnlyKillsThoseWhoFellBefore()
        {
            var c = new Campaign();
            c.Prepare(Mission());
            c.Agent("Greta").Falls = 1;
            var w = Start(c, Mission());
            Lose(w);
            c.Finish(w);
            Assert.True(c.Agent("Greta").Dead);
            Assert.False(c.Agent("Hanna").Dead);
            Assert.Equal(1, c.Agent("Hanna").Falls);
            Assert.True(c.Agent("Hanna").Infirmary);
        }

        [Fact]
        public void AbandoningAfterAShotCountsAsAFallForEveryone()
        {
            var c = new Campaign();
            var w = Start(c, Mission(Guard(3.5, 0.5)));
            MakeLoud(w);
            c.Abandon(w);
            Assert.All(c.Roster(), a => Assert.Equal(1, a.Falls));
            Assert.All(c.Roster(), a => Assert.False(a.Dead));
        }

        [Fact]
        public void FreeAbandonChangesNothingButMoney()
        {
            var c = new Campaign();
            var w = Start(c, Mission());
            w.Step();
            c.Abandon(w);
            Assert.All(c.Roster(), a => Assert.Equal(0, a.Falls));
            Assert.Equal(0, c.Support);
            Assert.Empty(c.Records);
        }

        [Fact]
        public void BadlyHurtAgentRestsOneMissionUnlessTheDoctorHelps()
        {
            var c = new Campaign();
            var w = Start(c, Mission());
            Agent(w, "Hanna").Hp = 40;
            Agent(w, "Otto").Hp = 60;
            Win(w);
            c.Finish(w);
            Assert.Equal(1, c.Agent("Hanna").Rest);
            Assert.False(c.Agent("Hanna").Infirmary);
            Assert.True(c.Agent("Otto").Ready);

            int money = c.Money;
            Assert.True(c.TryDoctor("Hanna"));
            Assert.Equal(money - Campaign.DoctorCost, c.Money);
            Assert.True(c.Agent("Hanna").Ready);
            Assert.False(c.TryDoctor("Hanna"));
        }

        [Fact]
        public void RecruitTakesTheEmptySlotWithItsSkillAtLevelOne()
        {
            var c = new Campaign { Money = 1000 };
            c.Prepare(Mission());
            c.Agent("Otto").Falls = 2;
            c.Agent("Otto").Dead = true;
            Assert.False(c.TryRecruit("Greta"));
            Assert.True(c.TryRecruit("Otto"));
            Assert.Equal(1000 - Campaign.RecruitCost, c.Money);
            var recruit = c.Roster()[1];
            Assert.Equal("Karl Hoffmann", recruit.Name);
            Assert.Equal("Schlosser", recruit.Job);
            Assert.Equal(1, recruit.Legs);

            var m = Mission();
            c.Prepare(m);
            var unit = m.Agents.First(a => a.Name == recruit.Name);
            Assert.Equal(World.SkillLocksmith, unit.Skill);
            Assert.Equal(4, m.Agents.Count);
        }

        [Fact]
        public void TooFewReadyAgentsBringFreeRecruits()
        {
            var c = new Campaign { Money = 0 };
            c.Prepare(Mission());
            foreach (var name in new[] { "Greta", "Otto", "Emil" })
            {
                c.Agent(name).Falls = 1;
            }
            var w = Start(c, Mission());
            Lose(w);
            c.Finish(w);
            Assert.Equal(0, c.Money);
            Assert.True(c.Roster()[2].Dead);
            Assert.Equal(Campaign.MinSquad, c.Roster().Count(a => a.Ready));
            var m = Mission();
            c.Prepare(m);
            Assert.True(m.Agents.Count >= Campaign.MinSquad);
        }

        [Fact]
        public void RestingAgentsComeAnywayWhenNobodyElseCan()
        {
            var c = new Campaign();
            var w = Start(c, Mission(Guard(3.5, 0.5)));
            MakeLoud(w);
            c.Abandon(w);
            var m = Mission();
            c.Prepare(m);
            Assert.Equal(Campaign.MinSquad, m.Agents.Count);
        }

        [Fact]
        public void LoudBloodyWinRaisesHeatAndHeatBringsMoreReinforcements()
        {
            var c = new Campaign();
            var w = Start(c, Mission(Guard(3.5, 0.5), Guard(20.5, 5.5), Guard(22.5, 5.5), Guard(24.5, 5.5)));
            MakeLoud(w);
            foreach (var g in w.Units.Where(u => u.Faction == Faction.Guard).Skip(1))
            {
                g.Hp = 0;
            }
            Win(w);
            c.Finish(w);
            Assert.Equal(2, c.Heat);

            var m = Mission();
            c.Prepare(m);
            Assert.Equal(6, m.Reinforcements!.FirstDelay);
            Assert.Equal(4, m.Reinforcements.Count);
        }

        [Fact]
        public void UnseenWinLowersHeatAndRaisesSupport()
        {
            var c = new Campaign { Heat = 3 };
            var w = Start(c, Mission());
            Win(w);
            c.Finish(w);
            Assert.Equal(2, c.Heat);
            Assert.Equal(1, c.Support);
        }

        [Fact]
        public void SupportPaysDuesEvenAfterADefeat()
        {
            var c = new Campaign { Support = 3, Money = 0 };
            var w = Start(c, Mission());
            Lose(w);
            c.Finish(w);
            Assert.Equal(2, c.Support);
            Assert.Equal(2 * Campaign.DuesPerSupport, c.Money);
        }

        [Fact]
        public void EachMissionKeepsARecordForTheStory()
        {
            var c = new Campaign();
            var lost = Start(c, Mission(), "kontobuch");
            Lose(lost);
            c.Finish(lost);
            Assert.False(c.Records["kontobuch"].Won);
            foreach (var a in c.Roster())
            {
                a.Rest = 0;
            }
            var w = Start(c, Mission(Guard(20.5, 5.5), Guard(22.5, 5.5)), "kontobuch");
            w.Units.First(u => u.Faction == Faction.Guard).Bribed = true;
            w.Units.Last(u => u.Faction == Faction.Guard).Hp = 0;
            Win(w);
            c.Finish(w);
            var record = c.Records["kontobuch"];
            Assert.Equal(2, record.Attempts);
            Assert.True(record.Won);
            Assert.Equal(MissionRating.Unseen, record.Rating);
            Assert.Equal(1, record.Bribed);
            Assert.Equal(1, record.DeadGuards);
            Assert.True(Campaign.FromJson(c.ToJson()).Records["kontobuch"].Won);
        }

        [Fact]
        public void OldSaveWithoutVersionIsTakenOver()
        {
            const string old = "{\"Money\":900,\"Missions\":2,\"Agents\":[{\"Name\":\"Greta\",\"Legs\":3,\"Heart\":2},{\"Name\":\"Emil\",\"Legs\":1,\"Heart\":7}]}";
            var c = Campaign.Load(old, out var status);
            Assert.Equal(SaveStatus.Migrated, status);
            Assert.Equal(Campaign.CurrentVersion, c.Version);
            Assert.Equal(900, c.Money);
            Assert.Equal(2, c.Missions);
            Assert.Equal("Greta", c.Agent("Greta").Slot);
            Assert.Equal(3, c.Agent("Emil").Heart);
            Assert.True(c.Agent("Greta").Ready);

            var m = Mission();
            c.Prepare(m);
            Assert.Equal(3, m.Agents.First(a => a.Name == "Greta").Legs);
            Assert.Equal(4, m.Agents.Count);
        }

        [Theory]
        [InlineData("kein json")]
        [InlineData("[1,2]")]
        [InlineData("null")]
        [InlineData("{\"Version\":99,\"Money\":5}")]
        [InlineData("{\"Version\":2,\"Money\":\"viel\"}")]
        public void BrokenOrNewerSaveIsDiscarded(string json)
        {
            var c = Campaign.Load(json, out var status);
            Assert.Equal(SaveStatus.Discarded, status);
            Assert.Equal(new Campaign().Money, c.Money);
        }

        [Fact]
        public void MissingSaveIsANewCampaign()
        {
            Campaign.Load(null, out var status);
            Assert.Equal(SaveStatus.New, status);
        }

        [Fact]
        public void LoadedValuesAreKeptInBounds()
        {
            var c = Campaign.Load("{\"Version\":3,\"Money\":-5,\"Heat\":9,\"Support\":-1,\"Agents\":null,\"Records\":null}", out var status);
            Assert.Equal(SaveStatus.Loaded, status);
            Assert.Equal(0, c.Money);
            Assert.Equal(Campaign.MaxHeat, c.Heat);
            Assert.Equal(0, c.Support);
            Assert.Empty(c.Agents);
            Assert.Empty(c.Records);
        }

        [Fact]
        public void SavingKeepsTheNewValues()
        {
            var c = new Campaign { Heat = 2, Support = 4 };
            c.Prepare(Mission());
            c.Agent("Otto").Falls = 1;
            c.Agent("Otto").Rest = 1;
            c.Agent("Otto").Infirmary = true;
            var loaded = Campaign.Load(c.ToJson(), out var status);
            Assert.Equal(SaveStatus.Loaded, status);
            Assert.Equal(2, loaded.Heat);
            Assert.Equal(4, loaded.Support);
            Assert.Equal(1, loaded.Agent("Otto").Falls);
            Assert.True(loaded.Agent("Otto").Infirmary);
        }
        [Fact]
        public void NewcomersComeInTheirOrderForTheFreeJob()
        {
            var c = new Campaign { Money = 5000 };
            c.Prepare(Mission());
            foreach (var name in new[] { "Greta", "Otto", "Emil" })
            {
                c.Agent(name).Dead = true;
            }
            Assert.True(c.TryRecruit("Emil"));
            Assert.True(c.TryRecruit("Greta"));
            Assert.True(c.TryRecruit("Otto"));
            Assert.Equal(new[] { "Lotte Becker", "Karl Hoffmann", "Frieda Kranz", "Hanna" }, c.Roster().Select(a => a.Name));
            Assert.Equal("Kurierin", c.Agent("Lotte Becker").Job);
            Assert.Equal("", c.Origin(c.Agent("Hanna")));

            c.Agent("Karl Hoffmann").Dead = true;
            Assert.True(c.TryRecruit("Otto"));
            Assert.Equal("Willi Saß", c.Roster()[1].Name);
            Assert.Equal("Lehrling", c.Origin(c.Agent("Willi Saß")));
            c.Agent("Otto").Dead = false;
            Assert.Equal("Lehrling bei Otto", c.Origin(c.Agent("Willi Saß")));
        }

        [Fact]
        public void ResultListsAndTelegramsFollowTheLastMission()
        {
            var c = new Campaign();
            c.Prepare(Mission());
            c.Agent("Greta").Falls = 1;
            c.Agent("Otto").Falls = 1;
            var w = Start(c, Mission());
            Agent(w, "Greta").Hp = 0;
            Agent(w, "Otto").Hp = 0;
            Agent(w, "Emil").Hp = 0;
            Win(w);
            c.Finish(w);
            Assert.Equal(new[] { "Greta", "Otto" }, c.LastDeaths);
            Assert.Equal(new[] { "Emil" }, c.LastFirstFalls);
            // Nur Hanna ist bereit, also kommt umsonst eine Neue auf Gretas Platz.
            Assert.Equal(new[] { "Lotte Becker" }, c.LastNewcomers);
            Assert.Equal(new[] { CampaignTexts.TelegramManyDead, CampaignTexts.TelegramNewcomers }, c.Telegrams());
            Assert.Equal("Emil liegt in der Krankenstube. Beine und Herz fallen auf Stufe 1. Ein zweites Mal geht das nicht gut.", CampaignTexts.FirstFall("Emil"));

            c.Prepare(Mission());
            Assert.Empty(c.LastDeaths);
            Assert.Empty(c.Telegrams());
        }

        [Fact]
        public void FreeNewcomersBringTheirTelegram()
        {
            var c = new Campaign { Money = 0 };
            c.Prepare(Mission());
            foreach (var name in new[] { "Greta", "Otto", "Emil" })
            {
                c.Agent(name).Falls = 1;
            }
            var w = Start(c, Mission());
            Lose(w);
            c.Finish(w);
            Assert.Equal(2, c.LastNewcomers.Count);
            Assert.Equal(new[] { CampaignTexts.TelegramManyDead, CampaignTexts.TelegramNewcomers }, c.Telegrams());
        }

        [Fact]
        public void DeathRuleHintIsRememberedAcrossSaves()
        {
            var c = new Campaign { DeathRuleExplained = true };
            Assert.True(Campaign.Load(c.ToJson(), out _).DeathRuleExplained);
            Assert.False(Campaign.Load("{\"Money\":5}", out _).DeathRuleExplained);
        }
        [Fact]
        public void DefeatTelegramReportsTheDeadInsteadOfTheInfirmary()
        {
            var c = new Campaign();
            c.Prepare(Mission());
            c.Agent("Greta").Falls = 1;
            var w = Start(c, Mission());
            Lose(w);
            c.Finish(w);
            var own = new TelegramData { Lost = "EIGEN KRANKENSTUBE BEREIT" };
            Assert.Equal(CampaignTexts.TelegramOneDead, SequenceTexts.Telegram(own, w, false, c));
            Assert.Equal(CampaignTexts.TelegramOneDead, SequenceTexts.For(Mission(), w, false, c)["telegram"]);
            Assert.DoesNotContain("KRANKENSTUBE", SequenceTexts.Telegram(null, w, false, c));

            c.Agent("Otto").Rest = 0;
            c.Agent("Emil").Rest = 0;
            var again = Start(c, Mission());
            Lose(again);
            c.Finish(again);
            Assert.Equal(CampaignTexts.TelegramManyDead, SequenceTexts.Telegram(null, again, false, c));
        }

        [Fact]
        public void DefeatWithoutDeathsKeepsTheInfirmaryTelegram()
        {
            var c = new Campaign();
            var w = Start(c, Mission());
            Lose(w);
            c.Finish(w);
            Assert.Empty(c.LastDeaths);
            Assert.Contains("KRANKENSTUBE BEREIT", SequenceTexts.Telegram(null, w, false, c));
            Assert.Equal("EIGEN", SequenceTexts.Telegram(new TelegramData { Lost = "EIGEN" }, w, false, c));
        }

        [Fact]
        public void AbandonAfterAShotWithDeathsReportsTheDead()
        {
            var c = new Campaign();
            c.Prepare(Mission());
            c.Agent("Hanna").Falls = 1;
            var w = Start(c, Mission(Guard(3.5, 0.5)));
            MakeLoud(w);
            c.Abandon(w);
            Assert.Equal(new[] { "Hanna" }, c.LastDeaths);
            Assert.Equal(CampaignTexts.TelegramOneDead, SequenceTexts.Telegram(null, w, true, c));
        }
        [Fact]
        public void StampShowsWhetherTheFallIsFatalBeforeAndAfterBooking()
        {
            var c = new Campaign();
            c.Prepare(Mission());
            c.Agent("Greta").Falls = 1;
            var w = Start(c, Mission());
            Agent(w, "Greta").Hp = 0;
            Agent(w, "Otto").Hp = 0;
            Assert.True(c.FallIsFatal("Greta"));
            Assert.False(c.FallIsFatal("Otto"));
            Win(w);
            c.Finish(w);
            Assert.True(c.FallIsFatal("Greta"));
            Assert.False(c.FallIsFatal("Otto"));
            Assert.False(c.FallIsFatal("Niemand"));
        }
    }
}
