using System;
using System.Collections.Generic;

namespace Messingstadt.Core
{
    public sealed partial class Unit
    {
        /// <summary>Was der Agent im Einsatz trägt, in der Reihenfolge seiner Plätze. Wachen tragen nichts, ihre Waffe steht in GuardType.</summary>
        public readonly List<CarriedItem> Items = new();

        /// <summary>Platz der Waffe in der Hand, -1 ohne Waffe.</summary>
        public int Weapon = -1;

        /// <summary>Takte bis das Magazin wieder voll ist, 0 wenn nicht nachgeladen wird.</summary>
        public int ReloadTicks;

        /// <summary>Takte, bis der Verband sitzt. Solange steht der Agent und schießt nicht, jeder Befehl bricht ab.</summary>
        public int BandageTicks;

        /// <summary>Takt des letzten Schlags im Nahkampf. Nur für die Darstellung.</summary>
        public int LastStrikeTick = -1000;

        public CarriedItem? ActiveItem => Weapon >= 0 && Weapon < Items.Count ? Items[Weapon] : null;
    }

    public sealed partial class World
    {
        /// <summary>Takte, die der Griff zur anderen Waffe kostet.</summary>
        public const int SwitchTicks = 10;

        private const int MeleeHitChance = 85;

        private readonly ItemCatalog _catalog;

        public List<SmokeCloud> Smokes { get; } = new();
        public List<StrikeEvent> Strikes { get; } = new();

        /// <summary>Werte der Waffe in der Hand. Wachen nehmen die Werte ihrer Art.</summary>
        private readonly record struct Arms(string Id, int Range, int Damage, int Cooldown, int Noise, int Accuracy, int Falloff, bool Melee);

        private static readonly Arms Unarmed = new("", 0, 0, 0, 0, 0, 0, false);

        private Arms ArmsOf(Unit u)
        {
            if (u.Faction == Faction.Guard)
            {
                return new Arms(u.Type.Id, u.Type.WeaponRange, u.Type.Damage, u.Type.Cooldown, AlertRadius, 0, HitChancePerTile, false);
            }
            var def = u.ActiveItem?.Def;
            return def == null
                ? Unarmed
                : new Arms(def.Id, def.RangeUnits, def.Damage, def.CooldownTicks, def.NoiseUnits, def.Accuracy, def.Falloff, def.Kind == ItemKind.Melee);
        }

        private int RangeOf(Unit u) => ArmsOf(u).Range;

        private static CarriedItem? ActiveWeapon(Unit u) => u.ActiveItem;

        /// <summary>Gibt dem Agenten seine Gegenstände. Unbekannte Kennungen fallen weg, die erste Waffe kommt in die Hand.</summary>
        private void Equip(Unit u, IEnumerable<string> ids)
        {
            foreach (string id in ids)
            {
                var def = _catalog.Get(id) ?? ItemCatalog.Fallback.Get(id);
                if (def != null && u.Items.Count < Campaign.ItemSlots)
                {
                    u.Items.Add(new CarriedItem(def));
                }
            }
            u.Weapon = u.Items.FindIndex(i => i.Def.IsWeapon);
        }

        /// <summary>Kennungen dessen, was die Einheit noch trägt. Verbrauchtes fehlt.</summary>
        public static List<string> ItemIds(Unit u) => u.Items.ConvertAll(i => i.Def.Id);

        public bool HasItem(Unit u, ItemKind kind) => u.Items.Exists(i => i.Def.Kind == kind);

        // ---------- Befehle ----------

        /// <summary>Nimmt die nächste Waffe in die Hand. Laufendes Nachladen bricht ab, das Magazin bleibt, wie es war.</summary>
        public bool CycleWeapon(int unitId)
        {
            var u = Units[unitId];
            if (!u.Alive || State != MissionState.Running)
            {
                return false;
            }
            for (int step = 1; step < u.Items.Count; step++)
            {
                int i = (Math.Max(u.Weapon, 0) + step) % u.Items.Count;
                if (u.Items[i].Def.IsWeapon && i != u.Weapon)
                {
                    return SelectWeapon(unitId, i);
                }
            }
            return false;
        }

        /// <summary>Nimmt die Waffe auf diesem Platz in die Hand.</summary>
        public bool SelectWeapon(int unitId, int slot)
        {
            var u = Units[unitId];
            if (!u.Alive || slot < 0 || slot >= u.Items.Count || !u.Items[slot].Def.IsWeapon || slot == u.Weapon || State != MissionState.Running)
            {
                return false;
            }
            u.Weapon = slot;
            u.ReloadTicks = 0;
            u.Cooldown = Math.Max(u.Cooldown, SwitchTicks);
            return true;
        }

        /// <summary>Wirft einen Rauchtopf auf ein Feld in Wurfweite und Sicht. Die Wolke steht sofort.</summary>
        public bool OrderThrowSmoke(int unitId, int tileX, int tileY)
        {
            var u = Units[unitId];
            int slot = u.Items.FindIndex(i => i.Def.Kind == ItemKind.Smoke);
            if (!u.Alive || slot < 0 || Map.IsBlocked(tileX, tileY) || State != MissionState.Running)
            {
                return false;
            }
            var def = u.Items[slot].Def;
            int x = tileX * TileUnits + TileUnits / 2, y = tileY * TileUnits + TileUnits / 2;
            if (DistanceTo(u, x, y) > def.RangeUnits || !Map.HasLineOfSight(u.TileX, u.TileY, tileX, tileY))
            {
                return false;
            }
            Smokes.Add(new SmokeCloud(x, y, (int)Math.Round(def.Radius * TileUnits), Tick + def.DurationTicks));
            Consume(u, slot);
            return true;
        }

