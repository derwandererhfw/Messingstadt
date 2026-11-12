using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Godot;
using Messingstadt.Core;

namespace Messingstadt
{
    /// <summary>
    /// Das nasse Pflaster spiegelt: Laternen, Fenster, Leuchtschrift und Feuer als zitternde Streifen im Boden,
    /// in den Pfützen am deutlichsten. Liegt unter den Gebäuden und ändert keine Spielregel.
    /// </summary>
    public partial class WetGround : Node2D
    {
        private const float MaxStreak = 92f;
        private const float MirrorShare = 0.62f;
        private const float LampHeight = 52f;
        private const float LampWidth = 13f;
        private const float LampAlpha = 0.95f;
        private const float WindowWidth = 13f;
        private const float WindowAlpha = 0.6f;
        private const float LetterWidth = 15f;
        private const float LetterAlpha = 0.3f;
        private const float FireWidth = 24f;
        private const float FireAlpha = 0.8f;
        private const int WetMapDivisor = 4;
        private const float PuddleWetScale = 1.1f;
        private const float MinWallHeightAround = 80f;
        private static readonly Color UnchangedWindow = new(0.62f, 0.46f, 0.26f);

        // Die Streifen verschieben sich zeilenweise wie auf bewegtem Wasser und reißen auf trockenerem Pflaster ab.
        private const string ShaderCode = @"
shader_type canvas_item;
render_mode blend_add;
uniform sampler2D noise_map : filter_linear, repeat_enable;
uniform sampler2D wet_map : filter_linear;
uniform vec2 ground_size;
uniform vec2 origin;
varying vec2 world;
void vertex() {
    world = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
}
void fragment() {
    // Auf halbe Bildpunkte gerastert wie der feine Boden.
    vec2 p = floor(world * 2.0) * 0.5;
    float wobble = texture(noise_map, vec2(p.x * 0.003, p.y * 0.035 + TIME * 0.22)).r - 0.5;
    float a = texture(TEXTURE, vec2(UV.x + wobble * 0.8, UV.y)).a;
    float grain = texture(noise_map, vec2(p.x * 0.035, p.y * 0.11 - TIME * 0.05)).r;
    float wet = texture(wet_map, world / ground_size).r;
    float mx = ((p.x - origin.x) / 32.0 + (p.y - origin.y) / 16.0) / 2.0;
    float my = ((p.y - origin.y) / 16.0 - (p.x - origin.x) / 32.0) / 2.0;
    float inside = step(0.0, mx) * step(0.0, my) * step(mx, 20.0) * step(my, 20.0);
    COLOR = vec4(COLOR.rgb, COLOR.a * a * mix(smoothstep(0.3, 0.6, grain) * 0.85, 1.4, wet) * inside);
}";

        private sealed class Streak
        {
            public Rect2 Area;
            public Color Tint;
            public float Alpha;
            public int PaneId = -1;
            public Vector2 Lamp = new(-1f, -1f);
            public bool Fire;
            public float Phase;
        }

        private sealed record Wall(Vector2 West, Vector2 South, Vector2 East, float Height);

        private readonly List<Streak> _streaks = new();
        private Texture2D _streak = null!;

        /// <summary>Fenster und Buchstaben mit dem Fuß ihrer Wand im Bild. Daraus entstehen die Spiegelbilder in den Pfützen.</summary>
        public List<(int Id, string Kind, Vector2 Center, float FootY)> PaneFeet { get; } = new();
        private float _time;

        public Main Game = null!;
        public WindowLife Windows = null!;
        public Texture2D Noise = null!;
        public Vector2 GroundSize;
        public string ManifestPath = "";
        public Color LampColor;
        public Color FireColor;
        public Vector2 StutterLamp;
        public IEnumerable<double[]> Lamps = new List<double[]>();
        public IEnumerable<Vector2> Fires = new List<Vector2>();
        public IEnumerable<Vector2> ExtraFires = new List<Vector2>();
        public IEnumerable<(Vector2 Pos, float Radius)> Puddles = new List<(Vector2, float)>();

