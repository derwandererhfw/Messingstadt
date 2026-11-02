using Messingstadt.Core;
using Xunit;

namespace Messingstadt.Core.Tests
{
    public class SettingsTests
    {
        [Fact]
        public void Defaults_LeaveSoundOnAndWindowed()
        {
            var s = new Settings();
            Assert.Equal(1.0, s.MasterVolume);
            Assert.False(s.Muted);
            Assert.False(s.Fullscreen);
            Assert.True(s.ScreenShake);
        }

        [Fact]
        public void Json_KeepsEveryValue()
        {
            var s = new Settings { MasterVolume = 0.5, MusicVolume = 0.25, EffectsVolume = 0.75, Muted = true, Fullscreen = true, ScreenShake = false };
            var back = Settings.FromJson(s.ToJson());
            Assert.Equal(0.5, back.MasterVolume);
            Assert.Equal(0.25, back.MusicVolume);
            Assert.Equal(0.75, back.EffectsVolume);
            Assert.True(back.Muted);
            Assert.True(back.Fullscreen);
            Assert.False(back.ScreenShake);
        }

        [Theory]
        [InlineData("")]
        [InlineData("kaputt")]
        [InlineData("{\"MasterVolume\":\"laut\"}")]
        public void BrokenFile_FallsBackToDefaults(string json)
        {
            Assert.Equal(1.0, Settings.FromJson(json).MasterVolume);
        }

        [Fact]
        public void VolumesOutsideTheSlider_AreLimited()
        {
            var s = Settings.FromJson("{\"MasterVolume\":7,\"MusicVolume\":-2}");
            Assert.Equal(1.0, s.MasterVolume);
            Assert.Equal(0.0, s.MusicVolume);
        }

        [Fact]
        public void MissingFields_KeepTheirDefaults()
        {
            var s = Settings.FromJson("{\"Muted\":true}");
            Assert.True(s.Muted);
            Assert.True(s.ScreenShake);
            Assert.Equal(1.0, s.EffectsVolume);
        }
    }
}
