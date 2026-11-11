using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Godot;

namespace Messingstadt
{
    /// <summary>
    /// Wo im Titelbild was liegt: Fenster, Werk, Tor, Laternen, Schlote, Pfützen und der Weg der Arbeiter. Alles in
    /// Bildpunkten des Titelbilds. Die Vorgaben sind am heutigen title.png ausgemessen. Liegt daneben eine title.json,
    /// ersetzt sie die Vorgaben Feld für Feld: Was dort fehlt, bleibt wie vorgegeben. So kann die Grafik das Bild neu
    /// zeichnen und die Lage mitliefern, ohne dass hier Zahlen nachgezogen werden.
    /// </summary>
    public sealed class TitleLayout
    {
        public Vector2 Size = new(1920f, 1080f);

        // Die Straße in der Flucht: Wer in der Tiefe z steht, hat die Füße auf Vanish.Y + GroundDrop / z und ist
        // FigureHeight / z groß. z 1 ist vorn, das Tor liegt bei GateZ.
        public Vector2 Vanish = new(1215f, 665f);
        public float GroundDrop = 235f;
        public float FigureHeight = 247f;
        public float GateZ = 7.8f;
        public Vector2 Gate = new(1215f, 668f);
        // Seitlicher Abstand der Arbeiter von der Straßenmitte, vorn und am Tor, in Bildpunkten bei z 1.
        public float StartLaneMin = 540f, StartLaneMax = 900f, GateLane = 150f;

        public List<Rect2> Windows = new()
        {
            new(808, 440, 18, 88), new(974, 328, 4, 52), new(1036, 488, 4, 40), new(1394, 512, 4, 34), new(1428, 402, 4, 38),
            new(1428, 478, 4, 42), new(1438, 384, 4, 44), new(1450, 368, 6, 46), new(1450, 458, 4, 46), new(1496, 304, 6, 56),
            new(1516, 160, 6, 64), new(1516, 394, 6, 60), new(1538, 154, 8, 34), new(1538, 498, 8, 64), new(1540, 254, 4, 56),
            new(1564, 92, 10, 56), new(1564, 344, 10, 74), new(1598, 66, 8, 30), new(1598, 164, 8, 80), new(1634, 440, 16, 88),
            new(1742, 380, 18, 110), new(1812, 330, 40, 136),
        };
        public List<Rect2> WorksPanes = new() { new(1092, 534, 12, 104), new(1126, 534, 26, 104), new(1270, 534, 26, 104), new(1318, 534, 26, 104) };
        public Rect2 Sign = new(1108, 372, 240, 38);

        /// <summary>Laternen: Kopf, Fuß am Boden und die Mitte ihres Spiegelstreifens im Pflaster.</summary>
        public List<(Vector2 Head, Vector2 Foot, Vector2 Mirror)> Lamps = new()
        {
            (new(888, 515), new(888, 735), new(884, 905)), (new(1050, 590), new(1050, 700), new(1048, 800)),
            (new(1389, 597), new(1389, 700), new(1388, 800)), (new(1435, 577), new(1435, 712), new(1436, 822)),
            (new(1514, 540), new(1514, 740), new(1512, 900)), (new(1694, 458), new(1694, 765), new(1692, 950)),
        };
        /// <summary>Welche Laterne an ihrem Draht pendelt, als Index in Lamps. Negativ: keine.</summary>
        public int SwingingLamp = 5;
        public List<Vector2> Chimneys = new() { new(1005, 150), new(1157, 62), new(1422, 192) };
        public List<Vector2> Gullies = new() { new(1330, 772), new(1010, 800) };
        /// <summary>Pfützen auf der Straße: Mitte und halbe Breite.</summary>
        public List<Vector3> Puddles = new()
        {
            new(1250, 820, 60), new(1420, 880, 80), new(1090, 905, 70), new(1560, 965, 90), new(1300, 1010, 110), new(980, 860, 50), new(1700, 880, 70),
        };
        /// <summary>Traufen, von denen es tropft: Ort der Kante und Höhe des Bodens darunter.</summary>
        public List<Vector3> Eaves = new() { new(1486, 548, 742), new(1572, 600, 772), new(1668, 560, 800), new(1790, 610, 838), new(1372, 610, 705) };
        /// <summary>Die Querstraße vor dem Werk, auf der die Bahn fährt: Boden, sichtbarer Abschnitt und Höhe des Wagens.</summary>
        public float TramGround = 702f, TramFrom = 1062f, TramTo = 1372f, TramHeight = 64f, TramLength = 230f;
        /// <summary>Wäscheleine zwischen zwei Häusern.</summary>
        public Vector2 LineFrom = new(1498, 300), LineTo = new(1600, 262);
        /// <summary>Höhe am Himmel, in der das Luftschiff zieht, und der Abschnitt, in dem man es sieht.</summary>
        public float AirshipY = 118f, AirshipFrom = 1030f, AirshipTo = 1560f;
        /// <summary>Links davon steht das Menü: Dort bewegt sich nur der Regen.</summary>
        public float QuietUntil = 900f;

