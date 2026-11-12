using System;
using Godot;

namespace Messingstadt
{
    /// <summary>
    /// Prüfschalter für alles, was die Luft sichtbar macht. Damit lässt sich messen, wie viel jede Schicht die Nacht
    /// aufhellt, und Stufen lassen sich nebeneinander stellen, ohne den Code umzubauen.
    /// --luft=0.4 schwächt Dunstschleier, Bodennebel und den Dunst am Kartenrand auf diesen Anteil.
    /// --ohne=dunst,nebel,rand,regen,glanz,kegel schaltet einzelne Schichten ganz ab.
    /// --licht zeigt die Vorschau der neuen Lichteffekte: Halo um Laternen, Regen im Licht, Mündungsfeuer.
    /// Ohne Schalter bleibt alles, wie es ist.
    /// </summary>
    public static class AirTest
    {
        private static bool s_read;
        private static float s_scale = 1f;
        private static string[] s_off = Array.Empty<string>();
        private static bool s_light;

        /// <summary>Anteil, auf den Dunst, Nebel und Randdunst geschwächt werden. 1 ist der Stand ohne Schalter.</summary>
        public static float Scale
        {
            get
            {
                Read();
                return s_scale;
            }
        }

        /// <summary>Ob die Vorschau der neuen Lichteffekte läuft.</summary>
        public static bool Light
        {
            get
            {
                Read();
                return s_light;
            }
        }

        public static bool Off(string layer)
        {
            Read();
            return Array.IndexOf(s_off, layer) >= 0;
        }

        private static void Read()
        {
            if (s_read)
            {
                return;
            }
            s_read = true;
            foreach (string a in OS.GetCmdlineUserArgs())
            {
                if (a.StartsWith("--luft="))
                {
                    s_scale = float.Parse(a["--luft=".Length..], System.Globalization.CultureInfo.InvariantCulture);
                }
                else if (a.StartsWith("--ohne="))
                {
                    s_off = a["--ohne=".Length..].Split(',');
                }
                else if (a == "--licht")
                {
                    s_light = true;
                }
            }
        }
    }
}
