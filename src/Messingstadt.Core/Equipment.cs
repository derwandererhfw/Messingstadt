using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Messingstadt.Core
{
    /// <summary>Wozu ein Gegenstand taugt. In den Daten als deutsches Wort, siehe ItemDef.KindId.</summary>
    public enum ItemKind
    {
        Firearm,
        Melee,
        Lockpick,
        Smoke,
        Medkit,
    }

    /// <summary>Ein Gegenstand aus dem Katalog, data/waffen.json. Zeiten in Sekunden, Reichweiten in Feldern.</summary>
    public sealed class ItemDef
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";

        /// <summary>Kurzer Name für die Akte im Einsatz. Ohne Angabe der Name.</summary>
        public string Short { get; set; } = "";

        [JsonIgnore]
        public string ShortName => Short.Length > 0 ? Short : Name;

        /// <summary>"schusswaffe", "nahkampf", "dietrich", "rauch" oder "verband".</summary>
        [JsonPropertyName("kind")]
        public string KindId { get; set; } = "";
        public int Price { get; set; }
        public int Damage { get; set; }

        /// <summary>Reichweite der Waffe, beim Rauchtopf die Wurfweite.</summary>
        public double Range { get; set; }

        /// <summary>Sekunden zwischen zwei Schüssen oder Schlägen.</summary>
        public double Cooldown { get; set; }

        /// <summary>In wie vielen Feldern Umkreis die Wachen den Schuss hören. 0 ist lautlos und macht den Einsatz nicht laut.</summary>
        public double Noise { get; set; }

        /// <summary>Punkte auf die Trefferchance, negativ für ungenaue Waffen.</summary>
        public int Accuracy { get; set; }

        /// <summary>Punkte weniger Trefferchance je Feld Entfernung.</summary>
        public int Falloff { get; set; }

        /// <summary>Schüsse bis zum Nachladen. 0 heißt ohne Nachladen.</summary>
        public int Magazine { get; set; }
        public double Reload { get; set; }

        /// <summary>Wie oft sich ein Verbrauchsgut benutzen lässt. Danach ist es weg.</summary>
        public int Charges { get; set; }

        /// <summary>Lebenspunkte, die das Verbandszeug zurückgibt.</summary>
        public int Heal { get; set; }

        /// <summary>Halbmesser der Rauchwolke in Feldern.</summary>
        public double Radius { get; set; }

        /// <summary>Wie lange der Rauch steht oder das Anlegen des Verbands dauert, in Sekunden.</summary>
        public double Duration { get; set; }

        /// <summary>Kurzer Text für die Waffenkammer.</summary>
        public string Text { get; set; } = "";

        [JsonIgnore]
        public ItemKind Kind => KindId switch
        {
            "nahkampf" => ItemKind.Melee,
            "dietrich" => ItemKind.Lockpick,
            "rauch" => ItemKind.Smoke,
            "verband" => ItemKind.Medkit,
            _ => ItemKind.Firearm,
        };

        [JsonIgnore]
        public bool IsWeapon => Kind is ItemKind.Firearm or ItemKind.Melee;

        [JsonIgnore]
        public int RangeUnits => (int)Math.Round(Range * World.TileUnits);

        [JsonIgnore]
        public int CooldownTicks => Seconds(Cooldown);

        [JsonIgnore]
        public int ReloadTicks => Seconds(Reload);

        [JsonIgnore]
        public int DurationTicks => Seconds(Duration);

        [JsonIgnore]
        public int NoiseUnits => (int)Math.Round(Noise * World.TileUnits);

        private static int Seconds(double s) => (int)Math.Round(s * World.TickRate);
    }

    /// <summary>Alle Gegenstände, die die Waffenkammer kennt.</summary>
    public sealed class ItemCatalog
    {
        /// <summary>Was ein Agent ohne eigenen Kauf trägt.</summary>
        public static readonly string[] StarterKit = { "pistole" };

        /// <summary>Die Pistole der ersten Fassung, ohne Nachladen. Gilt, wenn eine Mission keinen Katalog mitbringt, etwa in alten Tests.</summary>
        public static readonly ItemCatalog Fallback = new()
        {
            Items =
            {
                new ItemDef
                {
                    Id = "pistole", Name = "Pistole", KindId = "schusswaffe", Price = 100, Damage = 16, Range = 7, Cooldown = 0.7,
                    Noise = 8, Falloff = 5,
                },
            },
        };

        public List<ItemDef> Items { get; set; } = new();

        public ItemDef? Get(string id) => Items.FirstOrDefault(i => i.Id == id);

        /// <summary>Was der Verkauf zurückbringt.</summary>
        public int SellPrice(string id) => (Get(id)?.Price ?? 0) * Campaign.SellPercent / 100;
    }

    /// <summary>Ein Gegenstand in der Hand eines Agenten im Einsatz: Schüsse im Magazin, übrige Ladungen.</summary>
    public sealed class CarriedItem
    {
        public ItemDef Def = null!;
        public int Ammo;
        public int Charges;

        public CarriedItem(ItemDef def)
        {
            Def = def;
            Ammo = def.Magazine;
            Charges = def.Charges;
        }
    }

    /// <summary>Eine Rauchwolke. Durch sie hindurch sieht niemand, auch nicht hinein oder heraus.</summary>
    public readonly record struct SmokeCloud(int X, int Y, int Radius, int UntilTick);

    /// <summary>Ein Schlag im Nahkampf, nur für die Darstellung. Schüsse stehen in World.Shots.</summary>
    public readonly record struct StrikeEvent(int FromX, int FromY, int ToX, int ToY, bool Hit, bool KnockOut, int Tick, int AttackerId);
}
