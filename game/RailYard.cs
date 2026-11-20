using System;
using System.Collections.Generic;
using Godot;
using Messingstadt.Core;

namespace Messingstadt
{
    /// <summary>
    /// Was einen Güterbahnhof bei Nacht weit macht: Signallichter und ein Güterzug, der fern über den Bahndamm zieht,
    /// mit Rauch und rotem Schlusslicht. Die Orte nennt die Karte (signals, farTrain). Reine Darstellung: Kein Licht
    /// hier zählt für das Schleichen, der Zug fährt hinter allem, was der Trupp betritt.
    /// Abschaltbar mit --ohne=signale,zug. Prüflauf: --zug lässt den Zug gleich kommen.
    /// </summary>
    public partial class RailYard : Node2D
    {
        private const float WagonTiles = 1.7f;
        private const float GapTiles = 0.12f;
        private const float WagonPixels = 18f;
        private const float WagonDeepTiles = 0.4f;
        private const float TilesPerSecond = 1.5f;
        private const float GreenLeadSeconds = 9f;
        private const float SmokeEverySeconds = 0.35f;
        private const float SmokeSeconds = 7f;
        private const float SignalHalo = 16f;
        private static readonly Color WagonColor = new(0.085f, 0.075f, 0.07f);
        private static readonly Color RoofColor = new(0.15f, 0.155f, 0.18f);
        private static readonly Color SmokeColor = new(0.42f, 0.42f, 0.46f);
        private static readonly Color FireColor = new(1f, 0.5f, 0.18f);
        private static readonly Color Red = new(1f, 0.16f, 0.1f);
        private static readonly Color Green = new(0.2f, 1f, 0.55f);
        private static readonly Color White = new(1f, 0.92f, 0.75f);

        private sealed partial class Layer : Node2D
        {
            public Action<Layer> Paint = _ => { };

            public override void _Draw() => Paint(this);
        }

        private readonly Random _rng = new();
        private readonly List<(Vector2 Tile, float Age, float Drift)> _smoke = new();
        private Layer? _train;
        private Layer? _lights;
        private Color _unNight;
        private float _time;
        private float _nextTrain;
        private float _head = float.NaN;
        private int _wagons;
        private int _direction = -1;
        private float _nextSmoke;

        public Main Game = null!;
        public Texture2D Glow = null!;

        /// <summary>Fährt gerade ein Zug über den Damm? Für die Ton-Session, falls sie ein fernes Rollen dazu legt.</summary>
        public bool TrainPassing => !float.IsNaN(_head);

        private float Rand(float min, float max) => min + (float)_rng.NextDouble() * (max - min);

        private Vector2 Map(float x, float y) => Game.ToScreen((int)(x * World.TileUnits), (int)(y * World.TileUnits));

        private Color Own(Color c, float alpha = 1f) => new(c.R * _unNight.R, c.G * _unNight.G, c.B * _unNight.B, alpha);

        public override void _Ready()
        {
            var night = Game.NightColor;
            _unNight = new Color(1f / night.R, 1f / night.G, 1f / night.B);
            LightMask = 0;
            var site = MapSite.Current;
            if (site.FarTrain is { } far && !AirTest.Off("zug"))
            {
                // Der Zug fährt auf dem Damm: vor dessen Mauer, hinter allem, was südlich davon steht.
                _train = new Layer { ZIndex = Game.DepthZ(MapSite.Tiles.X / 2f, Mathf.Clamp(far.Y + 0.9f, 0.05f, MapSite.Tiles.Y - 0.05f)) + 1, LightMask = 0, Paint = PaintTrain };
                AddChild(_train);
                // Prüflauf: --zug lässt den Zug gleich kommen.
                _nextTrain = Array.IndexOf(OS.GetCmdlineUserArgs(), "--zug") >= 0 ? 1f : Rand(12f, 35f);
            }
            if (site.Signals.Count > 0 && !AirTest.Off("signale"))
            {
                _lights = new Layer
                {
                    Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add }, ZIndex = 2900, LightMask = 0, Paint = PaintSignals,
                };
                AddChild(_lights);
            }
            if (_train == null && _lights == null)
            {
                SetProcess(false);
            }
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            _time += dt;
            if (_train != null && MapSite.Current.FarTrain is { } far)
            {
                float length = _wagons * (WagonTiles + GapTiles);
                if (float.IsNaN(_head))
                {
                    if (_time >= _nextTrain)
                    {
                        _wagons = _rng.Next(8, 15);
                        _direction = _rng.Next(2) == 0 ? -1 : 1;
                        _head = _direction < 0 ? MapSite.Tiles.X + 1f : -1f;
                        _nextSmoke = 0f;
                    }
                }
                else
                {
                    _head += _direction * TilesPerSecond * dt;
                    _nextSmoke -= dt;
                    if (_nextSmoke <= 0f && _head > -1f && _head < MapSite.Tiles.X + 1f)
                    {
                        _nextSmoke = SmokeEverySeconds;
                        _smoke.Add((new Vector2(_head, far.Y), 0f, Rand(-0.25f, 0.25f)));
                    }
                    bool gone = _direction < 0 ? _head + length < -2f : _head - length > MapSite.Tiles.X + 2f;
                    if (gone)
                    {
                        _head = float.NaN;
                        _nextTrain = _time + Rand(far.Pause.X, far.Pause.Y);
                    }
                }
                for (int i = _smoke.Count - 1; i >= 0; i--)
                {
                    var s = _smoke[i];
                    s.Age += dt;
                    if (s.Age > SmokeSeconds)
                    {
                        _smoke.RemoveAt(i);
                    }
                    else
                    {
                        _smoke[i] = s;
                    }
                }
                if (TrainPassing || _smoke.Count > 0)
                {
                    _train.QueueRedraw();
                }
            }
            _lights?.QueueRedraw();
        }

