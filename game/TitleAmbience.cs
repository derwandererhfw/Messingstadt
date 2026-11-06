using System;
using System.Collections.Generic;
using Godot;

namespace Messingstadt
{
    /// <summary>
    /// Wetter über dem Startbildschirm: Regen, ab und zu ein ferner Blitz, eine flackernde Laterne und ziehender Dampf.
    /// Wird als eigene Ebene über ein stehendes Titelbild gelegt und füllt immer seinen ganzen Bereich. Alles bleibt
    /// gedämpft, damit der Menütext lesbar bleibt. Orte stehen anteilig (0 bis 1), so passt es zu jeder Fenstergröße.
    /// </summary>
    public partial class TitleAmbience : Control
    {
        private const int Drops = 170;
        private const float FallSpeed = 520f;
        private const float DriftSpeed = 90f;
        private const float DropLength = 13f;
        private const float RainAlpha = 0.22f;
        private const float MinStormPause = 22f;
        private const float MaxStormPause = 55f;
        private const float FirstStorm = 6f;
        private const float FlashFadePerSecond = 6f;
        private const float FlashAlpha = 0.28f;
        private const float LampRadius = 0.16f;
        private const float LampAlpha = 0.32f;
        private const float StutterChancePerSecond = 0.25f;
        private const int PuffsPerSecond = 3;
        private const float PuffSeconds = 6f;
        private const float PuffAlpha = 0.09f;
        private static readonly Color RainColor = new(0.78f, 0.84f, 1f);
        private static readonly Color LampColor = new(1f, 0.72f, 0.38f);
        private static readonly Color FlashColor = new(0.62f, 0.7f, 0.95f);
        private static readonly Color SteamColor = new(0.8f, 0.82f, 0.88f);

        private sealed class Puff
        {
            public Vector2 Pos;
            public float Age;
            public float Size;
        }

        private readonly Random _rng = new();
        private readonly Vector2[] _drops = new Vector2[Drops];
        private readonly Vector2[] _lines = new Vector2[Drops * 2];
        private readonly List<Puff> _puffs = new();
        private readonly List<float> _strokes = new();
        private Glow _glow = null!;
        private Texture2D _round = null!;
        private Texture2D _sky = null!;
        private float _time;
        private float _flash;
        private float _nextStorm = FirstStorm;
        private float _stutterUntil;
        private float _lamp = 1f;
        private float _puffDebt;

        /// <summary>Wie stark alles wirkt, von 0 (aus) bis 1. Kleiner, wenn der Hintergrund schon unruhig ist.</summary>
        public float Strength = 1f;

        /// <summary>Wo die Laterne im Titelbild steht, anteilig. Negativ: keine Laterne.</summary>
        public Vector2 LampAt = new(0.16f, 0.4f);

        /// <summary>Wo Dampf aufsteigt, anteilig, etwa aus Gullys oder Schloten im Titelbild.</summary>
        public List<Vector2> SteamAt = new() { new Vector2(0.78f, 0.86f), new Vector2(0.3f, 0.92f) };

        /// <summary>Prüflauf: hält den Blitz auf dieser Stärke, negativ heißt aus.</summary>
        public float HeldFlash = -1f;

        /// <summary>Die Laterne liegt auf einer eigenen Ebene, damit ihr Schein aufhellt statt zu überdecken.</summary>
        private sealed partial class Glow : Control
        {
            public TitleAmbience Owner_ = null!;

            public override void _Draw() => Owner_.DrawGlow(this);
        }

        private float Rand(float min, float max) => min + (float)_rng.NextDouble() * (max - min);

        public override void _Ready()
        {
            MouseFilter = MouseFilterEnum.Ignore;
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            for (int i = 0; i < Drops; i++)
            {
                _drops[i] = new Vector2((float)_rng.NextDouble(), (float)_rng.NextDouble());
            }
            _round = new GradientTexture2D
            {
                Gradient = new Gradient { Offsets = new[] { 0f, 0.4f, 1f }, Colors = new[] { Colors.White, new Color(1f, 1f, 1f, 0.35f), new Color(1f, 1f, 1f, 0f) } },
                Width = 64, Height = 64, Fill = GradientTexture2D.FillEnum.Radial,
                FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1f, 0.5f),
            };
            // Der Blitz hellt den Himmel oben auf, nach unten wird er schwächer, wo Häuser und Menü stehen.
            _sky = new GradientTexture2D
            {
                Gradient = new Gradient { Offsets = new[] { 0f, 1f }, Colors = new[] { Colors.White, new Color(1f, 1f, 1f, 0.1f) } },
                Width = 4, Height = 64, FillFrom = new Vector2(0.5f, 0f), FillTo = new Vector2(0.5f, 1f),
            };
            _glow = new Glow
            {
                Owner_ = this, MouseFilter = MouseFilterEnum.Ignore,
                Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
            };
            AddChild(_glow);
            _glow.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            _time += dt;
            UpdateRain(dt);
            UpdateStorm(dt);
            UpdateLamp(dt);
            UpdateSteam(dt);
            QueueRedraw();
            _glow.QueueRedraw();
        }

