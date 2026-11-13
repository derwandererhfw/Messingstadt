using System.Linq;

namespace Messingstadt.Core
{
    /// <summary>
    /// Was am nächsten Morgen in der Zeitung steht. Die Stadt reagiert darauf, wie der Einsatz lief.
    /// Die Texte kommen aus der Mission (siehe docs/story.md), hier werden nur die Platzhalter gefüllt:
    /// {item} Name des Ziels, {dead} tote Wachen mit Namen, wo die Mission einen nennt, {fallen} verwundete Agenten, {loot} fehlende Beute, {paid} bestochene Wachen,
    /// {prisoners} ein toter Gefangener beim Befreien.
    /// Platzhalter außer {item} werden zu einem Satz mit führendem Leerzeichen oder zu nichts.
    /// </summary>
    public static class Headlines
    {
        public const string Paper = "Messingstädter Anzeiger";

        private static readonly HeadlineText Unseen = new() { Head = "Einbruch, den niemand bemerkte",
            Line = "Erst am Morgen fiel auf, was fehlt: {item}. Gesehen haben will niemand etwas.{loot}{paid}" };
        private static readonly HeadlineText Quiet = new() { Head = "Einbruch bei Nacht: Wache sah Schatten",
            Line = "Ein Wachmann will Gestalten gesehen haben, gefasst wurde niemand. Es fehlt: {item}.{loot}{paid}" };
        private static readonly HeadlineText Loud = new() { Head = "Blutige Nacht in Messingstadt",
            Line = "Schüsse hallten durch die Straßen.{dead} Die Täter entkamen.{fallen}{loot}{paid}" };
        private static readonly HeadlineText Lost = new() { Head = "Roter Hammer zerschlagen?",
            Line = "Die Stadtwache meldet einen vereitelten Überfall und mehrere Festnahmen. Namen nennt sie nicht. Die Gewerkschaft schweigt.{dead}" };
        private static readonly HeadlineText Abandoned = new() { Head = "Schießerei in der Nacht, Täter flüchtig",
            Line = "Bewaffnete lieferten sich ein Gefecht mit der Stadtwache und zogen sich ohne Beute zurück.{dead}" };
        private static readonly HeadlineText Retreat = new() { Head = "Unruhe in der Nacht",
            Line = "Anwohner meldeten Gestalten auf der Straße. Als die Wache kam, war niemand mehr da." };

        public static (string Head, string Line) For(World world, bool abandoned)
        {
            var press = world.Press;
            HeadlineText text;
            if (abandoned)
            {
                text = Campaign.AbandonCostsSquad(world) ? press?.Abandoned ?? Abandoned : press?.Retreat ?? Retreat;
            }
            else if (world.State != MissionState.Won)
            {
                text = press?.Lost ?? Lost;
            }
            else
            {
                text = world.Rating switch
                {
                    MissionRating.Unseen => press?.Unseen ?? Unseen,
                    MissionRating.Quiet => press?.Quiet ?? Quiet,
                    _ => press?.Loud ?? Loud,
                };
            }
            return (Fill(world, text.Head), Fill(world, text.Line));
        }

        private static string Fill(World world, string text)
        {
            int fallen = world.Units.Count(u => u.Faction == Faction.Agent && !u.Alive);
            bool paid = world.Units.Any(u => u.Faction == Faction.Guard && u.Bribed);
            string loot = world.LootTaken ? " " + (world.Press?.Loot ?? "Gestohlen wurde außerdem: {lootName}.").Replace("{lootName}", world.LootName) : "";
            return text
                .Replace("{item}", world.ItemName)
                .Replace("{dead}", Dead(world))
                .Replace("{fallen}", Fallen(fallen))
                .Replace("{loot}", loot)
                .Replace("{prisoners}", world.Prisoners.Any(p => !p.Alive) ? " Einer der Gefangenen kam dabei ums Leben." : "")
                .Replace("{paid}", paid ? " Die Stadtwache schweigt auffällig." : "")
                .Trim();
        }

        /// <summary>Wie viele Tote die Zeitung höchstens mit Namen nennt. Der Rest fällt in die Zahl.</summary>
        public const int NamedDead = 3;

