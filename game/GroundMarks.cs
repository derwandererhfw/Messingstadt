using Godot;
using Messingstadt.Core;

namespace Messingstadt
{
    /// <summary>
    /// Alles, was auf dem Boden liegt: Sichtkegel, Lageplan und Beute. Die Ebene liegt unter den Gebäuden,
    /// damit Dächer und Wände verdecken, was dahinter liegt.
    /// </summary>
    public partial class GroundMarks : Node2D
    {
        private const int ConeRays = 28;
        private const int ConeStepUnits = 100;
        private const float SpottedConeAlpha = 0.3f;
        private const float HiddenConeAlpha = 0.13f;

        // Auch am Rand bleibt der Fächer schwach sichtbar, denn dort wird man genauso bemerkt.
        private const float ConeEdgeShare = 0.18f;

        private const float ShadowLampReach = 4.5f;
        private const float ShadowLength = 0.8f;
        private const float ShadowMaxTiles = 2.2f;
        private const float ShadowHalfWidth = 0.14f;
        private const float ShadowAlpha = 0.75f;

        public Main Game = null!;

        public override void _Process(double delta) => QueueRedraw();

        // Die Nacht dunkelt auch diese Ebene ab. Die Farben gleichen das aus, damit sie im Bild stimmen.
        private Color Lit(float r, float g, float b, float a)
        {
            var night = Game.NightColor;
            return new Color(r / night.R, g / night.G, b / night.B, a);
        }

        public override void _Draw()
        {
            var world = Game.World;
            if (world == null)
            {
                return;
            }
            DrawFigureShadows(world);
            if (Game.PlanHeld && !Game.Finished)
            {
                DrawPlan(world);
            }
            foreach (var u in world.Units)
            {
                if (u.Alive && u.Faction == Faction.Guard && !u.Bribed && u.GuardState == GuardState.Patrol)
                {
                    DrawSightCone(world, u, world.IsSpottedByAgents(u) ? SpottedConeAlpha : HiddenConeAlpha);
                }
            }
            if (world.HasLoot && !world.LootTaken && world.Doors.TrueForAll(d => d.Open))
            {
                DrawRing(Game.ToScreen(world.LootX, world.LootY), Lit(0.4f, 1f, 0.5f, 1f));
            }
        }

        /// <summary>Figuren werfen im Laternenlicht Schatten von der Lampe weg, schwächer mit dem Abstand.</summary>
        private void DrawFigureShadows(World w)
        {
            foreach (var u in w.Units)
            {
                if (!u.Alive)
                {
                    continue;
                }
                float x = u.X / (float)World.TileUnits, y = u.Y / (float)World.TileUnits;
                foreach (var lamp in Game.LampSpots)
                {
                    var away = new Vector2(x - (float)lamp[0], y - (float)lamp[1]);
                    float distance = away.Length();
                    if (distance > ShadowLampReach || distance < 0.05f
                        || !w.Map.HasLineOfSight(u.TileX, u.TileY, (int)lamp[0], (int)lamp[1]))
                    {
                        continue;
                    }
                    var dir = away / distance;
                    float length = Mathf.Min(ShadowMaxTiles, ShadowLength * distance);
                    var side = new Vector2(-dir.Y, dir.X) * ShadowHalfWidth;
                    var far = new Vector2(x, y) + dir * length;
                    float alpha = ShadowAlpha * (1f - distance / ShadowLampReach);
                    var dark = new Color(0f, 0f, 0f, alpha);
                    var clear = new Color(0f, 0f, 0f, 0f);
                    DrawPolygon(new[]
                    {
                        Map(x + side.X, y + side.Y), Map(x - side.X, y - side.Y),
                        Map(far.X - side.X * 1.5f, far.Y - side.Y * 1.5f), Map(far.X + side.X * 1.5f, far.Y + side.Y * 1.5f),
                    }, new[] { dark, dark, clear, clear });
                }
            }
        }