        /// <summary>Legt einen Verband an. Das dauert im Stand, erst dann gibt es Lebenspunkte zurück. Unverletzt geht das nicht.</summary>
        public bool OrderUseMedkit(int unitId)
        {
            var u = Units[unitId];
            int slot = u.Items.FindIndex(i => i.Def.Kind == ItemKind.Medkit);
            if (!u.Alive || slot < 0 || u.Hp >= u.MaxHp || u.BandageTicks > 0 || State != MissionState.Running)
            {
                return false;
            }
            u.Path.Clear();
            u.TargetId = -1;
            u.DoorTarget = -1;
            u.BribeTarget = -1;
            u.Sprinting = false;
            u.BandageTicks = Math.Max(1, u.Items[slot].Def.DurationTicks);
            return true;
        }

        private void Bandage(Unit a)
        {
            a.Path.Clear();
            int slot = a.Items.FindIndex(i => i.Def.Kind == ItemKind.Medkit);
            if (slot < 0)
            {
                a.BandageTicks = 0;
            }
            else if (--a.BandageTicks == 0)
            {
                a.Hp = Math.Min(a.MaxHp, a.Hp + a.Items[slot].Def.Heal);
                Consume(a, slot);
            }
        }

        private void Consume(Unit u, int slot)
        {
            if (--u.Items[slot].Charges > 0)
            {
                return;
            }
            var weapon = u.ActiveItem;
            u.Items.RemoveAt(slot);
            u.Weapon = weapon == null ? -1 : u.Items.IndexOf(weapon);
        }

        // ---------- Regeln ----------

        private bool CanPickLocks(Unit u) => u.Skill == SkillLocksmith || HasItem(u, ItemKind.Lockpick);

        /// <summary>Fortschritt an der Tür in diesem Takt, gemessen an DoorTicks.</summary>
        private int DoorStep(Unit a)
        {
            bool pick = HasItem(a, ItemKind.Lockpick);
            if (a.Skill == SkillLocksmith)
            {
                return pick ? 2 : 1;
            }
            return Tick % 2 == 0 ? 1 : 0;
        }

        /// <summary>Lädt nach, wenn das Magazin leer ist, und füllt es, wenn die Zeit um ist.</summary>
        private void Reload(Unit u)
        {
            var item = u.ActiveItem;
            if (item == null || item.Def.Magazine <= 0)
            {
                return;
            }
            if (u.ReloadTicks > 0)
            {
                if (--u.ReloadTicks == 0)
                {
                    item.Ammo = item.Def.Magazine;
                }
            }
            else if (item.Ammo <= 0)
            {
                u.ReloadTicks = Math.Max(1, item.Def.ReloadTicks);
            }
        }

        private static void UseAmmo(Unit u)
        {
            if (u.ActiveItem is { Def.Magazine: > 0 } item)
            {
                item.Ammo--;
            }
        }

        private void Attack(Unit u, Unit target, Arms arms)
        {
            if (arms.Melee)
            {
                Strike(u, target, arms);
            }
            else
            {
                Shoot(u, target, arms);
            }
        }

        /// <summary>
        /// Schlag im Nahkampf: lautlos, ohne Schuss, der Einsatz bleibt leise. Eine Wache, die noch nicht kämpft, geht sofort zu Boden,
        /// der gepanzerte Werkschutz nicht. Wer den Schlag übersteht, kämpft.
        /// </summary>
        private void Strike(Unit u, Unit target, Arms arms)
        {
            u.Cooldown = arms.Cooldown;
            u.FacingX = target.X - u.X;
            u.FacingY = target.Y - u.Y;
            u.LastStrikeTick = Tick;
            bool knockOut = target.Faction == Faction.Guard && target.GuardState != GuardState.Combat && target.Type != GuardType.Werkschutz;
            bool hit = knockOut || NextRandom() % 100 < MeleeHitChance;
            Strikes.Add(new StrikeEvent(u.X, u.Y, target.X, target.Y, hit, knockOut, Tick, u.Id));
            if (Strikes.Count > 32)
            {
                Strikes.RemoveAt(0);
            }
            if (knockOut)
            {
                Hurt(target, target.Hp);
                return;
            }
            Provoke(u, target);
            if (hit)
            {
                Hurt(target, arms.Damage);
            }
        }

        /// <summary>Sicht zwischen zwei Feldern: Gebäude und Rauch versperren sie.</summary>
        private bool HasSight(int x0, int y0, int x1, int y1) => Map.HasLineOfSight(x0, y0, x1, y1) && !SmokeBetween(x0, y0, x1, y1);

        private bool SmokeBetween(int x0, int y0, int x1, int y1)
        {
            if (Smokes.Count == 0)
            {
                return false;
            }
            long ax = x0 * TileUnits + TileUnits / 2, ay = y0 * TileUnits + TileUnits / 2;
            long bx = x1 * TileUnits + TileUnits / 2, by = y1 * TileUnits + TileUnits / 2;
            foreach (var s in Smokes)
            {
                // Abstand der Wolkenmitte von der Strecke zwischen beiden Feldmitten.
                long dx = bx - ax, dy = by - ay, px = s.X - ax, py = s.Y - ay;
                long len = dx * dx + dy * dy;
                double t = len == 0 ? 0 : Math.Clamp((double)(px * dx + py * dy) / len, 0, 1);
                double cx = ax + t * dx - s.X, cy = ay + t * dy - s.Y;
                if (cx * cx + cy * cy < (double)s.Radius * s.Radius)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
