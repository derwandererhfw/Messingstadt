using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Godot;
using Messingstadt.Core;

namespace Messingstadt
{
    /// <summary>
    /// Bewegtes Licht, das nicht aus den gebackenen Lichtkarten kommt: Höfe um Fenster, Leuchtschrift und Feuer, die
    /// schaltbaren Lichtflicken der Grafik, der Scheinwerfer der Straßenbahn, die Laternen von Hundeführer und
    /// Wachhabendem, pendelnde Lampen und der Schlagschatten im Mündungsfeuer. Reine Darstellung: Das Licht, nach dem
    /// die Regeln rechnen, bleibt unberührt. Jeder Teil lässt sich mit --ohne=hoefe,flicken,scheinwerfer,wachlicht,pendel
    /// einzeln abschalten.
    /// </summary>
    public partial class LightExtras : Node2D
    {
        // Über Boden und Häusern, unter Rauch, Regen und Blitz. Figuren stehen je nach Tiefe darunter: Das Licht fällt
        // dann auch auf sie.
        private const int OverCityZ = 2900;
        private const float WindowHaloPixels = 44f;
        private const float WindowHaloAlpha = 0.085f;
        private const float FireHaloScale = 0.5f;
        private const float FireHaloAlpha = 0.3f;
        private const float SignHaloScale = 0.8f;
        private const float SignHaloAlpha = 0.26f;
        private const int ConeRays = 12;
        private const float GuardLampAlpha = 0.2f;
        private const float GuardLampEdgeShare = 0.35f;
        private const float GuardGlowPixels = 46f;
        private const float BeamTiles = 5f;
        private const float BeamHalfTiles = 1.25f;
        private const float BeamAlpha = 0.26f;
        private const float ShotShadowTiles = 3.5f;
        private const float ShotShadowLength = 2.2f;
        private const float ShotShadowHalfWidth = 0.16f;
        private const float ShotShadowAlpha = 0.55f;
        private const int ShotShadowTicks = 2;
        private static readonly Color WindowWarm = new(1f, 0.76f, 0.46f);
        private static readonly Color ScreenCold = new(0.62f, 0.72f, 1f);
        private static readonly Color NeonRed = new(1f, 0.3f, 0.22f);
        private static readonly Color GuardLamp = new(1f, 0.82f, 0.5f);
        private static readonly Color TramLamp = new(1f, 0.86f, 0.6f);
        private static readonly string[] LampBearers = { "hundefuehrer", "offizier" };

        private sealed class Patch
        {
            public Sprite2D Sprite = null!;
            public string Kind = "";
            public int[] Panes = Array.Empty<int>();
        }

        /// <summary>Zeichnet, was sich jedes Bild ändert, in einer eigenen Ebene mit eigener Tiefe und Mischung.</summary>
        private sealed partial class Layer : Node2D
        {
            public Action<Layer> Paint = _ => { };

            public override void _Draw() => Paint(this);
        }

        private sealed class Swing
        {
            public Layer Body = null!;
            public Sprite2D Halo = null!;
            public PointLight2D Light = null!;
            public Vector2 Foot;
            public float Height;
            public float Cord;
            public float Phase;
            public Color Color;
        }

        private readonly List<Patch> _patches = new();
        private readonly List<(Sprite2D Sprite, float Alpha)> _fires = new();
        private readonly List<Swing> _swings = new();
        private Sprite2D? _signHalo;
        private readonly List<int> _letters = new();
        private Layer? _windowHalos;
        private Layer? _guardLamps;
        private Layer? _shotShadows;
        private Layer? _beam;
        private Color _unNight;
        private float _time;

        public Main Game = null!;
        public WindowLife Windows = null!;
        public Texture2D Glow = null!;
        /// <summary>Die Bahn der Karte. Sie entsteht erst nach dieser Ebene, deshalb wird sie bei Bedarf erfragt.</summary>
        public Func<Tram?> TramOf = () => null;
        public string ManifestPath = "";

        private Vector2 Map(float x, float y) => Game.ToScreen((int)(x * World.TileUnits), (int)(y * World.TileUnits));

