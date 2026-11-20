namespace Messingstadt.Core
{
    /// <summary>Stand des gefälschten Passierscheins in diesem Einsatz.</summary>
    public enum PassState
    {
        /// <summary>Niemand im Trupp führt einen.</summary>
        None,

        /// <summary>Das Papier ist da und noch nicht gezeigt.</summary>
        Ready,

        /// <summary>Die Truppführerin hält es der Wache hin, die Wache liest.</summary>
        Showing,

        /// <summary>Die Wache hat es geglaubt und schaut weg.</summary>
        Away,

        /// <summary>Verbraucht: gezeigt und abgelaufen, oder mitten im Zeigen abgebrochen.</summary>
        Spent,
    }

    /// <summary>Was eine Wache zum Passierschein sagen würde. Für Hilfezeile und Mauszeiger.</summary>
    public enum PassAnswer
    {
        Works,

        /// <summary>Der Agent führt keinen Passierschein.</summary>
        NoPapers,

        /// <summary>Das eine Papier dieses Einsatzes ist schon gezeigt.</summary>
        Used,

        /// <summary>Ein Offizier kennt die Unterschrift.</summary>
        Officer,

        /// <summary>Werkschutz, Hund, Junge und alle anderen, die kein einfacher Wachmann der Stadtwache sind.</summary>
        WrongKind,

        /// <summary>Die Wache ist schon aufgeschreckt, kämpft, sucht oder meldet.</summary>
        Alerted,

        /// <summary>Die Wache ist schon gekauft.</summary>
        Bribed,

        /// <summary>Die Stadt steht gegen den Trupp: Die Wache hat Tote zu beklagen und liest genau.</summary>
        Blood,
    }

    public sealed partial class Unit
    {
        /// <summary>Der Agent geht nicht zum Bestechen zu BribeTarget, sondern um den Passierschein zu zeigen.</summary>
        public bool ShowsPass;
    }

    public sealed partial class World
    {
        /// <summary>
        /// Wer die Truppführerin nach ihrem Tod ersetzt: Sie führt den Passierschein, weil er am Beruf hängt.
        /// Den halben Preis beim Bestechen hat sie nicht, den gibt die Wache nur der Frau, die sie kennt.
        /// </summary>
        public const string SkillDeputy = "nachfolgerin";

        /// <summary>So nah muss sie heran, wie beim Bestechen.</summary>
        public const int PassRange = BribeRange;

        /// <summary>So lange hält sie das Papier hin, bis die Wache es gelesen hat.</summary>
        public const int PassShowTicks = 2 * TickRate;

        /// <summary>So lange schaut die Wache danach weg, in Sekunden. Stellschraube für die Messung.</summary>
        public int PassAwaySeconds { get; set; } = 20;

        /// <summary>So lange wartet eine Wache höchstens ab, wenn die Truppführerin mit dem Papier in der Hand auf sie zukommt.</summary>
        public const int PassWaitTicks = 5 * TickRate;

        private PassState _pass;
        private int _passWaited;
        private int _passAgent = -1;
        private int _passReadAt;
        private int _passUntil;
        private int _passX, _passY;

        /// <summary>Führt dieser Agent einen Passierschein? Das hängt am Beruf, nicht am Namen.</summary>
        public static bool CarriesPass(Unit agent) => agent.Faction == Faction.Agent && agent.Skill is SkillLeader or SkillDeputy;

        /// <summary>Stand des Passierscheins. Anzeige und Ton hängen sich an den Wechsel: siehe PassSince.</summary>
        public PassState Pass
        {
            get => _pass != PassState.None ? _pass : Units.Exists(u => u.Alive && CarriesPass(u)) ? PassState.Ready : PassState.None;
            private set
            {
                if (_pass != value)
                {
                    _pass = value;
                    PassSince = Tick;
                }
            }
        }

        /// <summary>Takt, in dem der Passierschein in seinen jetzigen Stand kam. Bei Tick == PassSince den Satz zum Stand einmal zeigen.</summary>
        public int PassSince { get; private set; }

        /// <summary>Die Wache, der das Papier gezeigt wird oder wurde, -1 solange es niemand gesehen hat.</summary>
        public int PassGuardId { get; private set; } = -1;

        /// <summary>Wer es zeigt oder gezeigt hat, -1 solange es niemand gezeigt hat.</summary>
        public int PassAgentId => _passAgent;

        /// <summary>Kann das Papier in diesem Einsatz noch jemandem gezeigt werden? Bei Blutschuld gegen den Trupp nie.</summary>
        public bool PassAvailable => Pass == PassState.Ready && !PassBlocked;

        /// <summary>Die Stadt steht gegen den Trupp: Kein Wachmann glaubt das Papier.</summary>
        public bool PassBlocked => Stance == CityStance.Against || _memory is { Stance: CityStance.Against };

        /// <summary>Takte, bis die Wache fertig gelesen hat (Showing) oder wieder hinschaut (Away). Sonst 0.</summary>
        public int PassTicksLeft => _pass == PassState.Showing ? _passReadAt - Tick : _pass == PassState.Away ? _passUntil - Tick : 0;

        /// <summary>Die Wache kennt die Frau, die ihr das Papier zeigt: Es ist die Truppführerin selbst, keine Nachfolgerin. Für den Satz der Wache.</summary>
        public bool PassBearerKnown => _passAgent >= 0 && Units[_passAgent].Skill == SkillLeader;

        /// <summary>Schaut diese Wache gerade wegen des Papiers weg oder liest sie es?</summary>
        public bool HeldByPass(Unit guard) => guard.Id == PassGuardId && _pass is PassState.Showing or PassState.Away;

        /// <summary>Was diese Wache zum Papier dieses Agenten sagen würde.</summary>
        public PassAnswer PassCheck(Unit agent, Unit guard)
        {
            if (!agent.Alive || !CarriesPass(agent))
            {
                return PassAnswer.NoPapers;
            }
            if (_pass != PassState.None)
            {
                return PassAnswer.Used;
            }
            if (guard.Type == GuardType.Offizier)
            {
                return PassAnswer.Officer;
            }
            if (guard.Faction != Faction.Guard || guard.Type != GuardType.Wachmann)
            {
                return PassAnswer.WrongKind;
            }
            if (PassBlocked)
            {
                return PassAnswer.Blood;
            }
            if (guard.Bribed)
            {
                return PassAnswer.Bribed;
            }
            // Wer gerade erst stutzt, lässt sich das Papier noch zeigen. Wer schon sucht, kämpft oder Alarm gehört hat, nicht mehr.
            if (!guard.Alive || guard.Alerted || guard.AlarmTick > 0 || guard.GuardState is not (GuardState.Patrol or GuardState.Noticing))
            {
                return PassAnswer.Alerted;
            }
            return PassAnswer.Works;
        }

        /// <summary>
        /// Die Truppführerin geht zur Wache und zeigt ihr den Passierschein. Kostet kein Geld, geht einmal je Einsatz.
        /// Jeder andere Befehl an sie bricht das ab. Hat die Wache das Papier schon in der Hand, ist es damit verbraucht.
        /// </summary>
        public bool OrderShowPass(int unitId, int guardId)
        {
            var u = Units[unitId];
            var g = Units[guardId];
            if (State != MissionState.Running || PassCheck(u, g) != PassAnswer.Works)
            {
                return false;
            }
            u.TargetId = -1;
            u.DoorTarget = -1;
            u.BribeTarget = guardId;
            u.ShowsPass = true;
            u.RepathTick = 0;
            return true;
        }

        /// <summary>
        /// Die Wache stutzt, aber die Frau kommt offen mit einem Papier auf sie zu: Sie wartet ab, statt Alarm zu schlagen.
        /// Das gilt nur für die Wache, der das Papier gilt, nur solange die Trägerin die Nächste ist, die sie sieht, und zusammen höchstens PassWaitTicks.
        /// </summary>
        private bool WaitsForPass(Unit g, Unit seen) =>
            seen.ShowsPass && seen.BribeTarget == g.Id && _pass == PassState.None && _passWaited++ < PassWaitTicks;

        private void WorkOnPass(Unit a)
        {
            var g = Units[a.BribeTarget];
            if (_pass == PassState.Showing && _passAgent == a.Id)
            {
                // Sie hält das Papier hin und rührt sich nicht. Die Wache entscheidet, wann sie fertig gelesen hat.
                a.Path.Clear();
                a.FacingX = g.X - a.X;
                a.FacingY = g.Y - a.Y;
                return;
            }
            if (PassCheck(a, g) != PassAnswer.Works)
            {
                a.BribeTarget = -1;
                a.ShowsPass = false;
                a.Path.Clear();
            }
            else if (CanSee(a, g, PassRange))
            {
                PassGuardId = g.Id;
                _passAgent = a.Id;
                _passReadAt = Tick + PassShowTicks;
                (_passX, _passY) = (a.X, a.Y);
                Pass = PassState.Showing;
                // Wer das Papier gesehen hat, hat den Trupp gesehen: Unentdeckt ist damit vorbei.
                _everNoticed = true;
                g.Roused = true;
                g.GuardState = GuardState.Patrol;
                a.Path.Clear();
            }
            else if (Tick >= a.RepathTick)
            {
                a.RepathTick = Tick + RepathInterval;
                WalkTo(a, g.TileX, g.TileY);
            }
        }

        /// <summary>
        /// Die Wache mit dem Papier: Sie liest, dann schaut sie weg, bleibt auf ihrem Posten und ruft nicht.
        /// True heißt, sie tut in diesem Takt sonst nichts. Ein Schuss, ein Alarm oder ein Angriff beenden das sofort.
        /// </summary>
        private bool HoldPass(Unit g)
        {
            if (!HeldByPass(g))
            {
                return false;
            }
            var a = Units[_passAgent];
            if (g.AlarmTick > 0 || g.GuardState is not (GuardState.Patrol or GuardState.Noticing))
            {
                // Sie hat anderes zu tun bekommen. Das Papier ist trotzdem weg.
                EndPass(a);
                return false;
            }
            if (_pass == PassState.Showing)
            {
                if (!a.Alive || a.BribeTarget != g.Id || !a.ShowsPass)
                {
                    // Zurückgezogen, bevor sie fertig gelesen hat: Das macht sie sofort misstrauisch.
                    Distrust(g, a);
                    return false;
                }
                if (Tick >= _passReadAt)
                {
                    Pass = PassState.Away;
                    _passUntil = Tick + PassAwaySeconds * TickRate;
                    a.BribeTarget = -1;
                    a.ShowsPass = false;
                }
            }
            else if (Tick >= _passUntil)
            {
                Distrust(g, a);
                return false;
            }
            g.GuardState = GuardState.Patrol;
            g.Alerted = false;
            g.Path.Clear();
            if (_pass == PassState.Showing)
            {
                g.FacingX = a.X - g.X;
                g.FacingY = a.Y - g.Y;
            }
            return true;
        }

        private void EndPass(Unit a)
        {
            Pass = PassState.Spent;
            if (a.ShowsPass)
            {
                a.ShowsPass = false;
                a.BribeTarget = -1;
            }
        }

        /// <summary>"Moment. Zeigt das noch mal her.": Die Wache ist jetzt wachsam und sieht dort nach, wo ihr das Papier gezeigt wurde.</summary>
        private void Distrust(Unit g, Unit a)
        {
            EndPass(a);
            g.LastKnownX = _passX;
            g.LastKnownY = _passY;
            StartInvestigating(g);
        }
    }
}
