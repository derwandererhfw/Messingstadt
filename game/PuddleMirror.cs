using System.Collections.Generic;
using System.IO;
using Godot;
using Messingstadt.Core;

namespace Messingstadt
{
    /// <summary>
    /// Die Pfützen als Wasser: eine dunkle, glatte Fläche mit hellem Rand, in der sich Laternen, Fenster, Leuchtschrift,
    /// Feuer und die Figuren spiegeln. Regentropfen lassen das Spiegelbild zittern. Welche Bildpunkte Wasser sind,
    /// steht in ground.png: Dort sind die Pfützen bläulich-dunkel eingefärbt. Dazu kommen Pfützen genau dort, wo ein
    /// Licht sein Spiegelbild hinwirft, auf freiem Pflaster, das kein Haus von vorn verdeckt. Liegt der Boden auch fein
    /// vor (hd/ground.png), wird die sichtbare Maske in dessen Auflösung gebaut und ihr Rand weich geglättet, damit das
    /// Wasser genau auf den feinen Pfützen liegt. Welche Lichter sich spiegeln, entscheidet weiter das einfache Raster.
    /// Reine Darstellung.
    /// </summary>
    public partial class PuddleMirror : Node2D
    {
        // So färbt scatter_dirt in tools/art/generate_concept.py eine Pfütze: 80 Prozent Pfützenfarbe über dem Boden.
        private static readonly Color PuddleColor = new(34 / 255f, 40 / 255f, 56 / 255f);
        private const float PuddleShare = 0.8f;
        private const float MinBlueOverRed = 10 / 255f;
        private const float SearchScale = 1.4f;

        private const float LampHeadHeight = 54f;
        private const float FigureShade = 0.5f;
        private const float FigureAlpha = 0.62f;
        private const float PostAlpha = 0.45f;

        // Spiegelbilder: Größe der Lichtquelle im Bild und wie weit Regenwellen das Bild senkrecht ziehen.
        private const float MinSmear = 1.3f;
        private const float RainSmear = 1.7f;
        private static readonly Vector2 LampSize = new(7f, 8f);
        private static readonly Vector2 PaneSize = new(10f, 15f);
        private static readonly Vector2 LetterSize = new(9f, 11f);
        private static readonly Vector2 FireSize = new(10f, 9f);
        private const float BarrelFlameHeight = 26f;
        private const int MirrorPuddleSeed = 1927;
        private const float LampPuddleShare = 0.75f;
        private const float PanePuddleShare = 0.3f;
        private const float MinMirrorPuddle = 8f;
        private const float MaxMirrorPuddle = 15f;
        private const float AlreadyWetShare = 0.3f;
        private static readonly Color UnchangedWindow = new(0.62f, 0.46f, 0.26f);
        private static readonly Color IdleHall = new(0.24f, 0.22f, 0.2f);

