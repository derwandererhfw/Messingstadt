using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Godot;

namespace Messingstadt
{
    /// <summary>
    /// Was die Atmosphäre je Karte wissen muss und nicht aus der Geometrie ableiten kann: Sitzplätze der Tauben,
    /// Straßenabläufe und Kellerroste, Fahrbahnen, die Feuertonne der Streikposten, ob es Gehwege für Passanten und eine
    /// eigene Kulisse gibt. Die Daten kommen aus dem Manifest der Karte. Nur für das Kesselviertel, die erste Karte,
    /// gibt es Vorgaben im Code, weil ihr Manifest diese Felder noch nicht hat. Auf jeder anderen Karte bleibt ein
    /// Effekt ohne seine Daten einfach aus.
    /// Unter assets/sites/<Kartenordner>.json liegen die Daten der Atmosphäre. Sie haben dieselben Felder wie das
    /// Manifest und gehen vor. Dort überschreibt sie kein neu erzeugter Kartenordner.
    /// Der Lader der Karten ruft Use mit dem Manifest der Karte auf, bevor die Stadt gebaut wird. Ohne Aufruf gilt
    /// das alte Manifest unter res://assets/manifest.json.
    /// </summary>
    public sealed class MapSite
    {
        private const string LegacyManifest = "res://assets/manifest.json";
        // So heißt der Ordner des Kesselviertels unter assets/maps, falls das alte Manifest dorthin umzieht.
        private static readonly string[] KesselNames = { "01", "map_01", "mission_01", "kesselviertel" };

        public sealed record Roads(float X0, float X1, float Y0, float Y1);

        public sealed record Fire(Vector2 Barrel, Vector2[] Spots, Vector2 Banner);

        /// <summary>Ein Punkt auf dem Weg eines Passanten: wohin, wie lange er dort steht, was er dabei tut, wohin er schaut.</summary>
        public sealed record WalkPoint(Vector2 Tile, float WaitMin, float WaitMax, string Pastime, Vector2? Look);

        /// <summary>
        /// Ein Weg für Passanten. Der erste und der letzte Punkt liegen außerhalb der Karte, dort kommt und geht die Figur.
        /// Cast nennt die Zeilen des Figurenblatts, die diesen Weg gehen dürfen. Leer: jede.
        /// </summary>
        public sealed record Walk(string[] Cast, float Weight, bool BothWays, WalkPoint[] Points);

        /// <summary>
        /// Eine Lichtquelle der Karte, wie die Grafik sie backt: Feld, Höhe in Bildpunkten, Art (elektrisch, gas, feuer),
        /// Farbe und Reichweite in Feldern.
        /// </summary>
        public sealed record LightSource(Vector2 Tile, float Height, string Kind, Color Color, float Reach);

        private static MapSite? _current;

        /// <summary>Die Daten der Karte, auf der gerade gespielt wird.</summary>
        public static MapSite Current => _current ??= Load(ProjectSettings.GlobalizePath(LegacyManifest), TestWithoutKessel() ? false : null);

        // Prüflauf: --ohne-kessel tut so, als wäre die Karte nicht das Kesselviertel. Dann müssen alle Effekte ausbleiben,
        // die auf dessen Vorgaben bauen.
        private static bool TestWithoutKessel() => System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--ohne-kessel") >= 0;

        /// <summary>Stellt auf das Manifest einer Karte um. kessel erzwingt oder verbietet die Vorgaben des Kesselviertels.</summary>
        public static void Use(string manifestPath, bool? kessel = null) => _current = Load(manifestPath, TestWithoutKessel() ? false : kessel);

        public string ManifestPath { get; private set; } = "";

        /// <summary>Die erste Karte. Nur für sie gelten die Vorgaben im Code.</summary>
        public bool Kessel { get; private set; }

        /// <summary>Sitzplätze der Tauben: Feld x, y und Höhe in Bildpunkten.</summary>
        public List<Vector3> Perches { get; } = new();

        /// <summary>Straßenabläufe im Rinnstein: Feld und ob der Bordstein längs y läuft.</summary>
        public List<(Vector2 Tile, bool AlongY)> Gullies { get; } = new();

