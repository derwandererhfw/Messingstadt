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

        /// <summary>Art des Gegners, siehe GuardType. Ohne Angabe ein Wachmann.</summary>
        public string Type { get; set; } = "";

        /// <summary>Name des Menschen hinter dem Posten, etwa "Krüger". Name bleibt der Posten, an dem Schlüssel und Hundeführer hängen.</summary>
        public string Person { get; set; } = "";

        /// <summary>Geht nur mit, wenn im letzten Einsatz Wachen starben: der zweite Mann einer Streife. Höchstens World.MaxBloodPartners.</summary>
        public bool AfterBlood { get; set; }

        /// <summary>Schaut weg, wenn im letzten Einsatz niemand starb. Höchstens World.MaxAtEase.</summary>
        public bool AtEase { get; set; }

        /// <summary>Nimmt kein Geld, obwohl seine Art käuflich ist.</summary>
        public bool NoBribe { get; set; }

        /// <summary>Zeit bis zum Bemerken in Prozent, unabhängig vom letzten Einsatz. Über 100 schaut er gern weg.</summary>
        public int NoticeScale { get; set; } = 100;

        /// <summary>Name einer Wache aus einem früheren Einsatz, die diesen Posten übernimmt, wenn sie noch lebt. Sonst bleibt es bei Person.</summary>
        public string Returning { get; set; } = "";

        /// <summary>Namen der Türen, zu denen diese Wache den Schlüssel trägt. Wer sie besticht, bekommt ihn, wer sie niederschlägt, hebt ihn auf.</summary>
        public List<string> Keys { get; set; } = new();

        /// <summary>Für einen Hund an der Leine: der Name seines Hundeführers.</summary>
        public string Handler { get; set; } = "";
        public double X { get; set; }
        public double Y { get; set; }
        public List<double[]> Patrol { get; set; } = new();
        public double[]? Facing { get; set; }

        /// <summary>Besondere Fähigkeit: "schlosser" oder "buchhalter".</summary>
        public string Skill { get; set; } = "";

        /// <summary>Beruf für die Akte, etwa "Truppführerin". Ohne Angabe gilt der Beruf aus der Fähigkeit.</summary>
        public string Role { get; set; } = "";

        public string RoleName() => Role.Length > 0 ? Role
            : Skill == World.SkillLocksmith ? "Schlosser" : Skill == World.SkillBookkeeper ? "Buchhalter" : "Agent";

        /// <summary>Gegenstände eines Agenten nach Kennung aus data/waffen.json. Ohne Angabe die Grundausstattung, siehe ItemCatalog.StarterKit.</summary>
        public List<string>? Items { get; set; }

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

        /// <summary>Art der Verstärkung, siehe GuardType. Ohne Angabe Wachmänner.</summary>
        public string Type { get; set; } = "";
        public int FirstDelay { get; set; }
        public int Interval { get; set; }
        public int Count { get; set; }

        /// <summary>Posten des Telefonisten. Ohne Angabe gibt es kein Telefon, die Verstärkung kommt nach FirstDelay in voller Zahl.</summary>
        public string Phone { get; set; } = "";

        /// <summary>Sekunden vom fertigen Anruf bis zum ersten Mann.</summary>
        public int PhoneDelay { get; set; } = 1;

        /// <summary>Sekunden vom Alarm bis zum ersten Mann, wenn niemand anrufen kann. 0 heißt doppelt so lange wie FirstDelay.</summary>
        public int LateDelay { get; set; }

        /// <summary>So viele kommen ohne Anruf. 0 heißt die Hälfte, mindestens einer.</summary>
        public int LateCount { get; set; }

        /// <summary>Betten in der Kaserne: Mehr rücken nie an, auch nicht mit Fahndung. 0 heißt keine Grenze.</summary>
        public int Beds { get; set; }

        /// <summary>Namen der Leute, die als Verstärkung kommen, in dieser Reihenfolge. Fehlen welche, bleiben sie namenlos.</summary>
        public List<string> Persons { get; set; } = new();
    }

    /// <summary>Verschlossener Durchgang: Die Felder sind gesperrt, bis ein Schlosser die Tür öffnet.</summary>
    public sealed class DoorData
    {
        public string Name { get; set; } = "";
        public List<double[]> Tiles { get; set; } = new();

        /// <summary>Wo die Tür im Bild zu sehen ist, in Feldern. Ohne Angabe das erste Feld.</summary>
        public double[]? Mark { get; set; }

        /// <summary>Keine Tür, sondern der Telefondraht an der Wand: Wer ihn "öffnet", kappt ihn. Das Feld bleibt gesperrt.</summary>
        public bool Wire { get; set; }
    }

    /// <summary>Ein Gefangener in einem Einsatz der Art Befreien.</summary>
    public sealed class PrisonerData
    {
        public string Name { get; set; } = "";

        /// <summary>Kurzer Name für das Telegramm, etwa WEISS.</summary>
        public string Short { get; set; } = "";
        public string Sprite { get; set; } = "";
        public double X { get; set; }
        public double Y { get; set; }

        /// <summary>Name der Zellentür aus Doors. Leer, wenn er nicht eingeschlossen ist.</summary>
        public string Door { get; set; } = "";
        public int Hp { get; set; } = 60;

        /// <summary>Zurufe für die Darstellung: freed, follow, noticed, shots, wagon.</summary>
        public Dictionary<string, string> Lines { get; set; } = new();
    }

    /// <summary>Zeilen für den Telegrammstreifen beim Befreien. {NAME} ist der kurze Name eines Gefangenen.</summary>
    public sealed class RescueTextData
    {
        public string Start { get; set; } = "GEFANGENE BEFREIEN STOP DANN ZUM WAGEN STOP";
        public string Opening { get; set; } = "ZELLE WIRD GEÖFFNET STOP STEHEN BLEIBEN STOP";
        public string OneFree { get; set; } = "{NAME} FREI STOP DER ZWEITE FEHLT NOCH STOP";
        public string AllFree { get; set; } = "ALLE FREI STOP ZUM WAGEN STOP SIE SIND LANGSAM STOP";
        public string OneDead { get; set; } = "{NAME} TOT STOP DEN ANDEREN RAUSBRINGEN STOP";
        public string Boarding { get; set; } = "EINSTEIGEN STOP STEHEN BLEIBEN STOP";
    }

    /// <summary>Beute, die Geld bringt, wenn ein Agent sie erreicht und der Trupp entkommt.</summary>
    public sealed class LootData
    {
        public string Name { get; set; } = "";
        public double X { get; set; }
        public double Y { get; set; }
        public int Value { get; set; }
    }

    /// <summary>Wie hell es auf der Karte ist, je Zelle in Prozent. Stammt aus dem Pathtracer, siehe tools/art/bake_light.py.</summary>
    public sealed class LightData
    {
        /// <summary>Zellen je Feld.</summary>
        public int Cells { get; set; } = 1;

        /// <summary>Kantenlänge des Gitters in Zellen.</summary>
        public int Size { get; set; }
        public int[] Levels { get; set; } = System.Array.Empty<int>();
    }

    /// <summary>Kopf und Zeile einer Schlagzeile. Platzhalter siehe Headlines.</summary>
    public sealed class HeadlineText
    {
        public string Head { get; set; } = "";
        public string Line { get; set; } = "";
    }

    /// <summary>Was die Zeitung nach diesem Einsatz schreibt, je nach Ausgang. Fehlende Fälle nehmen einen allgemeinen Text.</summary>
    public sealed class HeadlineData
    {
        public HeadlineText? Unseen { get; set; }
        public HeadlineText? Quiet { get; set; }
        public HeadlineText? Loud { get; set; }
        public HeadlineText? Lost { get; set; }

        /// <summary>Abbruch nach dem ersten Schuss.</summary>
        public HeadlineText? Abandoned { get; set; }

        /// <summary>Abbruch, bevor ein Schuss fiel.</summary>
        public HeadlineText? Retreat { get; set; }

        /// <summary>Satz für den Platzhalter {loot}, wenn die Beute fehlt.</summary>
        public string? Loot { get; set; }
    }

    /// <summary>Telegramme von K. nach dem Einsatz. Fehlende nehmen einen allgemeinen Text.</summary>
    public sealed class TelegramData
    {
        public string? Lost { get; set; }

        /// <summary>Abbruch nach dem ersten Schuss.</summary>
        public string? Abandoned { get; set; }

        /// <summary>Rückzug, bevor ein Schuss fiel.</summary>
        public string? Retreat { get; set; }
    }

    /// <summary>Sätze, die nur zu diesem Einsatz passen. Allgemeine Sätze ohne Spielhinweis stehen in game/Chatter.cs.</summary>
    public sealed class ChatterData
    {
        public List<string> Guards { get; set; } = new();
        public List<string> Others { get; set; } = new();

        /// <summary>Was eine bestochene Wache sagt, nach ihrem Namen.</summary>
        public Dictionary<string, string> Bribed { get; set; } = new();
    }

    public sealed class MissionData
    {
        /// <summary>Kennung der Regelkarte in data, etwa map_02. Ohne Angabe die Nummer der Mission.</summary>
        public string MapId { get; set; } = "";

        /// <summary>Kennung der Lichtkarte in data. Fehlt die Datei, gilt überall dieselbe Reichweite beim Bemerken.</summary>
        public string LightId { get; set; } = "";

        /// <summary>Ordner der Grafik unter game/assets. Leer für die Stadt des ersten Einsatzes.</summary>
        public string Scene { get; set; } = "";

        /// <summary>Gespräche nur für diesen Einsatz. Ohne Angabe gelten die Listen des ersten Einsatzes.</summary>
        public ChatterData? Chatter { get; set; }

        public string Title { get; set; } = "";
        public string Briefing { get; set; } = "";
        public int Seed { get; set; }
        public List<UnitData> Agents { get; set; } = new();
        public List<UnitData> Guards { get; set; } = new();

        /// <summary>Was die Wachen vom letzten Einsatz wissen, siehe World.Recall.cs. Kommt von der Kampagne.</summary>
        public GuardMemoryData? Memory { get; set; }

        /// <summary>Dieselbe Mission mit anderer Wachenliste, ohne das Original zu ändern.</summary>
        public MissionData WithGuards(List<UnitData> guards)
        {
            var copy = (MissionData)MemberwiseClone();
            copy.Guards = guards;
            return copy;
        }
        public double[] Item { get; set; } = new double[2];
        public string ItemName { get; set; } = "";
        public ZoneData Exit { get; set; } = new();
        public ReinforcementData? Reinforcements { get; set; }
        public List<DoorData> Doors { get; set; } = new();
        public LootData? Loot { get; set; }

        /// <summary>Gefangene. Gibt es welche, ist der Einsatz von der Art Befreien und Item zählt nicht.</summary>
        public List<PrisonerData> Prisoners { get; set; } = new();
        public RescueTextData? RescueTexts { get; set; }

        /// <summary>Zeitungstexte nach dem Einsatz. Ohne Angabe allgemeine Texte.</summary>
        public HeadlineData? Headlines { get; set; }

        /// <summary>Ort und Uhrzeit für die Karte vor dem Einsatz.</summary>
        public string Place { get; set; } = "";
        public string Time { get; set; } = "";

        public TelegramData? Telegrams { get; set; }

        /// <summary>Helligkeit der Karte. Ohne Angabe gilt überall dieselbe Reichweite beim Bemerken.</summary>
        public LightData? Light { get; set; }

        /// <summary>Waffenkatalog, steht nicht in der Missionsdatei. Ohne Angabe gibt es nur die Pistole der ersten Fassung.</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public ItemCatalog? Catalog { get; set; }

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

        public static LightData ParseLight(string json) => JsonSerializer.Deserialize<LightData>(json, Options)!;

        public static ItemCatalog ParseCatalog(string json) => JsonSerializer.Deserialize<ItemCatalog>(json, Options)!;

        /// <summary>
        /// Lädt einen Einsatz nach seiner Kennung aus dem Datenordner: Mission, dazu Karte und Licht, die sie nennt.
        /// mission_02 nimmt ohne Angabe map_02 und light_02.
        /// </summary>
        public static (MapData Map, MissionData Mission) LoadMission(string dataDir, string missionId, ItemCatalog? catalog = null)
        {
            var mission = ParseMission(System.IO.File.ReadAllText(System.IO.Path.Combine(dataDir, missionId + ".json")));
            string number = missionId.Contains('_') ? missionId[(missionId.LastIndexOf('_') + 1)..] : missionId;
            if (mission.MapId.Length == 0)
            {
                mission.MapId = "map_" + number;
            }
            if (mission.LightId.Length == 0)
            {
                mission.LightId = "light_" + number;
            }
            string light = System.IO.Path.Combine(dataDir, mission.LightId + ".json");
            mission.Light = System.IO.File.Exists(light) ? ParseLight(System.IO.File.ReadAllText(light)) : null;
            mission.Catalog = catalog;
            var map = ParseMap(System.IO.File.ReadAllText(System.IO.Path.Combine(dataDir, mission.MapId + ".json")));
            return (map, mission);
        }
    }
}