        private const string SurfaceShader = @"
shader_type canvas_item;
uniform sampler2D ground_light : filter_linear;
uniform sampler2D ground_light_b : filter_linear;
uniform vec2 ground_size;
uniform vec3 lamp_color;
uniform vec3 fire_color;
uniform float stutter = 1.0;
uniform float unrest = 1.0;
uniform float fire = 1.0;
uniform vec3 weld = vec3(0.0);
uniform sampler2D noise_map : filter_linear, repeat_enable;
uniform vec3 night;
uniform float lightning = 0.0;
uniform float rain = 0.3;
void fragment() {
    vec4 m = texture(TEXTURE, UV);
    // Die Maske ist weich gefiltert: Die Linie bei halber Deckung ist der Rand, über einen Bildschirmpunkt geglättet.
    float cover = clamp((m.a - 0.5) / max(fwidth(m.a), 0.001) + 0.5, 0.0, 1.0);
    if (cover <= 0.0) {
        discard;
    }
    // Ort in einfachen Bildpunkten, auf halbe Punkte gerastert wie der feine Boden.
    vec2 px = floor(UV * ground_size * 2.0) * 0.5;
    vec3 a = texture(ground_light, UV).rgb;
    vec4 b = texture(ground_light_b, UV);
    // Dasselbe bewegte Licht wie auf dem Boden: Wackelkontakt, unruhige Laternen, Feuer.
    vec3 l = 4.0 * a * a + 4.0 * b.r * b.r * (stutter - 1.0) * lamp_color
        + 4.0 * b.b * b.b * (unrest - 1.0) * lamp_color + 4.0 * b.g * b.g * (fire - 1.0) * fire_color;
    l = max(l, vec3(0.0));
    // Spiegel des Nachthimmels: Wolken ziehen langsam durch, das Licht ringsum glänzt auf dem Wasser. Im Blitz leuchtet es auf.
    float cloud = texture(noise_map, px * vec2(0.004, 0.008) + vec2(TIME * 0.01, 0.0)).r;
    // Nachts ist Wasser dunkler als nasser Stein: Es schluckt das Streulicht und zeigt nur, was sich darin spiegelt.
    vec3 c = mix(vec3(0.045, 0.055, 0.09), vec3(0.09, 0.105, 0.16), smoothstep(0.35, 0.75, cloud)) + l * 0.12 + lightning * vec3(0.5, 0.56, 0.75);
    // Am hinteren Rand eine feine, helle Kante, wo das Wasser auf den Stein trifft.
    c += m.r * (l * 0.16 + vec3(0.03, 0.035, 0.05) + lightning * 0.25);
    // Tropfen glitzern kurz auf, im Schauer öfter.
    float drop = texture(noise_map, px * vec2(0.09, 0.18) + vec2(TIME * 0.9, -TIME * 0.7)).r;
    c += smoothstep(0.86 - rain * 0.08, 0.93, drop) * (l * 0.6 + vec3(0.08, 0.09, 0.12));
    // Schweißlicht der Halle: Das Wasser blitzt bläulich mit jedem Lichtbogen.
    float dw = length((px - weld.xy) * vec2(1.0, 2.0)) / 150.0;
    c += vec3(0.5, 0.7, 1.0) * weld.z * pow(max(0.0, 1.0 - dw), 2.0) * 0.6;
    COLOR = vec4(c / night, 0.92 * cover);
}";

        private const string MirrorShader = @"
shader_type canvas_item;
render_mode {0};
uniform sampler2D mask : filter_linear;
uniform vec2 ground_size;
uniform float rain = 0.3;
varying vec2 world;
void vertex() {
    world = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
}
void fragment() {
    vec2 p = floor(world * 2.0) * 0.5;
    float m = texture(mask, world / ground_size).a;
    float cover = clamp((m - 0.5) / max(fwidth(m), 0.001) + 0.5, 0.0, 1.0);
    // Das Spiegelbild zittert zeilenweise, stärker im Regen.
    float wobble = sin(p.y * 0.9 + TIME * 4.0) * 0.6 + sin(p.y * 2.3 - TIME * 6.5) * 0.4;
    vec2 uv = UV + vec2(TEXTURE_PIXEL_SIZE.x * wobble * (0.5 + 1.6 * rain), 0.0);
    vec4 t = texture(TEXTURE, uv) * COLOR;
    COLOR = vec4(t.rgb, t.a * cover);
}";

        private enum Kind
        {
            Lamp,
            Pane,
            Fire,
            Barrel,
            Tram,
        }

        /// <summary>Ein Licht, dessen Spiegelbild in eine Pfütze fällt. Mirror ist die Mitte des Spiegelbilds im Bild.</summary>
        private sealed class Source
        {
            public Kind Kind;
            public Vector2 Mirror;
            public Vector2 Size;
            public Vector2 Tile;
            public int PaneId = -1;
            public string PaneKind = "";
            public float Phase;
            public int Index;
        }

        private readonly List<(Vector2 Foot, Vector2 Tile)> _lamps = new();
        private readonly List<Source> _sources = new();
        private bool[,] _water = new bool[0, 0];
        private readonly List<Spot> _spots = new();

        /// <summary>Eine Pfütze, die am Spiegelpunkt eines Lichts angelegt wird, in einfachen Bildpunkten.</summary>
        private sealed record Spot(Vector2 Center, float Radius, float Phase, float Phase2);
        private bool _tramChecked;
        private GameMap _blocked = null!;

