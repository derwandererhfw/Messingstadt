using System.Collections.Generic;
using System.Text.Json;

namespace Messingstadt.Core
{
    /// <summary>Rechteck in Feldkoordinaten, Blockade ohne Sichtsperre möglich.</summary>
    public sealed class SolidData
    {
        public double X0 { get; set; }
        public double Y0 { get; set; }
        public double X1 { get; set; }
        public double Y1 { get; set; }
        public bool Opaque { get; set; } = true;
    }

    public sealed class MapData
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public List<SolidData> Solids { get; set; } = new();

        public GameMap Build()
        {
            var map = new GameMap(Width, Height);
            foreach (var s in Solids)
            {
                for (int y = (int)s.Y0; y < s.Y1; y++)
                {
                    for (int x = (int)s.X0; x < s.X1; x++)
                    {
                        map.Block(x, y, s.Opaque);
                    }
                }
            }
            return map;
        }
    }

    public sealed class UnitData
    {
        public string Name { get; set; } = "";
        public string Sprite { get; set; } = "";
        public double X { get; set; }
        public double Y { get; set; }
        public List<double[]> Patrol { get; set; } = new();
    }

    public sealed class ZoneData
    {
        public double X0 { get; set; }
        public double Y0 { get; set; }
        public double X1 { get; set; }
        public double Y1 { get; set; }
    }

    public sealed class MissionData
    {
        public string Title { get; set; } = "";
        public string Briefing { get; set; } = "";
        public int Seed { get; set; }
        public List<UnitData> Agents { get; set; } = new();
        public List<UnitData> Guards { get; set; } = new();
        public double[] Item { get; set; } = new double[2];
        public string ItemName { get; set; } = "";
        public ZoneData Exit { get; set; } = new();
    }

    public static class Loader
    {
        private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

        public static MapData ParseMap(string json) => JsonSerializer.Deserialize<MapData>(json, Options)!;

        public static MissionData ParseMission(string json) => JsonSerializer.Deserialize<MissionData>(json, Options)!;
    }
}
