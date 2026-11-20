using System;
using System.Collections.Generic;
using System.Linq;

namespace Messingstadt.Core
{
    /// <summary>Ein Sabotageziel im laufenden Einsatz.</summary>
    public sealed class SabotageTarget
    {
        public string Name = "";
        public string Short = "";
        public int X;
        public int Y;

        /// <summary>Takte Arbeit, bis das Ziel erledigt ist.</summary>
        public int Ticks;

        /// <summary>Bisher geleistete Arbeit in Takten. Fällt auf 0, sobald niemand mehr daran arbeitet.</summary>
        public int Progress;
        public bool Done;

        /// <summary>Der Schlosser braucht hier die halbe Zeit.</summary>
        public bool Locksmith;
    }

    /// <summary>
    /// Auftragsart Sabotieren: Die Mission nennt Ziele mit Ort und Dauer. Ein Agent steht die Dauer lang daneben, jeder andere Befehl
    /// bricht ab. Sind alle Ziele erledigt und steht ein Agent eine Sekunde in der Zielzone, ist der Einsatz gewonnen.
    /// Dazu die Uhr und der Schotter.
    /// </summary>
    public sealed partial class World
    {
        /// <summary>So nah muss die Feldmitte des Agenten am Ziel liegen, damit er daran arbeiten kann.</summary>
        public const int SabotageRange = 3 * TileUnits / 2;

        /// <summary>So weit hört eine Wache jemanden, der über Schotter geht. Wer steht, ist so leise wie überall.</summary>
        public const int GravelHeardRange = 3 * TileUnits;

        /// <summary>True in einem Einsatz der Art Sabotieren.</summary>
        public bool IsSabotage { get; private set; }

        public List<SabotageTarget> Targets { get; } = new();

        /// <summary>Uhr in Takten, 0 ohne Uhr.</summary>
        public int TimeLimitTicks { get; private set; }

        /// <summary>Takt, in dem das letzte Ziel erledigt wurde, -1 solange noch eines fehlt. Ab dann steht die Uhr.</summary>
        public int AllDoneTick { get; private set; } = -1;

        /// <summary>True, wenn der Einsatz an der Uhr verloren ging.</summary>
        public bool TimeUp { get; private set; }

        /// <summary>Takte bis zum Ablauf der Uhr. -1 ohne Uhr oder wenn alle Ziele erledigt sind.</summary>
        public int TicksLeft => TimeLimitTicks <= 0 || AllDoneTick >= 0 ? -1 : Math.Max(0, TimeLimitTicks - Tick);

        public bool AllSabotaged => IsSabotage && Targets.All(t => t.Done);

        private SabotageTextData _sabotageTexts = new();
        private readonly List<(int X0, int Y0, int X1, int Y1)> _gravel = new();
        private readonly List<(int X0, int Y0, int X1, int Y1)> _planks = new();

        private void SetUpSabotage(MissionData mission)
        {
            foreach (var z in mission.Gravel)
            {
                _gravel.Add((ToUnits(z.X0), ToUnits(z.Y0), ToUnits(z.X1), ToUnits(z.Y1)));
            }
            foreach (var z in mission.Planks)
            {
                _planks.Add((ToUnits(z.X0), ToUnits(z.Y0), ToUnits(z.X1), ToUnits(z.Y1)));
            }
            if (mission.Sabotage.Count == 0)
            {
                return;
            }
            if (mission.Prisoners.Count > 0)
            {
                throw new ArgumentException("Ein Einsatz kann nicht zugleich Befreien und Sabotieren sein");
            }
            IsSabotage = true;
            TimeLimitTicks = Math.Max(0, mission.TimeLimit) * TickRate;
            _sabotageTexts = mission.SabotageTexts ?? _sabotageTexts;
            foreach (var t in mission.Sabotage)
            {
                Targets.Add(new SabotageTarget
                {
                    Name = t.Name, Short = t.Short.Length > 0 ? t.Short : t.Name.ToUpperInvariant(), X = ToUnits(t.X), Y = ToUnits(t.Y),
                    Ticks = Math.Max(1, (int)Math.Round(t.Seconds * TickRate)), Locksmith = t.Locksmith,
                });
            }
        }

