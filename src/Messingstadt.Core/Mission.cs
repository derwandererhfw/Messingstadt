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
        public double[]? Facing { get; set; }

        /// <summary>Besondere Fähigkeit: "schlosser" oder "buchhalter".</summary>
        public string Skill { get; set; } = "";

        /// <summary>Ausbaustufe 1 bis 3: bestimmt das Tempo beim Rennen.</summary>
        public int Legs { get; set; } = 1;

        /// <summary>Ausbaustufe 1 bis 3: bestimmt, wie lange die Puste reicht und wie schnell sie zurückkommt.</summary>
        public int Heart { get; set; } = 1;
    }

    public sealed class ZoneData
    {
        public double X0 { get; set; }
        public double Y0 { get; set; }
        public double X1 { get; set; }
        public double Y1 { get; set; }
    }

    /// <summary>Wachen, die nach dem ersten Schuss in Wellen anrücken. Zeiten in Sekunden.</summary>
    public sealed class ReinforcementData
    {
        public double X { get; set; }
        public double Y { get; set; }
        public string Sprite { get; set; } = "";
        public int FirstDelay { get; set; }
        public int Interval { get; set; }
        public int Count { get; set; }
    }

    /// <summary>Verschlossener Durchgang: Die Felder sind gesperrt, bis ein Schlosser die Tür öffnet.</summary>
    public sealed class DoorData
    {
        public string Name { get; set; } = "";
        public List<double[]> Tiles { get; set; } = new();

        /// <summary>Wo die Tür im Bild zu sehen ist, in Feldern. Ohne Angabe das erste Feld.</summary>
        public double[]? Mark { get; set; }
    }

    /// <summary>Beute, die Geld bringt, wenn ein Agent sie erreicht und der Trupp entkommt.</summary>
    public sealed class LootData
    {
        public string Name { get; set; } = "";
        public double X { get; set; }
        public double Y { get; set; }
        public int Value { get; set; }
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
        public ReinforcementData? Reinforcements { get; set; }
        public List<DoorData> Doors { get; set; } = new();
        public LootData? Loot { get; set; }

        /// <summary>Geld zum Einsatzbeginn. Kommt von der Kampagne.</summary>
        public int Money { get; set; }

        /// <summary>Standorte der Straßenlaternen in Feldern. Nur für die Darstellung.</summary>
        public List<double[]> Lamps { get; set; } = new();
    }

    public static class Loader
    {
        private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

        public static MapData ParseMap(string json) => JsonSerializer.Deserialize<MapData>(json, Options)!;

        public static MissionData ParseMission(string json) => JsonSerializer.Deserialize<MissionData>(json, Options)!;
    }
}
