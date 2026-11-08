using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Messingstadt.Core
{
    /// <summary>Kamerafahrt eines Abschnitts. Ziele sind Ankerpunkte wie @trupp oder Felder als "x,y".</summary>
    public sealed class SequenceCamera
    {
        public string From { get; set; } = "";
        public string To { get; set; } = "";
        public double ZoomFrom { get; set; } = 2;
        public double ZoomTo { get; set; } = 2;

        /// <summary>Verschiebung des Ziels in Bildpunkten der Welt, etwa damit ein Papier daneben Platz hat.</summary>
        public double[]? Offset { get; set; }
    }

    /// <summary>Ein Geräusch, Sekunden nach Beginn des Abschnitts.</summary>
    public sealed class SequenceSound
    {
        public double At { get; set; }
        public string Name { get; set; } = "";
        public double Volume { get; set; } = -10;
    }

    /// <summary>
    /// Ein Abschnitt einer Sequenz. Show sagt, was auf dem Bild liegt: title, card, paper, telegram, newspaper, image oder nichts.
    /// Bei card, paper, telegram und image tippt die Schreibmaschine den Body, die Dauer zählt erst danach.
    /// Bei image ist der Body Kolbs Erzählkasten (docs/story/NOIR_STYLE_GUIDE.md).
    /// </summary>
    public sealed class SequenceBeat
    {
        public double Seconds { get; set; }

        /// <summary>Wartet am Ende auf Klick oder Leertaste.</summary>
        public bool Hold { get; set; }

        public string Show { get; set; } = "";

        /// <summary>Kleine rote Kopfzeile, große Zeile darunter, dann der Text.</summary>
        public string Head { get; set; } = "";
        public string Title { get; set; } = "";
        public string Body { get; set; } = "";

        /// <summary>Schwarzblende von, bis. Ohne Angabe bleibt sie, wie sie war.</summary>
        public double[]? Fade { get; set; }
        public double FadeStart { get; set; }

        /// <summary>Dauer der Blende. Negativ heißt: über den ganzen Abschnitt.</summary>
        public double FadeSeconds { get; set; } = -1;

        public SequenceCamera? Camera { get; set; }
        public List<SequenceSound> Sounds { get; set; } = new();

        /// <summary>Bild für show "image", Pfad unter game/assets.</summary>
        public string Image { get; set; } = "";

        /// <summary>Bild als kleiner Einschub über dem vorigen Bild statt bildschirmfüllend.</summary>
        public bool Inset { get; set; }

        /// <summary>Langsames Heranfahren an das Bild: Vergrößerung von, bis.</summary>
        public double[]? Zoom { get; set; }

        /// <summary>Sprechblasen, erscheinen, wenn der Erzählkasten fertig getippt ist. Form „Name: Satz“.</summary>
        public List<string> Speech { get; set; } = new();

        /// <summary>Beschriftungen nebeneinander am unteren Bildrand, etwa Namen unter Silhouetten.</summary>
        public List<string> Captions { get; set; } = new();

        public bool Typed => Show is "card" or "paper" or "telegram" or "image";
    }

    /// <summary>Eine Zwischensequenz aus data/sequences. Sie läuft in der Spielgrafik und lässt sich jederzeit überspringen.</summary>
    public sealed class SequenceData
    {
        public static readonly string[] Shows = { "", "title", "card", "paper", "telegram", "newspaper", "image" };
        public static readonly string[] Anchors = { "@start", "@trupp", "@ziel", "@ausgang", "@beute", "@gefallen" };

        private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

        public string Id { get; set; } = "";
        public List<SequenceBeat> Beats { get; set; } = new();

        public static SequenceData Parse(string json) => JsonSerializer.Deserialize<SequenceData>(json, Options)!;

        /// <summary>Fehler in den Daten, für Tests und Prüfläufe. Leer heißt: abspielbar.</summary>
        public IEnumerable<string> Problems()
        {
            if (Id.Length == 0)
            {
                yield return "keine Id";
            }
            if (Beats.Count == 0)
            {
                yield return "keine Abschnitte";
            }
            for (int i = 0; i < Beats.Count; i++)
            {
                var b = Beats[i];
                if (b.Seconds < 0)
                {
                    yield return $"Abschnitt {i}: negative Dauer";
                }
                if (!Shows.Contains(b.Show))
                {
                    yield return $"Abschnitt {i}: unbekannte Anzeige {b.Show}";
                }
                if (b.Show == "image" && b.Image.Length == 0)
                {
                    yield return $"Abschnitt {i}: Bild fehlt";
                }
                if (b.Zoom is { Length: not 2 })
                {
                    yield return $"Abschnitt {i}: Zoom braucht zwei Werte";
                }
                if (b.Fade is { Length: not 2 })
                {
                    yield return $"Abschnitt {i}: Blende braucht zwei Werte";
                }
                if (b.Camera is { } c && !(ValidTarget(c.From) && ValidTarget(c.To)))
                {
                    yield return $"Abschnitt {i}: unbekanntes Kameraziel";
                }
            }
        }

        private static bool ValidTarget(string target) => Anchors.Contains(target) || ParseTile(target) != null;

        /// <summary>Feld als "x,y" in Feldern, sonst null.</summary>
        public static (double X, double Y)? ParseTile(string target)
        {
            var parts = target.Split(',');
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            return parts.Length == 2 && double.TryParse(parts[0], System.Globalization.NumberStyles.Float, inv, out double x)
                && double.TryParse(parts[1], System.Globalization.NumberStyles.Float, inv, out double y) ? (x, y) : null;
        }

        /// <summary>Wo ein Kameraziel in der Welt liegt, in Welteinheiten. @start kennt nur die Darstellung und liefert null.</summary>
        public static (int X, int Y)? Locate(string target, World world)
        {
            var agents = world.Units.Where(u => u.Faction == Faction.Agent).ToList();
            (int, int) Squad(IEnumerable<Unit> units)
            {
                var list = units.Any() ? units.ToList() : agents;
                return list.Count == 0 ? (world.ItemX, world.ItemY) : ((int)list.Average(u => u.X), (int)list.Average(u => u.Y));
            }
            switch (target)
            {
                case "@trupp": return Squad(agents.Where(u => u.Alive));
                case "@ziel": return (world.ItemX, world.ItemY);
                case "@ausgang": return ((world.Exit.X0 + world.Exit.X1) / 2, (world.Exit.Y0 + world.Exit.Y1) / 2);
                case "@beute": return world.LootName.Length > 0 ? (world.LootX, world.LootY) : (world.ItemX, world.ItemY);
                case "@gefallen": return Squad(agents.Where(u => !u.Alive));
            }
            return ParseTile(target) is { } t ? ((int)(t.X * World.TileUnits), (int)(t.Y * World.TileUnits)) : null;
        }
    }

    /// <summary>
    /// Ablauf einer Sequenz, ohne Bild und Ton. Advance schiebt die Zeit vor und liefert fällige Geräusche,
    /// Press schreibt den Text fertig oder geht weiter, Skip beendet alles.
    /// </summary>
    public sealed class SequenceRun
    {
        public const double LettersPerSecond = 38;

        private readonly SequenceData _data;
        private readonly IReadOnlyDictionary<string, string> _texts;
        private readonly HashSet<int> _fired = new();
        private double _fade = 1;

        public SequenceRun(SequenceData data, IReadOnlyDictionary<string, string> texts)
        {
            _data = data;
            _texts = texts;
            Done = data.Beats.Count == 0;
            if (!Done)
            {
                _fade = Current.Fade?[0] ?? 1;
            }
        }

        public string Id => _data.Id;
        public int Beat { get; private set; }
        public double Time { get; private set; }
        public bool Done { get; private set; }

        /// <summary>Wie oft der Abschnitt gewechselt hat. Die Darstellung baut bei einer Änderung neu auf.</summary>
        public int Changes { get; private set; }

        public SequenceBeat Current => _data.Beats[Math.Min(Beat, _data.Beats.Count - 1)];
        public string Head => SequenceTexts.Fill(Current.Head, _texts);
        public string Title => SequenceTexts.Fill(Current.Title, _texts);
        public string Body => SequenceTexts.Fill(Current.Body, _texts);

        private double TypeSeconds => Current.Typed ? Body.Length / LettersPerSecond : 0;
        public double Length => TypeSeconds + Current.Seconds;
        public int Letters => Current.Typed ? Math.Min(Body.Length, (int)(Time * LettersPerSecond)) : Body.Length;
        public bool Typing => Letters < Body.Length;

        /// <summary>Der Abschnitt ist durch und wartet auf den Spieler.</summary>
        public bool Waiting => !Done && Current.Hold && Time >= Length;

        /// <summary>Weicher Verlauf der Kamerafahrt von 0 bis 1.</summary>
        public double Progress
        {
            get
            {
                double t = Length <= 0 ? 1 : Math.Clamp(Time / Length, 0, 1);
                return t * t * (3 - 2 * t);
            }
        }

        /// <summary>Deckkraft der Schwarzblende.</summary>
        public double Fade
        {
            get
            {
                if (Current.Fade is not { Length: 2 } f)
                {
                    return _fade;
                }
                double span = Current.FadeSeconds < 0 ? Length - Current.FadeStart : Current.FadeSeconds;
                double t = span <= 0 ? 1 : Math.Clamp((Time - Current.FadeStart) / span, 0, 1);
                return f[0] + (f[1] - f[0]) * t;
            }
        }

        public List<SequenceSound> Advance(double dt)
        {
            var due = new List<SequenceSound>();
            if (Done)
            {
                return due;
            }
            Time += dt;
            var sounds = Current.Sounds;
            for (int i = 0; i < sounds.Count; i++)
            {
                if (sounds[i].At <= Time && _fired.Add(i))
                {
                    due.Add(sounds[i]);
                }
            }
            if (!Current.Hold && Time >= Length)
            {
                NextBeat();
            }
            return due;
        }

        /// <summary>Klick oder Leertaste: erst den Text fertig zeigen, dann weiter.</summary>
        public void Press()
        {
            if (Done)
            {
                return;
            }
            if (Typing)
            {
                Time = TypeSeconds;
                return;
            }
            NextBeat();
        }

        public void Skip() => Done = true;

        private void NextBeat()
        {
            _fade = Fade;
            Beat++;
            Time = 0;
            _fired.Clear();
            Changes++;
            if (Beat >= _data.Beats.Count)
            {
                Done = true;
            }
        }
    }

    /// <summary>Welche Sequenzen beim Betreten des Einsatzes laufen.</summary>
    public static class SequenceEntry
    {
        /// <summary>
        /// Jedes neue Spiel beginnt mit Kolbs Prolog, danach Kamerafahrt und Auftrag.
        /// Fortsetzen, Neustart und nächster Einsatz zeigen nur den Übergang. forced kommt aus --seq.
        /// </summary>
        public static string[] For(bool newCampaign, string? forced) =>
            forced != null ? new[] { forced }
            : newCampaign ? new[] { "prolog", "einstieg_nach_prolog" }
            : new[] { "uebergang" };
    }

    /// <summary>Texte für die Platzhalter einer Sequenz, aus Mission und Ausgang des Einsatzes.</summary>
    public static class SequenceTexts
    {
        public const string DefaultPlace = "Messingstadt";

        private const string LostTelegram = "AN ABTEILUNG K STOP TRUPP NICHT ZURUECK STOP WACHE MELDET FESTNAHMEN STOP GLAUBE KEIN WORT STOP KRANKENSTUBE BEREIT STOP K";
        private const string AbandonedTelegram = "AN ABTEILUNG K STOP EINSATZ ABGEBROCHEN STOP ALLE RAUS STOP KOPF RUNTER BIS AUF WEITERES STOP K";
        private const string RetreatTelegram = "AN ABTEILUNG K STOP ZU HEISS STOP RUECKZUG RICHTIG STOP NEUER VERSUCH FOLGT STOP K";

        /// <summary>Ersetzt {name} durch den Text. Unbekannte Platzhalter bleiben stehen, damit Fehler auffallen.</summary>
        public static string Fill(string text, IReadOnlyDictionary<string, string> texts)
        {
            foreach (var (key, value) in texts)
            {
                text = text.Replace("{" + key + "}", value);
            }
            return text;
        }

        public static Dictionary<string, string> For(MissionData mission, World world, bool abandoned, Campaign? campaign = null)
        {
            var (head, line) = Headlines.For(world, abandoned);
            return new Dictionary<string, string>
            {
                ["title"] = mission.Title,
                ["briefing"] = mission.Briefing,
                ["place"] = mission.Place.Length > 0 ? mission.Place : DefaultPlace,
                ["time"] = mission.Time,
                ["paper"] = Headlines.Paper.ToUpperInvariant(),
                ["headline"] = head,
                ["line"] = line,
                ["telegram"] = Telegram(mission.Telegrams, world, abandoned, campaign),
            };
        }

        /// <summary>
        /// Was K. nach einem verlorenen oder abgebrochenen Einsatz telegrafiert. Ist dabei jemand zum zweiten Mal gefallen,
        /// meldet er die Toten statt der Krankenstube. Dafür muss die Kampagne den Einsatz schon verbucht haben.
        /// </summary>
        public static string Telegram(TelegramData? telegrams, World world, bool abandoned, Campaign? campaign = null)
        {
            int deaths = campaign?.LastDeaths.Count ?? 0;
            if (deaths > 0 && (!abandoned || Campaign.AbandonCostsSquad(world)))
            {
                return deaths == 1 ? CampaignTexts.TelegramOneDead : CampaignTexts.TelegramManyDead;
            }
            if (!abandoned)
            {
                return telegrams?.Lost ?? LostTelegram;
            }
            return Campaign.AbandonCostsSquad(world) ? telegrams?.Abandoned ?? AbandonedTelegram : telegrams?.Retreat ?? RetreatTelegram;
        }
    }
}
