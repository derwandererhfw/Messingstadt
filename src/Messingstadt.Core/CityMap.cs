using System.Collections.Generic;
using System.Text.Json;

namespace Messingstadt.Core
{
    public enum DistrictOwner
    {
        Brandt,
        RoterHammer,
    }

    /// <summary>Was der Stadtplan zu einem Viertel zeigt.</summary>
    public enum DistrictState
    {
        /// <summary>Sichtbar, aber gesperrt: Es gibt dort noch keinen Einsatz.</summary>
        NoMission,

        /// <summary>Gesperrt, bis die Viertel aus Requires dem Roten Hammer gehören.</summary>
        Locked,
        Open,

        /// <summary>Gehört dem Roten Hammer.</summary>
        Taken,
    }

    /// <summary>Ein Viertel des Stadtplans mit seinem Auftrag, aus data/stadtplan.json.</summary>
    public sealed class DistrictData
    {
        /// <summary>Feste Kennung, viertel_1 bis viertel_5. Steht im Spielstand.</summary>
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";

        /// <summary>Auftragsart, etwa Diebstahl oder Sabotage.</summary>
        public string Kind { get; set; } = "";
        public string Briefing { get; set; } = "";

        /// <summary>Kennung der Mission, etwa mission_01. Leer, solange es keinen Einsatz gibt.</summary>
        public string Mission { get; set; } = "";

        /// <summary>Grundlohn für einen Sieg. Die Zuschläge für Leise und Unentdeckt kommen dazu.</summary>
        public int Reward { get; set; } = Campaign.BaseReward;

        /// <summary>Kennungen der Viertel, die vorher dem Roten Hammer gehören müssen.</summary>
        public List<string> Requires { get; set; } = new();
    }

    /// <summary>Der Stadtplan: die Viertel in fester Reihenfolge.</summary>
    public sealed class CityMapData
    {
        public List<DistrictData> Districts { get; set; } = new();

        public DistrictData? Get(string id) => Districts.Find(d => d.Id == id);
    }

    public static class CityMap
    {
        private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

        public static CityMapData Parse(string json) => JsonSerializer.Deserialize<CityMapData>(json, Options) ?? new CityMapData();
    }
}