        private void UpdateRain(float dt)
        {
            var size = Size;
            if (size.X <= 0f || size.Y <= 0f)
            {
                return;
            }
            var step = new Vector2(DriftSpeed / size.X, FallSpeed / size.Y) * dt;
            var tail = new Vector2(DriftSpeed, FallSpeed).Normalized() * DropLength;
            for (int i = 0; i < Drops; i++)
            {
                _drops[i] += step;
                if (_drops[i].Y > 1f || _drops[i].X > 1f)
                {
                    _drops[i] = new Vector2((float)_rng.NextDouble() * 1.15f - 0.15f, 0f);
                }
                var p = _drops[i] * size;
                _lines[i * 2] = p;
                _lines[i * 2 + 1] = p + tail;
            }
        }

        /// <summary>Zwei bis vier schnelle Entladungen weit hinter der Stadt, ohne sichtbaren Strahl.</summary>
        private void UpdateStorm(float dt)
        {
            if (_time > _nextStorm)
            {
                _nextStorm = _time + Rand(MinStormPause, MaxStormPause);
                float at = _time;
                for (int i = _rng.Next(2, 5); i > 0; i--)
                {
                    _strokes.Add(at);
                    at += Rand(0.06f, 0.25f);
                }
            }
            for (int i = _strokes.Count - 1; i >= 0; i--)
            {
                if (_time >= _strokes[i])
                {
                    _flash = Mathf.Max(_flash, Rand(0.6f, 1f));
                    _strokes.RemoveAt(i);
                }
            }
            _flash = HeldFlash >= 0f ? HeldFlash : Mathf.Max(0f, _flash - _flash * FlashFadePerSecond * dt - 0.2f * dt);
        }

        /// <summary>Leises Unruhigsein, dazu ab und zu ein Wackelkontakt, bei dem das Licht kurz aussetzt.</summary>
        private void UpdateLamp(float dt)
        {
            if (_time > _stutterUntil && _rng.NextDouble() < StutterChancePerSecond * dt)
            {
                _stutterUntil = _time + Rand(0.2f, 0.7f);
            }
            float unrest = 0.92f + 0.05f * Mathf.Sin(_time * 3f) + 0.03f * Mathf.Sin(_time * 11f);
            _lamp = _time < _stutterUntil && _rng.NextDouble() < 0.55 ? 0.15f : unrest;
        }

        private void UpdateSteam(float dt)
        {
            _puffDebt += PuffsPerSecond * SteamAt.Count * dt;
            while (_puffDebt >= 1f && SteamAt.Count > 0)
            {
                _puffDebt -= 1f;
                var at = SteamAt[_rng.Next(SteamAt.Count)] * Size;
                _puffs.Add(new Puff { Pos = at + new Vector2(Rand(-8f, 8f), 0f), Size = Rand(14f, 26f) });
            }
            foreach (var p in _puffs)
            {
                p.Age += dt;
                p.Pos += new Vector2(9f + 6f * Mathf.Sin(_time * 0.7f + p.Size), -16f) * dt;
            }
            _puffs.RemoveAll(p => p.Age >= PuffSeconds);
        }

        public override void _Draw()
        {
            float strength = Mathf.Clamp(Strength, 0f, 1f);
            foreach (var p in _puffs)
            {
                float f = p.Age / PuffSeconds;
                float radius = p.Size * (1f + 2.2f * f);
                float alpha = PuffAlpha * strength * Mathf.Min(1f, f * 5f) * (1f - f);
                DrawTextureRect(_round, new Rect2(p.Pos - new Vector2(radius, radius), new Vector2(radius * 2f, radius * 2f)), false, new Color(SteamColor, alpha));
            }
            DrawMultiline(_lines, new Color(RainColor, RainAlpha * strength * (1f + _flash)), 1f);
        }

        private void DrawGlow(CanvasItem canvas)
        {
            float strength = Mathf.Clamp(Strength, 0f, 1f);
            var size = Size;
            if (LampAt.X >= 0f)
            {
                float radius = LampRadius * Mathf.Min(size.X, size.Y);
                var at = LampAt * size;
                canvas.DrawTextureRect(_round, new Rect2(at - new Vector2(radius, radius), new Vector2(radius * 2f, radius * 2f)), false,
                    new Color(LampColor, LampAlpha * _lamp * strength));
            }
            if (_flash > 0.01f)
            {
                canvas.DrawTextureRect(_sky, new Rect2(Vector2.Zero, size), false, new Color(FlashColor, FlashAlpha * _flash * strength));
            }
        }
    }
}