        public override void _Ready()
        {
            _streak = MakeStreakTexture();
            var material = new ShaderMaterial { Shader = new Shader { Code = ShaderCode } };
            material.SetShaderParameter("noise_map", Noise);
            material.SetShaderParameter("wet_map", MakeWetMap());
            material.SetShaderParameter("ground_size", GroundSize);
            material.SetShaderParameter("origin", Game.ToScreen(0, 0));
            Material = material;
            LightMask = 0;
            foreach (var lamp in Lamps)
            {
                var tile = new Vector2((float)lamp[0], (float)lamp[1]);
                var foot = Game.ToScreen((int)(tile.X * World.TileUnits), (int)(tile.Y * World.TileUnits));
                _streaks.Add(new Streak
                {
                    Area = StreakArea(foot, LampHeight, LampWidth), Tint = Lit(LampColor), Alpha = LampAlpha, Lamp = tile,
                });
            }
            var walls = ReadWalls();
            foreach (var (id, kind, center) in Windows.AllPanes)
            {
                if (kind != "dark")
                {
                    AddPaneStreak(id, kind, center, walls);
                }
            }
            foreach (var p in Fires)
            {
                _streaks.Add(new Streak { Area = StreakArea(p, 26f, FireWidth), Tint = Lit(FireColor), Alpha = FireAlpha, Fire = true, Phase = p.X });
            }
            foreach (var p in ExtraFires)
            {
                _streaks.Add(new Streak { Area = StreakArea(p, 22f, FireWidth * 0.7f), Tint = Lit(FireColor), Alpha = FireAlpha, Fire = true, Phase = p.X });
            }
        }

        private Color Lit(Color c)
        {
            var night = Game.NightColor;
            return new Color(c.R / night.R, c.G / night.G, c.B / night.B);
        }

        /// <summary>Spiegelbild einer Quelle in dieser Höhe: Es beginnt am Fuß und ist am Spiegelpunkt am hellsten.</summary>
        private static Rect2 StreakArea(Vector2 foot, float height, float width)
        {
            float length = Mathf.Min(MaxStreak, height / MirrorShare);
            return new Rect2(foot.X - width / 2f, foot.Y, width, length);
        }

        private void AddPaneStreak(int id, string kind, Vector2 center, List<Wall> walls)
        {
            // Fuß der Wand unter dem Fenster suchen: die vordere Unterkante des Hauses an dieser Bildspalte.
            float? foot = null;
            foreach (var wall in walls)
            {
                if (center.X < wall.West.X || center.X > wall.East.X)
                {
                    continue;
                }
                float y = center.X < wall.South.X
                    ? Mathf.Lerp(wall.West.Y, wall.South.Y, (center.X - wall.West.X) / (wall.South.X - wall.West.X))
                    : Mathf.Lerp(wall.South.Y, wall.East.Y, (center.X - wall.South.X) / (wall.East.X - wall.South.X));
                if (center.Y <= y && center.Y >= y - wall.Height - MinWallHeightAround && (foot == null || y > foot))
                {
                    foot = y;
                }
            }
            if (foot == null)
            {
                return;
            }
            bool letter = kind == "sign";
            PaneFeet.Add((id, kind, center, foot.Value));
            _streaks.Add(new Streak
            {
                Area = StreakArea(new Vector2(center.X, foot.Value), foot.Value - center.Y, letter ? LetterWidth : WindowWidth),
                Alpha = letter ? LetterAlpha : WindowAlpha, PaneId = id,
            });
        }

