using System;
using System.Collections.Generic;
using Godot;

namespace Messingstadt
{
    /// <summary>
    /// Leben hinter den Scheiben: Fenster gehen an und aus, jemand läuft vorbei, in manchen flimmert der Bildfunk,
    /// und das Hotelschild hat einen Wackelkontakt. Jedes Fenster und jeder Buchstabe hat in der Grafik eine Kennung
    /// (Lichtkarte _w), sein Zustand steht in einer kleinen Textur, die der Shader liest. Reine Darstellung.
    /// </summary>
    public partial class WindowLife : Node
    {
        public const int MaxPanes = 256;

        private const float SomeoneHomeShare = 0.8f;
        private const float MinToggleSeconds = 15f;
        private const float MaxToggleSeconds = 70f;
        private const float PasserChancePerSecond = 0.02f;
        private const float BrokenLetterStutterPerSecond = 0.5f;
        private const float SignDropoutPerSecond = 0.03f;
        private static readonly Color DarkPane = new(0.09f, 0.12f, 0.16f, 1f);
        private static readonly Color PasserPane = new(0.3f, 0.2f, 0.12f, 1f);
        private static readonly Color NeonColor = new(1f, 0.28f, 0.2f, 1f);
        private static readonly Color DeadLetter = new(0.28f, 0.2f, 0.16f, 1f);
        private static readonly Color Unchanged = new(0f, 0f, 0f, 0f);

        private enum PaneKind
        {
            Window,
            Screen,
            Letter,
        }

        private sealed class Pane
        {
            public int Id;
            public PaneKind Kind;
            public Vector2 Center;
            public bool On = true;
            public float NextToggle;
            public float PasserUntil;
            public Color Picture;
            public float NextCut;
            public PointLight2D? Light;
        }

        private readonly Random _rng = new();
        private readonly List<Pane> _panes = new();
        private readonly Image _states = Image.CreateEmpty(MaxPanes, 1, false, Image.Format.Rgba8);
        private PointLight2D? _signLight;
        private Pane? _brokenLetter;
        private float _time;
        private float _stutterUntil;
        private float _dropoutUntil;
        private float _nextStutter;

        public ImageTexture States { get; }

        public Texture2D Glow = null!;
        public Node2D LightParent = null!;

        public WindowLife()
        {
            States = ImageTexture.CreateFromImage(_states);
        }

        private float Rand(float min, float max) => min + (float)_rng.NextDouble() * (max - min);

        /// <summary>Meldet ein Fenster oder einen Buchstaben an. kind: window, tv oder sign.</summary>
        public void AddPane(int id, string kind, Vector2 center)
        {
            var pane = new Pane
            {
                Id = id, Center = center,
                Kind = kind == "tv" ? PaneKind.Screen : kind == "sign" ? PaneKind.Letter : PaneKind.Window,
                On = _rng.NextDouble() < SomeoneHomeShare,
                NextToggle = Rand(MinToggleSeconds, MaxToggleSeconds),
            };
            _panes.Add(pane);
        }

        public override void _Ready()
        {
            var letters = new List<Pane>();
            var signCenter = Vector2.Zero;
            foreach (var pane in _panes)
            {
                if (pane.Kind == PaneKind.Screen)
                {
                    // Das bläuliche Flimmern fällt durchs Fenster auf Wand und Boden davor.
                    pane.On = true;
                    pane.Light = new PointLight2D
                    {
                        Texture = Glow, TextureScale = 0.34f, Color = new Color(0.5f, 0.68f, 1f),
                        RangeZMin = -1, RangeZMax = 4096, Position = pane.Center + new Vector2(0, 8),
                    };
                    LightParent.AddChild(pane.Light);
                }
                else if (pane.Kind == PaneKind.Letter)
                {
                    letters.Add(pane);
                    signCenter += pane.Center;
                }
            }
            if (letters.Count > 0)
            {
                _brokenLetter = letters[letters.Count / 2 + 1];
                _signLight = new PointLight2D
                {
                    Texture = Glow, TextureScale = 0.6f, Color = new Color(1f, 0.3f, 0.22f), Energy = 0.55f,
                    RangeZMin = -1, RangeZMax = 4096, Position = signCenter / letters.Count + new Vector2(0, 14),
                };
                LightParent.AddChild(_signLight);
            }
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            _time += dt;
            UpdateSign(dt);
            foreach (var pane in _panes)
            {
                _states.SetPixel(pane.Id, 0, pane.Kind switch
                {
                    PaneKind.Screen => UpdateScreen(pane),
                    PaneKind.Letter => pane == _brokenLetter && _time < _stutterUntil && _rng.NextDouble() < 0.5 || _time < _dropoutUntil ? DeadLetter : NeonColor,
                    _ => UpdateWindow(pane, dt),
                });
            }
            States.Update(_states);
        }

        private Color UpdateWindow(Pane pane, float dt)
        {
            if (_time > pane.NextToggle)
            {
                pane.On = !pane.On;
                pane.NextToggle = _time + Rand(MinToggleSeconds, MaxToggleSeconds);
            }
            if (!pane.On)
            {
                return DarkPane;
            }
            if (_time > pane.PasserUntil && _rng.NextDouble() < PasserChancePerSecond * dt)
            {
                pane.PasserUntil = _time + Rand(0.8f, 2.2f);
            }
            return _time < pane.PasserUntil ? PasserPane : Unchanged;
        }

        /// <summary>Wechselnde Bilder: Mal hell, mal dunkel, dazu ein feines Zittern wie bei einer alten Röhre.</summary>
        private Color UpdateScreen(Pane pane)
        {
            if (_time > pane.NextCut)
            {
                float level = Rand(0.35f, 1f);
                pane.Picture = new Color(level * Rand(0.5f, 0.72f), level * Rand(0.72f, 0.88f), level);
                pane.NextCut = _time + (_rng.NextDouble() < 0.7 ? Rand(0.12f, 0.6f) : Rand(0.8f, 2.4f));
            }
            float tremble = 0.92f + 0.08f * Mathf.Sin(_time * 60f + pane.Id);
            var c = pane.Picture * tremble;
            if (pane.Light != null)
            {
                pane.Light.Energy = c.B * 1.1f;
            }
            return new Color(c.R, c.G, c.B, 1f);
        }

        private void UpdateSign(float dt)
        {
            if (_time > _nextStutter)
            {
                _stutterUntil = _time + Rand(0.4f, 1.4f);
                _nextStutter = _stutterUntil + Rand(0f, 2f / BrokenLetterStutterPerSecond);
            }
            if (_time > _dropoutUntil && _rng.NextDouble() < SignDropoutPerSecond * dt)
            {
                _dropoutUntil = _time + Rand(0.1f, 0.5f);
            }
            if (_signLight != null)
            {
                _signLight.Energy = _time < _dropoutUntil ? 0.05f : 0.55f;
            }
        }
    }
}