        /// <summary>Liest title.json, falls vorhanden. Unbekannte und fehlende Felder bleiben bei der Vorgabe.</summary>
        public static TitleLayout Load(string path)
        {
            var layout = new TitleLayout();
            if (!File.Exists(path))
            {
                return layout;
            }
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                var root = doc.RootElement;
                layout.Size = Point(root, "size", layout.Size);
                layout.Vanish = Point(root, "vanish", layout.Vanish);
                layout.Gate = Point(root, "gate", layout.Gate);
                layout.GroundDrop = Number(root, "groundDrop", layout.GroundDrop);
                layout.FigureHeight = Number(root, "figureHeight", layout.FigureHeight);
                layout.GateZ = Number(root, "gateZ", layout.GateZ);
                layout.GateLane = Number(root, "gateLane", layout.GateLane);
                layout.StartLaneMin = Number(root, "startLaneMin", layout.StartLaneMin);
                layout.StartLaneMax = Number(root, "startLaneMax", layout.StartLaneMax);
                layout.QuietUntil = Number(root, "quietUntil", layout.QuietUntil);
                layout.SwingingLamp = (int)Number(root, "swingingLamp", layout.SwingingLamp);
                Rects(root, "windows", ref layout.Windows);
                Rects(root, "worksPanes", ref layout.WorksPanes);
                if (root.TryGetProperty("sign", out var sign))
                {
                    layout.Sign = Rect(sign);
                }
                Points(root, "chimneys", ref layout.Chimneys);
                Points(root, "gullies", ref layout.Gullies);
                Triples(root, "puddles", ref layout.Puddles);
                Triples(root, "eaves", ref layout.Eaves);
                if (root.TryGetProperty("lamps", out var lamps))
                {
                    layout.Lamps = new();
                    foreach (var lamp in lamps.EnumerateArray())
                    {
                        var head = Point(lamp, "head", Vector2.Zero);
                        var foot = Point(lamp, "foot", head + new Vector2(0f, 200f));
                        layout.Lamps.Add((head, foot, Point(lamp, "mirror", foot + new Vector2(0f, 150f))));
                    }
                }
                if (root.TryGetProperty("tram", out var tram))
                {
                    layout.TramGround = Number(tram, "ground", layout.TramGround);
                    layout.TramFrom = Number(tram, "from", layout.TramFrom);
                    layout.TramTo = Number(tram, "to", layout.TramTo);
                    layout.TramHeight = Number(tram, "height", layout.TramHeight);
                    layout.TramLength = Number(tram, "length", layout.TramLength);
                }
                if (root.TryGetProperty("line", out var line))
                {
                    layout.LineFrom = Point(line, "from", layout.LineFrom);
                    layout.LineTo = Point(line, "to", layout.LineTo);
                }
                if (root.TryGetProperty("airship", out var airship))
                {
                    layout.AirshipY = Number(airship, "y", layout.AirshipY);
                    layout.AirshipFrom = Number(airship, "from", layout.AirshipFrom);
                    layout.AirshipTo = Number(airship, "to", layout.AirshipTo);
                }
            }
            catch (JsonException e)
            {
                GD.PushWarning($"title.json nicht lesbar, es gelten die Vorgaben: {e.Message}");
                return new TitleLayout();
            }
            return layout;
        }

        private static float Number(JsonElement parent, string name, float fallback) =>
            parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetSingle() : fallback;

        private static Vector2 Point(JsonElement parent, string name, Vector2 fallback) =>
            parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array && value.GetArrayLength() >= 2
                ? new Vector2(value[0].GetSingle(), value[1].GetSingle()) : fallback;

        private static Rect2 Rect(JsonElement value) => new(value[0].GetSingle(), value[1].GetSingle(), value[2].GetSingle(), value[3].GetSingle());

        private static void Rects(JsonElement parent, string name, ref List<Rect2> list)
        {
            if (parent.TryGetProperty(name, out var values))
            {
                list = new();
                foreach (var value in values.EnumerateArray())
                {
                    list.Add(Rect(value));
                }
            }
        }

        private static void Points(JsonElement parent, string name, ref List<Vector2> list)
        {
            if (parent.TryGetProperty(name, out var values))
            {
                list = new();
                foreach (var value in values.EnumerateArray())
                {
                    list.Add(new Vector2(value[0].GetSingle(), value[1].GetSingle()));
                }
            }
        }

        private static void Triples(JsonElement parent, string name, ref List<Vector3> list)
        {
            if (parent.TryGetProperty(name, out var values))
            {
                list = new();
                foreach (var value in values.EnumerateArray())
                {
                    list.Add(new Vector3(value[0].GetSingle(), value[1].GetSingle(), value[2].GetSingle()));
                }
            }
        }
    }
}
