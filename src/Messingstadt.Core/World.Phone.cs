using System;

namespace Messingstadt.Core
{
    /// <summary>Stand des Telefons, an dem die Verstärkung hängt.</summary>
    public enum PhoneState
    {
        /// <summary>Der Einsatz hat kein Telefon, die Verstärkung kommt wie eh und je.</summary>
        None,
        Idle,

        /// <summary>Der Alarm läuft, der Mann am Telefon ist auf dem Weg zum Apparat. Wer ihn jetzt aufhält, bekommt die Verstärkung spät und dünn.</summary>
        Fetching,

        /// <summary>Er wählt. Auch jetzt hilft es noch, ihn aufzuhalten oder den Draht zu kappen.</summary>
        Dialing,

        /// <summary>Der Anruf ist durch: Die Kaserne kommt sofort und voll.</summary>
        Called,

        /// <summary>Kein Anruf möglich: Draht gekappt, der Mann am Telefon gekauft, niedergeschlagen oder tot.</summary>
        Silent,
    }

    public sealed partial class World
    {
        /// <summary>So lange dauert es am Apparat, bis der Anruf durch ist.</summary>
        public const int PhoneTicks = 3 * TickRate;
        private const int PhoneReach = 1500;

        private int _phoneId = -1;
        private int _dialUntil;
        private int _alarmTick;
        private int _reinforceLimit;
        private bool _phonePlaced;
        private int _phoneX, _phoneY;

        /// <summary>Der Telefondraht ist gekappt.</summary>
        public bool WireCut { get; private set; }

        private PhoneState _phone;

        /// <summary>Stand des Telefons. Anzeige und Ton hängen sich an den Wechsel: siehe PhoneSince.</summary>
        public PhoneState Phone
        {
            get => _phone;
            private set
            {
                if (_phone != value)
                {
                    _phone = value;
                    PhoneSince = Tick;
                }
            }
        }

        /// <summary>Takt, in dem das Telefon in seinen jetzigen Stand kam. Wer Tick == PhoneSince sieht, spielt den Ton zum Stand genau einmal.</summary>
        public int PhoneSince { get; private set; }

        /// <summary>Wo der Apparat steht, in Welteinheiten. Ohne eigenen Platz dort, wo der Mann am Telefon gerade ist. Null ohne Telefon.</summary>
        public (int X, int Y)? PhoneSpot => _phoneId < 0 ? null : _phonePlaced ? (_phoneX, _phoneY) : (Units[_phoneId].X, Units[_phoneId].Y);

        /// <summary>Wer in diesem Einsatz telefoniert, -1 ohne Telefon.</summary>
        public int PhoneOperatorId => _phoneId;

        /// <summary>Takte, bis der Anruf durch ist, -1 wenn niemand wählt.</summary>
        public int TicksUntilCall => Phone == PhoneState.Dialing ? _dialUntil - Tick : -1;

        /// <summary>So viele rücken in diesem Einsatz höchstens an. Erst bekannt, wenn der Alarm läuft.</summary>
        public int ReinforcementLimit => _reinforceLimit;

        private void SetUpPhone()
        {
            if (_reinforcements is not { Phone.Length: > 0 } r)
            {
                return;
            }
            var operatorUnit = Units.Find(u => u.Faction == Faction.Guard && u.Name == r.Phone) ?? throw new ArgumentException($"Unbekannter Posten am Telefon {r.Phone}");
            _phoneId = operatorUnit.Id;
            Phone = PhoneState.Idle;
            if (r.PhoneAt is { Length: 2 })
            {
                _phonePlaced = true;
                _phoneX = ToUnits(r.PhoneAt[0]);
                _phoneY = ToUnits(r.PhoneAt[1]);
            }
        }

        /// <summary>Kann jetzt jemand die Kaserne anrufen?</summary>
        public bool PhoneWorks => _phoneId >= 0 && !WireCut && Units[_phoneId].Alive && !Units[_phoneId].Bribed;

        /// <summary>Mehr als Betten in der Kaserne stehen, rückt nie an, auch nicht mit Fahndung.</summary>
        private int Housed(int count) => _reinforcements!.Beds > 0 ? Math.Min(count, _reinforcements.Beds) : count;

        private void CallReinforcements()
        {
            if (_reinforcements == null || _reinforceTick != 0 || _reinforcedCount != 0 || Phone is not (PhoneState.None or PhoneState.Idle))
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
                Phone = PhoneState.Fetching;
            }
            else
            {
                ComeLate();
            }
        }

        private void Telephone()
        {
            if (Phone is not (PhoneState.Fetching or PhoneState.Dialing))
            {
                return;
            }
            if (!PhoneWorks)
            {
                ComeLate();
                return;
            }
            var man = Units[_phoneId];
            bool atPhone = !_phonePlaced || DistanceTo(man, _phoneX, _phoneY) <= PhoneReach;
            if (!atPhone)
            {
                // Er lässt alles stehen und geht zum Apparat. Führt kein Weg hin, ruft er von dort an, wo er steht.
                Phone = PhoneState.Fetching;
                if (man.Path.Count == 0 || man.Path[^1] != (_phoneX / TileUnits, _phoneY / TileUnits))
                {
                    WalkTo(man, _phoneX / TileUnits, _phoneY / TileUnits);
                    atPhone = man.Path.Count == 0;
                }
                if (!atPhone)
                {
                    return;
                }
            }
            if (Phone == PhoneState.Fetching)
            {
                Phone = PhoneState.Dialing;
                _dialUntil = Tick + PhoneTicks;
                man.Path.Clear();
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
