using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Godot;

namespace Messingstadt
{
    /// <summary>
    /// Wo im Titelbild was liegt: Ebenen, Fenster, Leuchtschrift, Tor, Schlote, Laternen, Pfützen und der Weg der
    /// Arbeiter. Alles in Bildpunkten des ganzen Titelbilds, Ursprung oben links. Die Grafik schreibt das nach
    /// game/assets/title/title.json (tools/art/title.py). Fehlt die Datei, gelten Vorgaben, die am alten, einteiligen
    /// title.png ausgemessen sind. Was die JSON nicht kennt (Gullys, Traufen, Bahn, Luftschiff), wird aus dem Rest
    /// abgeleitet oder darf als zusätzliches Feld in der JSON stehen.
    /// </summary>
    public sealed class TitleLayout
    {
        // So groß sind die Dinge der Szene in Metern. Der Maßstab am Ort (Bildpunkte je Meter) kommt aus dem Weg.
        public const float PersonMeters = 1.75f;
        private const float LampMeters = 4.3f;
        private const float MirrorMeters = 3.3f;
        private const float TramHeightMeters = 3.3f;
        private const float TramLengthMeters = 11f;
        private const float DefaultFrontScale = 141f;

        public sealed record Layer(string Name, string File, string? Lit, Rect2 Area, float Depth);

        public Vector2 Size = new(1920f, 1080f);
        public Vector2 Vanish = new(1215f, 665f);

        /// <summary>Ebenen von hinten nach vorn. Leer heißt: Es gibt nur das fertige title.png.</summary>
        public List<Layer> Layers = new();

        /// <summary>Fenster mit dem Namen ihrer Ebene. Mit Ebenen liegen sie dunkel im Bild und werden aus der Leuchtebene eingeschaltet.</summary>
        public List<(Rect2 Area, string Layer)> Windows = new();
        public Rect2 Sign;
        public List<Rect2> Letters = new();
        public Rect2 GateArea;
        /// <summary>Mündung der Schlote: Ort und Breite.</summary>
        public List<Vector3> Stacks = new();
        /// <summary>Laternen: Mitte des Glases und Maßstab am Ort in Bildpunkten je Meter.</summary>
        public List<(Vector2 Head, float Scale)> Lamps = new();
        /// <summary>Pfützen: Mitte und Halbachsen.</summary>
        public List<(Vector2 At, Vector2 Radius)> Puddles = new();
        /// <summary>Weg der Arbeiter von vorn bis zum Tor: Fußpunkte und Maßstab in Bildpunkten je Meter.</summary>
        public List<(Vector2 At, float Scale)> Walk = new();

        // Abgeleitet oder als Zusatzfeld in der JSON.
        public List<Rect2> WorksPanes = new();
        public List<Vector2> Gullies = new();
        /// <summary>Stellen, von denen es tropft: Ort oben und Höhe des Bodens darunter.</summary>
        public List<Vector3> Eaves = new();
        public int SwingingLamp;
        public float TramGround, TramFrom, TramTo, TramHeight, TramLength;
        public Vector2 LineFrom, LineTo;
        public bool OwnLaundry;
        public float AirshipY, AirshipFrom, AirshipTo;
        /// <summary>Links davon steht das Menü: Dort bewegt sich nur der Regen.</summary>
        public float QuietUntil = 900f;

        public bool Layered => Layers.Count > 0;

        /// <summary>Mitte des Tors am Boden. Dorthin geht der Zug, dort glüht der Ofen.</summary>
        public Vector2 Gate => new(GateArea.GetCenter().X, GateArea.End.Y);

        public float GateScale => Walk.Count > 0 ? Walk[^1].Scale : DefaultFrontScale / 7.8f;

        public float DepthOf(string layer, float fallback)
        {
            foreach (var l in Layers)
            {
                if (l.Name == layer)
                {
                    return l.Depth;
                }
            }
            return fallback;
        }

        public Vector2 LampFoot(int i) => Lamps[i].Head + new Vector2(0f, LampMeters * Lamps[i].Scale);

        public Vector2 LampMirror(int i) => LampFoot(i) + new Vector2(0f, MirrorMeters * Lamps[i].Scale);

        /// <summary>Liest title.json. Fehlt sie oder ist sie unlesbar, gelten die Vorgaben für das alte Bild.</summary>
        public static TitleLayout Load(string path)
        {
            if (File.Exists(path))
            {
                try
                {
                    var layout = new TitleLayout();
                    using var doc = JsonDocument.Parse(File.ReadAllText(path));
                    layout.Read(doc.RootElement);
                    layout.Derive(doc.RootElement);
                    return layout;
                }
                catch (System.Exception e) when (e is JsonException or System.InvalidOperationException or KeyNotFoundException or System.IndexOutOfRangeException)
                {
                    GD.PushWarning($"title.json nicht lesbar, es gelten die Vorgaben: {e.Message}");
                }
            }
            return Default();
        }

