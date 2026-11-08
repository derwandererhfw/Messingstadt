using System;
using System.IO;
using Messingstadt.Core;
using Xunit;

namespace Messingstadt.Core.Tests
{
    public sealed class SaveFolderTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "messingstadt-akten-" + Guid.NewGuid().ToString("N"));

        public SaveFolderTests() => Directory.CreateDirectory(_dir);

        public void Dispose() => Directory.Delete(_dir, true);

        [Fact]
        public void FirstSlot_IsTheOldSaveFile()
        {
            File.WriteAllText(Path.Combine(_dir, "kampagne.json"), new Campaign { Money = 777, Missions = 2 }.ToJson());
            var folder = new SaveFolder(_dir);
            Assert.True(folder.Exists(1));
            Assert.Equal(777, folder.Load(1).Money);
            Assert.False(folder.Exists(2));
        }

        [Fact]
        public void Slots_KeepTheirOwnCampaign()
        {
            var folder = new SaveFolder(_dir);
            folder.Save(2, new Campaign { Money = 50 });
            folder.Save(3, new Campaign { Money = 900 });
            Assert.Equal(50, folder.Load(2).Money);
            Assert.Equal(900, folder.Load(3).Money);
            Assert.False(folder.Exists(1));
        }

        [Fact]
        public void BrokenSlot_CanBeSetAsideWithoutTouchingOthers()
        {
            var folder = new SaveFolder(_dir);
            folder.Save(1, new Campaign { Money = 600 });
            File.WriteAllText(folder.PathOf(2), "kaputt");
            var loaded = folder.Load(2, out var status);
            Assert.Equal(SaveStatus.Discarded, status);
            Assert.Equal(new Campaign().Money, loaded.Money);
            folder.SetAside(2);
            Assert.Equal("kaputt", File.ReadAllText(Path.Combine(_dir, "kampagne_2.alt.json")));
            Assert.Equal(600, folder.Load(1, out var first).Money);
            Assert.Equal(SaveStatus.Loaded, first);
        }

        [Fact]
        public void Saving_LeavesNoTemporaryFile()
        {
            var folder = new SaveFolder(_dir);
            folder.Save(1, new Campaign());
            folder.Save(1, new Campaign { Money = 10 });
            Assert.Single(Directory.GetFiles(_dir));
            Assert.Equal(10, folder.Load(1).Money);
        }

        [Fact]
        public void EmptySlot_LoadsANewCampaign()
        {
            var folder = new SaveFolder(_dir);
            Assert.Equal(new Campaign().Money, folder.Load(3).Money);
            Assert.Equal("leer", folder.Describe(3));
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(4, 1)]
        [InlineData(2, 2)]
        public void UnknownSlots_FallBackToTheFirst(int slot, int expected)
        {
            Assert.Equal(expected, SaveFolder.ClampSlot(slot));
        }

        [Fact]
        public void Begun_OnlyForCampaignsThatWerePlayed()
        {
            var folder = new SaveFolder(_dir);
            Assert.False(folder.Begun(1));
            folder.Save(1, new Campaign());
            Assert.False(folder.Begun(1));
            folder.Save(2, new Campaign { Money = 120 });
            Assert.True(folder.Begun(2));
            var hurt = new Campaign();
            hurt.Agents.Add(new AgentRecord { Name = "Emil", Falls = 1 });
            Assert.True(SaveFolder.Begun(hurt));
            Assert.True(SaveFolder.Begun(new Campaign { Missions = 1 }));
            // Die Startstufen aus der Mission zählen nicht als erlebt.
            var fresh = new Campaign();
            fresh.Agents.Add(new AgentRecord { Name = "Greta", Legs = 2, Heart = 2 });
            Assert.False(SaveFolder.Begun(fresh));
        }

        [Fact]
        public void Describe_CountsMissionsInGerman()
        {
            Assert.Equal("Kasse 300, 1 Einsatz", SaveFolder.Describe(new Campaign { Money = 300, Missions = 1 }));
            Assert.Equal("Kasse 20, 0 Einsätze", SaveFolder.Describe(new Campaign { Money = 20 }));
        }
    }
}