        /// <summary>
        /// Die Toten der Wache. Wer in der Mission einen Namen hat, steht mit Anrede und Namen in der Zeitung,
        /// der Ranghöchste vorn. Wer nur einen Posten hat, fällt in die Zahl. Hunde zählen nicht,
        /// Niedergeschlagene sind nicht tot, der Werkschutz und die Jungen bekommen einen eigenen Satz.
        /// </summary>
        private static string Dead(World world)
        {
            var dead = world.Units
                .Where(u => u.Faction == Faction.Guard && !u.Type.Bites && World.FateOf(u) == GuardFate.Dead)
                .ToList();
            var boys = dead.Where(u => u.Type.Reports).ToList();
            var men = dead.Where(u => !u.Type.Reports).ToList();
            if (dead.Count == 0)
            {
                return " Verletzt wurde niemand.";
            }

            var works = men.Where(u => u.Type == GuardType.Werkschutz && u.Person.Length > 0).ToList();
            var named = men.Where(u => u.Person.Length > 0 && !works.Contains(u))
                .OrderByDescending(Standing)
                .ToList();
            var shown = named.Take(NamedDead).ToList();
            int others = men.Count - works.Count - shown.Count;
            string text = Men(shown, others);
            if (works.Count > 0)
            {
                text += $" Vom Werkschutz {(works.Count == 1 ? "starb" : "starben")} {Join(works.Select(u => u.Person).ToList())}.";
            }

            bool also = men.Count > 0;
            foreach (var boy in boys.Where(u => u.Person.Length > 0))
            {
                string age = boy.Age > 0 ? $", {Word(boy.Age)} Jahre alt," : "";
                text += $" {(also ? "Auch der" : "Der")} {Title(boy)} {boy.Person}{age} ist tot.";
                also = true;
            }
            int nameless = boys.Count(u => u.Person.Length == 0);
            text += (nameless, also) switch
            {
                (0, _) => "",
                (1, true) => " Auch ein Laufbursche, vierzehn Jahre alt, ist unter den Toten.",
                (1, false) => " Ein Laufbursche, vierzehn Jahre alt, ist unter den Toten.",
                (_, true) => $" Auch {nameless} Laufburschen sind unter den Toten.",
                _ => $" {nameless} Laufburschen sind unter den Toten.",
            };
            return text;
        }

        private static string Men(System.Collections.Generic.List<Unit> shown, int others)
        {
            if (shown.Count == 0)
            {
                return others switch
                {
                    0 => "",
                    1 => " Ein Wachmann kam ums Leben.",
                    _ => $" {others} Wachleute kamen ums Leben.",
                };
            }
            bool plain = shown.All(u => Title(u) == "Wachmann");
            var names = plain && shown.Count > 1
                ? shown.Select(u => u.Person).ToList()
                : shown.Select(u => $"{Title(u)} {u.Person}").ToList();
            string lead = plain && shown.Count > 1 ? "Die Wachleute " : "";
            if (others > 0)
            {
                string rest = others == 1 ? "ein weiterer" : $"{Word(others)} weitere";
                return $" {lead}{string.Join(", ", names)} und {rest} kamen ums Leben.";
            }
            return $" {lead}{Join(names)} {(shown.Count == 1 ? "kam" : "kamen")} ums Leben.";
        }

        private static string Title(Unit u) => LedgerEntry.TitleFor(LedgerSide.Guard, u.Type.Id, u.Rank);

        // Wer eine eigene Anrede hat oder Offizier ist, steht vorn.
        private static int Standing(Unit u) => (u.Rank.Length > 0 ? 2 : 0) + (u.Type == GuardType.Offizier ? 1 : 0);

        private static string Join(System.Collections.Generic.List<string> names) => names.Count switch
        {
            0 => "",
            1 => names[0],
            _ => string.Join(", ", names.Take(names.Count - 1)) + " und " + names[^1],
        };

        private static readonly string[] Words =
        {
            "null", "ein", "zwei", "drei", "vier", "fünf", "sechs", "sieben", "acht", "neun", "zehn",
            "elf", "zwölf", "dreizehn", "vierzehn", "fünfzehn", "sechzehn", "siebzehn", "achtzehn", "neunzehn", "zwanzig",
        };

        private static string Word(int n) => n >= 0 && n < Words.Length ? Words[n] : n.ToString();

        // Gefallene Agenten sind schwer verwundet und kommen zurück, die Zeitung findet nur Blut.
        private static string Fallen(int agents) => agents switch
        {
            0 => "",
            1 => " Blutspuren führen bis zur nächsten Kreuzung.",
            _ => " Die Blutspuren mehrerer Verletzter verlieren sich im Regen.",
        };
    }
}
