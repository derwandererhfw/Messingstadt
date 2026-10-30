using Godot;
using Messingstadt.Core;

namespace Messingstadt
{
    /// <summary>Zeichnet Auswahlringe, Lebensbalken, Schüsse und die Ausgangszone über die Welt.</summary>
    public partial class Overlay : Node2D
    {
        public Main Game = null!;

        public override void _Process(double delta) => QueueRedraw();

        public override void _Draw()
        {
            var world = Game.World;
            DrawExit(world);
            if (!world.ItemTaken)
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
            foreach (var u in world.Units)
            {
                if (u.Alive && u.Faction == Faction.Guard && u.GuardState == GuardState.Patrol && world.IsSpottedByAgents(u))
                {
                    DrawSightCone(world, u);
                }
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
            foreach (var u in world.Units)
            {
                if (!u.Alive)
                {
                    continue;
                }
                var p = Game.ToScreen(u.X, u.Y);
                if (Game.Selected.Contains(u.Id))
                {
                    DrawIsoRing(p, new Color(1f, 0.85f, 0.35f));
                }
                // Wachen ohne Sichtkontakt verraten ihre Position nicht über den Lebensbalken.
                if (u.Faction == Faction.Agent || world.IsSpottedByAgents(u))
                {
                    DrawBar(p + new Vector2(-10, -46), u);
                    if (u.Faction == Faction.Guard && u.GuardState != GuardState.Patrol)
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
                    DrawIsoRing(Game.ToScreen(world.Units[u.TargetId].X, world.Units[u.TargetId].Y), new Color(1f, 0.3f, 0.25f));
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
            foreach (var s in world.Shots)
            {
                int age = world.Tick - s.Tick;
                if (age > 3)
                {
                    continue;
                }
                var a = Game.ToScreen(s.FromX, s.FromY) + new Vector2(0, -22);
                var b = Game.ToScreen(s.ToX, s.ToY) + new Vector2(0, -20);
                DrawLine(a, b, s.Hit ? new Color(1f, 0.9f, 0.5f) : new Color(0.7f, 0.7f, 0.7f, 0.6f), 1f);
            }
        }

        /// <summary>Zeichnet den Sichtkegel als Fächer. Jeder Strahl endet am ersten Gebäude, das die Sicht sperrt.</summary>
        private void DrawSightCone(World w, Unit g)
        {
            const int Rays = 12;
            const int StepUnits = 200;
            var color = new Color(1f, 0.95f, 0.5f, 0.13f);
            float center = Mathf.Atan2(g.FacingY, g.FacingX);
            float half = Mathf.DegToRad(World.GuardConeHalfDegrees);
            var origin = Game.ToScreen(g.X, g.Y);
            Vector2 previous = default;
            for (int i = 0; i <= Rays; i++)
            {
                float a = center - half + 2 * half * i / Rays;
                float cos = Mathf.Cos(a), sin = Mathf.Sin(a);
                int reach = 0;
                while (reach < World.GuardConeRange)
                {
                    int next = reach + StepUnits;
                    if (w.Map.IsOpaque((int)Mathf.Floor((g.X + cos * next) / World.TileUnits), (int)Mathf.Floor((g.Y + sin * next) / World.TileUnits)))
                    {
                        break;
                    }
                    reach = next;
                }
                var end = Game.ToScreen(g.X + (int)(cos * reach), g.Y + (int)(sin * reach));
                if (i > 0 && (end != origin || previous != origin))
                {
                    DrawColoredPolygon(new[] { origin, previous, end }, color);
                }
                previous = end;
            }
        }

        private void DrawExit(World w)
        {
            var (x0, y0, x1, y1) = w.Exit;
            var pts = new[] { Game.ToScreen(x0, y0), Game.ToScreen(x1, y0), Game.ToScreen(x1, y1), Game.ToScreen(x0, y1), Game.ToScreen(x0, y0) };
            DrawPolyline(pts, new Color(0.4f, 1f, 0.5f, w.ItemTaken ? 1f : 0.35f), 1f);
        }

        private void DrawIsoRing(Vector2 p, Color c)
        {
            var pts = new Vector2[17];
            for (int i = 0; i <= 16; i++)
            {
                float a = i / 16f * Mathf.Tau;
                pts[i] = p + new Vector2(Mathf.Cos(a) * 13, Mathf.Sin(a) * 6.5f);
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