        private void PaintTrain(Layer layer)
        {
            if (MapSite.Current.FarTrain is not { } far)
            {
                return;
            }
            var up = new Vector2(0, -far.Height);
            if (TrainPassing)
            {
                for (int i = 0; i <= _wagons; i++)
                {
                    // Wagen 0 ist die Lok: kürzer, höher, mit Feuerschein am Führerstand.
                    float front = _head - _direction * i * (WagonTiles + GapTiles);
                    float back = front - _direction * (i == 0 ? WagonTiles * 0.8f : WagonTiles);
                    if (Mathf.Max(front, back) < 0f || Mathf.Min(front, back) > MapSite.Tiles.X)
                    {
                        continue;
                    }
                    float x0 = Mathf.Clamp(Mathf.Min(front, back), 0f, MapSite.Tiles.X), x1 = Mathf.Clamp(Mathf.Max(front, back), 0f, MapSite.Tiles.X);
                    var a = Map(x0, far.Y) + up;
                    var b = Map(x1, far.Y) + up;
                    float tall = i == 0 ? WagonPixels + 3f : WagonPixels - (i * 5 % 3);
                    var top = new Vector2(0, -tall);
                    // Seitenwand nach Süden, darüber das Dach, das nach hinten wegläuft.
                    var deep = Map(x0, far.Y - WagonDeepTiles) - Map(x0, far.Y);
                    layer.DrawColoredPolygon(new[] { a, b, b + top, a + top }, Own(WagonColor));
                    layer.DrawColoredPolygon(new[] { a + top, b + top, b + top + deep, a + top + deep }, Own(RoofColor));
                    if (i == 0)
                    {
                        var cab = (_direction < 0 ? b : a) + new Vector2(0, -tall * 0.55f);
                        float fire = 0.75f + 0.25f * Mathf.Sin(_time * 9f);
                        layer.DrawTextureRect(Glow, new Rect2(cab - new Vector2(7f, 7f), new Vector2(14f, 14f)), false, Own(FireColor, 0.55f * fire));
                    }
                    else if (i == _wagons)
                    {
                        var tail = (_direction < 0 ? b : a) + new Vector2(0, -3f);
                        layer.DrawTextureRect(Glow, new Rect2(tail - new Vector2(4f, 4f), new Vector2(8f, 8f)), false, Own(Red, 0.9f));
                    }
                }
            }
            foreach (var (tile, age, drift) in _smoke)
            {
                float life = age / SmokeSeconds;
                float size = 14f + 30f * life;
                var at = Map(Mathf.Clamp(tile.X, 0f, MapSite.Tiles.X), tile.Y) + up + new Vector2((drift + Game.Climate.Wind / 20f) * 40f * life, -WagonPixels - 6f - 26f * life);
                layer.DrawTextureRect(Glow, new Rect2(at - new Vector2(size, size * 0.7f) / 2f, new Vector2(size, size * 0.7f)), false, Own(SmokeColor, 0.55f * (1f - life)));
            }
        }

        private void PaintSignals(Layer layer)
        {
            // Streckensignale zeigen Fahrt, kurz bevor der Zug kommt, und fallen hinter ihm wieder auf Halt.
            bool clear = TrainPassing || (_train != null && _nextTrain - _time < GreenLeadSeconds);
            foreach (var (tile, height, kind) in MapSite.Current.Signals)
            {
                var color = kind switch
                {
                    "gruen" => Green,
                    "weiss" => White,
                    "strecke" => clear ? Green : Red,
                    _ => Red,
                };
                var at = Map(tile.X, tile.Y) - new Vector2(0, height);
                // Petroleum hinter farbigem Glas: fast ruhig, nie ganz gleich.
                float glow = 0.85f + 0.15f * Mathf.Sin(_time * 3.1f + tile.X * 2.3f);
                layer.DrawTextureRect(Glow, new Rect2(at - new Vector2(SignalHalo, SignalHalo) / 2f, new Vector2(SignalHalo, SignalHalo)), false, Own(color, 0.3f * glow));
                layer.DrawTextureRect(Glow, new Rect2(at - new Vector2(3f, 3f), new Vector2(6f, 6f)), false, Own(color, 0.95f * glow));
            }
        }
    }
}