        /// <summary>Wie viele Lichtquellen es gibt und wie viele davon sich in einer Pfütze zeigen. Für Prüfläufe.</summary>
        public (int Lights, int Mirrored) Counted { get; private set; }

        private ShaderMaterial _surface = null!;
        private ShaderMaterial _addMaterial = null!;
        private ShaderMaterial _mixMaterial = null!;
        private Node2D _lights = null!;
        private Node2D _shapes = null!;
        private Texture2D _glow = null!;
        private Texture2D _soft = null!;
        private Texture2D _white = null!;
        private float _time;

        public Main Game = null!;
        public WetGround Wet = null!;
        public WindowLife Windows = null!;
        public string GroundPath = "";
        public Texture2D GroundLight = null!;
        public Texture2D GroundLightB = null!;
        public Texture2D Noise = null!;
        public Color LampColor;
        public Color FireColor;
        public Vector2 StutterLamp;
        public IEnumerable<double[]> Lamps = new List<double[]>();
        public string ManifestPath = "";
        public Tram? Tram;
        public IEnumerable<(Vector2 Pos, float Radius)> Puddles = new List<(Vector2, float)>();

        public override void _Ready()
        {
            LightMask = 0;
            var mask = BuildMask(out var size, out int res);
            _surface = new ShaderMaterial { Shader = new Shader { Code = SurfaceShader } };
            _surface.SetShaderParameter("ground_light", GroundLight);
            _surface.SetShaderParameter("ground_light_b", GroundLightB);
            _surface.SetShaderParameter("lamp_color", LampColor);
            _surface.SetShaderParameter("fire_color", FireColor);
            _surface.SetShaderParameter("noise_map", Noise);
            _surface.SetShaderParameter("night", Game.NightColor);
            _surface.SetShaderParameter("ground_size", size);
            AddChild(new Sprite2D
            {
                Texture = mask, Centered = false, Scale = Vector2.One / res, Material = _surface, LightMask = 0, TextureFilter = TextureFilterEnum.Linear,
            });
            _mixMaterial = MirrorMaterial("blend_mix", mask, size);
            _addMaterial = MirrorMaterial("blend_add", mask, size);
            // Erst die dunklen Spiegelbilder von Figuren und Pfosten, darüber das Licht.
            _shapes = new Mirror { Owner_ = this, Lights = false, Material = _mixMaterial, LightMask = 0 };
            AddChild(_shapes);
            _lights = new Mirror { Owner_ = this, Lights = true, Material = _addMaterial, LightMask = 0 };
            AddChild(_lights);
            _glow = MakeGlow(32, 2.2f);
            _soft = MakeGlow(16, 6f);
            var dot = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
            dot.Fill(Colors.White);
            _white = ImageTexture.CreateFromImage(dot);
            foreach (var lamp in Lamps)
            {
                var tile = new Vector2((float)lamp[0], (float)lamp[1]);
                _lamps.Add((Game.ToScreen((int)(tile.X * World.TileUnits), (int)(tile.Y * World.TileUnits)), tile));
            }
            CollectSources();
        }

        private Vector2 Screen(Vector2 tile) => Game.ToScreen((int)(tile.X * World.TileUnits), (int)(tile.Y * World.TileUnits));

