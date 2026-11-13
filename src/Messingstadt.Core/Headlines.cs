using System.Linq;

namespace Messingstadt.Core
{
    /// <summary>
    /// Was am nächsten Morgen in der Zeitung steht. Die Stadt reagiert darauf, wie der Einsatz lief.
    /// Die Texte kommen aus der Mission (siehe docs/story.md), hier werden nur die Platzhalter gefüllt:
    /// {item} Name des Ziels, {dead} tote Wachen, {fallen} verwundete Agenten, {loot} fehlende Beute, {paid} bestochene Wachen,
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
            // Hunde zählen nicht als Wachleute, Laufburschen werden eigens genannt.
            // Niedergeschlagene liegen am Boden, sind aber nicht tot.
            int deadGuards = world.Units.Count(u => u.Faction == Faction.Guard && !u.Type.Bites && !u.Type.Reports && World.FateOf(u) == GuardFate.Dead);
            int deadBoys = world.Units.Count(u => u.Faction == Faction.Guard && u.Type.Reports && World.FateOf(u) == GuardFate.Dead);
            int fallen = world.Units.Count(u => u.Faction == Faction.Agent && !u.Alive);
            bool paid = world.Units.Any(u => u.Faction == Faction.Guard && u.Bribed);
            string loot = world.LootTaken ? " " + (world.Press?.Loot ?? "Gestohlen wurde außerdem: {lootName}.").Replace("{lootName}", world.LootName) : "";
            return text
                .Replace("{item}", world.ItemName)
                .Replace("{dead}", Dead(deadGuards, deadBoys))
                .Replace("{fallen}", Fallen(fallen))
                .Replace("{loot}", loot)
                .Replace("{prisoners}", world.Prisoners.Any(p => !p.Alive) ? " Einer der Gefangenen kam dabei ums Leben." : "")
                .Replace("{paid}", paid ? " Die Stadtwache schweigt auffällig." : "")
                .Trim();
        }

        private static string Dead(int guards, int boys)
        {
            string men = guards switch
            {
                0 => boys == 0 ? " Verletzt wurde niemand." : "",
                1 => " Ein Wachmann kam ums Leben.",
                _ => $" {guards} Wachleute kamen ums Leben.",
            };
            string young = (boys, guards > 0) switch
            {
                (0, _) => "",
                (1, true) => " Auch ein Laufbursche, vierzehn Jahre alt, ist unter den Toten.",
                (1, false) => " Ein Laufbursche, vierzehn Jahre alt, ist unter den Toten.",
                (_, true) => $" Auch {boys} Laufburschen sind unter den Toten.",
                _ => $" {boys} Laufburschen sind unter den Toten.",
            };
            return men + young;
        }

        // Gefallene Agenten sind schwer verwundet und kommen zurück, die Zeitung findet nur Blut.
        private static string Fallen(int agents) => agents switch
        {
            0 => "",
            1 => " Blutspuren führen bis zur nächsten Kreuzung.",
            _ => " Die Blutspuren mehrerer Verletzter verlieren sich im Regen.",
        };
    }
}
