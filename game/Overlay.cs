using System;
using System.Linq;
using System.Collections.Generic;
using Godot;
using Messingstadt.Core;

namespace Messingstadt
{
    /// <summary>Zeichnet Auswahlringe, Lebensbalken, Schüsse und die Ausgangszone über die Welt.</summary>
    public partial class Overlay : Node2D
    {
        private const float LabelWidth = 11f;
        private const float LabelHeight = 14f;
        private const float DogHeadDrop = 22f;
        private static readonly Vector2 LeashHand = new(0f, -20f);
        private static readonly Vector2 LeashCollar = new(0f, -13f);
        private static readonly Color LeashColor = new(0.2f, 0.13f, 0.08f);

        public Main Game = null!;

        public override void _Process(double delta) => QueueRedraw();

        public override void _Draw()
        {
            var world = Game.World;
            DrawExit(world);
            DrawPhone(world);
            if (!world.ItemTaken && !world.IsRescue)
            {
                // Pulsierende Markierung: Das Missionsziel bleibt sichtbar, auch wenn etwas davorsteht.
                var item = Game.ToScreen(world.ItemX, world.ItemY);
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.GetTicksMsec() / 200f);
                DrawIsoRing(item, new Color(1f, 0.85f, 0.3f, 0.4f + 0.6f * pulse));
                if (world.ItemProgress > 0)
                {
                    float f = (float)world.ItemProgress / World.PickupTicks;
                    DrawRect(new Rect2(item + new Vector2(-14, 10), new Vector2(28, 4)), new Color(0, 0, 0, 0.7f));
                    DrawRect(new Rect2(item + new Vector2(-13, 11), new Vector2(26 * f, 2)), new Color(1f, 0.85f, 0.3f));
                }
            }
            DrawDoors(world);
            if (Game.PlanHeld)
            {
                DrawPreview();
            }
            if (Game.MarkerTime > 0)
            {
                float size = 6 + 10 * Game.MarkerTime;
                var c = new Color(0.5f, 1f, 0.6f, Mathf.Min(1f, Game.MarkerTime * 2));
                DrawPolyline(new[]
                {
                    Game.MarkerPos + new Vector2(0, -size / 2), Game.MarkerPos + new Vector2(size, 0),
                    Game.MarkerPos + new Vector2(0, size / 2), Game.MarkerPos + new Vector2(-size, 0),
                    Game.MarkerPos + new Vector2(0, -size / 2),
                }, c, 1f);
            }
            // Stehen Agenten eng beieinander, rücken Nummer und Balken zur Seite, damit jede Nummer lesbar bleibt.
            var labels = new List<Vector2>();
            foreach (var u in world.Units)
            {
                if (!u.Alive || world.HiddenInCell(u))
                {
                    continue;
                }
                var p = Game.UnitPos(u);
                if (u.Faction == Faction.Agent)
                {
                    // Der innere Ring gehört immer zum Agenten, der äußere weiße Ring zeigt die Auswahl.
                    // Steht der Agent hinter einem Haus oder der Bahn, wird der Ring blass. Sonst sieht es aus, als stünde er auf dem Dach.
                    float ring = Game.IsFootHiddenByThing(u) ? 0.35f : 1f;
                    DrawIsoRing(p, new Color(Main.AgentColor, ring), 11f);
                    if (Game.Selected.Contains(u.Id))
                    {
                        DrawIsoRing(p, new Color(Colors.White, ring), 14f);
                    }
                    while (labels.Exists(l => Mathf.Abs(l.X - p.X) < LabelWidth && Mathf.Abs(l.Y - p.Y) < LabelHeight))
                    {
                        p.X += LabelWidth;
                    }
                    labels.Add(p);
                    if (Game.WaitingIds.Contains(u.Id))
                    {
                        DrawFieldText(ThemeDB.FallbackFont, p + new Vector2(-14, -58), $"wartet {Game.WaitSeconds} s", 8, Colors.White);
                    }
                    // Der Schatten hält die Nummer auch im Laternenlicht lesbar.
                    string number = (u.Id + 1).ToString();
                    DrawString(ThemeDB.FallbackFont, p + new Vector2(-2, -49), number, HorizontalAlignment.Left, -1, 10, Colors.Black);
                    DrawString(ThemeDB.FallbackFont, p + new Vector2(-3, -50), number, HorizontalAlignment.Left, -1, 10, Main.AgentColor);
                }
                // Wachen ohne Sichtkontakt verraten ihre Position nicht über den Lebensbalken.
                // Steht eine Wache im Bild hinter einem Haus, soll der Balken nicht auf dem Dach schweben.
                if (u.Faction == Faction.Agent || (world.IsSpottedByAgents(u) && !Game.IsHiddenByThing(u)))
                {
                    if (u.LeashedTo >= 0)
                    {
                        DrawLine(Game.UnitPos(world.Units[u.LeashedTo]) + LeashHand, Game.UnitPos(u) + LeashCollar, LeashColor, 1f);
                    }
                    // Der Hund ist kleiner, sein Balken sitzt tiefer.
                    if (u.Type.Bites)
                    {
                        p.Y += DogHeadDrop;
                    }
                    DrawBar(p + new Vector2(-10, -46), u);
                    if (u.Faction == Faction.Agent)
                    {
                        // Blau: Puste zum Rennen. Heller, solange der Agent gerade rennt.
                        float breath = (float)u.Stamina / World.MaxStamina(u);
                        var color = u.Sprinting ? new Color(0.6f, 0.9f, 1f) : new Color(0.35f, 0.6f, 0.95f);
                        DrawRect(new Rect2(p + new Vector2(-10, -43), new Vector2(20, 2)), new Color(0, 0, 0, 0.7f));
                        DrawRect(new Rect2(p + new Vector2(-9, -43), new Vector2(18 * breath, 1)), color);
                    }
                    if (u.Bribed)
                    {
                        DrawRect(new Rect2(p + new Vector2(-2, -54), new Vector2(4, 6)), new Color(0.35f, 0.95f, 0.45f));
                    }
                    else if (u.Faction == Faction.Guard && u.GuardState != GuardState.Patrol)
                    {
                        // Gelb: bemerkt etwas. Rot: kämpft. Orange: sucht.
                        var mark = u.GuardState switch
                        {
                            GuardState.Noticing => new Color(1f, 0.9f, 0.3f),
                            GuardState.Combat => new Color(0.95f, 0.25f, 0.2f),
                            _ => new Color(1f, 0.6f, 0.2f),
                        };
                        DrawRect(new Rect2(p + new Vector2(-2, -54), new Vector2(4, 6)), mark);
                    }
                }
                if (u.TargetId >= 0 && u.Faction == Faction.Agent && Game.Selected.Contains(u.Id))
                {
                    DrawIsoRing(Game.UnitPos(world.Units[u.TargetId]), new Color(1f, 0.3f, 0.25f));
                }
                if (u.CarriesItem)
                {
                    DrawRect(new Rect2(p + new Vector2(-3, -53), new Vector2(6, 5)), new Color(0.9f, 0.3f, 0.3f));
                    if (world.EscapeProgress > 0)
                    {
                        float f = (float)world.EscapeProgress / World.EscapeTicks;
                        DrawRect(new Rect2(p + new Vector2(-14, 10), new Vector2(28, 4)), new Color(0, 0, 0, 0.7f));
                        DrawRect(new Rect2(p + new Vector2(-13, 11), new Vector2(26 * f, 2)), new Color(0.4f, 1f, 0.5f));
                    }
                }
            }
            DrawHoveredGuard();
            foreach (var s in world.Shots)
            {
                int age = world.Tick - s.Tick;
                // Nach dem Einsatz steht die Zeit still, sonst bliebe der letzte Schuss stehen.
                if (age > 3 || Game.Finished)
                {
                    continue;
                }
                DrawShot(s, age);
            }
        }

