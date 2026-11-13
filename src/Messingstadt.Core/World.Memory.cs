using System;
using System.Collections.Generic;

namespace Messingstadt.Core
{
    /// <summary>Was aus einer Wache im Einsatz geworden ist. Niedergeschlagene sind nicht tot.</summary>
    public enum GuardFate
    {
        /// <summary>Hat etwas bemerkt, gesucht, gemeldet oder gekämpft und lebt. Steht an erster Stelle, weil Spielstände der Version 5 den Wert 0 so ablegen.</summary>
        Alerted = 0,

        /// <summary>Hat bis zum Schluss nichts gemerkt.</summary>
        Unaware = 1,
        Bribed = 2,

        /// <summary>Niedergeschlagen, lebt.</summary>
        KnockedOut = 3,
        Dead = 4,
    }

    /// <summary>Eine Wache im Ergebnis des Einsatzes. Role ist der Posten aus der Mission, Person der Name des Menschen.</summary>
    public sealed record GuardOutcome(string Role, string Person, string Type, GuardFate Fate, bool Reinforcement);

    public enum CallKind
    {
        /// <summary>Zwei Wachen begegnen sich auf dem Rundgang, eine nennt die andere beim Namen.</summary>
        Greeting,

        /// <summary>Eine Wache sieht einen Kollegen fallen und ruft seinen Namen.</summary>
        Fallen,
    }

    /// <summary>Eine Wache ruft den Namen einer anderen. Nur für Darstellung und Ton, ohne Wirkung auf die Regeln.</summary>
    public readonly record struct CallEvent(int Tick, int SpeakerId, int SubjectId, CallKind Kind);

    public sealed partial class Unit
    {
        /// <summary>Name des Menschen hinter dem Posten, etwa "Krüger". Leer, wenn die Mission keinen nennt.</summary>
        public string Person = "";

        /// <summary>
        /// Im Nahkampf niedergeschlagen: liegt am Boden, lebt aber und steht nicht im Totenbuch. Er steht in diesem Einsatz nicht
        /// wieder auf, sieht, hört und meldet nichts. Für die Regeln ist er wie ein Gefallener aus dem Spiel (Alive ist false),
        /// gezählt wird er nicht als Toter: siehe Dead. Wer auf ihn schießt und trifft, tötet ihn.
        /// </summary>
        public bool KnockedOut;

        /// <summary>Wirklich tot. Niedergeschlagene sind es nicht.</summary>
        public bool Dead => !Alive && !KnockedOut;

        /// <summary>Hat den Rundgang im Einsatz mindestens einmal verlassen.</summary>
        public bool Roused;

        /// <summary>Kam als Verstärkung, stand nicht in der Mission.</summary>
        public bool Reinforcement;
        public int LastGreetTick = -100000;
    }

    public sealed partial class World
    {
        private const int GreetRange = 1500;
        private const int GreetPauseTicks = 30 * TickRate;
        private const int GreetCheckTicks = 10;
        private const int MournRange = 8 * TileUnits;

        /// <summary>Rufe der Wachen mit Namen, die jüngsten zuletzt.</summary>
        public List<CallEvent> Calls { get; } = new();

        /// <summary>Was aus jeder Wache geworden ist, in der Reihenfolge der Mission, Verstärkung am Ende. Für Spielstand und Zeitung.</summary>
        public List<GuardOutcome> GuardOutcomes()
        {
            var list = new List<GuardOutcome>();
            foreach (var u in Units)
            {
                if (u.Faction == Faction.Guard)
                {
                    list.Add(new GuardOutcome(u.Name, u.Person, u.Type.Id, FateOf(u), u.Reinforcement));
                }
            }
            return list;
        }

        public static GuardFate FateOf(Unit guard) =>
            guard.KnockedOut ? GuardFate.KnockedOut
            : !guard.Alive ? GuardFate.Dead
            : guard.Bribed ? GuardFate.Bribed
            : guard.Roused || guard.GuardState != GuardState.Patrol ? GuardFate.Alerted
            : GuardFate.Unaware;

        private void AddCall(Unit speaker, Unit subject, CallKind kind)
        {
            Calls.Add(new CallEvent(Tick, speaker.Id, subject.Id, kind));
            if (Calls.Count > 32)
            {
                Calls.RemoveAt(0);
            }
        }

        /// <summary>Merkt sich, wer den Rundgang verlassen hat, und lässt Wachen, die sich begegnen, einander beim Namen nennen.</summary>
        private void Remember(Unit g)
        {
            g.Roused |= g.GuardState != GuardState.Patrol;
            if (Tick % GreetCheckTicks != 0 || !CanGreet(g))
            {
                return;
            }
            foreach (var o in Units)
            {
                if (o.Id > g.Id && o.Faction == Faction.Guard && o.Alive && CanGreet(o) && (g.Moving || o.Moving) && CanSee(g, o, GreetRange))
                {
                    g.LastGreetTick = o.LastGreetTick = Tick;
                    AddCall(g, o, CallKind.Greeting);
                    return;
                }
            }
        }

        private bool CanGreet(Unit g) =>
            g.Person.Length > 0 && !g.Type.Bites && !g.Bribed && g.GuardState == GuardState.Patrol && Tick - g.LastGreetTick >= GreetPauseTicks;

        /// <summary>Fällt eine Wache mit Namen, ruft ihn der nächste Kollege, der sie sieht, einmal.</summary>
        private void Mourn(Unit fallen)
        {
            if (fallen.Faction != Faction.Guard || fallen.KnockedOut || fallen.Person.Length == 0)
            {
                return;
            }
            Unit? nearest = null;
            foreach (var o in Units)
            {
                if (o.Id != fallen.Id && o.Alive && o.Faction == Faction.Guard && !o.Type.Bites && !o.Bribed && CanSee(o, fallen, MournRange)
                    && (nearest == null || Distance(o, fallen) < Distance(nearest, fallen)))
                {
                    nearest = o;
                }
            }
            if (nearest != null)
            {
                AddCall(nearest, fallen, CallKind.Fallen);
            }
        }

        private void NameReinforcement(Unit g)
        {
            g.Reinforcement = true;
            var persons = _reinforcements!.Persons;
            if (_reinforcedCount <= persons.Count)
            {
                g.Person = persons[_reinforcedCount - 1];
            }
        }
    }
}