        /// <summary>
        /// Prüft jede Lichtquelle: In der Schrägansicht liegt das Spiegelbild eines Lichts in Höhe h genau h Bildpunkte unter
        /// seinem Fußpunkt. Nur wenn dort Wasser ist, auch mit der Unschärfe durch Regenwellen, zeigt die Pfütze das Licht.
        /// </summary>
        private void CollectSources()
        {
            int lights = 0;
            foreach (var (foot, tile) in _lamps)
            {
                lights++;
                Consider(new Source { Kind = Kind.Lamp, Mirror = foot + new Vector2(0f, LampHeadHeight), Size = LampSize, Tile = tile });
            }
            foreach (var (id, kind, center, foot) in Wet.PaneFeet)
            {
                lights++;
                Consider(new Source
                {
                    Kind = Kind.Pane, Mirror = new Vector2(center.X, 2f * foot - center.Y), Size = kind == "sign" ? LetterSize : PaneSize,
                    PaneId = id, PaneKind = kind,
                });
            }
            using (var manifest = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(ManifestPath)))
            {
                int index = 0;
                foreach (var f in manifest.RootElement.GetProperty("fireLights").EnumerateArray())
                {
                    lights++;
                    var foot = Screen(new Vector2(f[0].GetSingle(), f[1].GetSingle()));
                    Consider(new Source { Kind = Kind.Fire, Mirror = foot + new Vector2(0f, f[2].GetSingle()), Size = FireSize, Index = index++ });
                }
            }
            lights++;
            if (Strikers.Present)
            {
                Consider(new Source { Kind = Kind.Barrel, Mirror = Screen(Strikers.BarrelTile) + new Vector2(0f, BarrelFlameHeight), Size = FireSize });
            }
            Counted = (lights, _sources.Count);
        }

        /// <summary>Die Fenster der Straßenbahn kommen dazu, sobald sie bereit ist.</summary>
        private void CollectTram()
        {
            _tramChecked = true;
            if (Tram == null)
            {
                return;
            }
            int lights = Counted.Lights;
            foreach (var (light, foot) in Tram.PaneLights())
            {
                lights++;
                Consider(new Source { Kind = Kind.Tram, Mirror = new Vector2(light.X, 2f * foot - light.Y), Size = PaneSize });
            }
            Counted = (lights, _sources.Count);
        }

