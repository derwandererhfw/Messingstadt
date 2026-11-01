using Messingstadt.Core;
using Xunit;

namespace Messingstadt.Core.Tests
{
    public class WatchMapTests
    {
        private static MissionData Patrol() => new()
        {
            Seed = 3, Item = new[] { 0.5, 0.5 }, ItemName = "Buch",
            Exit = new ZoneData { X0 = 0, Y0 = 0, X1 = 1, Y1 = 1 },
            Agents = new() { new UnitData { Name = "A", Sprite = "Agent", X = 0.5, Y = 0.5 } },
            Guards = new()
            {
                new UnitData { Name = "W", Sprite = "Wachmann", X = 2.5, Y = 2.5, Patrol = new() { new[] { 2.0, 2.0 }, new[] { 8.0, 2.0 } } },
            },
        };

        private static MapData Field() => new()
        {
            Width = 20, Height = 12,
            Solids = { new SolidData { X0 = 0, Y0 = 5, X1 = 20, Y1 = 6 } },
        };

        [Fact]
        public void RouteIsWatchedAndTheFarSideOfAWallIsNot()
        {
            var map = WatchMap.Measure(Field(), Patrol());
            Assert.True(map.Share(5, 2) > 0);
            Assert.Equal(0, map.Share(5, 8));
            Assert.Contains((5, 2), map.Routes);
            Assert.DoesNotContain((5, 8), map.Routes);
        }

        [Fact]
        public void ForecastMatchesTheRealMissionWhileNobodyIsNoticed()
        {
            var mission = Patrol();
            var map = WatchMap.Measure(Field(), mission);
            var world = new World(Field(), mission);
            for (int tick = 0; tick < 1200; tick++)
            {
                if (tick % 5 == 0)
                {
                    Assert.Equal(map.IsWatched(5, 2, world.Tick), world.WatcherOf(5, 2) != null);
                }
                world.Step();
            }
            Assert.Equal(MissionRating.Unseen, world.Rating);
        }

        [Fact]
        public void WayAcrossTheRouteIsOnlyClearAtTheRightTime()
        {
            var map = WatchMap.Measure(Field(), Patrol());
            var path = new (int X, int Y)[] { (12, 3), (12, 2), (12, 1) };
            int fromX = 12500, fromY = 4500, speed = 120;
            int wait = map.TicksUntilClear(path, fromX, fromY, speed, 0, 40, 2400);
            Assert.True(wait >= 0);
            Assert.Equal(-1, map.FirstRisk(path, fromX, fromY, speed, wait, 40));
            bool someTimeRisky = false;
            for (int t = 0; t < 400; t += 5)
            {
                someTimeRisky |= map.FirstRisk(path, fromX, fromY, speed, t, 40) >= 0;
            }
            Assert.True(someTimeRisky);
        }

        [Fact]
        public void MeasuringDoesNotChangeTheMission()
        {
            var mission = Patrol();
            var before = new World(Field(), mission).StateHash();
            WatchMap.Measure(Field(), mission);
            Assert.Equal(before, new World(Field(), mission).StateHash());
        }
    }
}