        private void Read(JsonElement root)
        {
            Size = Point(root, "size", Size);
            Vanish = Point(root, "vanish", Vanish);
            if (root.TryGetProperty("layers", out var layers))
            {
                foreach (var l in layers.EnumerateArray())
                {
                    string? lit = l.TryGetProperty("lit", out var litFile) && litFile.ValueKind == JsonValueKind.String ? litFile.GetString() : null;
                    Layers.Add(new Layer(l.GetProperty("name").GetString() ?? "", l.GetProperty("file").GetString() ?? "", lit,
                        new Rect2(Number(l, "x", 0f), Number(l, "y", 0f), Number(l, "w", Size.X), Number(l, "h", Size.Y)), Number(l, "depth", 0.5f)));
                }
            }
            if (root.TryGetProperty("windows", out var windows))
            {
                foreach (var w in windows.EnumerateArray())
                {
                    Windows.Add((Rect(w.GetProperty("rect")), w.TryGetProperty("layer", out var layer) ? layer.GetString() ?? "" : ""));
                }
            }
            if (root.TryGetProperty("sign", out var sign))
            {
                Sign = Rect(sign.GetProperty("rect"));
                if (sign.TryGetProperty("letters", out var letters))
                {
                    foreach (var letter in letters.EnumerateArray())
                    {
                        Letters.Add(Rect(letter));
                    }
                }
            }
            if (root.TryGetProperty("gate", out var gate))
            {
                GateArea = Rect(gate.GetProperty("rect"));
            }
            Each(root, "stacks", s => Stacks.Add(new Vector3(Number(s, "x", 0f), Number(s, "y", 0f), Number(s, "width", 30f))));
            Each(root, "lamps", l => Lamps.Add((new Vector2(Number(l, "x", 0f), Number(l, "y", 0f)), Number(l, "scale", 40f))));
            Each(root, "puddles", p => Puddles.Add((new Vector2(Number(p, "x", 0f), Number(p, "y", 0f)), new Vector2(Number(p, "rx", 40f), Number(p, "ry", 10f)))));
            Each(root, "walk", p => Walk.Add((new Vector2(Number(p, "x", 0f), Number(p, "y", 0f)), Number(p, "scale", 40f))));
        }

        /// <summary>Was die Grafik nicht mitliefert, ergibt sich aus Tor, Weg und Laternen. Zusatzfelder in der JSON gehen vor.</summary>
        private void Derive(JsonElement root)
        {
            if (Walk.Count < 2)
            {
                Walk = Default().Walk;
            }
            if (GateArea.Size == Vector2.Zero)
            {
                GateArea = new Rect2(Walk[^1].At - new Vector2(40f, 60f), new Vector2(80f, 60f));
            }
            QuietUntil = Number(root, "quietUntil", QuietUntil);
            foreach (var (area, layer) in Windows)
            {
                if (layer == "factory")
                {
                    WorksPanes.Add(area);
                }
            }
            Each(root, "gullies", g => Gullies.Add(new Vector2(Number(g, "x", 0f), Number(g, "y", 0f))));
            if (Gullies.Count == 0)
            {
                // Zwei Gullys neben dem Weg, einer im Mittelgrund, einer weiter hinten.
                foreach (float share in new[] { 0.45f, 0.7f })
                {
                    var (at, scale) = Walk[(int)(share * (Walk.Count - 1))];
                    Gullies.Add(at + new Vector2(-2.4f * scale, 0.15f * scale));
                }
            }
            Each(root, "eaves", e => Eaves.Add(new Vector3(Number(e, "x", 0f), Number(e, "y", 0f), Number(e, "ground", 0f))));
            for (int i = 0; i < Lamps.Count && Eaves.Count < Lamps.Count; i++)
            {
                // Ohne Traufen tropft es von den Laternen.
                Eaves.Add(new Vector3(Lamps[i].Head.X, Lamps[i].Head.Y + 0.4f * Lamps[i].Scale, LampFoot(i).Y));
            }
            // Die größte Laterne rechts vom Menü pendelt.
            SwingingLamp = -1;
            for (int i = 0; i < Lamps.Count; i++)
            {
                if (Lamps[i].Head.X > QuietUntil && (SwingingLamp < 0 || Lamps[i].Scale > Lamps[SwingingLamp].Scale))
                {
                    SwingingLamp = i;
                }
            }
            float gateScale = GateScale;
            TramGround = Gate.Y + 0.5f * gateScale;
            TramFrom = QuietUntil - 200f;
            TramTo = Size.X + 50f;
            TramHeight = TramHeightMeters * gateScale * 1.1f;
            TramLength = TramLengthMeters * gateScale * 1.1f;
            if (root.TryGetProperty("tram", out var tram))
            {
                TramGround = Number(tram, "ground", TramGround);
                TramFrom = Number(tram, "from", TramFrom);
                TramTo = Number(tram, "to", TramTo);
            }
            float top = Size.Y;
            foreach (var stack in Stacks)
            {
                top = Mathf.Min(top, stack.Y);
            }
            AirshipY = Mathf.Max(40f, (Stacks.Count > 0 ? top : Size.Y * 0.15f) + 30f);
            AirshipFrom = QuietUntil;
            AirshipTo = Size.X - 150f;
            // Wäscheleinen malt die Grafik in die vordere Ebene, eigene Wäsche gibt es dann nicht.
            OwnLaundry = false;
        }