        private List<Wall> ReadWalls()
        {
            var walls = new List<Wall>();
            using var manifest = JsonDocument.Parse(File.ReadAllText(ManifestPath));
            foreach (var t in manifest.RootElement.GetProperty("things").EnumerateArray())
            {
                if (t.GetProperty("panes").GetArrayLength() == 0)
                {
                    continue;
                }
                float x0 = t.GetProperty("x0").GetSingle(), y0 = t.GetProperty("y0").GetSingle();
                float x1 = t.GetProperty("x1").GetSingle(), y1 = t.GetProperty("y1").GetSingle();
                walls.Add(new Wall(Tile(x0, y1), Tile(x1, y1), Tile(x1, y0), t.GetProperty("z1").GetSingle()));
            }
            return walls;
        }

        private Vector2 Tile(float x, float y) => Game.ToScreen((int)(x * World.TileUnits), (int)(y * World.TileUnits));

        /// <summary>Streifen mit weichen Seiten, oben schwach, am Spiegelpunkt hell, unten auslaufend.</summary>
        private static Texture2D MakeStreakTexture()
        {
            const int W = 16, H = 64;
            var image = Image.CreateEmpty(W, H, false, Image.Format.Rgba8);
            for (int y = 0; y < H; y++)
            {
                float v = y / (H - 1f);
                float along = v < MirrorShare ? Mathf.Lerp(0.25f, 1f, v / MirrorShare) : 1f - (v - MirrorShare) / (1f - MirrorShare);
                for (int x = 0; x < W; x++)
                {
                    float u = (x + 0.5f) / W * 2f - 1f;
                    float across = Mathf.Exp(-u * u * 4f) * (1f - Mathf.Abs(u));
                    image.SetPixel(x, y, new Color(1f, 1f, 1f, along * across));
                }
            }
            return ImageTexture.CreateFromImage(image);
        }

        /// <summary>Wo das Pflaster unter Wasser steht: grobe Karte aus den Pfützen.</summary>
        private Texture2D MakeWetMap()
        {
            int w = (int)GroundSize.X / WetMapDivisor, h = (int)GroundSize.Y / WetMapDivisor;
            var image = Image.CreateEmpty(w, h, false, Image.Format.R8);
            foreach (var (pos, radius) in Puddles)
            {
                float rx = radius * PuddleWetScale / WetMapDivisor, ry = rx * 0.5f;
                var c = pos / WetMapDivisor;
                for (int y = Mathf.Max(0, (int)(c.Y - ry)); y <= Mathf.Min(h - 1, (int)(c.Y + ry)); y++)
                {
                    for (int x = Mathf.Max(0, (int)(c.X - rx)); x <= Mathf.Min(w - 1, (int)(c.X + rx)); x++)
                    {
                        float d = new Vector2((x - c.X) / rx, (y - c.Y) / ry).Length();
                        image.SetPixel(x, y, new Color(Mathf.Max(image.GetPixel(x, y).R, Mathf.Clamp((1f - d) * 2.5f, 0f, 1f)), 0f, 0f));
                    }
                }
            }
            return ImageTexture.CreateFromImage(image);
        }

        public override void _Process(double delta)
        {
            _time += (float)delta;
            Visible = !AirTest.Off("glanz");
            QueueRedraw();
        }

        public override void _Draw()
        {
            foreach (var s in _streaks)
            {
                var tint = s.Tint;
                float alpha = s.Alpha;
                if (s.PaneId >= 0)
                {
                    var look = Windows.PaneLook(s.PaneId);
                    tint = Lit(look.A > 0f ? new Color(look.R, look.G, look.B) : UnchangedWindow);
                }
                else if (s.Fire)
                {
                    alpha *= 0.8f + 0.2f * Mathf.Sin(_time * 9f + s.Phase) * Mathf.Sin(_time * 14f);
                }
                else
                {
                    bool stutters = Mathf.Abs(s.Lamp.X - StutterLamp.X) < 0.01f && Mathf.Abs(s.Lamp.Y - StutterLamp.Y) < 0.01f;
                    alpha *= stutters ? Game.StutterLevel : Game.UnrestLevel;
                }
                DrawTextureRect(_streak, s.Area, false, new Color(tint, alpha));
            }
        }
    }
}
