using System.Collections.Generic;
using System.IO;
using System.Linq;
using Messingstadt.Core;
using Xunit;

namespace Messingstadt.Core.Tests
{
    public class SequenceTests
    {
        private static readonly Dictionary<string, string> Texts = new() { ["place"] = "Querstraße", ["briefing"] = "Holt es." };

        private static SequenceData Data(params SequenceBeat[] beats) => new() { Id = "probe", Beats = beats.ToList() };

        private static MissionData Mission() => new()
        {
            Seed = 7, Item = new[] { 8.5, 0.5 }, ItemName = "Buch",
            Exit = new ZoneData { X0 = 0, Y0 = 0, X1 = 2, Y1 = 2 },
            Agents = new()
            {
                new UnitData { Name = "A", Sprite = "Agent", X = 4.5, Y = 4.5 },
                new UnitData { Name = "B", Sprite = "Agent", X = 6.5, Y = 4.5 },
            },
        };

        private static World NewWorld() => new(new MapData { Width = 12, Height = 12 }, Mission());

        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "data")))
            {
                dir = dir.Parent;
            }
            return dir!.FullName;
        }

        private static void RunFor(SequenceRun run, double seconds)
        {
            for (double t = 0; t < seconds && !run.Done; t += 0.05)
            {
                run.Advance(0.05);
            }
        }

        [Fact]
        public void AllSequenceFilesAreValidAndUseExistingSounds()
        {
            string root = RepoRoot();
            var files = Directory.GetFiles(Path.Combine(root, "data", "sequences"), "*.json");
            Assert.NotEmpty(files);
            foreach (string file in files)
            {
                var data = SequenceData.Parse(File.ReadAllText(file));
                Assert.Equal(Path.GetFileNameWithoutExtension(file), data.Id);
                Assert.Empty(data.Problems());
                foreach (var beat in data.Beats.Where(b => b.Image.Length > 0))
                {
                    Assert.True(File.Exists(Path.Combine(root, "game", "assets", beat.Image)), $"{data.Id}: Bild {beat.Image} fehlt");
                }
                foreach (var sound in data.Beats.SelectMany(b => b.Sounds))
                {
                    Assert.True(File.Exists(Path.Combine(root, "game", "assets", "audio", sound.Name + ".wav")), $"{data.Id}: {sound.Name} fehlt");
                }
            }
        }

        [Fact]
        public void SequencesUsedByTheGameExist()
        {
            foreach (string id in new[] { "prolog", "einstieg_nach_prolog", "einstieg", "uebergang", "sieg", "niederlage", "rueckzug" })
            {
                Assert.True(File.Exists(Path.Combine(RepoRoot(), "data", "sequences", id + ".json")), id);
            }
        }

        [Fact]
        public void PlaceholdersInTextsAreFilledFromTheMission()
        {
            var texts = SequenceTexts.For(Loader.ParseMission(File.ReadAllText(Path.Combine(RepoRoot(), "data", "mission_01.json"))), NewWorld(), false);
            foreach (var file in Directory.GetFiles(Path.Combine(RepoRoot(), "data", "sequences"), "*.json"))
            {
                foreach (var beat in SequenceData.Parse(File.ReadAllText(file)).Beats)
                {
                    foreach (string text in new[] { beat.Head, beat.Title, beat.Body }.Concat(beat.Speech).Concat(beat.Captions))
                    {
                        Assert.DoesNotContain("{", SequenceTexts.Fill(text, texts));
                    }
                }
            }
            Assert.Equal("Querstraße, hinter dem Mietshaus", texts["place"]);
        }

        [Fact]
        public void ProblemsFindBrokenData()
        {
            Assert.NotEmpty(new SequenceData { Id = "leer" }.Problems());
            Assert.NotEmpty(Data(new SequenceBeat { Show = "film" }).Problems());
            Assert.NotEmpty(Data(new SequenceBeat { Seconds = -1 }).Problems());
            Assert.NotEmpty(Data(new SequenceBeat { Fade = new[] { 1.0 } }).Problems());
            Assert.NotEmpty(Data(new SequenceBeat { Show = "image" }).Problems());
            Assert.NotEmpty(Data(new SequenceBeat { Show = "image", Image = "a.jpg", Zoom = new[] { 1.0 } }).Problems());
            Assert.NotEmpty(Data(new SequenceBeat { Camera = new SequenceCamera { From = "@mond", To = "@trupp" } }).Problems());
            Assert.Empty(Data(new SequenceBeat { Camera = new SequenceCamera { From = "3.5,4", To = "@trupp" } }).Problems());
        }

        [Fact]
        public void BeatsWithoutHoldRunOutOnTheirOwn()
        {
            var run = new SequenceRun(Data(new SequenceBeat { Seconds = 1 }, new SequenceBeat { Seconds = 1 }), Texts);
            RunFor(run, 1.5);
            Assert.Equal(1, run.Beat);
            RunFor(run, 1);
            Assert.True(run.Done);
        }

        [Fact]
        public void HoldWaitsForThePlayer()
        {
            var run = new SequenceRun(Data(new SequenceBeat { Seconds = 1, Hold = true }), Texts);
            RunFor(run, 10);
            Assert.False(run.Done);
            Assert.True(run.Waiting);
            run.Press();
            Assert.True(run.Done);
        }

        [Fact]
        public void FirstPressFinishesTheTypingSecondGoesOn()
        {
            var run = new SequenceRun(Data(new SequenceBeat { Show = "paper", Body = "{briefing}", Hold = true }, new SequenceBeat { Seconds = 1 }), Texts);
            run.Advance(0.05);
            Assert.True(run.Typing);
            run.Press();
            Assert.False(run.Typing);
            Assert.Equal("Holt es.".Length, run.Letters);
            Assert.Equal(0, run.Beat);
            run.Press();
            Assert.Equal(1, run.Beat);
        }

        [Fact]
        public void SkipEndsAtOnceAndNothingPlaysAfterwards()
        {
            var beat = new SequenceBeat { Seconds = 5, Sounds = { new SequenceSound { At = 1, Name = "thunder_1" } } };
            var run = new SequenceRun(Data(beat), Texts);
            run.Skip();
            Assert.True(run.Done);
            Assert.Empty(run.Advance(2));
            run.Press();
            Assert.True(run.Done);
        }

        [Fact]
        public void SoundsPlayExactlyOnceIncludingAtZero()
        {
            var beat = new SequenceBeat
            {
                Seconds = 3, Hold = true,
                Sounds = { new SequenceSound { At = 0, Name = "pages" }, new SequenceSound { At = 1, Name = "thunder_1" } },
            };
            var run = new SequenceRun(Data(beat), Texts);
            var played = new List<string>();
            for (int i = 0; i < 200; i++)
            {
                played.AddRange(run.Advance(0.05).Select(s => s.Name));
            }
            Assert.Equal(new[] { "pages", "thunder_1" }, played);
        }

        [Fact]
        public void FadeRunsOverItsSpanAndCarriesIntoTheNextBeat()
        {
            var run = new SequenceRun(Data(
                new SequenceBeat { Seconds = 2, Fade = new[] { 1.0, 0.0 }, FadeSeconds = 1 },
                new SequenceBeat { Seconds = 2 }), Texts);
            Assert.Equal(1.0, run.Fade, 3);
            RunFor(run, 0.5);
            Assert.InRange(run.Fade, 0.4, 0.6);
            RunFor(run, 2);
            Assert.Equal(1, run.Beat);
            Assert.Equal(0.0, run.Fade, 3);
        }

        [Fact]
        public void AnchorsPointAtSquadExitAndItem()
        {
            var w = NewWorld();
            Assert.Equal((5500, 4500), SequenceData.Locate("@trupp", w));
            Assert.Equal((1000, 1000), SequenceData.Locate("@ausgang", w));
            Assert.Equal((8500, 500), SequenceData.Locate("@ziel", w));
            Assert.Equal((2500, 3000), SequenceData.Locate("2.5,3", w));
            Assert.Null(SequenceData.Locate("@start", w));
            // Ohne Gefallene zeigt die Kamera auf den Trupp, mit Gefallenen auf sie.
            Assert.Equal((5500, 4500), SequenceData.Locate("@gefallen", w));
            w.Units[1].Hp = 0;
            w.Step();
            Assert.Equal((w.Units[1].X, w.Units[1].Y), SequenceData.Locate("@gefallen", w));
        }

        [Fact]
        public void TelegramDependsOnHowTheMissionEnded()
        {
            var w = NewWorld();
            Assert.Contains("TRUPP NICHT ZURUECK", SequenceTexts.Telegram(null, w, false));
            Assert.Contains("ZU HEISS", SequenceTexts.Telegram(null, w, true));
            Assert.Equal("EIGEN", SequenceTexts.Telegram(new TelegramData { Retreat = "EIGEN" }, w, true));
        }

        [Fact]
        public void UnknownPlaceholdersStayVisible()
        {
            Assert.Equal("Querstraße {nirgends}", SequenceTexts.Fill("{place} {nirgends}", Texts));
        }

        [Fact]
        public void EveryNewGameStartsWithThePrologAndContinuingDoesNot()
        {
            Assert.Equal(new[] { "prolog", "einstieg_nach_prolog" }, SequenceEntry.For(true, null));
            Assert.Equal(new[] { "uebergang" }, SequenceEntry.For(false, null));
            Assert.Equal(new[] { "einstieg" }, SequenceEntry.For(true, "einstieg"));
        }

        [Fact]
        public void MissionSequencesRunOncePerCampaign()
        {
            Assert.Equal(new[] { "vor_02", "einstieg_nach_prolog" }, SequenceEntry.For(false, null, "vor_02", false));
            Assert.Equal(new[] { "uebergang" }, SequenceEntry.For(false, null, "vor_02", true));
            Assert.Equal(new[] { "uebergang" }, SequenceEntry.For(false, null, "", false));
            Assert.Equal(new[] { "prolog", "einstieg_nach_prolog" }, SequenceEntry.For(true, null, "vor_02", false));
            Assert.Equal(new[] { "sieg", "nach_02" }, SequenceEntry.AfterMission(true, "nach_02", false));
            Assert.Equal(new[] { "sieg" }, SequenceEntry.AfterMission(true, "nach_02", true));
            Assert.Equal(new[] { "sieg" }, SequenceEntry.AfterMission(true, "", false));
            Assert.Equal(new[] { "niederlage" }, SequenceEntry.AfterMission(false, "nach_02", false));
        }

        [Fact]
        public void BeatsWithConditionsOnlyRunWhenTheyHold()
        {
            var data = Data(
                new SequenceBeat { Seconds = 1 },
                new SequenceBeat { Seconds = 1, When = "lebt:Anton Weiß, lebt:Paul Roth" },
                new SequenceBeat { Seconds = 1, When = "!lebt:Anton Weiß" },
                new SequenceBeat { Seconds = 1, When = " keine_toten " });
            var both = data.For(new HashSet<string> { "lebt:Anton Weiß", "lebt:Paul Roth", "nur_wachen" });
            Assert.Equal(2, both.Beats.Count);
            Assert.Equal("probe", both.Id);
            var none = data.For(new HashSet<string> { "keine_toten" });
            Assert.Equal(new[] { "", "!lebt:Anton Weiß", " keine_toten " }, none.Beats.Select(b => b.When));
        }

        [Fact]
        public void FlagsAndLedgerPageComeFromTheLastNight()
        {
            var w = NewWorld();
            var campaign = new Campaign { Nights = 2 };
            Assert.Contains(SequenceFlags.NoDead, SequenceFlags.For(campaign, w));
            Assert.Contains(SequenceFlags.NoDead, SequenceFlags.For(null, w));
            Assert.Equal("", SequenceTexts.Ledger(campaign, LedgerSide.Guard));
            campaign.Ledger.Add(new LedgerEntry { Name = "Alt", Side = LedgerSide.Guard, Kind = "offizier", Night = 1 });
            Assert.Contains(SequenceFlags.NoDead, SequenceFlags.For(campaign, w));
            campaign.Ledger.Add(new LedgerEntry { Name = "Karl Mertens", Side = LedgerSide.Guard, Kind = "offizier", Night = 2 });
            Assert.Contains(SequenceFlags.GuardsOnly, SequenceFlags.For(campaign, w));
            campaign.Ledger.Add(new LedgerEntry { Name = "Otto Brenner", Side = LedgerSide.Squad, Kind = "Schlosser", Night = 2 });
            var flags = SequenceFlags.For(campaign, w);
            Assert.Contains(SequenceFlags.BothSides, flags);
            Assert.DoesNotContain(SequenceFlags.GuardsOnly, flags);
            campaign.Ledger.RemoveAt(1);
            Assert.Contains(SequenceFlags.OwnOnly, SequenceFlags.For(campaign, w));
            campaign.Ledger.Insert(1, new LedgerEntry { Name = "Karl Mertens", Side = LedgerSide.Guard, Kind = "offizier", Night = 2 });
            Assert.Equal("Schlosser Otto Brenner", SequenceTexts.Ledger(campaign, LedgerSide.Squad));
            Assert.Equal("Wachtmeister Karl Mertens", SequenceTexts.For(Mission(), w, false, campaign)["totenbuch_wache"]);
            Assert.Equal("2", SequenceTexts.For(Mission(), w, false, campaign)["nacht"]);
        }

        [Fact]
        public void LedgerBeatsAreTypedAndNeedNoImage()
        {
            Assert.Empty(Data(new SequenceBeat { Show = "ledger", Body = "x" }).Problems());
            Assert.True(new SequenceBeat { Show = "ledger" }.Typed);
        }

        [Fact]
        public void SeenSequencesSurviveSavingAndOldSavesHaveNone()
        {
            var campaign = new Campaign();
            campaign.SeenSequences.Add("vor_02");
            Assert.Contains("vor_02", Campaign.FromJson(campaign.ToJson()).SeenSequences);
        }

        [Fact]
        public void ImageBeatsTypeTheNarration()
        {
            var run = new SequenceRun(Data(new SequenceBeat { Show = "image", Image = "a.jpg", Body = "{briefing}", Seconds = 1 }), Texts);
            run.Advance(0.05);
            Assert.True(run.Typing);
            run.Press();
            Assert.False(run.Typing);
            Assert.Equal(0, run.Beat);
        }
    }
}