        /// <summary>Eine Farbe so, dass sie nach der Nachtabdunkelung des Bilds wieder stimmt.</summary>
        private Color Lit(Color c, float alpha) => new(c.R * _unNight.R, c.G * _unNight.G, c.B * _unNight.B, alpha);

        public override void _Ready()
        {
            var night = Game.NightColor;
            _unNight = new Color(1f / night.R, 1f / night.G, 1f / night.B);
            LightMask = 0;
            var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
            var site = MapSite.Current;
            if (!AirTest.Off("flicken"))
            {
                LoadPatches(additive);
            }
            if (!AirTest.Off("hoefe"))
            {
                // Feuer: Kessel und die Tonne der Streikposten. Die Glut atmet mit dem Flackern des gebackenen Feuerlichts.
                var fires = new List<(Vector2 Tile, float Height, Color Color)>();
                foreach (var s in site.Sources)
                {
                    if (s.Kind == "feuer")
                    {
                        fires.Add((s.Tile, s.Height, s.Color));
                    }
                }
                if (site.Strikers != null)
                {
                    fires.Add((site.Strikers.Barrel, 14f, new Color(1f, 0.55f, 0.25f)));
                }
                foreach (var (tile, height, color) in fires)
                {
                    var sprite = new Sprite2D
                    {
                        Texture = Glow, Material = additive, Position = Map(tile.X, tile.Y) - new Vector2(0, height), Scale = Vector2.One * FireHaloScale,
                        ZIndex = Game.DepthZ(Mathf.Clamp(tile.X, 0.05f, MapSite.Tiles.X - 0.05f), Mathf.Clamp(tile.Y, 0.05f, MapSite.Tiles.Y - 0.05f)) + 1, LightMask = 0, Modulate = Lit(color, FireHaloAlpha),
                    };
                    AddChild(sprite);
                    _fires.Add((sprite, FireHaloAlpha));
                }
                var signCenter = Vector2.Zero;
                foreach (var (id, kind, center) in Windows.AllPanes)
                {
                    if (kind == "sign")
                    {
                        _letters.Add(id);
                        signCenter += center;
                    }
                }
                if (_letters.Count > 0)
                {
                    _signHalo = new Sprite2D
                    {
                        Texture = Glow, Material = additive, Position = signCenter / _letters.Count, Scale = new Vector2(SignHaloScale * 1.5f, SignHaloScale),
                        ZIndex = OverCityZ, LightMask = 0, Modulate = Lit(NeonRed, SignHaloAlpha),
                    };
                    AddChild(_signHalo);
                }
                _windowHalos = new Layer { Material = additive, ZIndex = OverCityZ, LightMask = 0, Paint = PaintWindowHalos };
                AddChild(_windowHalos);
            }
            if (!AirTest.Off("wachlicht"))
            {
                // Am Boden, unter den Häusern: Der Schein endet wie der Blick an der Wand.
                _guardLamps = new Layer { Material = additive, ZIndex = 0, LightMask = 0, Paint = PaintGuardLamps };
                AddChild(_guardLamps);
            }
            if (!AirTest.Off("muendung"))
            {
                _shotShadows = new Layer { ZIndex = 0, LightMask = 0, Paint = PaintShotShadows };
                AddChild(_shotShadows);
            }
            if (!AirTest.Off("scheinwerfer") && MapSite.Current.Tram)
            {
                _beam = new Layer { Material = additive, ZIndex = 0, LightMask = 0, Paint = PaintTramBeam };
                AddChild(_beam);
            }
            if (!AirTest.Off("pendel"))
            {
                foreach (var (tile, height, cord) in site.SwingLamps)
                {
                    AddSwing(tile, height, cord, additive);
                }
            }
        }

