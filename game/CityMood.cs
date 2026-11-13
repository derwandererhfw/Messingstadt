using System;
using Godot;
using Messingstadt.Core;

namespace Messingstadt
{
    /// <summary>
    /// Wie die Stadt zum Trupp steht. Die eine Stelle, an der die Kulisse das fragt: Kerzen in Helferfenstern,
    /// die Zahl der brennenden Fenster, geschlossene Läden und die Passanten hängen alle an dieser Stufe.
    /// Heute folgt sie dem Blut des letzten zählenden Einsatzes im Viertel. Entscheidet der Nutzer sich für eine
    /// Blutschuld der ganzen Stadt, wird nur Of umgestellt.
    /// Prüfläufe: --stimmung=hilft|schweigt|gegen setzt die Stufe, --hilfe=witwe,wirtin nennt die Häuser, die helfen
    /// (leer: keines).
    /// </summary>
    public static class CityMood
    {
        public enum Stance
        {
            /// <summary>Kein Blut im Viertel: Fenster brennen, Helfer stellen Kerzen auf, Passanten sind unterwegs.</summary>
            Helps,

            /// <summary>Es floss Blut: Die Straße wird stiller, Läden gehen zu.</summary>
            Silent,

            /// <summary>Eine blutige Nacht: Die Fenster sind dunkel, niemand geht mehr hinaus.</summary>
            Against,
        }

        private static string Arg(string name)
        {
            foreach (string a in OS.GetCmdlineUserArgs())
            {
                if (a.StartsWith(name))
                {
                    return a[name.Length..];
                }
            }
            return "\0";
        }

        private static string District(Main game) => game.Campaign.Selected(game.City)?.Id ?? "";

        public static Stance Of(Main game)
        {
            switch (Arg("--stimmung="))
            {
                case "hilft":
                    return Stance.Helps;
                case "schweigt":
                    return Stance.Silent;
                case "gegen":
                    return Stance.Against;
            }
            return game.Campaign.Stance switch
            {
                CityStance.Helps => Stance.Helps,
                CityStance.Silent => Stance.Silent,
                _ => Stance.Against,
            };
        }

        /// <summary>Hilft dieses Haus dem Trupp? Die Kennungen und die Bedingung gehören der Kampagne.</summary>
        public static bool HouseHelps(Main game, string house)
        {
            string forced = Arg("--hilfe=");
            if (forced != "\0")
            {
                return Array.IndexOf(forced.Split(','), house) >= 0;
            }
            return game.Campaign.HouseHelps(house, District(game));
        }

        /// <summary>Welcher Anteil der Fenster überhaupt brennt.</summary>
        public static float LitShare(Stance stance) => stance switch { Stance.Helps => 1f, Stance.Silent => 0.6f, _ => 0.3f };

        /// <summary>Welcher Anteil der Passanten sich noch auf die Straße traut.</summary>
        public static float StreetShare(Stance stance) => stance switch { Stance.Helps => 1f, Stance.Silent => 0.5f, _ => 0f };
    }
}