        private void Consider(Source source)
        {
            float width = source.Size.X, height = source.Size.Y * (MinSmear + RainSmear);
            int x0 = Mathf.Max(0, (int)(source.Mirror.X - width / 2f)), x1 = Mathf.Min(_water.GetLength(0) - 1, (int)(source.Mirror.X + width / 2f));
            int y0 = Mathf.Max(0, (int)(source.Mirror.Y - height / 2f)), y1 = Mathf.Min(_water.GetLength(1) - 1, (int)(source.Mirror.Y + height / 2f));
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    if (_water[x, y])
                    {
                        _sources.Add(source);
                        return;
                    }
                }
            }
        }

        private ShaderMaterial MirrorMaterial(string mode, Texture2D mask, Vector2 size)
        {
            var material = new ShaderMaterial { Shader = new Shader { Code = MirrorShader.Replace("{0}", mode) } };
            material.SetShaderParameter("mask", mask);
            material.SetShaderParameter("ground_size", size);
            return material;
        }

        /// <summary>
        /// Sucht die Bildpunkte jeder Pfütze, erst im einfachen Boden für die Frage, welche Lichter sich spiegeln, dann im
        /// feinen Boden für das sichtbare Wasser. Alpha: Deckung, weich ausgeblendet. Helligkeit: heller Saum am hinteren Rand.
        /// </summary>
        private Texture2D BuildMask(out Vector2 size, out int res)
        {
            var ground = Image.LoadFromFile(GroundPath);
            int w = ground.GetWidth(), h = ground.GetHeight();
            size = new Vector2(w, h);
            var water = new bool[w, h];
            var boxes = new List<Rect2I>();
            FindPainted(ground, water, 1, boxes);
            var open = new bool[w, h];
            AddMirrorPuddles(water, open, w, h, boxes);
            _water = Clean(water, w, h, boxes);
            string fine = Path.Combine(Path.GetDirectoryName(GroundPath) ?? "", "hd", Path.GetFileName(GroundPath));
            var image = File.Exists(fine) ? Image.LoadFromFile(fine) : null;
            res = image == null ? 1 : image.GetWidth() / w;
            if (image == null || res < 2 || image.GetWidth() != w * res || image.GetHeight() != h * res)
            {
                res = 1;
                return MaskTexture(_water, w, h, boxes);
            }
            int fw = w * res, fh = h * res;
            var fineWater = new bool[fw, fh];
            var fineBoxes = new List<Rect2I>();
            FindPainted(image, fineWater, res, fineBoxes);
            int step = res;
            foreach (var spot in _spots)
            {
                fineBoxes.Add(RasterSpot(fineWater, spot, res, (x, y) => open[Mathf.Clamp(x / step, 0, w - 1), Mathf.Clamp(y / step, 0, h - 1)]));
            }
            return MaskTexture(Clean(fineWater, fw, fh, fineBoxes), fw, fh, fineBoxes);
        }

        /// <summary>Gemalte Pfützen im Boden der Auflösung res: Orte und Radien stehen im Manifest in einfachen Bildpunkten.</summary>
        private void FindPainted(Image ground, bool[,] water, int res, List<Rect2I> boxes)
        {
            int w = ground.GetWidth(), h = ground.GetHeight();
            foreach (var (pos, radius) in Puddles)
            {
                var at = pos * res;
                float rx = radius * SearchScale * res, ry = radius * 0.5f * SearchScale * res;
                int x0 = Mathf.Max(0, (int)(at.X - rx)), x1 = Mathf.Min(w - 1, (int)(at.X + rx));
                int y0 = Mathf.Max(0, (int)(at.Y - ry)), y1 = Mathf.Min(h - 1, (int)(at.Y + ry));
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        float d = new Vector2((x - at.X) / rx, (y - at.Y) / ry).Length();
                        water[x, y] |= d < 1f && IsPuddle(ground.GetPixel(x, y));
                    }
                }
                boxes.Add(new Rect2I(x0, y0, x1 - x0 + 1, y1 - y0 + 1));
            }
        }

        /// <summary>Einzelne Ausreißer weg, kleine Löcher zu: Mehrheit der Nachbarn entscheidet. Nur in den Kästen um die Pfützen.</summary>
        private static bool[,] Clean(bool[,] water, int w, int h, List<Rect2I> boxes)
        {
            var clean = new bool[w, h];
            foreach (var box in boxes)
            {
                for (int y = Mathf.Max(1, box.Position.Y - 1); y <= Mathf.Min(h - 2, box.End.Y); y++)
                {
                    for (int x = Mathf.Max(1, box.Position.X - 1); x <= Mathf.Min(w - 2, box.End.X); x++)
                    {
                        clean[x, y] = Around(water, x, y) >= 5;
                    }
                }
            }
            return clean;
        }

        private static int Around(bool[,] water, int x, int y)
        {
            int around = 0;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    around += water[x + dx, y + dy] ? 1 : 0;
                }
            }
            return around;
        }

        /// <summary>
        /// Maske als Bild: Alpha ist der Anteil Wasser in den neun Nachbarpunkten. Gefiltert und bei halber Deckung
        /// abgeschnitten ergibt das einen glatten Rand statt einer Treppe. Helligkeit markiert den hinteren Saum.
        /// </summary>
        private static Texture2D MaskTexture(bool[,] water, int w, int h, List<Rect2I> boxes)
        {
            var data = new byte[w * h * 2];
            foreach (var box in boxes)
            {
                for (int y = Mathf.Max(1, box.Position.Y - 2); y <= Mathf.Min(h - 2, box.End.Y + 1); y++)
                {
                    for (int x = Mathf.Max(1, box.Position.X - 2); x <= Mathf.Min(w - 2, box.End.X + 1); x++)
                    {
                        int i = (y * w + x) * 2;
                        data[i] = (byte)(water[x, y] && !water[x, y - 1] ? 255 : 0);
                        data[i + 1] = (byte)(Around(water, x, y) * 255 / 9);
                    }
                }
            }
            return ImageTexture.CreateFromImage(Image.CreateFromData(w, h, false, Image.Format.La8, data));
        }

        /// <summary>Zeichnet eine Pfütze am Spiegelpunkt ins Raster der Auflösung res. Gibt den Kasten zurück, den sie belegt.</summary>
        private static Rect2I RasterSpot(bool[,] water, Spot spot, int res, System.Func<int, int, bool> open)
        {
            int w = water.GetLength(0), h = water.GetLength(1);
            var c = spot.Center;
            float rx = spot.Radius;
            int x0 = Mathf.Max(1, (int)((c.X - rx * 1.4f) * res)), x1 = Mathf.Min(w - 1, (int)((c.X + rx * 1.4f) * res));
            int y0 = Mathf.Max(1, (int)((c.Y - rx) * res)), y1 = Mathf.Min(h - 1, (int)((c.Y + rx) * res));
            for (int y = y0; y < y1; y++)
            {
                for (int x = x0; x < x1; x++)
                {
                    // Mitte des Punkts in einfachen Bildpunkten. Bei res 1 genau der Punkt selbst wie früher.
                    float fx = res == 1 ? x : (x + 0.5f) / res - 0.5f, fy = res == 1 ? y : (y + 0.5f) / res - 0.5f;
                    // Unregelmäßiger Rand wie bei den gemalten Pfützen.
                    var d = new Vector2((fx - c.X) / rx, (fy - c.Y) / (rx * 0.5f));
                    float edge = 1f + 0.2f * Mathf.Sin(d.Angle() * 3f + spot.Phase) + 0.12f * Mathf.Sin(d.Angle() * 5f + spot.Phase2);
                    if (d.Length() < edge && open(x, y))
                    {
                        water[x, y] = true;
                    }
                }
            }
            return new Rect2I(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
        }

        /// <summary>
        /// Legt Pfützen an die Spiegelpunkte der Lichter: bei den meisten Laternen, bei jedem Feuer und bei einem Teil der
        /// Fenster. Nur auf freiem Pflaster innerhalb der Karte, das von keinem Haus davor verdeckt wird.
        /// </summary>
        private void AddMirrorPuddles(bool[,] water, bool[,] open, int w, int h, List<Rect2I> areas)
        {
            _blocked = Game.Map.Build();
            var rng = new System.Random(MirrorPuddleSeed);
            var boxes = new List<(float X0, float Y0, float X1, float Y1, float Z1)>();
            var points = new List<(Vector2 At, float Share)>();
            using (var manifest = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(ManifestPath)))
            {
                foreach (var t in manifest.RootElement.GetProperty("things").EnumerateArray())
                {
                    boxes.Add((t.GetProperty("x0").GetSingle(), t.GetProperty("y0").GetSingle(), t.GetProperty("x1").GetSingle(),
                        t.GetProperty("y1").GetSingle(), t.GetProperty("z1").GetSingle()));
                }
                foreach (var f in manifest.RootElement.GetProperty("fireLights").EnumerateArray())
                {
                    points.Add((Screen(new Vector2(f[0].GetSingle(), f[1].GetSingle())) + new Vector2(0f, f[2].GetSingle()), 1f));
                }
            }
            foreach (var lamp in Lamps)
            {
                points.Add((Screen(new Vector2((float)lamp[0], (float)lamp[1])) + new Vector2(0f, LampHeadHeight), LampPuddleShare));
            }
            foreach (var (_, kind, center, foot) in Wet.PaneFeet)
            {
                if (kind != "dark")
                {
                    points.Add((new Vector2(center.X, 2f * foot - center.Y), PanePuddleShare));
                }
            }
            if (Strikers.Present)
            {
                points.Add((Screen(Strikers.BarrelTile) + new Vector2(0f, BarrelFlameHeight), 1f));
            }
            foreach (var (at, share) in points)
            {
                if (rng.NextDouble() >= share || !OpenGround(at, boxes) || WetShare(water, at, w, h) > AlreadyWetShare)
                {
                    continue;
                }
                float rx = MinMirrorPuddle + (float)rng.NextDouble() * (MaxMirrorPuddle - MinMirrorPuddle);
                var c = at + new Vector2(((float)rng.NextDouble() - 0.5f) * rx * 0.6f, ((float)rng.NextDouble() - 0.5f) * rx * 0.3f);
                float phase = (float)rng.NextDouble() * Mathf.Tau, phase2 = (float)rng.NextDouble() * Mathf.Tau;
                var spot = new Spot(c, rx, phase, phase2);
                _spots.Add(spot);
                // Frei oder verdeckt merkt sich das einfache Raster, das feine fragt dort nach. Etwas über den Rand
                // hinaus prüfen, sonst fehlen dem feinen Rand dort Punkte.
                RasterSpot(open, spot with { Radius = rx * 1.15f }, 1, (x, y) => OpenGround(new Vector2(x, y), boxes));
                areas.Add(RasterSpot(water, spot, 1, (x, y) => open[x, y]));
            }
        }

        private static float WetShare(bool[,] water, Vector2 at, int w, int h)
        {
            int wet = 0, all = 0;
            for (int y = (int)at.Y - 4; y <= (int)at.Y + 4; y++)
            {
                for (int x = (int)at.X - 6; x <= (int)at.X + 6; x++)
                {
                    if (x >= 0 && y >= 0 && x < w && y < h)
                    {
                        all++;
                        wet += water[x, y] ? 1 : 0;
                    }
                }
            }
            return all == 0 ? 1f : wet / (float)all;
        }

        /// <summary>
        /// Freies Pflaster innerhalb der Karte, das man von oben sieht. Ein Haus davor verdeckt den Punkt, wenn es auf der
        /// Bildlinie nach vorn (x und y wachsen gemeinsam) hoch genug aufragt: je Feld nach vorn 32 Bildpunkte Höhe.
        /// </summary>
        private bool OpenGround(Vector2 at, List<(float X0, float Y0, float X1, float Y1, float Z1)> boxes)
        {
            var tile = Game.ScreenToMap(at);
            if (tile.X < 0.2f || tile.Y < 0.2f || tile.X > 19.8f || tile.Y > 19.8f || _blocked.IsBlocked((int)tile.X, (int)tile.Y))
            {
                return false;
            }
            foreach (var (x0, y0, x1, y1, z1) in boxes)
            {
                for (float t = 0f; t * 32f <= z1; t += 0.1f)
                {
                    float x = tile.X + t, y = tile.Y + t;
                    if (x >= x0 && x <= x1 && y >= y0 && y <= y1)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        // Pfützenfarbe zu 80 Prozent über dem Boden: Was übrig bleibt, muss ein gültiger Bodenton sein, und das Wasser ist blauer als rot.
        private static bool IsPuddle(Color c)
        {
            float lowR = PuddleColor.R * PuddleShare, lowG = PuddleColor.G * PuddleShare, lowB = PuddleColor.B * PuddleShare;
            float rest = 1f - PuddleShare;
            return c.R >= lowR && c.R <= lowR + rest && c.G >= lowG && c.G <= lowG + rest && c.B >= lowB && c.B <= lowB + rest
                && c.B - c.R >= MinBlueOverRed;
        }

        private static Texture2D MakeGlow(int size, float falloff)
        {
            var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = new Vector2((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f).Length();
                    image.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp(1f - d, 0f, 1f), 1f / falloff * 2f)));
                }
            }
            return ImageTexture.CreateFromImage(image);
        }

        public override void _Process(double delta)
        {
            _time += (float)delta;
            if (!_tramChecked)
            {
                CollectTram();
            }
            float rain = Game.Climate.RainStrength;
            _surface.SetShaderParameter("lightning", Mathf.Min(1f, Game.Lightning));
            _surface.SetShaderParameter("rain", rain);
            _surface.SetShaderParameter("stutter", Game.StutterLevel);
            _surface.SetShaderParameter("unrest", Game.UnrestLevel);
            _surface.SetShaderParameter("fire", Game.FireLevel);
            var (weldAt, weldEnergy) = Windows.WeldLight;
            _surface.SetShaderParameter("weld", new Vector3(weldAt.X, weldAt.Y, weldEnergy));
            _mixMaterial.SetShaderParameter("rain", rain);
            _addMaterial.SetShaderParameter("rain", rain);
            _shapes.QueueRedraw();
            _lights.QueueRedraw();
        }

        private sealed partial class Mirror : Node2D
        {
            public PuddleMirror Owner_ = null!;
            public bool Lights;

            public override void _Draw()
            {
                if (Lights)
                {
                    Owner_.DrawLights(this);
                }
                else
                {
                    Owner_.DrawShapes(this);
                }
            }
        }

        private Color Lit(Color c, float a)
        {
            var night = Game.NightColor;
            return new Color(c.R / night.R, c.G / night.G, c.B / night.B, a);
        }

        private void DrawShapes(CanvasItem canvas)
        {
            foreach (var (foot, _) in _lamps)
            {
                canvas.DrawTextureRect(_white, new Rect2(foot.X - 0.5f, foot.Y, 1.5f, LampHeadHeight), false, new Color(0f, 0f, 0f, PostAlpha));
            }
            foreach (var sprite in Game.UnitSprites)
            {
                if (!sprite.Visible || sprite.Texture == null)
                {
                    continue;
                }
                var region = sprite.RegionRect;
                var size = region.Size * sprite.Scale;
                float footY = sprite.Position.Y + Game.FigureFoot.Y;
                // Kopfüber unter dem Fuß: negative Höhe spiegelt das Bild senkrecht, negative Breite folgt der Blickrichtung.
                float top = 2f * footY - sprite.Position.Y;
                var rect = sprite.FlipH
                    ? new Rect2(sprite.Position.X + size.X, top, -size.X, -size.Y)
                    : new Rect2(sprite.Position.X, top, size.X, -size.Y);
                var m = sprite.Modulate;
                var small = new Rect2(region.Position / Game.FigureMirrorScale, region.Size / Game.FigureMirrorScale);
                canvas.DrawTextureRectRegion(Game.FigureMirror, rect, small, new Color(m.R * FigureShade, m.G * FigureShade, m.B * FigureShade, FigureAlpha * m.A));
            }
        }

        private void DrawLights(CanvasItem canvas)
        {
            float smear = MinSmear + RainSmear * Game.Climate.RainStrength;
            foreach (var src in _sources)
            {
                var (color, level) = Look(src);
                if (level <= 0.01f)
                {
                    continue;
                }
                var size = new Vector2(src.Size.X * 1.3f, src.Size.Y * smear);
                canvas.DrawTextureRect(_glow, new Rect2(src.Mirror - size / 2f, size), false, Lit(color, level));
                // Heller Kern: Das Wasser gibt die Lichtquelle selbst fast ungeschwächt zurück.
                var core = new Vector2(src.Size.X * 0.45f, src.Size.Y * 0.5f * smear);
                canvas.DrawTextureRect(_soft, new Rect2(src.Mirror - core / 2f, core), false, Lit(color.Lerp(new Color(1.4f, 1.3f, 1.15f), 0.4f), level));
            }
        }

        /// <summary>Farbe und Helligkeit der Quelle gerade jetzt. Flackert die Quelle, flackert ihr Spiegelbild mit.</summary>
        private (Color Color, float Level) Look(Source src)
        {
            switch (src.Kind)
            {
                case Kind.Lamp:
                    bool stutters = Mathf.Abs(src.Tile.X - StutterLamp.X) < 0.01f && Mathf.Abs(src.Tile.Y - StutterLamp.Y) < 0.01f;
                    return (LampColor * 1.4f, 0.9f * (stutters ? Game.StutterLevel : Game.UnrestLevel));
                case Kind.Fire:
                    return (FireColor * 1.4f, 0.8f * Game.FireLevel);
                case Kind.Barrel:
                    return (FireColor * 1.4f, 0.75f * (0.82f + 0.18f * Mathf.Sin(_time * 11f) * Mathf.Sin(_time * 17f + 1f)));
                case Kind.Tram:
                    return (new Color(1f, 0.74f, 0.4f), 0.55f * (Tram?.Glow ?? 0f));
                default:
                    var look = Windows.PaneLook(src.PaneId);
                    if (look.A <= 0f)
                    {
                        // Wie im vorberechneten Bild: Fenster warm, die Halle ohne Schweißlicht nur matt.
                        return (src.PaneKind == "weld" ? IdleHall : UnchangedWindow, 0.75f);
                    }
                    var color = new Color(look.R, look.G, look.B) * 1.5f;
                    return (color, src.PaneKind == "sign" ? 0.9f : src.PaneKind == "weld" ? 1f : 0.75f);
            }
        }
    }
}