        /// <summary>
        /// Die schaltbaren Lichtflicken der Grafik (patches im Manifest): je Quelle ein Bild mit dem Licht, das sie auf
        /// Boden, Fassaden und Dächer wirft, schon mit der Flächenfarbe multipliziert. Fehlt die Liste, gibt es keine.
        /// </summary>
        private void LoadPatches(Material additive)
        {
            if (!File.Exists(ManifestPath))
            {
                return;
            }
            using var manifest = JsonDocument.Parse(File.ReadAllText(ManifestPath));
            if (!manifest.RootElement.TryGetProperty("patches", out var patches))
            {
                return;
            }
            string folder = Path.GetDirectoryName(ManifestPath) ?? "";
            foreach (var p in patches.EnumerateArray())
            {
                string file = Path.Combine(folder, p.GetProperty("file").GetString() ?? "");
                if (!File.Exists(file))
                {
                    continue;
                }
                var panes = new List<int>();
                foreach (var id in p.GetProperty("panes").EnumerateArray())
                {
                    panes.Add(id.GetInt32());
                }
                var sprite = new Sprite2D
                {
                    Texture = ImageTexture.CreateFromImage(Image.LoadFromFile(file)), Centered = false, Material = additive,
                    Position = new Vector2(p.GetProperty("px").GetSingle(), p.GetProperty("py").GetSingle()),
                    TextureFilter = TextureFilterEnum.Linear, ZIndex = OverCityZ, LightMask = 0, Modulate = Colors.Black,
                };
                AddChild(sprite);
                _patches.Add(new Patch { Sprite = sprite, Kind = p.GetProperty("kind").GetString() ?? "", Panes = panes.ToArray() });
            }
        }

        private void AddSwing(Vector2 tile, float height, float cord, Material additive)
        {
            var color = MapSite.Current.Sources.Count > 0 ? MapSite.Current.Sources[0].Color : WindowWarm;
            int z = Game.DepthZ(Mathf.Clamp(tile.X, 0.05f, MapSite.Tiles.X - 0.05f), Mathf.Clamp(tile.Y, 0.05f, MapSite.Tiles.Y - 0.05f)) + 1;
            var swing = new Swing { Foot = Map(tile.X, tile.Y), Height = height, Cord = cord, Phase = tile.X * 1.7f + tile.Y, Color = color };
            swing.Body = new Layer { ZIndex = z, LightMask = 0, Paint = layer => PaintSwing(layer, swing) };
            swing.Halo = new Sprite2D { Texture = Glow, Material = additive, Scale = Vector2.One * 0.3f, ZIndex = z, LightMask = 0, Modulate = Lit(color, 0.34f) };
            // Schwach und klein: Der Schein wandert sichtbar über das Pflaster, hellt die Gegend aber nicht auf.
            swing.Light = new PointLight2D { Texture = Glow, TextureScale = 0.32f, Color = color, Energy = 0.3f, RangeZMin = -1, RangeZMax = 4096 };
            AddChild(swing.Body);
            AddChild(swing.Halo);
            AddChild(swing.Light);
            _swings.Add(swing);
        }

        private float SwingAngle(Swing s) => 0.13f * Mathf.Sin(_time * 2.3f + s.Phase) + Game.Climate.Wind / 34f * 0.24f * Mathf.Sin(_time * 3.4f + s.Phase * 2f);

        public override void _Process(double delta)
        {
            _time += (float)delta;
            foreach (var patch in _patches)
            {
                float level = 0f;
                foreach (int id in patch.Panes)
                {
                    level += Windows.PaneLevel(id);
                }
                // Fenster und Schweißlicht haben eine Kennung, das Schild leuchtet nach dem Anteil seiner Buchstaben.
                level = patch.Panes.Length > 0 ? Mathf.Clamp(level / patch.Panes.Length, 0f, 1f) : 0f;
                patch.Sprite.Modulate = new Color(_unNight.R * level, _unNight.G * level, _unNight.B * level);
                patch.Sprite.Visible = level > 0.004f;
            }
            foreach (var (sprite, alpha) in _fires)
            {
                sprite.Modulate = new Color(sprite.Modulate, alpha * Game.FireLevel);
            }
            if (_signHalo != null)
            {
                float lit = 0f;
                foreach (int id in _letters)
                {
                    lit += Windows.PaneLevel(id);
                }
                _signHalo.Modulate = new Color(_signHalo.Modulate, SignHaloAlpha * lit / _letters.Count);
            }
            foreach (var s in _swings)
            {
                float a = SwingAngle(s);
                var bob = s.Foot - new Vector2(0, s.Height) + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * s.Cord;
                s.Halo.Position = bob;
                s.Light.Position = s.Foot + new Vector2(Mathf.Sin(a) * s.Height * 0.9f, 0f);
                s.Body.QueueRedraw();
            }
            _windowHalos?.QueueRedraw();
            _guardLamps?.QueueRedraw();
            _shotShadows?.QueueRedraw();
            _beam?.QueueRedraw();
        }