        private Vector2 Map(float x, float y) => Game.ToScreen((int)(x * World.TileUnits), (int)(y * World.TileUnits));

        /// <summary>Fächer, der nach außen ausläuft. Jeder Strahl endet am ersten Gebäude, das die Sicht sperrt.</summary>
        private void DrawSightCone(World w, Unit g, float alpha)
        {
            float center = Mathf.Atan2(g.FacingY, g.FacingX);
            float half = Mathf.DegToRad(World.GuardConeHalfDegrees);
            var origin = Game.ToScreen(g.X, g.Y);
            var far = Lit(1f, 0.93f, 0.55f, 0f);
            Vector2 previous = default;
            Color previousColor = far;
            for (int i = 0; i <= ConeRays; i++)
            {
                // Zur Mitte hin am stärksten: Das nimmt dem Fächer in der Schrägansicht die rechteckige Form.
                float offCenter = Mathf.Abs(2f * i / ConeRays - 1f);
                var near = Lit(1f, 0.93f, 0.55f, alpha * Mathf.Lerp(1f, ConeEdgeShare, offCenter * offCenter));
                float a = center - half + 2 * half * i / ConeRays;
                float cos = Mathf.Cos(a), sin = Mathf.Sin(a);
                int reach = 0;
                while (reach < World.GuardConeRange)
                {
                    int next = reach + ConeStepUnits;
                    if (w.Map.IsOpaque(Mathf.FloorToInt((g.X + cos * next) / World.TileUnits), Mathf.FloorToInt((g.Y + sin * next) / World.TileUnits)))
                    {
                        break;
                    }
                    reach = next;
                }
                var end = Game.ToScreen(g.X + (int)(cos * reach), g.Y + (int)(sin * reach));
                float distance = (float)reach / World.GuardConeRange;
                var color = near.Lerp(far, Mathf.Sqrt(distance));
                if (i > 0 && (end != origin || previous != origin))
                {
                    DrawPolygon(new[] { origin, previous, end }, new[] { near, previousColor, color });
                }
                previous = end;
                previousColor = color;
            }
        }

        /// <summary>Färbt jedes freie Feld danach, wie oft es bewacht wird, und markiert die Wege der Wachen.</summary>
        private void DrawPlan(World w)
        {
            var watch = Game.Watch;
            for (int y = 0; y < watch.Height; y++)
            {
                for (int x = 0; x < watch.Width; x++)
                {
                    if (w.Map.IsBlocked(x, y))
                    {
                        continue;
                    }
                    float share = Mathf.Sqrt((float)watch.Share(x, y));
                    var color = share <= 0f ? Lit(0.25f, 0.8f, 0.35f, 0.3f) : Lit(1f, Mathf.Lerp(0.8f, 0.15f, share), 0.15f, 0.2f + 0.3f * share);
                    int u0 = x * World.TileUnits, v0 = y * World.TileUnits, size = World.TileUnits;
                    DrawColoredPolygon(new[]
                    {
                        Game.ToScreen(u0, v0), Game.ToScreen(u0 + size, v0),
                        Game.ToScreen(u0 + size, v0 + size), Game.ToScreen(u0, v0 + size),
                    }, color);
                }
            }
            foreach (var (x, y) in watch.Routes)
            {
                DrawCircle(Game.TileCenter(x, y), 2.5f, Lit(0.95f, 0.3f, 0.25f, 0.9f));
            }
        }

        private void DrawRing(Vector2 p, Color c)
        {
            const float Radius = 13f;
            var pts = new Vector2[17];
            for (int i = 0; i <= 16; i++)
            {
                float a = i / 16f * Mathf.Tau;
                pts[i] = p + new Vector2(Mathf.Cos(a) * Radius, Mathf.Sin(a) * Radius / 2f);
            }
            DrawPolyline(pts, c, 1f);
        }
    }
}
