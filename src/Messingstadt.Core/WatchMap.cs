using System;
using System.Collections.Generic;

namespace Messingstadt.Core
{
    /// <summary>
    /// Lageplan: Wann ahnungslose Wachen welches Feld im Blick haben und wo sie entlanggehen.
    /// Gemessen in einer Probe der Mission ohne Agenten, ändert also nichts am Einsatz. Solange keine
    /// Wache etwas bemerkt hat, läuft der Einsatz genau wie die Probe und der Plan stimmt auch für die Zukunft.
    /// </summary>
    public sealed class WatchMap
    {
        private const int ProbeSeconds = 600;
        private const int SampleTicks = 5;

        // Spielraum für Klicks, die nicht auf den Takt genau kommen.
        private const int MarginTicks = 6;

        private readonly bool[] _watched;
        private readonly int[] _watchedCount;
        private readonly int _samples;

        public int Width { get; }
        public int Height { get; }
        public HashSet<(int X, int Y)> Routes { get; } = new();

        private WatchMap(int width, int height, int samples)
        {
            Width = width;
            Height = height;
            _samples = samples;
            _watched = new bool[samples * width * height];
            _watchedCount = new int[width * height];
        }

        /// <summary>Anteil der Zeit von 0 bis 1, in der mindestens eine Wache dieses Feld bemerken würde.</summary>
        public double Share(int x, int y) => x < 0 || y < 0 || x >= Width || y >= Height
            ? 0
            : (double)_watchedCount[y * Width + x] / _samples;

        /// <summary>Hat zu diesem Takt eine Wache das Feld im Blick? Hinter dem Ende der Probe gilt das Feld als frei.</summary>
        public bool IsWatched(int x, int y, int tick)
        {
            int sample = tick / SampleTicks;
            return x >= 0 && y >= 0 && x < Width && y < Height && sample >= 0 && sample < _samples
                && _watched[(sample * Height + y) * Width + x];
        }

        private bool IsWatchedDuring(int x, int y, int fromTick, int toTick)
        {
            for (int t = Math.Max(0, fromTick); t <= toTick; t += SampleTicks)
            {
                if (IsWatched(x, y, t))
                {
                    return true;
                }
            }
            return IsWatched(x, y, toTick);
        }

        /// <summary>
        /// Geht einen Weg in Gedanken ab: Index des ersten Feldes, auf dem man bemerkt würde, sonst -1.
        /// Am letzten Feld bleibt die Figur noch holdTicks stehen, etwa beim Aufnehmen.
        /// </summary>
        public int FirstRisk(IReadOnlyList<(int X, int Y)> path, int fromX, int fromY, int speed, int startTick, int holdTicks)
        {
            int x = fromX, y = fromY, t = startTick;
            for (int i = 0; i < path.Count; i++)
            {
                int cx = path[i].X * World.TileUnits + World.TileUnits / 2, cy = path[i].Y * World.TileUnits + World.TileUnits / 2;
                int walk = (int)(Math.Sqrt((double)(cx - x) * (cx - x) + (double)(cy - y) * (cy - y)) / speed) + 1;
                int until = t + walk + (i == path.Count - 1 ? holdTicks : 0);
                if (IsWatchedDuring(path[i].X, path[i].Y, t - MarginTicks, until + MarginTicks))
                {
                    return i;
                }
                t += walk;
                x = cx;
                y = cy;
            }
            return -1;
        }

        /// <summary>Takte, bis der Weg frei ist: 0 heißt sofort, -1 heißt innerhalb von maxTicks gar nicht.</summary>
        public int TicksUntilClear(IReadOnlyList<(int X, int Y)> path, int fromX, int fromY, int speed, int startTick, int holdTicks, int maxTicks)
        {
            for (int wait = 0; wait <= maxTicks; wait += SampleTicks)
            {
                if (FirstRisk(path, fromX, fromY, speed, startTick + wait, holdTicks) < 0)
                {
                    return wait;
                }
            }
            return -1;
        }

        public static WatchMap Measure(MapData mapData, MissionData mission)
        {
            var world = new World(mapData, new MissionData
            {
                Seed = mission.Seed, Guards = mission.Guards, Doors = mission.Doors, Item = mission.Item, Exit = mission.Exit,
            });
            int width = world.Map.Width, height = world.Map.Height, samples = ProbeSeconds * World.TickRate / SampleTicks;
            var map = new WatchMap(width, height, samples);
            for (int sample = 0; sample < samples; sample++)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (!world.Map.IsBlocked(x, y) && world.WatcherOf(x, y) != null)
                        {
                            map._watched[(sample * height + y) * width + x] = true;
                            map._watchedCount[y * width + x]++;
                        }
                    }
                }
                foreach (var g in world.Units)
                {
                    if (g.Patrol.Count > 0)
                    {
                        map.Routes.Add((g.TileX, g.TileY));
                    }
                }
                for (int i = 0; i < SampleTicks; i++)
                {
                    world.Step();
                }
            }
            return map;
        }
    }
}
