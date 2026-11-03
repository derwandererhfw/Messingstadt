using System.IO;

namespace Messingstadt.Core
{
    /// <summary>
    /// Drei Aktenordner für Kampagnen. Akte 1 ist die alte Datei kampagne.json, ein bestehender Spielstand bleibt so erhalten.
    /// Gespeichert wird erst in eine Nebendatei und dann umbenannt, damit ein Absturz beim Schreiben keine halbe Akte hinterlässt.
    /// </summary>
    public sealed class SaveFolder
    {
        public const int Slots = 3;
        private const string TempSuffix = ".neu";

        private readonly string _dir;

        public SaveFolder(string dir) => _dir = dir;

        public static int ClampSlot(int slot) => slot < 1 || slot > Slots ? 1 : slot;

        public string PathOf(int slot) => Path.Combine(_dir, ClampSlot(slot) == 1 ? "kampagne.json" : $"kampagne_{ClampSlot(slot)}.json");

        public bool Exists(int slot) => File.Exists(PathOf(slot));

        public Campaign Load(int slot) => Exists(slot) ? Campaign.FromJson(File.ReadAllText(PathOf(slot))) : new Campaign();

        public void Save(int slot, Campaign campaign)
        {
            string path = PathOf(slot);
            File.WriteAllText(path + TempSuffix, campaign.ToJson());
            File.Move(path + TempSuffix, path, true);
        }

        /// <summary>Eine Zeile für das Menü: Kasse und Zahl der Einsätze, oder "leer".</summary>
        public string Describe(int slot) => Exists(slot) ? Describe(Load(slot)) : "leer";

        public static string Describe(Campaign campaign) =>
            $"Kasse {campaign.Money}, {campaign.Missions} {(campaign.Missions == 1 ? "Einsatz" : "Einsätze")}";
    }
}
