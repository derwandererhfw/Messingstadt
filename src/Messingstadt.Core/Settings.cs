using System;
using System.Text.Json;

namespace Messingstadt.Core
{
    /// <summary>Was der Spieler im Menü einstellt. Getrennt vom Spielstand, eine neue Kampagne ändert daran nichts.</summary>
    public sealed class Settings
    {
        public double MasterVolume { get; set; } = 1.0;
        public double MusicVolume { get; set; } = 1.0;
        public double EffectsVolume { get; set; } = 1.0;
        public bool Muted { get; set; }
        public bool Fullscreen { get; set; }
        public bool ScreenShake { get; set; } = true;

        public string ToJson() => JsonSerializer.Serialize(this);

        /// <summary>Lädt die Einstellungen. Unlesbares führt zu den Vorgaben, Werte außerhalb der Regler werden begrenzt.</summary>
        public static Settings FromJson(string json)
        {
            Settings settings;
            try
            {
                settings = JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
            }
            catch (JsonException)
            {
                return new Settings();
            }
            settings.MasterVolume = Limit(settings.MasterVolume);
            settings.MusicVolume = Limit(settings.MusicVolume);
            settings.EffectsVolume = Limit(settings.EffectsVolume);
            return settings;
        }

        private static double Limit(double volume) => double.IsNaN(volume) ? 1.0 : Math.Clamp(volume, 0.0, 1.0);
    }
}