        /// <summary>Kellerroste an den Hauswänden, aus denen Dampf steigt: x0, y0, x1, y1 in Feldern.</summary>
        public List<Rect2> Grates { get; } = new();

        /// <summary>Die beiden Fahrbahnen der Kreuzung. Null: keine Rinnsteine.</summary>
        public Roads? Road { get; private set; }

        /// <summary>Feuertonne, Plätze der Streikposten und ihr Banner. Null: keine Streikposten.</summary>
        public Fire? Strikers { get; private set; }

        /// <summary>Ob es die Gehwege, die Haltestelle und die Hauswände gibt, auf die Passersby gebaut ist.</summary>
        public bool Passersby { get; private set; }

        /// <summary>Ob auf der Karte die Straßenbahn steht. Sonst leuchten keine Wagenfenster und es tropft von keinem Wagendach.</summary>
        public bool Tram { get; private set; }

        /// <summary>Ob die Kulisse aus skyline.png, skyline_fx.png und skyline.json zu dieser Karte gehört.</summary>
        public bool Skyline { get; private set; }

        /// <summary>Ob sich die Stadt jenseits des Kartenrands im Dunst verliert, auch ohne gemalte Kulisse.</summary>
        public bool Haze { get; private set; } = true;

        /// <summary>Die Lichtquellen aus dem Manifest (sources). Leer: Die Karte nennt keine, dann gelten die Laternen des Einsatzes.</summary>
        public List<LightSource> Sources { get; } = new();

        /// <summary>
        /// Wie unruhig die Laternen brennen: 0 ruhig wie elektrisches Licht, 1 wie Gaslicht, das wogt und zuckt.
        /// Aus dem Manifest (lampUnrest). Fehlt der Eintrag, brennt eine Karte mit Gaslaternen unruhig.
        /// </summary>
        public float LampUnrest { get; private set; }

        /// <summary>Lampen, die an einer Schnur pendeln: Feld x, y, Höhe der Aufhängung und Länge der Schnur in Bildpunkten.</summary>
        public List<(Vector2 Tile, float Height, float Cord)> SwingLamps { get; } = new();

        /// <summary>Die eine Laterne, die flackert und aussetzt. Null: Auf dieser Karte brennen alle ruhig.</summary>
        public Vector2? StutterLamp { get; private set; }

        /// <summary>Wege der Passanten aus den Daten. Leer: keine, außer im Kesselviertel mit seinen festen Gehwegen.</summary>
        public List<Walk> Walks { get; } = new();

        /// <summary>Wie viele Passanten höchstens zugleich unterwegs sind und wie viele schon da sind, wenn der Einsatz beginnt.</summary>
        public int WalkersMax { get; private set; } = 5;

        public int WalkersAtStart { get; private set; } = 4;

        /// <summary>Pause in Sekunden, bis der nächste Passant kommt.</summary>
        public Vector2 WalkerPause { get; private set; } = new(5f, 12f);