        /// <summary>Die Lage im alten, einteiligen title.png.</summary>
        private static TitleLayout Default()
        {
            var layout = new TitleLayout { GateArea = new Rect2(1165f, 600f, 100f, 68f), Sign = new Rect2(1108, 372, 240, 38), OwnLaundry = true };
            foreach (var w in new Rect2[]
            {
                new(808, 440, 18, 88), new(974, 328, 4, 52), new(1036, 488, 4, 40), new(1394, 512, 4, 34), new(1428, 402, 4, 38),
                new(1428, 478, 4, 42), new(1438, 384, 4, 44), new(1450, 368, 6, 46), new(1450, 458, 4, 46), new(1496, 304, 6, 56),
                new(1516, 160, 6, 64), new(1516, 394, 6, 60), new(1538, 154, 8, 34), new(1538, 498, 8, 64), new(1540, 254, 4, 56),
                new(1564, 92, 10, 56), new(1564, 344, 10, 74), new(1598, 66, 8, 30), new(1598, 164, 8, 80), new(1634, 440, 16, 88),
                new(1742, 380, 18, 110), new(1812, 330, 40, 136),
            })
            {
                layout.Windows.Add((w, ""));
            }
            layout.WorksPanes = new() { new(1092, 534, 12, 104), new(1126, 534, 26, 104), new(1270, 534, 26, 104), new(1318, 534, 26, 104) };
            foreach (var (head, foot) in new[]
            {
                (new Vector2(888, 515), 735f), (new Vector2(1050, 590), 700f), (new Vector2(1389, 597), 700f), (new Vector2(1435, 577), 712f),
                (new Vector2(1514, 540), 740f), (new Vector2(1694, 458), 765f),
            })
            {
                layout.Lamps.Add((head, (foot - head.Y) / LampMeters));
            }
            layout.SwingingLamp = 5;
            layout.Stacks = new() { new(1005, 150, 30), new(1157, 62, 30), new(1422, 192, 30) };
            layout.Gullies = new() { new(1330, 772), new(1010, 800) };
            foreach (var p in new Vector3[] { new(1250, 820, 60), new(1420, 880, 80), new(1090, 905, 70), new(1560, 965, 90), new(1300, 1010, 110), new(980, 860, 50), new(1700, 880, 70) })
            {
                layout.Puddles.Add((new Vector2(p.X, p.Y), new Vector2(p.Z, p.Z * 0.18f)));
            }
            layout.Eaves = new() { new(1486, 548, 742), new(1572, 600, 772), new(1668, 560, 800), new(1790, 610, 838), new(1372, 610, 705) };
            // Der Weg in der Flucht der Straße: Füße bei Vanish.Y + 235 / z, vorn rechts, am Tor in der Mitte.
            foreach (float z in new[] { 0.6f, 0.8f, 1f, 1.3f, 1.7f, 2.2f, 3f, 4f, 5.5f, 7.8f })
            {
                float progress = Mathf.SmoothStep(0f, 1f, (z - 0.6f) / 7.2f);
                layout.Walk.Add((new Vector2(layout.Vanish.X + Mathf.Lerp(720f, 0f, progress) / z, layout.Vanish.Y + 235f / z), DefaultFrontScale / z));
            }
            layout.TramGround = 702f;
            layout.TramFrom = 1062f;
            layout.TramTo = 1372f;
            layout.TramHeight = 64f;
            layout.TramLength = 230f;
            layout.LineFrom = new Vector2(1498, 300);
            layout.LineTo = new Vector2(1600, 262);
            layout.AirshipY = 118f;
            layout.AirshipFrom = 1030f;
            layout.AirshipTo = 1560f;
            return layout;
        }

        private static void Each(JsonElement parent, string name, System.Action<JsonElement> read)
        {
            if (parent.TryGetProperty(name, out var values) && values.ValueKind == JsonValueKind.Array)
            {
                foreach (var value in values.EnumerateArray())
                {
                    read(value);
                }
            }
        }

        private static float Number(JsonElement parent, string name, float fallback) =>
            parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetSingle() : fallback;

        private static Vector2 Point(JsonElement parent, string name, Vector2 fallback) =>
            parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array && value.GetArrayLength() >= 2
                ? new Vector2(value[0].GetSingle(), value[1].GetSingle()) : fallback;

        private static Rect2 Rect(JsonElement value) => new(value[0].GetSingle(), value[1].GetSingle(), value[2].GetSingle(), value[3].GetSingle());
    }
}
