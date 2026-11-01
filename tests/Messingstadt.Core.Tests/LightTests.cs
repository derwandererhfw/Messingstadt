using Messingstadt.Core;
using Xunit;

namespace Messingstadt.Core.Tests
{
    public class LightTests
    {
        private const int Size = 20;

        private static LightData Uniform(int level)
        {
            var light = new LightData { Cells = 1, Size = Size, Levels = new int[Size * Size] };
            System.Array.Fill(light.Levels, level);
            return light;
        }

        // Eine Wache blickt nach Osten, der Agent steht genau vor ihr im gegebenen Abstand.
        private static MissionData Mission(double distance, LightData? light) => new()
        {
            Seed = 3, Item = new[] { 19.5, 19.5 }, ItemName = "Buch", Light = light,
            Exit = new ZoneData { X0 = 0, Y0 = 18, X1 = 1, Y1 = 19 },
            Agents = new() { new UnitData { Name = "A", Sprite = "Agent", X = 2.5 + distance, Y = 5.5 } },
            Guards = new() { new UnitData { Name = "W", Sprite = "Wachmann", X = 2.5, Y = 5.5, Facing = new[] { 1.0, 0.0 } } },
        };

        private static World NewWorld(double distance, LightData? light) =>
            new(new MapData { Width = Size, Height = Size }, Mission(distance, light));

        private static bool Noticed(double distance, LightData? light)
        {
            var w = NewWorld(distance, light);
            for (int i = 0; i < 5; i++)
            {
                w.Step();
            }
            return w.Rating != MissionRating.Unseen;
        }

        [Fact]
        public void InDarknessGuardsNoticeLessFar()
        {
            Assert.False(Noticed(4.5, Uniform(World.DarkLevel)));
            Assert.True(Noticed(3, Uniform(World.DarkLevel)));
        }

        [Fact]
        public void BrightLightKeepsTheFullRange()
        {
            Assert.True(Noticed(4.5, Uniform(World.BrightLevel)));
            Assert.False(Noticed(5.5, Uniform(World.BrightLevel)));
        }

        [Fact]
        public void WithoutLightDataTheOldRangeApplies()
        {
            Assert.True(Noticed(4.5, null));
            Assert.False(Noticed(5.5, null));
        }

        [Fact]
        public void RangeGrowsSteadilyBetweenDarkAndBright()
        {
            var w = NewWorld(1, Uniform((World.DarkLevel + World.BrightLevel) / 2));
            int range = w.NoticeRangeAt(1000, 1000);
            Assert.InRange(range, World.DarkNoticeRange + 1, World.MaxNoticeRange - 1);
        }

        [Fact]
        public void NoticingTakesLongerInTheDark()
        {
            static int Ticks(int level)
            {
                var w = NewWorld(3, Uniform(level));
                for (int i = 0; i < 3; i++)
                {
                    w.Step();
                }
                return w.Units[1].NoticeTicks;
            }
            Assert.True(Ticks(World.DarkLevel) > Ticks(World.BrightLevel));
        }

        [Fact]
        public void CloseToAGuardLightDoesNotHelp()
        {
            Assert.True(Noticed(1.5, Uniform(0)));
        }

        [Fact]
        public void ForecastUsesTheSameLight()
        {
            var dark = WatchMap.Measure(new MapData { Width = Size, Height = Size }, Mission(1, Uniform(World.DarkLevel)));
            var bright = WatchMap.Measure(new MapData { Width = Size, Height = Size }, Mission(1, Uniform(World.BrightLevel)));
            Assert.True(bright.Share(7, 5) > dark.Share(7, 5));
        }
    }
}
