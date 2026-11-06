using System;
using System.Collections.Generic;

namespace Messingstadt.Core
{
    /// <summary>Was eine Art Gegner kann. Reichweiten in Einheiten, Tempo in Einheiten pro Takt, Abklingzeit in Takten.</summary>
    public sealed record GuardType(
        string Id, string Title, int Hp, int PatrolSpeed, int AlertSpeed, int Damage, int Cooldown,
        int WeaponRange, int SightRange, int BribeCost, int NoticePercent, bool CallsAlarm, bool Bites, int HearRange,
        bool OrderOnly = false, bool Reports = false)
    {
        private const int Tile = World.TileUnits;

        public static readonly GuardType Wachmann = new(
            "wachmann", "Wachmann", 60, 80, 110, 10, 14, 7 * Tile, 7 * Tile, World.BribeCost, 100, false, false, 0);

        /// <summary>Bemerkt schneller und ruft sofort Alarm, wenn er in den Kampf geht. Bestechung kostet das Doppelte.</summary>
        public static readonly GuardType Offizier = new(
            "offizier", "Offizier", 70, 80, 110, 12, 14, 7 * Tile, 8 * Tile, 2 * World.BribeCost, 70, true, false, 0);

        /// <summary>Gepanzert und langsam, mit Schrotflinte nur auf kurze Entfernung gefährlich. Nicht käuflich.</summary>
        public static readonly GuardType Werkschutz = new(
            "werkschutz", "Werkschutz", 110, 80, 90, 18, 26, 4 * Tile, 6 * Tile, 0, 100, false, false, 0);

        /// <summary>Gewehr: trifft von weit her, schießt aber selten und hält wenig aus.</summary>
        public static readonly GuardType Schuetze = new(
            "schuetze", "Schütze", 50, 80, 100, 18, 40, 11 * Tile, 11 * Tile, World.BribeCost, 100, false, false, 0);

        /// <summary>Wachhund: wittert ringsum und im Dunkeln, ist schnell und beißt. Schlägt an und kann nicht bestochen werden.
        /// Agenten schießen nur auf Befehl auf ihn, denn jeder Schuss macht den Einsatz laut.</summary>
        public static readonly GuardType Hund = new(
            "hund", "Wachhund", 35, 95, 170, 12, 14, 0, 6 * Tile, 0, 50, true, true, 5 * Tile / 2, OrderOnly: true);

        /// <summary>Wachmann mit Hund an der Leine. Wer ihn besticht, hat auch den Hund ruhig.</summary>
        public static readonly GuardType Hundefuehrer = new(
            "hundefuehrer", "Hundeführer", 60, 80, 110, 10, 14, 7 * Tile, 7 * Tile, 3 * World.BribeCost / 2, 100, false, false, 0);

        /// <summary>Unbewaffnet: Bemerkt er jemanden, rennt er zur nächsten Wache und meldet es. Schneller als Gehen, langsamer als Rennen.</summary>
        public static readonly GuardType Laufbursche = new(
            "laufbursche", "Laufbursche", 15, 70, 125, 0, 0, 0, 6 * Tile, World.BribeCost / 4, 100, false, false, 0, OrderOnly: true, Reports: true);

        private static readonly Dictionary<string, GuardType> Table = new(StringComparer.OrdinalIgnoreCase)
        {
            [Wachmann.Id] = Wachmann, [Offizier.Id] = Offizier, [Werkschutz.Id] = Werkschutz, [Schuetze.Id] = Schuetze, [Hund.Id] = Hund,
            [Hundefuehrer.Id] = Hundefuehrer, [Laufbursche.Id] = Laufbursche,
        };

        /// <summary>Art zu einer Kennung aus den Missionsdaten. Ohne Angabe ein Wachmann.</summary>
        public static GuardType ForId(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return Wachmann;
            }
            return Table.TryGetValue(id, out var type) ? type : throw new ArgumentException($"Unbekannte Gegnerart {id}");
        }
    }
}
