using System.Linq;

namespace Messingstadt.Core
{
    /// <summary>Was am nächsten Morgen in der Zeitung steht. Die Stadt reagiert darauf, wie der Einsatz lief.</summary>
    public static class Headlines
    {
        public const string Paper = "Messingstädter Anzeiger";

        public static (string Head, string Line) For(World world, bool abandoned)
        {
            int deadGuards = world.Units.Count(u => u.Faction == Faction.Guard && !u.Alive);
            int deadAgents = world.Units.Count(u => u.Faction == Faction.Agent && !u.Alive);
            int bribed = world.Units.Count(u => u.Faction == Faction.Guard && u.Bribed);
            string loot = world.LootTaken ? $" Auch die {world.LootName} der Brandt-Werke fehlt." : "";
            string paid = bribed > 0 ? " Die Stadtwache schweigt auffällig." : "";

            if (abandoned)
            {
                return ("Schießerei in der Nacht, Täter flüchtig",
                    $"Bewaffnete lieferten sich am Mietshaus ein Gefecht mit der Stadtwache und zogen sich ohne Beute zurück. {Dead(deadGuards)}");
            }
            if (world.State != MissionState.Won)
            {
                return ("Roter Hammer zerschlagen?",
                    $"Die Stadtwache meldet einen vereitelten Überfall. Vier Bewaffnete seien gestellt worden. Die Gewerkschaft schweigt. {Dead(deadGuards)}");
            }
            return world.Rating switch
            {
                MissionRating.Unseen => ("Kontobuch der Brandt-Werke spurlos verschwunden",
                    $"Niemand will etwas bemerkt haben. In den Fabriken wird schon von Schwarzgeld getuschelt.{loot}{paid}"),
                MissionRating.Quiet => ("Einbruch bei Nacht: Wache sah Schatten",
                    $"Ein Wachmann will Gestalten in der Gasse gesehen haben, gefasst wurde niemand. Das Kontobuch der Brandt-Werke fehlt.{loot}{paid}"),
                _ => ("Blutige Nacht am Mietshaus",
                    $"Stundenlang hallten Schüsse durch die Querstraße. {Dead(deadGuards)} Der Fluchtwagen entkam Richtung Osten.{Fallen(deadAgents)}{loot}{paid}"),
            };
        }

        private static string Dead(int guards) => guards switch
        {
            0 => "Verletzt wurde niemand.",
            1 => "Ein Wachmann kam ums Leben.",
            _ => $"{guards} Wachleute kamen ums Leben.",
        };

        private static string Fallen(int agents) => agents switch
        {
            0 => "",
            1 => " Am Tatort blieb ein Toter zurück.",
            _ => $" Am Tatort blieben {agents} Tote zurück.",
        };
    }
}
