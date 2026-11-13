using System;

namespace Messingstadt.Core
{
    /// <summary>Stand des Telefons, an dem die Verstärkung hängt.</summary>
    public enum PhoneState
    {
        /// <summary>Der Einsatz hat kein Telefon, die Verstärkung kommt wie eh und je.</summary>
        None,
        Idle,

        /// <summary>Der Telefonist wählt. Wer ihn jetzt noch aufhält, bekommt die Verstärkung spät und dünn.</summary>
        Dialing,

        /// <summary>Der Anruf ist durch: Die Kaserne kommt sofort und voll.</summary>
        Called,

        /// <summary>Kein Anruf möglich: Draht gekappt, Telefonist gekauft, niedergeschlagen oder tot.</summary>
        Silent,
    }

    public sealed partial class World
    {
        /// <summary>So lange braucht der Telefonist, bis der Anruf durch ist.</summary>
        public const int PhoneTicks = 3 * TickRate;

        private int _phoneId = -1;
        private int _dialUntil;
        private int _alarmTick;
        private int _reinforceLimit;

        /// <summary>Der Telefondraht ist gekappt.</summary>
        public bool WireCut { get; private set; }

        public PhoneState Phone { get; private set; }

        /// <summary>Takte, bis der Anruf durch ist, -1 wenn niemand wählt.</summary>
        public int TicksUntilCall => Phone == PhoneState.Dialing ? _dialUntil - Tick : -1;

        /// <summary>So viele rücken in diesem Einsatz höchstens noch an. Erst bekannt, wenn der Alarm läuft.</summary>
        public int ReinforcementLimit => _reinforceLimit;

        private void SetUpPhone()
        {
            if (_reinforcements is not { Phone.Length: > 0 } r)
            {
                return;
            }
            var operatorUnit = Units.Find(u => u.Faction == Faction.Guard && u.Name == r.Phone) ?? throw new ArgumentException($"Unbekannter Telefonist {r.Phone}");
            _phoneId = operatorUnit.Id;
            Phone = PhoneState.Idle;
        }

        /// <summary>Kann jetzt jemand die Kaserne anrufen?</summary>
        public bool PhoneWorks => _phoneId >= 0 && !WireCut && Units[_phoneId].Alive && !Units[_phoneId].Bribed;

        /// <summary>Mehr als Betten in der Kaserne stehen, rückt nie an, auch nicht mit Fahndung.</summary>
        private int Housed(int count) => _reinforcements!.Beds > 0 ? Math.Min(count, _reinforcements.Beds) : count;

        private void CallReinforcements()
        {
            if (_reinforcements == null || _reinforceTick != 0 || _reinforcedCount != 0 || Phone is PhoneState.Dialing or PhoneState.Called or PhoneState.Silent)
            {
                return;
            }
            _alarmTick = Tick;
            if (_phoneId < 0)
            {
                _reinforceLimit = Housed(_reinforcements.Count);
                _reinforceTick = Tick + _reinforcements.FirstDelay * TickRate;
            }
            else if (PhoneWorks)
            {
                Phone = PhoneState.Dialing;
                _dialUntil = Tick + PhoneTicks;
            }
            else
            {
                ComeLate();
            }
        }

        private void Telephone()
        {
            if (Phone != PhoneState.Dialing)
            {
                return;
            }
            if (!PhoneWorks)
            {
                ComeLate();
            }
            else if (Tick >= _dialUntil)
            {
                Phone = PhoneState.Called;
                _reinforceLimit = Housed(_reinforcements!.Count);
                _reinforceTick = Tick + Math.Max(1, _reinforcements.PhoneDelay * TickRate);
            }
        }

        /// <summary>Ohne Anruf merkt die Kaserne den Alarm erst spät, und es kommen nur wenige.</summary>
        private void ComeLate()
        {
            var r = _reinforcements!;
            Phone = PhoneState.Silent;
            int late = r.LateDelay > 0 ? r.LateDelay : 2 * r.FirstDelay;
            int few = r.LateCount > 0 ? r.LateCount : Math.Max(1, r.Count / 2);
            _reinforceLimit = Math.Min(Housed(r.Count), few);
            _reinforceTick = Math.Max(Tick + 1, _alarmTick + late * TickRate);
        }
    }
}