        /// <summary>Ein weicher Hof um jedes Fenster, hinter dem Licht brennt. Geht das Licht aus, geht der Hof mit.</summary>
        private void PaintWindowHalos(Layer layer)
        {
            var size = new Vector2(WindowHaloPixels, WindowHaloPixels);
            var seen = ViewCull.Seen(layer, WindowHaloPixels);
            foreach (var (id, kind, center) in Windows.AllPanes)
            {
                if (kind == "sign" || !seen.HasPoint(center))
                {
                    continue;
                }
                float level = Windows.PaneLevel(id);
                if (level <= 0.01f)
                {
                    continue;
                }
                layer.DrawTextureRect(Glow, new Rect2(center - size / 2f, size), false, Lit(kind == "tv" ? ScreenCold : WindowWarm, WindowHaloAlpha * Mathf.Min(1f, level)));
            }
        }

        /// <summary>
        /// Hundeführer und Wachhabender tragen eine Laterne. Ihr Schein ist derselbe Fächer wie der Blick der Wache:
        /// gleiche Richtung, gleicher Winkel, und er endet, wo die Wache in diesem Licht niemanden mehr bemerkt oder
        /// eine Wand die Sicht sperrt. So zeigt er nichts, was nach den Regeln nicht stimmt.
        /// </summary>
        private void PaintGuardLamps(Layer layer)
        {
            var w = Game.World;
            if (w == null)
            {
                return;
            }
            foreach (var g in w.Units)
            {
                if (!g.Alive || g.Faction != Faction.Guard || Array.IndexOf(LampBearers, g.Type.Id) < 0 || (g.FacingX == 0 && g.FacingY == 0))
                {
                    continue;
                }
                float center = Mathf.Atan2(g.FacingY, g.FacingX);
                float half = Mathf.DegToRad(World.GuardConeHalfDegrees);
                var origin = Game.ToScreen(g.X, g.Y);
                var far = Lit(GuardLamp, 0f);
                Vector2 previous = default;
                Color previousColor = far;
                int step = World.TileUnits / 4;
                for (int i = 0; i <= ConeRays; i++)
                {
                    float offCenter = Mathf.Abs(2f * i / ConeRays - 1f);
                    var near = Lit(GuardLamp, GuardLampAlpha * Mathf.Lerp(1f, GuardLampEdgeShare, offCenter * offCenter));
                    float a = center - half + 2 * half * i / ConeRays;
                    float cos = Mathf.Cos(a), sin = Mathf.Sin(a);
                    int reach = 0;
                    while (reach < World.MaxNoticeRange)
                    {
                        int next = reach + step;
                        int px = g.X + (int)(cos * next), py = g.Y + (int)(sin * next);
                        if (w.Map.IsOpaque(Mathf.FloorToInt(px / (float)World.TileUnits), Mathf.FloorToInt(py / (float)World.TileUnits)) || next > w.NoticeRangeAt(px, py))
                        {
                            break;
                        }
                        reach = next;
                    }
                    var end = Game.ToScreen(g.X + (int)(cos * reach), g.Y + (int)(sin * reach));
                    var color = near.Lerp(far, Mathf.Sqrt((float)reach / World.MaxNoticeRange));
                    if (i > 0 && (end != origin || previous != origin))
                    {
                        layer.DrawPolygon(new[] { origin, previous, end }, new[] { near, previousColor, color });
                    }
                    previous = end;
                    previousColor = color;
                }
                // Die Laterne selbst: ein kleiner Schein in Hüfthöhe.
                var size = new Vector2(GuardGlowPixels, GuardGlowPixels);
                layer.DrawTextureRect(Glow, new Rect2(origin - new Vector2(0, 12f) - size / 2f, size), false, Lit(GuardLamp, 0.6f));
            }
        }

