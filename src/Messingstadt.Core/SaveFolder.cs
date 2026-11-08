using System.Collections.Generic;
using System.IO;
using System.Linq;

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

        public Campaign Load(int slot) => Load(slot, out _);

        /// <summary>Lädt eine Akte. Ein unlesbarer oder zu neuer Stand ergibt eine neue Kampagne, siehe Campaign.Load.</summary>
        public Campaign Load(int slot, out SaveStatus status) =>
            Campaign.Load(Exists(slot) ? File.ReadAllText(PathOf(slot)) : null, out status);

        /// <summary>Wohin ein verworfener Stand beiseitegelegt wird, etwa kampagne.alt.json.</summary>
        public string AsidePathOf(int slot) => Path.ChangeExtension(PathOf(slot), ".alt.json");

        public void SetAside(int slot) => File.Copy(PathOf(slot), AsidePathOf(slot), true);

        public void Save(int slot, Campaign campaign)
        {
            string path = PathOf(slot);
            File.WriteAllText(path + TempSuffix, campaign.ToJson());
            File.Move(path + TempSuffix, path, true);
        }

        /// <summary>Eine Zeile für das Menü: Kasse und Zahl der Einsätze, oder "leer".</summary>
        public string Describe(int slot) => Exists(slot) ? Describe(Load(slot)) : "leer";

        /// <summary>
        /// Hat die Kampagne in der Akte schon etwas erlebt? Eine Akte, die nur angelegt und nie gespielt wurde, gleicht einer neuen
        /// und darf ohne Rückfrage überschrieben werden.
        /// </summary>
        public bool Begun(int slot) => Exists(slot) && Begun(Load(slot));

        public static bool Begun(Campaign campaign)
        {
            var blank = new Campaign();
            return campaign.Missions > 0 || campaign.Records.Count > 0 || campaign.Money != blank.Money || campaign.Heat != 0 || campaign.Support != 0
                || campaign.Agents.Any(a => a.Falls > 0 || a.Dead);
        }

        /// <summary>
        /// Wer im nächsten Einsatz aussetzt, je eine Zeile wie "Otto: angeschlagen, setzt aus". Krankenstube heißt einmal gefallen,
        /// angeschlagen heißt mit weniger als halben Lebenspunkten durchgekommen. Wer trotzdem mitgeht, steht in deployed und fehlt hier.
        /// </summary>
        public static List<string> Resting(Campaign campaign, ICollection<string>? deployed = null) =>
            campaign.Agents
                .Where(a => !a.Dead && a.Rest > 0 && (deployed == null || !deployed.Contains(a.Name)))
                .Select(a => $"{a.Name}: {(a.Infirmary ? "Krankenstube" : "angeschlagen")}, setzt aus")
                .ToList();

        public static string Describe(Campaign campaign) =>
            $"Kasse {campaign.Money}, {campaign.Missions} {(campaign.Missions == 1 ? "Einsatz" : "Einsätze")}";
    }
}