        private static MapSite Load(string manifestPath, bool? kessel)
        {
            var site = new MapSite { ManifestPath = manifestPath };
            string folder = Path.GetFileName(Path.GetDirectoryName(manifestPath) ?? "").ToLowerInvariant();
            bool legacy = Path.GetFullPath(manifestPath) == Path.GetFullPath(ProjectSettings.GlobalizePath(LegacyManifest));
            site.Kessel = kessel ?? (legacy || System.Array.IndexOf(KesselNames, folder) >= 0);
            if (site.Kessel)
            {
                site.KesselDefaults();
            }
            // Eine gemalte Kulisse gilt, sobald ihre Bilder im Ordner der Karte liegen.
            string here = Path.GetDirectoryName(manifestPath) ?? "";
            site.Skyline |= !legacy && File.Exists(Path.Combine(here, "skyline.png"));
            // Was im Manifest steht, ersetzt die Vorgabe. Was in site.json steht, ersetzt beides.
            // Die Daten der Atmosphäre liegen unter assets/sites/<Kartenordner>.json: Der Kartenordner selbst wird von der
            // Grafik neu erzeugt und dabei geleert. Eine site.json im Kartenordner gilt weiter, falls dort eine liegt.
            string own = ProjectSettings.GlobalizePath($"res://assets/sites/{folder}.json");
            foreach (string file in new[] { manifestPath, own, Path.Combine(here, "site.json") })
            {
                if (File.Exists(file))
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(file));
                    site.Read(doc.RootElement);
                }
            }
            return site;
        }

        private static Vector2 Pair(JsonElement e) => new(e[0].GetSingle(), e[1].GetSingle());

        private void Read(JsonElement root)
        {
            if (root.TryGetProperty("perches", out var perches))
            {
                Perches.Clear();
                foreach (var p in perches.EnumerateArray())
                {
                    Perches.Add(new Vector3(p[0].GetSingle(), p[1].GetSingle(), p[2].GetSingle()));
                }
            }
            if (root.TryGetProperty("gullies", out var gullies))
            {
                Gullies.Clear();
                foreach (var g in gullies.EnumerateArray())
                {
                    Gullies.Add((Pair(g), g[2].GetString() == "y"));
                }
            }
            if (root.TryGetProperty("grates", out var grates))
            {
                Grates.Clear();
                foreach (var g in grates.EnumerateArray())
                {
                    Grates.Add(new Rect2(g[0].GetSingle(), g[1].GetSingle(), g[2].GetSingle() - g[0].GetSingle(), g[3].GetSingle() - g[1].GetSingle()));
                }
            }
            if (root.TryGetProperty("roads", out var roads))
            {
                Road = new Roads(roads[0].GetSingle(), roads[1].GetSingle(), roads[2].GetSingle(), roads[3].GetSingle());
            }
            if (root.TryGetProperty("tram", out var tram))
            {
                Tram = tram.ValueKind == JsonValueKind.True;
            }
            if (root.TryGetProperty("skyline", out var skyline))
            {
                Skyline = skyline.ValueKind == JsonValueKind.True;
            }
            if (root.TryGetProperty("haze", out var haze))
            {
                Haze = haze.ValueKind == JsonValueKind.True;
            }
            if (root.TryGetProperty("stutterLamp", out var stutter))
            {
                // null schaltet das Flackern ab.
                StutterLamp = stutter.ValueKind == JsonValueKind.Array ? Pair(stutter) : null;
            }
            if (root.TryGetProperty("sources", out var sources))
            {
                Sources.Clear();
                foreach (var s in sources.EnumerateArray())
                {
                    var c = s.GetProperty("color");
                    Sources.Add(new LightSource(
                        new Vector2(s.GetProperty("x").GetSingle(), s.GetProperty("y").GetSingle()), s.GetProperty("z").GetSingle(),
                        s.GetProperty("kind").GetString() ?? "", new Color(c[0].GetSingle(), c[1].GetSingle(), c[2].GetSingle()),
                        s.GetProperty("reach").GetSingle()));
                }
            }
            if (Sources.Exists(s => s.Kind == "gas"))
            {
                LampUnrest = 1f;
            }
            if (root.TryGetProperty("lampUnrest", out var lampUnrest))
            {
                LampUnrest = lampUnrest.GetSingle();
            }
            if (root.TryGetProperty("swingLamps", out var swing))
            {
                SwingLamps.Clear();
                foreach (var l in swing.EnumerateArray())
                {
                    SwingLamps.Add((Pair(l), l[2].GetSingle(), l[3].GetSingle()));
                }
            }
            if (root.TryGetProperty("passersby", out var people))
            {
                ReadPassersby(people);
            }
        }

        /// <summary>
        /// passersby: { "max": 2, "atStart": 1, "pause": [20, 45], "walks": [ { "cast": ["Zivil_Herr"], "weight": 1,
        /// "bothWays": false, "points": [ { "at": [x, y], "wait": [8, 16], "pastime": "smoke", "look": [dx, dy] } ] } ] }.
        /// Nur at ist Pflicht. pastime kennt smoke und read.
        /// </summary>
        private void ReadPassersby(JsonElement people)
        {
            Walks.Clear();
            if (people.TryGetProperty("max", out var max))
            {
                WalkersMax = max.GetInt32();
            }
            WalkersAtStart = people.TryGetProperty("atStart", out var atStart) ? atStart.GetInt32() : System.Math.Min(WalkersAtStart, WalkersMax);
            if (people.TryGetProperty("pause", out var pause))
            {
                WalkerPause = Pair(pause);
            }
            if (!people.TryGetProperty("walks", out var walks))
            {
                return;
            }
            foreach (var w in walks.EnumerateArray())
            {
                var points = new List<WalkPoint>();
                foreach (var p in w.GetProperty("points").EnumerateArray())
                {
                    var wait = p.TryGetProperty("wait", out var waitRange) ? Pair(waitRange) : Vector2.Zero;
                    points.Add(new WalkPoint(Pair(p.GetProperty("at")), wait.X, wait.Y,
                        p.TryGetProperty("pastime", out var pastime) ? pastime.GetString() ?? "" : "",
                        p.TryGetProperty("look", out var look) ? Pair(look) : null));
                }
                if (points.Count < 2)
                {
                    continue;
                }
                var cast = new List<string>();
                if (w.TryGetProperty("cast", out var names))
                {
                    foreach (var n in names.EnumerateArray())
                    {
                        cast.Add(n.GetString() ?? "");
                    }
                }
                Walks.Add(new Walk(cast.ToArray(), w.TryGetProperty("weight", out var weight) ? weight.GetSingle() : 1f,
                    w.TryGetProperty("bothWays", out var both) && both.ValueKind == JsonValueKind.True, points.ToArray()));
            }
        }

        /// <summary>Das Kesselviertel, wie es tools/art/generate_concept.py zeichnet (GULLIES, GRATES, Fahrbahnen).</summary>
        private void KesselDefaults()
        {
            Perches.AddRange(new Vector3[]
            {
                new(5.95f, 2.6f, 128), new(5.95f, 3.2f, 128), new(5.95f, 3.7f, 128), new(5.95f, 4.4f, 128),
                new(2.6f, 16.95f, 170), new(3.3f, 16.95f, 170), new(3.9f, 16.95f, 170),
                // Lager: an der Traufe der rechten Dachseite. Fabrik: in den Kehlen des Sheddachs über der linken Wand.
                new(16.95f, 13.4f, 70), new(16.95f, 14.1f, 70), new(16.95f, 14.7f, 70), new(16.95f, 16.2f, 70),
                new(13.25f, 5.95f, 110), new(14.45f, 5.95f, 110), new(15.65f, 5.95f, 110),
            });
            foreach (var (x, y, alongY) in new[]
            {
                (8.11f, 3.4f, true), (8.11f, 13.3f, true), (9.89f, 16.4f, true), (9.89f, 7.6f, true),
                (4.3f, 9.15f, false), (17.3f, 9.15f, false), (14.5f, 11.85f, false), (6.9f, 11.85f, false),
            })
            {
                Gullies.Add((new Vector2(x, y), alongY));
            }
            foreach (var (x0, y0, x1, y1) in new[]
            {
                (2.2f, 6.03f, 2.7f, 6.2f), (4.4f, 6.03f, 4.9f, 6.2f), (6.03f, 2.6f, 6.2f, 3.1f), (2.0f, 17.03f, 2.5f, 17.2f), (6.03f, 13.2f, 6.2f, 13.7f),
                (6.03f, 15.6f, 6.2f, 16.1f), (13.0f, 18.03f, 13.5f, 18.2f), (17.03f, 14.2f, 17.2f, 14.7f), (18.03f, 2.2f, 18.2f, 2.7f),
            })
            {
                Grates.Add(new Rect2(x0, y0, x1 - x0, y1 - y0));
            }
            // Längs x zwischen y 9 und 12, längs y zwischen x 8 und 10.
            Road = new Roads(8f, 10f, 9f, 12f);
            Strikers = new Fire(new Vector2(13.25f, 18.7f), new Vector2[] { new(12.65f, 18.55f), new(13.85f, 18.6f), new(13.4f, 19.3f) }, new Vector2(12.35f, 18.02f));
            Passersby = true;
            StutterLamp = new Vector2(5.6f, 10.5f);
            // Über der Seitentür des Hotels, wo der Arbeiter raucht.
            SwingLamps.Add((new Vector2(6.12f, 15.75f), 44f, 10f));
            Tram = true;
            Skyline = true;
        }
    }
}
