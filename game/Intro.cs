using Godot;

namespace Messingstadt
{
    /// <summary>
    /// Filmischer Einstieg: Titel auf Schwarz, die Kamera sinkt durch den Regen auf den Trupp, dann tippt
    /// eine Schreibmaschine den Auftrag. Das Spiel steht so lange still. Jede Taste oder ein Klick springt weiter.
    /// </summary>
    public partial class Intro : CanvasLayer
    {
        private const float TitleEnd = 3.6f;
        private const float FadeSeconds = 1.6f;
        private const float DescentEnd = 9.5f;
        private const float StartZoom = 1.05f;
        private const float EndZoom = 2f;
        private const float LettersPerSecond = 38f;

        private ColorRect _black = null!;
        private Label _title = null!;
        private Label _subtitle = null!;
        private PanelContainer _paper = null!;
        private Label _typed = null!;
        private Label _hint = null!;
        private float _time;
        private float _typing = -1f;
        private int _shownLetters;

        public Main Game = null!;
        public string Briefing = "";
        public string MissionTitle = "";
        public Vector2 CameraFrom;
        public Vector2 CameraTo;

        public override void _Ready()
        {
            Layer = 10;
            _black = new ColorRect { Size = new Vector2(1280, 800), Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Ignore };
            AddChild(_black);
            _title = PaperUi.Text("MESSINGSTADT", 64, new Color(0.82f, 0.68f, 0.38f), PaperUi.Serif);
            _title.Position = new Vector2(0, 320);
            _title.Size = new Vector2(1280, 90);
            _title.HorizontalAlignment = HorizontalAlignment.Center;
            AddChild(_title);
            _subtitle = PaperUi.Text("1927. Die Stadt gehört den Brandt-Werken. Noch.", 20, new Color(0.65f, 0.62f, 0.55f), PaperUi.Typewriter);
            _subtitle.Position = new Vector2(0, 408);
            _subtitle.Size = new Vector2(1280, 40);
            _subtitle.HorizontalAlignment = HorizontalAlignment.Center;
            AddChild(_subtitle);
            _paper = new PanelContainer { Position = new Vector2(300, 190), CustomMinimumSize = new Vector2(680, 300), Visible = false };
            _paper.AddThemeStyleboxOverride("panel", PaperUi.PaperBox(PaperUi.PaperColor));
            AddChild(_paper);
            var body = new VBoxContainer();
            body.AddThemeConstantOverride("separation", 10);
            _paper.AddChild(body);
            body.AddChild(PaperUi.Text("ROTER HAMMER   VERTRAULICH", 14, PaperUi.StampRed, PaperUi.Serif));
            body.AddChild(PaperUi.Text("Einsatz: " + MissionTitle, 24, PaperUi.Ink, PaperUi.Serif));
            _typed = PaperUi.Text("", 17, PaperUi.Ink, PaperUi.Typewriter);
            _typed.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _typed.CustomMinimumSize = new Vector2(640, 170);
            body.AddChild(_typed);
            _hint = PaperUi.Text("Klick oder Taste: Einsatz beginnen", 13, new Color(PaperUi.Ink, 0.6f), PaperUi.Typewriter);
            _hint.Visible = false;
            body.AddChild(_hint);
            Game.Sound.PlayEverywhere("thunder_1", -12f);
        }

        public override void _Process(double delta)
        {
            _time += (float)delta;
            if (_typing >= 0f)
            {
                UpdateTyping((float)delta);
                return;
            }
            float titleAlpha = Mathf.Min(1f, _time / 1.2f) * (1f - Mathf.Clamp((_time - TitleEnd + 1f) / 1f, 0f, 1f));
            _title.Modulate = new Color(1f, 1f, 1f, titleAlpha);
            _subtitle.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(titleAlpha * 2f - 0.6f, 0f, 1f));
            _black.Color = new Color(0f, 0f, 0f, 1f - Mathf.Clamp((_time - TitleEnd) / FadeSeconds, 0f, 1f));
            float t = Mathf.Clamp((_time - TitleEnd + 1f) / (DescentEnd - TitleEnd + 1f), 0f, 1f);
            float eased = t * t * (3f - 2f * t);
            Game.SetIntroCamera(CameraFrom.Lerp(CameraTo, eased), Mathf.Lerp(StartZoom, EndZoom, eased));
            if (_time >= DescentEnd)
            {
                StartTyping();
            }
        }

        private void StartTyping()
        {
            _typing = 0f;
            _black.Color = new Color(0f, 0f, 0f, 0.35f);
            _title.Visible = false;
            _subtitle.Visible = false;
            _paper.Visible = true;
            Game.SetIntroCamera(CameraTo, EndZoom);
        }

        private void UpdateTyping(float dt)
        {
            _typing += dt;
            int letters = Mathf.Min(Briefing.Length, (int)(_typing * LettersPerSecond));
            if (letters != _shownLetters)
            {
                _shownLetters = letters;
                _typed.Text = Briefing[..letters];
                if (letters % 2 == 0)
                {
                    Game.Sound.Play("typewriter", Game.CameraCenter, -14f, 100000f, 0.12f);
                }
            }
            _hint.Visible = letters >= Briefing.Length;
        }

        public override void _Input(InputEvent e)
        {
            bool pressed = e is InputEventKey { Pressed: true, Echo: false } or InputEventMouseButton { Pressed: true };
            if (!pressed)
            {
                return;
            }
            GetViewport().SetInputAsHandled();
            if (_typing < 0f)
            {
                StartTyping();
            }
            else if (_shownLetters < Briefing.Length)
            {
                _typing = Briefing.Length / LettersPerSecond;
            }
            else
            {
                Game.EndIntro();
                QueueFree();
            }
        }
    }
}