        /// <summary>Im Mündungsfeuer werfen die Figuren ringsum für einen Augenblick einen harten Schatten vom Schützen weg.</summary>
        private void PaintShotShadows(Layer layer)
        {
            var w = Game.World;
            if (w == null)
            {
                return;
            }
            for (int i = w.Shots.Count - 1; i >= 0; i--)
            {
                var shot = w.Shots[i];
                int age = w.Tick - shot.Tick;
                if (age > ShotShadowTicks)
                {
                    break;
                }
                var from = new Vector2(shot.FromX, shot.FromY) / World.TileUnits;
                foreach (var u in w.Units)
                {
                    if (!u.Alive || u.Id == shot.ShooterId)
                    {
                        continue;
                    }
                    var at = new Vector2(u.X, u.Y) / World.TileUnits;
                    float distance = at.DistanceTo(from);
                    if (distance > ShotShadowTiles || distance < 0.2f)
                    {
                        continue;
                    }
                    var dir = (at - from) / distance;
                    var side = new Vector2(-dir.Y, dir.X) * ShotShadowHalfWidth;
                    var end = at + dir * ShotShadowLength;
                    var dark = new Color(0f, 0f, 0f, ShotShadowAlpha * (1f - age / (ShotShadowTicks + 1f)) * (1f - distance / ShotShadowTiles));
                    var clear = new Color(0f, 0f, 0f, 0f);
                    layer.DrawPolygon(
                        new[] { Map(at.X + side.X, at.Y + side.Y), Map(at.X - side.X, at.Y - side.Y), Map(end.X - side.X * 1.6f, end.Y - side.Y * 1.6f), Map(end.X + side.X * 1.6f, end.Y + side.Y * 1.6f) },
                        new[] { dark, dark, clear, clear });
                }
            }
        }

        /// <summary>Die stehende Bahn leuchtet mit ihren Scheinwerfern die Gleise vor sich aus.</summary>
        private void PaintTramBeam(Layer layer)
        {
            var tram = TramOf();
            if (tram == null || !tram.Exists)
            {
                return;
            }
            float x = tram.MiddleX, y = tram.FrontY;
            float light = tram.Light;
            var near = Lit(TramLamp, BeamAlpha * light);
            var far = Lit(TramLamp, 0f);
            var apex = Map(x, y + 0.05f);
            const int fans = 6;
            Vector2 previous = default;
            for (int i = 0; i <= fans; i++)
            {
                float side = (2f * i / fans - 1f) * BeamHalfTiles;
                var end = Map(x + side, y + BeamTiles - Mathf.Abs(side) * 0.8f);
                if (i > 0)
                {
                    layer.DrawPolygon(new[] { apex, previous, end }, new[] { near, far, far });
                }
                previous = end;
            }
            var size = new Vector2(34f, 34f);
            foreach (float lamp in new[] { -0.32f, 0.32f })
            {
                layer.DrawTextureRect(Glow, new Rect2(Map(x + lamp, y) - new Vector2(0, 9f) - size / 2f, size), false, Lit(TramLamp, 0.55f * light));
            }
        }

        private void PaintSwing(Layer layer, Swing s)
        {
            float a = SwingAngle(s);
            var anchor = s.Foot - new Vector2(0, s.Height);
            var bob = anchor + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * s.Cord;
            var dark = Lit(new Color(0.05f, 0.05f, 0.06f), 1f);
            layer.DrawLine(anchor, bob, dark, 0.6f);
            // Blechschirm und Birne.
            layer.DrawColoredPolygon(new[] { bob + new Vector2(-3.5f, 0f), bob + new Vector2(3.5f, 0f), bob + new Vector2(1.2f, -2.5f), bob + new Vector2(-1.2f, -2.5f) }, dark);
            layer.DrawRect(new Rect2(bob + new Vector2(-1f, 0f), new Vector2(2f, 1.6f)), Lit(s.Color, 1f));
        }
    }
}
