using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;

namespace Messingstadt
{
    /// <summary>
    /// Aktenbilder der Grafik für Gegenstände und Ausbauten (game/assets/akte). Welche Datei zu welchem Gegenstand gehört,
    /// steht in zuordnung.json und wird zur Laufzeit gelesen. Fehlt ein Bild, gibt es keins: Ersatz wird hier nicht gebaut,
    /// Missing nennt, was fehlt.
    /// </summary>
    public static class FilePictures
    {
        private const string Folder = "res://assets/akte/";

        private static Dictionary<string, string>? _items;
        private static readonly Dictionary<string, Dictionary<string, string>> Upgrades = new();
        private static readonly Dictionary<string, Texture2D?> Cache = new();

        /// <summary>Seitenverhältnis der Bilder, Breite zu Höhe.</summary>
        public const float Aspect = 256f / 192f;

        /// <summary>Bild eines Gegenstands nach seiner Kennung aus dem Katalog. Null, wenn die Zuordnung keins nennt oder die Datei fehlt.</summary>
        public static Texture2D? Item(string id)
        {
            Read();
            return _items!.TryGetValue(id, out string? file) ? Load(file) : null;
        }

        /// <summary>Bild einer Ausbaustufe, Beine oder Herz. Null, wenn es für die Stufe kein Bild gibt.</summary>
        public static Texture2D? Upgrade(bool legs, int level)
        {
            Read();
            return Upgrades.TryGetValue(legs ? "legs" : "heart", out var levels) && levels.TryGetValue(level.ToString(), out string? file) ? Load(file) : null;
        }

        /// <summary>Für den Prüflauf und die Meldung an die Grafik: was von den verlangten Bildern fehlt, leer wenn alles da ist.</summary>
        public static string Missing(IEnumerable<string> itemIds, int maxLevel)
        {
            var missing = itemIds.Where(id => Item(id) == null).ToList();
            foreach (bool legs in new[] { true, false })
            {
                missing.AddRange(Enumerable.Range(1, maxLevel).Where(l => Upgrade(legs, l) == null).Select(l => $"{(legs ? "legs" : "heart")} {l}"));
            }
            return string.Join(", ", missing);
        }

        private static void Read()
        {
            if (_items != null)
            {
                return;
            }
            _items = new Dictionary<string, string>();
            string path = ProjectSettings.GlobalizePath(Folder + "zuordnung.json");
            if (!File.Exists(path))
            {
                return;
            }
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.TryGetProperty("items", out var items))
            {
                foreach (var item in items.EnumerateObject())
                {
                    _items[item.Name] = item.Value.GetString() ?? "";
                }
            }
            if (doc.RootElement.TryGetProperty("upgrades", out var upgrades))
            {
                foreach (var kind in upgrades.EnumerateObject())
                {
                    Upgrades[kind.Name] = kind.Value.EnumerateObject().ToDictionary(l => l.Name, l => l.Value.GetString() ?? "");
                }
            }
        }

        private static Texture2D? Load(string file)
        {
            if (Cache.TryGetValue(file, out var cached))
            {
                return cached;
            }
            string path = ProjectSettings.GlobalizePath(Folder + file);
            Texture2D? picture = null;
            if (file.Length > 0 && File.Exists(path))
            {
                var image = Image.LoadFromFile(path);
                // Die Bilder werden stark verkleinert gezeigt, ohne Mipmaps flimmern sie.
                image.GenerateMipmaps();
                picture = ImageTexture.CreateFromImage(image);
            }
            Cache[file] = picture;
            return picture;
        }

        /// <summary>Ein Bild in fester Breite, die Höhe folgt dem Seitenverhältnis.</summary>
        public static TextureRect Rect(Texture2D picture, float width) => new()
        {
            Texture = picture, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
            CustomMinimumSize = new Vector2(width, width / Aspect), MouseFilter = Control.MouseFilterEnum.Ignore,
            TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };

        /// <summary>Kleines Bild vorn auf einem Knopf.</summary>
        public static void OnButton(Button button, string itemId, int width)
        {
            if (Item(itemId) is not { } picture)
            {
                return;
            }
            button.Icon = picture;
            button.ExpandIcon = true;
            button.AddThemeConstantOverride("icon_max_width", width);
            button.AddThemeConstantOverride("h_separation", 6);
        }
    }
}