        /// <summary>Liegt dieser Punkt auf Schotter? Bohlen gehen vor.</summary>
        public bool OnGravel(int x, int y)
        {
            foreach (var (x0, y0, x1, y1) in _planks)
            {
                if (x >= x0 && x < x1 && y >= y0 && y < y1)
                {
                    return false;
                }
            }
            foreach (var (x0, y0, x1, y1) in _gravel)
            {
                if (x >= x0 && x < x1 && y >= y0 && y < y1)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Wie weit man diesen Schritt hört, auch im Rücken der Wache: auf Schotter weiter als auf Pflaster.</summary>
        private int StepHeardRange(Unit o) => o.Moving && _gravel.Count > 0 && OnGravel(o.X, o.Y) ? GravelHeardRange : GuardCloseRange;

        /// <summary>
        /// Schickt einen Agenten an ein Sabotageziel. Er geht auf das nächste freie Feld daneben und arbeitet, bis das Ziel erledigt ist
        /// oder er einen anderen Befehl bekommt. Liegt das Ziel hinter einer verschlossenen Tür, gibt es kein freies Feld: erst die Tür.
        /// </summary>
        public bool OrderSabotage(int unitId, int targetId)
        {
            var u = Units[unitId];
            if (targetId < 0 || targetId >= Targets.Count || !u.Alive || u.Faction != Faction.Agent || State != MissionState.Running || Targets[targetId].Done)
            {
                return false;
            }
            var t = Targets[targetId];
            List<(int X, int Y)>? best = null;
            int tx = t.X / TileUnits, ty = t.Y / TileUnits;
            for (int y = ty - 2; y <= ty + 2; y++)
            {
                for (int x = tx - 2; x <= tx + 2; x++)
                {
                    if (Map.IsBlocked(x, y) || !InSabotageRange(x * TileUnits + TileUnits / 2, y * TileUnits + TileUnits / 2, t) || !HasSight(x, y, tx, ty))
                    {
                        continue;
                    }
                    var path = Map.FindPath(u.TileX, u.TileY, x, y);
                    if (path != null && (best == null || path.Count < best.Count))
                    {
                        best = path;
                    }
                }
            }
            if (best == null)
            {
                return false;
            }
            u.Path.Clear();
            u.Path.AddRange(best);
            u.TargetId = -1;
            u.DoorTarget = -1;
            u.BribeTarget = -1;
            u.BandageTicks = 0;
            u.Sprinting = false;
            u.SabotageTarget = targetId;
            return true;
        }

        private static bool InSabotageRange(int x, int y, SabotageTarget t) =>
            (long)(x - t.X) * (x - t.X) + (long)(y - t.Y) * (y - t.Y) <= (long)SabotageRange * SabotageRange;

        /// <summary>Arbeitet dieser Agent gerade an seinem Ziel, steht also daneben und geht nicht mehr?</summary>
        public bool IsSabotaging(Unit a) => a.Alive && a.SabotageTarget >= 0 && a.Path.Count == 0 && !a.Moving
            && !Targets[a.SabotageTarget].Done && InSabotageRange(a.X, a.Y, Targets[a.SabotageTarget]);

        private void StepSabotage()
        {
            if (!IsSabotage || State != MissionState.Running)
            {
                return;
            }
            for (int i = 0; i < Targets.Count; i++)
            {
                var t = Targets[i];
                if (t.Done)
                {
                    continue;
                }
                int step = 0;
                foreach (var a in Units)
                {
                    if (a.Faction == Faction.Agent && a.SabotageTarget == i && IsSabotaging(a))
                    {
                        step = Math.Max(step, t.Locksmith && a.Skill == SkillLocksmith ? 2 : 1);
                    }
                }
                // Wer weggeht oder einen anderen Befehl bekommt, fängt von vorn an.
                t.Progress = step > 0 ? t.Progress + step : 0;
                if (t.Progress >= t.Ticks)
                {
                    t.Done = true;
                    foreach (var a in Units)
                    {
                        if (a.SabotageTarget == i)
                        {
                            a.SabotageTarget = -1;
                        }
                    }
                    if (Targets.All(x => x.Done))
                    {
                        AllDoneTick = Tick;
                    }
                }
            }
        }

        private void CheckSabotage()
        {
            if (AllDoneTick < 0)
            {
                EscapeProgress = 0;
                if (TimeLimitTicks > 0 && Tick >= TimeLimitTicks)
                {
                    TimeUp = true;
                    State = MissionState.Lost;
                }
                return;
            }
            bool boarding = Units.Exists(u => u.Alive && u.Faction == Faction.Agent && !u.Moving && InExit(u));
            EscapeProgress = boarding ? EscapeProgress + 1 : 0;
            if (EscapeProgress >= EscapeTicks)
            {
                State = MissionState.Won;
            }
        }

        /// <summary>Zeile für den Telegrammstreifen in einem Einsatz der Art Sabotieren.</summary>
        public string SabotageLine()
        {
            if (TimeUp)
            {
                return _sabotageTexts.TimeUp;
            }
            if (EscapeProgress > 0)
            {
                return _sabotageTexts.Boarding;
            }
            var working = Targets.Find(t => !t.Done && t.Progress > 0);
            if (working != null)
            {
                return _sabotageTexts.Working.Replace("{ZIEL}", working.Short);
            }
            var done = Targets.FindAll(t => t.Done);
            if (done.Count == Targets.Count)
            {
                return _sabotageTexts.AllDone;
            }
            if (done.Count > 0)
            {
                string rest = string.Join(" UND ", Targets.Where(t => !t.Done).Select(t => t.Short));
                return _sabotageTexts.OneDone.Replace("{ZIEL}", done[^1].Short).Replace("{REST}", rest);
            }
            return _sabotageTexts.Start;
        }

        /// <summary>Zusatz zur Uhr für die zweite Zeile: Minuten aufgerundet, in der letzten Minute Sekunden. Leer ohne Uhr oder wenn alles steht.</summary>
        public string ClockLine()
        {
            int left = TicksLeft;
            if (left < 0 || State != MissionState.Running)
            {
                return "";
            }
            int seconds = (left + TickRate - 1) / TickRate;
            return seconds > 60
                ? _sabotageTexts.Minutes.Replace("{n}", ((seconds + 59) / 60).ToString()).Replace("{N}", ((seconds + 59) / 60).ToString())
                : _sabotageTexts.Seconds.Replace("{n}", seconds.ToString()).Replace("{N}", seconds.ToString());
        }
    }
}