        /// <summary>
        /// Zeichen am Telefon der Wache: ein Apparat, der zeigt, wie es um den Anruf steht. Ruhig blass, auf dem Weg zum Apparat
        /// blinkt er gelb, beim Wählen rot mit einem Balken, der sich füllt, nach dem Anruf steht er rot, ohne Leitung ist er durchgestrichen.
        /// </summary>
        private void DrawPhone(World world)
        {
            if (world.PhoneSpot is not { } spot)
            {
                return;
            }
            var at = Game.ToScreen(spot.X, spot.Y) + new Vector2(0f, -44f);
            var state = world.Phone;
            bool dead = state == PhoneState.Silent || world.WireCut;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.GetTicksMsec() / 120f);
            var color = dead ? new Color(0.62f, 0.60f, 0.56f, 0.9f)
                : state == PhoneState.Fetching ? new Color(1f, 0.82f, 0.3f, 0.55f + 0.45f * pulse)
                : state == PhoneState.Dialing ? new Color(0.95f, 0.25f, 0.2f, 0.55f + 0.45f * pulse)
                : state == PhoneState.Called ? new Color(0.95f, 0.25f, 0.2f)
                : new Color(0.87f, 0.82f, 0.69f, 0.6f);
            var dark = new Color(0f, 0f, 0f, 0.75f);
            // Apparat: Sockel, Wählscheibe, Hörer quer darüber.
            DrawRect(new Rect2(at + new Vector2(-8f, -3f), new Vector2(16f, 10f)), dark);
            DrawRect(new Rect2(at + new Vector2(-6f, -1f), new Vector2(12f, 7f)), color);
            DrawCircle(at + new Vector2(0f, 2.5f), 2.2f, dark);
            DrawRect(new Rect2(at + new Vector2(-10f, -8f), new Vector2(20f, 5f)), dark);
            DrawRect(new Rect2(at + new Vector2(-9f, -7f), new Vector2(18f, 3f)), color);
            DrawRect(new Rect2(at + new Vector2(-9f, -7f), new Vector2(4f, 5f)), color);
            DrawRect(new Rect2(at + new Vector2(5f, -7f), new Vector2(4f, 5f)), color);
            if (dead)
            {
                DrawLine(at + new Vector2(-11f, 8f), at + new Vector2(11f, -9f), new Color(0.9f, 0.2f, 0.15f), 2f);
            }
            if (world.TicksUntilCall >= 0)
            {
                float f = 1f - Mathf.Clamp((float)world.TicksUntilCall / World.PhoneTicks, 0f, 1f);
                DrawRect(new Rect2(at + new Vector2(-14f, 10f), new Vector2(28f, 4f)), dark);
                DrawRect(new Rect2(at + new Vector2(-13f, 11f), new Vector2(26f * f, 2f)), new Color(0.95f, 0.25f, 0.2f));
            }
        }

        /// <summary>Unter dem Mauszeiger steht, was das für ein Gegner ist.</summary>
        private void DrawHoveredGuard()
        {
            GuardLineLeft = GuardLineRight = -1f;
            if (Game.HoveredGuard() is not { } guard)
            {
                return;
            }
            string text = Game.GuardLine(guard);
            var font = PaperUi.Typewriter;
            // Breiter als das Spielfeld: an einem Komma in zwei Zeilen teilen.
            string[] rows = { text };
            float width = font.GetStringSize(text, HorizontalAlignment.Left, -1, 9).X;
            if (width > FieldRoom && text.Contains(", "))
            {
                int cut = text.LastIndexOf(", ", Math.Max(2, text.Length / 2), StringComparison.Ordinal);
                cut = cut > 0 ? cut : text.IndexOf(", ", StringComparison.Ordinal);
                rows = new[] { text[..(cut + 1)], text[(cut + 2)..] };
                width = Math.Max(font.GetStringSize(rows[0], HorizontalAlignment.Left, -1, 9).X, font.GetStringSize(rows[1], HorizontalAlignment.Left, -1, 9).X);
            }
            float line = font.GetHeight(9);
            var at = KeepInField(Game.UnitPos(guard) + new Vector2(-width / 2f, 14f), width, rows.Length * line);
            GuardLineLeft = (GetCanvasTransform() * at).X;
            GuardLineRight = (GetCanvasTransform() * (at + new Vector2(width, 0f))).X;
            for (int i = 0; i < rows.Length; i++)
            {
                var rowAt = at + new Vector2(0f, i * line);
                DrawStringOutline(font, rowAt, rows[i], HorizontalAlignment.Left, -1, 9, 3, new Color(0f, 0f, 0f, 0.85f));
                DrawString(font, rowAt, rows[i], HorizontalAlignment.Left, -1, 9, new Color(1f, 0.85f, 0.6f));
            }
        }

        // Abstand der Einblendungen zu Spalte, Rand, Telegramm und Hilfezeile, in Bildpunkten der Oberfläche.
        private const float FieldMargin = 8f, FieldTop = 70f, FieldBottom = 48f;

        /// <summary>Für den Prüflauf: linker und rechter Rand der Zeile zur Wache, in Bildpunkten der Oberfläche. -1 ohne Zeile.</summary>
        public float GuardLineLeft { get; private set; } = -1f;
        public float GuardLineRight { get; private set; } = -1f;

        // Sichtbares Spielfeld in Weltpunkten: rechts der Spalte, unter dem Telegramm, über der Hilfezeile.
        private Rect2 Field
        {
            get
            {
                var view = GetCanvasTransform().AffineInverse() * GetViewportRect();
                float scale = view.Size.X / GetViewportRect().Size.X;
                var from = view.Position + new Vector2(Game.HudColumn + FieldMargin, FieldTop) * scale;
                var to = view.End - new Vector2(FieldMargin, FieldBottom) * scale;
                return new Rect2(from, to - from);
            }
        }

        private float FieldRoom => Field.Size.X;

        /// <summary>
        /// Hält eine Einblendung im Spielfeld: nie unter der Spalte links, nie über den rechten Rand, nicht unter Telegramm und Hilfezeile.
        /// at ist die Grundlinie der ersten Zeile, height die Höhe aller Zeilen.
        /// </summary>
        private Vector2 KeepInField(Vector2 at, float width, float height)
        {
            var field = Field;
            at.X = Mathf.Clamp(at.X, field.Position.X, Mathf.Max(field.Position.X, field.End.X - width));
            at.Y = Mathf.Clamp(at.Y, field.Position.Y + height, Mathf.Max(field.Position.Y + height, field.End.Y));
            return at;
        }

        /// <summary>Kurzer Text an einer Stelle der Welt, im Spielfeld gehalten.</summary>
        private void DrawFieldText(Font font, Vector2 at, string text, int size, Color color, bool shadow = false)
        {
            at = KeepInField(at, font.GetStringSize(text, HorizontalAlignment.Left, -1, size).X, font.GetHeight(size));
            if (shadow)
            {
                DrawString(font, at + new Vector2(1, 1), text, HorizontalAlignment.Left, -1, size, Colors.Black);
            }
            DrawString(font, at, text, HorizontalAlignment.Left, -1, size, color);
        }

        /// <summary>Zeigt im Lageplan den Weg zum Mauszeiger: grün bis zum ersten Feld, auf dem man bemerkt würde.</summary>
        private void DrawPreview()
        {
            var path = Game.PreviewPath;
            for (int i = 0; i < path.Count; i++)
            {
                bool risky = Game.PreviewRisk >= 0 && i >= Game.PreviewRisk;
                DrawCircle(Game.TileCenter(path[i].X, path[i].Y), 2f, risky ? new Color(0.95f, 0.3f, 0.25f) : new Color(0.45f, 1f, 0.55f));
            }
            if (Game.PreviewText.Length > 0)
            {
                var at = GetGlobalMousePosition() + new Vector2(10, -6);
                DrawFieldText(ThemeDB.FallbackFont, at, Game.PreviewText, 9, Colors.White, true);
            }
        }

        /// <summary>Das Schloss sitzt auf der gemalten Tür. Das andere Ende des Durchgangs liegt hinter dem Haus.</summary>
        private void DrawDoors(World w)
        {
            var green = new Color(0.4f, 1f, 0.5f);
            var brass = new Color(0.95f, 0.75f, 0.25f);
            foreach (var door in w.Doors)
            {
                var p = Game.DoorMark(door);
                if (door.Open)
                {
                    DrawRect(new Rect2(p + new Vector2(-3, -3), new Vector2(6, 5)), green);
                    DrawArc(p + new Vector2(3, -3), 2.5f, Mathf.Pi, Mathf.Tau, 6, green, 1f);
                    // Nur an der Tür, hinter der die Beute wirklich liegt.
                    if (w.HasLoot && !w.LootTaken && door.Tiles.Contains((w.LootX / World.TileUnits, w.LootY / World.TileUnits)))
                    {
                        DrawFieldText(ThemeDB.FallbackFont, p + new Vector2(-16, -8), $"{w.LootName} drinnen", 8, green);
                    }
                    continue;
                }
                DrawRect(new Rect2(p + new Vector2(-4, -3), new Vector2(8, 7)), brass);
                DrawArc(p + new Vector2(0, -3), 2.5f, Mathf.Pi, Mathf.Tau, 6, brass, 1f);
                DrawRect(new Rect2(p + new Vector2(-1, -1), new Vector2(2, 3)), new Color(0.15f, 0.1f, 0.05f));
                // Wer hinter dieser Tür sitzt, steht neben dem Schloss: Der Gefangene selbst ist nicht zu sehen, solange sie zu ist.
                int index = w.Doors.IndexOf(door);
                var inmate = w.Prisoners.FirstOrDefault(u => u.Alive && u.Captive && u.CellDoor == index);
                if (inmate != null)
                {
                    // Passt der Name rechts vom Schloss nicht mehr ins Spielfeld, steht er links davon.
                    string who = w.PrisonerShort(inmate);
                    float wide = ThemeDB.FallbackFont.GetStringSize(who, HorizontalAlignment.Left, -1, 8).X;
                    bool fits = p.X + 8f + wide <= Field.End.X;
                    DrawFieldText(ThemeDB.FallbackFont, p + new Vector2(fits ? 8f : -8f - wide, 4), who, 8, brass);
                }
                if (door.Progress > 0)
                {
                    float f = (float)door.Progress / World.DoorTicks;
                    DrawRect(new Rect2(p + new Vector2(-14, 7), new Vector2(28, 4)), new Color(0, 0, 0, 0.7f));
                    DrawRect(new Rect2(p + new Vector2(-13, 8), new Vector2(26 * f, 2)), brass);
                }
            }
        }

        /// <summary>Mündungsfeuer an der Waffe, Leuchtspur und Einschlag: Blut bei Treffern, Staub bei Fehlschüssen.</summary>
        private void DrawShot(ShotEvent s, int age)
        {
            var from = Game.ToScreen(s.FromX, s.FromY) + new Vector2(0, -28);
            var dir = (Game.ToScreen(s.ToX, s.ToY) - Game.ToScreen(s.FromX, s.FromY)).Normalized();
            var muzzle = from + dir * 10f;
            int hash = (s.Tick * 31 + s.ToX + s.FromY) & 0xffff;
            float missX = (hash % 21 - 10) * 1.2f, missY = hash / 7 % 13 - 2;
            var impact = Game.ToScreen(s.ToX, s.ToY) + (s.Hit ? new Vector2(0, -22) : new Vector2(missX, missY));
            if (age == 0)
            {
                DrawMuzzleFlame(muzzle, dir, hash);
            }
            else if (age == 1)
            {
                // Nur noch die Glut der ausgestoßenen Gase, kleiner und dunkler
                DrawMuzzleFlame(muzzle, dir, hash + 1, 0.55f, 0.55f);
            }
            // Der Weg der Kugel als kurzer Strich, der sich zum Ziel bewegt, statt einer Linie über die ganze Strecke.
            float from0 = age / 3f, from1 = Mathf.Min(1f, from0 + 0.3f);
            DrawLine(muzzle.Lerp(impact, from0), muzzle.Lerp(impact, from1), new Color(1f, 0.9f, 0.65f, 0.5f * (1f - from0)), 1f);
            if (age >= 1)
            {
                DrawRect(new Rect2(muzzle + dir * age * 2f + new Vector2(-1, -1 - age), new Vector2(2 + age, 2)), new Color(0.72f, 0.72f, 0.72f, 0.3f));
            }
            for (int i = 0; i < 5; i++)
            {
                int bit = (hash >> i) * (i + 3);
                var spray = new Vector2(bit % 9 - 4, bit / 3 % 7 - 4) * (0.5f + age * 0.5f);
                if (s.Hit)
                {
                    spray += dir * age * 1.5f;
                    DrawRect(new Rect2(impact + spray, new Vector2(1.5f, 1.5f)), new Color(0.62f, 0.07f, 0.06f));
                }
                else
                {
                    DrawRect(new Rect2(impact + new Vector2(spray.X, -Mathf.Abs(spray.Y)), new Vector2(1.5f, 1.5f)), new Color(0.6f, 0.58f, 0.54f, 0.85f));
                }
            }
        }

        /// <summary>Flamme aus der Mündung: ein schmaler, gezackter Stoß nach vorn mit kleinen Seitenzungen, jedes Mal etwas anders.</summary>
        private void DrawMuzzleFlame(Vector2 at, Vector2 dir, int hash, float size = 1f, float brightness = 1f)
        {
            var side = new Vector2(-dir.Y, dir.X);
            float length = (9f + hash % 5) * size;
            float width = (2f + hash / 5 % 2) * size;
            float bend = ((hash / 3 % 5) - 2) * 0.5f * size;
            var outer = new[]
            {
                at + side * width * 0.7f,
                at + dir * length * 0.35f + side * width * 1.1f,
                at + dir * length * 0.5f + side * width * 0.3f,
                at + dir * length + side * bend,
                at + dir * length * 0.55f - side * width * 0.4f,
                at + dir * length * 0.3f - side * width * 1.2f,
                at - side * width * 0.7f,
            };
            DrawColoredPolygon(outer, new Color(1f, 0.55f, 0.15f, 0.85f * brightness));
            DrawColoredPolygon(new[]
            {
                at + side * width * 0.4f, at + dir * length * 0.65f + side * bend * 0.5f, at - side * width * 0.4f,
            }, new Color(1f, 0.9f, 0.55f, brightness));
            DrawLine(at, at + dir * length * 0.4f, new Color(1f, 1f, 0.92f, brightness), 1f);
            // Seitenzungen quer zur Mündung
            float tongue = (3f + hash / 7 % 3) * size;
            DrawLine(at + dir, at + dir * 2f + side * tongue, new Color(1f, 0.65f, 0.25f, 0.8f * brightness), 1f);
            DrawLine(at + dir, at + dir * 2f - side * tongue, new Color(1f, 0.65f, 0.25f, 0.8f * brightness), 1f);
        }

        private void DrawExit(World w)
        {
            var (x0, y0, x1, y1) = w.Exit;
            var pts = new[] { Game.ToScreen(x0, y0), Game.ToScreen(x1, y0), Game.ToScreen(x1, y1), Game.ToScreen(x0, y1), Game.ToScreen(x0, y0) };
            DrawPolyline(pts, new Color(0.4f, 1f, 0.5f, w.ItemTaken ? 1f : 0.35f), 1f);
        }

        private void DrawIsoRing(Vector2 p, Color c, float radius = 13f)
        {
            var pts = new Vector2[17];
            for (int i = 0; i <= 16; i++)
            {
                float a = i / 16f * Mathf.Tau;
                pts[i] = p + new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius / 2f);
            }
            DrawPolyline(pts, c, 1f);
        }

        private void DrawBar(Vector2 p, Unit u)
        {
            DrawRect(new Rect2(p, new Vector2(20, 3)), new Color(0, 0, 0, 0.7f));
            float f = (float)u.Hp / u.MaxHp;
            var c = u.Faction == Faction.Agent ? new Color(0.4f, 0.9f, 0.45f) : new Color(0.9f, 0.35f, 0.3f);
            DrawRect(new Rect2(p + new Vector2(1, 1), new Vector2(18 * f, 1)), c);
        }
    }
}
