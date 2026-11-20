using Messingstadt.Core;

// Misst die Spielweisen einer Mission in der echten Spiellogik.
// Aufruf: dotnet run -c Release --project tools/Messingstadt.Balance
// Kampagne über mehrere Einsätze: ... -- --kampagne
// Ausrüstung im Vergleich: ... -- --waffen, gemischte Trupps je Mark: -- --preis
// Kampagne mit Einkauf: -- --einkauf. Totschläger: -- --knueppel-plan (Zeitfenster wie beim Schleichplan) und -- --knueppel (ohne Plan)
// Mit --ohne-magazin vorweg gilt die Pistole der ersten Fassung, zum Vergleich mit älteren Messungen.
// Werte ausprobieren, ohne die Datei zu ändern: --wert maschinenpistole.Magazine=20, Startgeld der Kampagnen: --startgeld 600
// Dritter Ausbau Arme je Stufe: -- --arme
// Anderer Einsatz: --mission=mission_02 vorweg. Einsätze der Art Befreien messen dann ihre eigenen Spielweisen.

// MS_LAEUFE=1 für einen einzelnen Lauf, etwa mit MS_SPUR.
int Runs = int.TryParse(Environment.GetEnvironmentVariable("MS_LAEUFE"), out int runsWanted) ? runsWanted : 300;
const int MaxTicks = 20 * 300;
const int SearchTicks = 400;
const int MinWindowTicks = 30;

string dataDir = FindDataDir();
string missionId = "mission_01";
if (args.Length > 0 && args[0].StartsWith("--mission="))
{
    missionId = args[0]["--mission=".Length..];
    args = args[1..];
}
var files = Loader.LoadMission(dataDir, missionId).Mission;
string mapJson = File.ReadAllText(Path.Combine(dataDir, files.MapId + ".json"));
string missionJson = File.ReadAllText(Path.Combine(dataDir, missionId + ".json"));
string lightJson = File.ReadAllText(Path.Combine(dataDir, files.LightId + ".json"));
var catalog = Loader.ParseCatalog(File.ReadAllText(Path.Combine(dataDir, "waffen.json")));

// Ausrüstung je Agent in der Reihenfolge der Mission. Null heißt Grundausstattung, ohne Katalog die Pistole ohne Nachladen.
string[][]? loadout = null;
// Schalter für den Güterbahnhof, an beliebiger Stelle: --uhr=<s> setzt die Uhr, --haltung=hilft|schweigt|gegen die Haltung der Stadt
// (bei schweigt und gegen rufen die Bewohner), --pforten=offen lässt beide Zaunpforten offen stehen, wie wenn die Stadt hilft.
Action<MissionData>? yardSetup = null;
// --wagen: Der leise Plan geht nicht durch den Barackenhof, sondern vom Kohlenweg über den Schotter zwischen den Wagen nach Osten.
bool yardWagons = args.Contains("--wagen");
args = args.Where(x => x != "--wagen").ToArray();
{
    string? Arg(string key) => args.FirstOrDefault(x => x.StartsWith(key))?[key.Length..];
    string? clockArg = Arg("--uhr="), stanceArg = Arg("--haltung="), gatesArg = Arg("--pforten=");
    if (clockArg != null || stanceArg != null || gatesArg != null)
    {
        yardSetup = m =>
        {
            if (clockArg != null)
            {
                m.TimeLimit = int.Parse(clockArg);
            }
            if (stanceArg != null)
            {
                m.Stance = stanceArg == "gegen" ? CityStance.Against : stanceArg == "schweigt" ? CityStance.Silent : CityStance.Helps;
            }
            if (gatesArg == "offen")
            {
                m.Support = 5;
                m.Helpers = m.Doors.Select(d => d.Helper).Where(h => h.Length > 0).Distinct().ToList();
            }
        };
        Console.WriteLine($"Schalter: Uhr {clockArg ?? "wie Daten"}, Haltung {stanceArg ?? "hilft"}, Pforten {gatesArg ?? "zu"}");
    }
    args = args.Where(x => !x.StartsWith("--uhr=") && !x.StartsWith("--haltung=") && !x.StartsWith("--pforten=")).ToArray();
}

// Ändert den Einsatz für eine Messreihe, ohne die Datei anzufassen, etwa ein zusätzlicher Posten.
Action<MissionData>? variant = null;
// Stellt eine neue Welt für eine Messreihe ein, etwa den Nachlass der Truppführerin.
Action<World>? tune = null;
// Der Lageplan für die leisen Spielweisen, einmal je Fassung des Einsatzes gemessen.
WatchMap? quietWatch = null;
bool withCatalog = true;
if (args.Length > 0 && args[0] == "--ohne-magazin")
{
    withCatalog = false;
    args = args[1..];
}

// Zum Ausprobieren ohne die Datei zu ändern: --wert pistole.Magazine=15 setzt einen Wert im geladenen Katalog,
// --startgeld 600 gibt jeder gemessenen Kampagne anderes Startgeld.
int? startMoney = null;
while (args.Length > 1 && (args[0] == "--wert" || args[0] == "--startgeld"))
{
    if (args[0] == "--startgeld")
    {
        startMoney = int.Parse(args[1]);
    }
    else
    {
        var (key, value) = (args[1].Split('=')[0], args[1].Split('=')[1]);
        var def = catalog.Get(key.Split('.')[0]) ?? throw new ArgumentException($"Unbekannter Gegenstand in {key}");
        var prop = typeof(ItemDef).GetProperty(key.Split('.')[1]) ?? throw new ArgumentException($"Unbekanntes Feld in {key}");
        prop.SetValue(def, Convert.ChangeType(value, prop.PropertyType, System.Globalization.CultureInfo.InvariantCulture));
    }
    args = args[2..];
}

// Erinnerung und Telefon für Einsatz 2 ausprobieren, ohne die Datei zu ändern. Gilt für alle Messreihen danach:
// --erinnerung=blut      die Stadt steht gegen den Trupp, Groll trifft Schließer und Hundeführer, Streifen gehen zu zweit, soweit die Decke es zulässt
// --erinnerung=streifen  dasselbe, aber der Groll trifft die beiden Wallstreifen
// --erinnerung=frieden   niemand starb, der Hundeführer wurde gekauft, Türposten und eine Streife schauen weg
// --telefon=an           der Wachhabende ruft vom Apparat an seinem Posten an, Kaserne mit drei Betten, es kommt Stadtwache
// --telefon=stumm        dasselbe, aber der Draht ist gekappt: Die Verstärkung kommt spät und dünn
// --telefon=stadtwache   kein Telefon, nur die Art der Verstärkung geleert: Stadtwache statt Werkschutz
Action<MissionData>? remembered = null;
bool phoneSilent = false;
while (args.Length > 0 && (args[0].StartsWith("--erinnerung=") || args[0].StartsWith("--telefon=")))
{
    string value = args[0].Split('=')[1];
    var before = remembered;
    if (args[0].StartsWith("--telefon="))
    {
        phoneSilent = value == "stumm";
        remembered = m =>
        {
            before?.Invoke(m);
            m.Reinforcements!.Type = "";
            m.Reinforcements.Sprite = "Wachmann";
            if (value == "stadtwache")
            {
                return;
            }
            m.Reinforcements.Beds = 3;
            if (phoneSilent)
            {
                // Kein Anruf: Die Kaserne kommt so spät und so dünn, wie es der Kern ohne Telefon rechnet.
                m.Reinforcements.FirstDelay *= 2;
                m.Reinforcements.Count = Math.Max(1, m.Reinforcements.Count / 2);
            }
            else
            {
                m.Reinforcements.Phone = "Wachhabender";
                m.Reinforcements.PhoneAt = new[] { 14.5, 9.5 };
            }
        };
    }
    else
    {
        remembered = m =>
        {
            before?.Invoke(m);
            string[] persons = { "Krüger", "Behrens", "Tietz", "Pieper", "Wolff", "Adam" };
            for (int i = 0; i < persons.Length; i++)
            {
                m.Guards[i].Person = persons[i];
            }
            if (value == "frieden")
            {
                m.Guards[2].AtEase = m.Guards[1].AtEase = true;
                m.Memory = new GuardMemoryData { Stance = CityStance.Helps, Known = { new KnownGuardData { Person = "Adam", Fate = GuardFate.Bribed } } };
                return;
            }
            string[] grudge = value == "streifen" ? new[] { "Krüger", "Behrens" } : new[] { "Wolff", "Adam" };
            m.Memory = new GuardMemoryData { Stance = CityStance.Against, Known = grudge.Select(p => new KnownGuardData { Person = p, Fate = GuardFate.Alerted }).ToList() };
            m.Guards.Add(new UnitData { Name = "Wallstreife 1b", Person = "Noack", Sprite = "Wachmann", AfterBlood = true, X = 1.5, Y = 11.5, Patrol = { new double[] { 13, 11 }, new double[] { 0, 11 } } });
            m.Guards.Add(new UnitData { Name = "Wallstreife 2b", Person = "Hartung", Sprite = "Wachmann", AfterBlood = true, X = 12.5, Y = 12.5, Patrol = { new double[] { 0, 12 }, new double[] { 13, 12 } } });
        };
    }
    args = args[1..];
}

MissionData LitMission()
{
    var mission = Loader.ParseMission(missionJson);
    mission.Light = Loader.ParseLight(lightJson);
    mission.Catalog = withCatalog ? catalog : null;
    for (int i = 0; loadout != null && i < mission.Agents.Count; i++)
    {
        mission.Agents[i].Items = loadout[Math.Min(i, loadout.Length - 1)].ToList();
    }
    remembered?.Invoke(mission);
    variant?.Invoke(mission);
    yardSetup?.Invoke(mission);
    return mission;
}

World NewWorld(int seed)
{
    var mission = LitMission();
    mission.Seed = seed;
    var world = new World(Loader.ParseMap(mapJson), mission);
    tune?.Invoke(world);
    return world;
}

(int X, int Y) ExitTile(World w) => (w.Exit.X0 / World.TileUnits, (w.Exit.Y0 + w.Exit.Y1) / 2 / World.TileUnits);

void Report(string name, Func<Action<World>> make)
{
    int wins = 0, lost = 0, dead = 0, hp = 0, ticks = 0, loud = 0, down = 0, knocked = 0, quiet = 0;
    for (int seed = 1; seed <= Runs; seed++)
    {
        var w = NewWorld(seed * 7919);
        var strategy = make();
        for (int i = 0; i < MaxTicks && w.State == MissionState.Running; i++)
        {
            if (i % 10 == 0)
            {
                strategy(w);
            }
            w.Step();
        }
        wins += w.State == MissionState.Won ? 1 : 0;
        lost += w.State == MissionState.Lost ? 1 : 0;
        dead += w.Units.Count(u => u.Faction == Faction.Agent && !u.Alive);
        hp += w.Units.Where(u => u.Faction == Faction.Agent).Sum(u => u.Hp);
        ticks += w.Tick;
        loud += w.Rating == MissionRating.Loud ? 1 : 0;
        down += w.Units.Count(u => u.Faction == Faction.Guard && !u.Alive);
        // Niedergeschlagene zählen hier mit: Die Spalte heißt "Wachen nieder", nicht "tot".
        knocked += w.Strikes.Count(k => k.KnockOut);
        quiet += w.State == MissionState.Won && w.Rating != MissionRating.Loud ? 1 : 0;
    }
    Console.WriteLine($"{name,-18} Sieg {100 * wins / Runs,3}%  verloren {100 * lost / Runs,3}%  tote Agenten {(double)dead / Runs:0.00}  LP {hp / Runs,3}/400  Dauer {ticks / Runs / 20,3}s  laut {100 * loud / Runs,3}%  Wachen nieder {(double)down / Runs:0.0}"
        + (knocked > 0 ? $", davon geschlagen {(double)knocked / Runs:0.0}, leise gewonnen {100 * quiet / Runs}%" : ""));
}

Action<World> Rush() => RushWith(false);

Action<World> RushSprint() => RushWith(true);

Action<World> RushWith(bool sprint)
{
    string stage = "";
    return w =>
    {
        var squad = w.Units.Where(u => u.Faction == Faction.Agent && u.Alive).Select(u => u.Id).ToList();
        var book = (w.ItemX / World.TileUnits, w.ItemY / World.TileUnits);
        bool nobodyAtBook = !w.ItemTaken && !w.Units.Any(u => u.Faction == Faction.Agent && u.Alive
            && (u.Path.Count > 0 ? u.Path[^1] : (u.TileX, u.TileY)) == book);
        string now = w.ItemTaken ? "wagen" : $"buch{book}";
        if (now != stage || nobodyAtBook)
        {
            stage = now;
            var (x, y) = w.ItemTaken ? ExitTile(w) : book;
            w.OrderGroupMove(squad, x, y, sprint);
        }
    };
}

// Wie Zum Buch gehen, aber der nächste Agent schießt auf einen Hund, der angreift, wie es ein Spieler befehlen würde.
Action<World> RushShootDog()
{
    var rush = Rush();
    return w =>
    {
        rush(w);
        foreach (var dog in w.Units.Where(u => u.Alive && u.Type.Bites && u.GuardState == GuardState.Combat))
        {
            var shooter = w.Units.Where(a => a.Faction == Faction.Agent && a.Alive && w.CanSee(a, dog, 6 * World.TileUnits)).OrderBy(a => World.Distance(a, dog)).FirstOrDefault();
            if (shooter != null && shooter.TargetId != dog.Id)
            {
                w.OrderAttack(shooter.Id, dog.Id);
            }
        }
    };
}

Action<World> AttackAll()
{
    var rush = Rush();
    return w =>
    {
        var guards = w.Units.Where(u => u.Faction == Faction.Guard && u.Alive).ToList();
        if (guards.Count == 0)
        {
            rush(w);
            return;
        }
        foreach (var a in w.Units.Where(u => u.Faction == Faction.Agent && u.Alive && u.TargetId < 0))
        {
            w.OrderAttack(a.Id, guards.OrderBy(g => World.Distance(a, g)).First().Id);
        }
    };
}

// Wie ein bedachter Spieler: Der Trupp bleibt zusammen und schießt erst eine Wache aus, dann die nächste.
// Hund und Laufbursche lässt er aus, die schießen nicht zurück, und ein Schuss auf sie macht den Einsatz nur früher laut.
Action<World> FocusFire()
{
    var rush = Rush();
    return w =>
    {
        var squad = w.Units.Where(u => u.Faction == Faction.Agent && u.Alive).ToList();
        var guards = w.Units.Where(u => u.Faction == Faction.Guard && u.Alive && !u.Type.OrderOnly).ToList();
        if (guards.Count == 0 || squad.Count == 0)
        {
            rush(w);
            return;
        }
        var lead = squad[0];
        var target = guards.OrderBy(g => World.Distance(lead, g)).First();
        foreach (var a in squad.Where(a => a.TargetId < 0 || !w.Units[a.TargetId].Alive))
        {
            w.OrderAttack(a.Id, target.Id);
        }
    };
}

// Hinterhalt: Der Trupp wartet zwischen den Häusern, einer lockt die Wachen an, dann lässt man sie kommen.
Action<World> Ambush()
{
    var rush = Rush();
    int stage = 0, quiet = 0;
    return w =>
    {
        var squad = w.Units.Where(u => u.Faction == Faction.Agent && u.Alive).ToList();
        if (squad.Count == 0)
        {
            return;
        }
        var ids = squad.Select(u => u.Id).ToList();
        switch (stage)
        {
            case 0:
                w.OrderGroupMove(ids, 8, 14);
                stage = 1;
                return;
            case 1:
                if (w.Tick >= 100)
                {
                    w.OrderMove(ids[0], 8, 11);
                    stage = 2;
                }
                return;
            case 2:
                if (w.Rating != MissionRating.Unseen)
                {
                    w.OrderMove(ids[0], 8, 14);
                    stage = 3;
                }
                return;
            case 3:
                var guards = w.Units.Where(u => u.Faction == Faction.Guard && u.Alive).ToList();
                bool near = guards.Any(g => squad.Any(a => World.Distance(a, g) <= 10 * World.TileUnits));
                quiet = near ? 0 : quiet + 1;
                foreach (var a in squad.Where(a => a.TargetId < 0 && a.Path.Count == 0))
                {
                    var target = guards.Where(g => w.CanSee(a, g, 7 * World.TileUnits)).OrderBy(g => World.Distance(a, g)).FirstOrDefault();
                    if (target != null)
                    {
                        w.OrderAttack(a.Id, target.Id);
                    }
                }
                if (quiet >= 10 || guards.Count == 0)
                {
                    stage = 4;
                }
                return;
            default:
                rush(w);
                return;
        }
    };
}

// Wie ein Spieler, der den Trupp auf die Straße stellt und ihn kämpfen lässt: keine Angriffsbefehle.
Action<World> HoldThenRush()
{
    var rush = Rush();
    int quiet = 0;
    bool placed = false, lured = false;
    return w =>
    {
        var squad = w.Units.Where(u => u.Faction == Faction.Agent && u.Alive).ToList();
        if (squad.Count == 0)
        {
            return;
        }
        if (!placed)
        {
            placed = true;
            w.OrderGroupMove(squad.Select(u => u.Id).ToList(), 8, 11);
            return;
        }
        var guards = w.Units.Where(u => u.Faction == Faction.Guard && u.Alive).ToList();
        bool near = guards.Any(g => squad.Any(a => World.Distance(a, g) <= 10 * World.TileUnits));
        lured |= w.Rating == MissionRating.Loud;
        quiet = near ? 0 : quiet + 1;
        if (lured && (quiet >= 10 || guards.Count == 0))
        {
            rush(w);
        }
    };
}

// Spieler ohne vorab bekannte Zeiten: Greta geht von Versteck zu Versteck und läuft los, wenn der Lageplan den Weg frei zeigt.
// Die Reaktion kommt zufällig bis zu lag Takte zu spät.
void Sneak((int X, int Y)[] stops, int runs, int lag)
{
    var rng = new Random(1927);
    var watch = WatchMap.Measure(Loader.ParseMap(mapJson), LitMission());
    int won = 0, unseen = 0, ticks = 0;
    for (int seed = 1; seed <= runs; seed++)
    {
        var w = NewWorld(seed * 7919);
        int next = 0, wait = rng.Next(0, 200), goAt = -1;
        for (int i = 0; i < MaxTicks && w.State == MissionState.Running; i++)
        {
            var a = w.Units[0];
            bool busy = a.Path.Count > 0 || (next > 0 && stops[next - 1] == (w.ItemX / World.TileUnits, w.ItemY / World.TileUnits) && !w.ItemTaken);
            if (!busy && next < stops.Length && i >= wait && i % 5 == 0)
            {
                var path = w.Map.FindPath(a.TileX, a.TileY, stops[next].X, stops[next].Y);
                bool book = stops[next] == (w.ItemX / World.TileUnits, w.ItemY / World.TileUnits) && !w.ItemTaken;
                int hold = book ? World.PickupTicks : next == stops.Length - 1 ? World.EscapeTicks : 40;
                if (path != null && goAt < 0 && watch.FirstRisk(path, a.X, a.Y, World.WalkSpeed(a), w.Tick, hold) < 0)
                {
                    goAt = w.Tick + rng.Next(0, lag + 1);
                }
                if (goAt >= 0 && w.Tick >= goAt)
                {
                    w.OrderMove(0, stops[next].X, stops[next].Y);
                    next++;
                    goAt = -1;
                }
            }
            w.Step();
        }
        won += w.State == MissionState.Won ? 1 : 0;
        unseen += w.State == MissionState.Won && w.Rating == MissionRating.Unseen ? 1 : 0;
        ticks += w.Tick;
    }
    Console.WriteLine($"Nach Lageplan schleichen, Reaktion bis {lag / 20.0:0.0} s: Sieg {100 * won / runs}%, unentdeckt {100 * unseen / runs}%, Dauer {ticks / runs / 20} s");
}

// Kampagne: Viele Kampagnen über mehrere Einsätze mit einer Spielweise. Vor jedem Einsatz erst Arzt, dann Anwerben, dann Einkauf, Rest in Ausbau.
// Ohne Einkauf ist das dieselbe Reihenfolge wie früher, nur dass der Ausbau vor dem ersten Einsatz am Rückhalt für das Anwerben scheitert.
void CampaignRun(string name, Func<Action<World>> make, int campaigns, int missions, Action<Campaign>? shop = null)
{
    int won = 0, played = 0, deaths = 0, recruits = 0, shortSquads = 0, heat = 0, money = 0, spent = 0;
    for (int run = 1; run <= campaigns; run++)
    {
        var c = new Campaign();
        c.Money = startMoney ?? c.Money;
        for (int m = 0; m < missions; m++)
        {
            foreach (var a in c.Roster().Where(a => a.Rest > 0).ToList())
            {
                c.TryDoctor(a.Name);
            }
            foreach (var a in c.Roster().Where(a => a.Dead).ToList())
            {
                c.TryRecruit(a.Slot);
            }
            if (shop != null)
            {
                // Vor dem ersten Einsatz kennt die Kampagne ihre Agenten noch nicht, Prepare legt sie an.
                if (c.Agents.Count == 0)
                {
                    c.Prepare(LitMission(), "mission_01");
                }
                int before = c.Money;
                shop(c);
                spent += before - c.Money;
            }
            foreach (var a in c.Roster().Where(a => !a.Dead).OrderBy(a => a.Legs + a.Heart).ToList())
            {
                bool legs = a.Legs <= a.Heart;
                if (c.Money - Campaign.UpgradeCost(legs ? a.Legs : a.Heart) >= Campaign.RecruitCost)
                {
                    c.TryUpgrade(a.Name, legs);
                }
            }
            var mission = LitMission();
            mission.Seed = (run * 31 + m) * 7919;
            c.Prepare(mission, "mission_01");
            shortSquads += mission.Agents.Count < 4 ? 1 : 0;
            var w = new World(Loader.ParseMap(mapJson), mission);
            var strategy = make();
            for (int i = 0; i < MaxTicks && w.State == MissionState.Running; i++)
            {
                if (i % 10 == 0)
                {
                    strategy(w);
                }
                w.Step();
            }
            if (w.State == MissionState.Running)
            {
                c.Abandon(w);
            }
            else
            {
                c.Finish(w);
            }
            played++;
            won += w.State == MissionState.Won ? 1 : 0;
        }
        deaths += c.Agents.Count(a => a.Dead);
        recruits += c.Agents.Count(a => a.Job != "");
        heat += c.Heat;
        money += c.Money;
    }
    Console.WriteLine($"{name,-24} Sieg {100 * won / played,3}%  Tote je Kampagne {(double)deaths / campaigns:0.0}  Rekruten {(double)recruits / campaigns:0.0}  "
        + $"ohne vollen Trupp {100 * shortSquads / played,3}%  Fahndung {(double)heat / campaigns:0.0}  Kasse am Ende {money / campaigns}  Einkauf {spent / campaigns}");
}

// Einkauf zwischen den Einsätzen nach einer Wunschliste je Agent. Gekauft wird nur, was die Kasse über dem Rückhalt hergibt.
// Jeder Eintrag gilt für jeden einsatzbereiten Agenten der Reihe nach, bevor der nächste Eintrag drankommt.
Action<Campaign> Shopper(int reserve, params string[] wishes) => c =>
{
    foreach (string id in wishes)
    {
        var def = catalog.Get(id)!;
        foreach (var a in c.Roster().Where(a => a.Ready))
        {
            if (!a.Items.Contains(id) && c.Money - def.Price >= reserve)
            {
                c.TryBuy(catalog, id, a.Name);
            }
        }
    }
};

// Ein Spieler wechselt die Waffe wie mit E. Er hält die teuerste Schusswaffe, wenn sie so weit reicht wie die stärkste Waffe der meisten
// im Trupp. Reicht sie kürzer, bleibt die weite Waffe in der Hand, bis ein Gegner auf Reichweite plus ein Feld heran ist, sonst stünde der
// Agent beim Bündeln vor den anderen und bekäme das Feuer allein ab. Der Wechsel kostet wie im Spiel 0,5 s.
Func<Action<World>> Armed(Func<Action<World>> make) => () =>
{
    var inner = make();
    static List<(CarriedItem it, int i)> Guns(Unit a) => a.Items.Select((it, i) => (it, i)).Where(p => p.it.Def.Kind == ItemKind.Firearm).ToList();
    return w =>
    {
        inner(w);
        var agents = w.Units.Where(u => u.Faction == Faction.Agent && u.Alive).ToList();
        var reaches = agents.Select(a => Guns(a).OrderByDescending(p => p.it.Def.Price).Select(p => p.it.Def.RangeUnits).DefaultIfEmpty(0).First()).OrderBy(r => r).ToList();
        int squadReach = reaches.Count == 0 ? 0 : reaches[reaches.Count / 2];
        foreach (var a in agents.Where(a => a.BandageTicks == 0))
        {
            var guns = Guns(a);
            if (guns.Count == 0)
            {
                continue;
            }
            var foe = a.TargetId >= 0 && w.Units[a.TargetId].Alive ? w.Units[a.TargetId]
                : w.Units.Where(g => g.Faction == Faction.Guard && g.Alive && g.Alerted && w.CanSee(a, g, 7 * World.TileUnits)).OrderBy(g => World.Distance(a, g)).FirstOrDefault();
            int dist = foe == null ? int.MaxValue : World.Distance(a, foe);
            var best = guns.OrderByDescending(p => p.it.Def.Price).First();
            var far = guns.OrderByDescending(p => p.it.Def.RangeUnits).First();
            bool keepFar = best.it.Def.RangeUnits < squadReach && dist > best.it.Def.RangeUnits + World.TileUnits;
            int pick = keepFar ? far.i : best.i;
            if (a.Weapon != pick && a.ReloadTicks == 0)
            {
                w.SelectWeapon(a.Id, pick);
            }
        }
    };
};

// Ausschalten mit dem Totschläger: Greta geht mit dem Knüppel in der Hand voraus und schlägt Wachen nieder, eine nach der anderen.
// Sie greift an, wenn sie die Wache erreicht, bevor die sich umdreht, und keine andere Wache den Weg im Blick hat. Sonst lauert sie in einem
// Versteck möglichst nah an der Wache. Die anderen warten am Start oder folgen ihr. Sobald eine Wache kämpft, nehmen alle die Pistole und
// bündeln das Feuer. Ist niemand mehr zu schlagen, geht der Trupp zum Buch.
// Werkschutz, Hund und Laufbursche lässt sie aus: Der Werkschutz fällt nicht, der Hund hört sie, der Laufbursche schießt nicht.
// Mit strike false lauert sie nur, zum Vergleich.
Func<Action<World>> Clubbing(bool squadFollows, bool strike = true) => () =>
{
    var focus = Armed(FocusFire)();
    var rush = Rush();
    bool loud = false;
    int target = -1;
    return w =>
    {
        var squad = w.Units.Where(u => u.Faction == Faction.Agent && u.Alive).ToList();
        if (squad.Count == 0)
        {
            return;
        }
        var guards = w.Units.Where(u => u.Faction == Faction.Guard && u.Alive && !u.Bribed).ToList();
        if (!loud && guards.Any(g => g.GuardState == GuardState.Combat || g.GuardState == GuardState.Report))
        {
            loud = true;
            foreach (var a in squad)
            {
                a.TargetId = -1;
            }
        }
        if (loud)
        {
            focus(w);
            return;
        }
        var lead = squad[0];
        var prey = guards.Where(g => g.Type != GuardType.Werkschutz && !g.Type.OrderOnly && g.Type.WeaponRange > 0
            && w.Map.FindPath(lead.TileX, lead.TileY, g.TileX, g.TileY) != null).ToList();
        if (prey.Count == 0)
        {
            rush(w);
            return;
        }
        // Ist sie schon dicht dran, zieht sie den Schlag durch: Umkehren auf offener Straße ist schlimmer.
        if (target >= 0 && w.Units[target].Alive && lead.TargetId == target && World.Distance(lead, w.Units[target]) <= 5 * World.TileUnits / 2)
        {
            return;
        }
        // Sonst jede Wache, die sie vor dem Umdrehen erreicht und zu der der Weg frei ist, die nächste zuerst.
        Unit? free = null;
        foreach (var g in prey.OrderBy(g => World.Distance(lead, g)))
        {
            var way = w.Map.FindPath(lead.TileX, lead.TileY, g.TileX, g.TileY) ?? new List<(int X, int Y)>();
            way.Insert(0, (lead.TileX, lead.TileY));
            if (CatchesBeforeTurn(lead, g) && !guards.Any(o => o.Id != g.Id && way.Any(f => Watches(w, o, f.X, f.Y))))
            {
                free = g;
                break;
            }
        }
        if (free != null && strike)
        {
            target = free.Id;
            if (lead.TargetId != free.Id)
            {
                w.OrderAttack(lead.Id, free.Id);
            }
        }
        else
        {
            if (lead.TargetId >= 0)
            {
                lead.TargetId = -1;
                lead.Path.Clear();
            }
            // Warten nur im Versteck. Liegt eines näher an der Wache, auf die sie es abgesehen hat, schleicht sie dorthin und lauert.
            var near = prey.OrderBy(g => World.Distance(lead, g)).First();
            bool exposed = !SafeSpot(w, guards, lead.TileX, lead.TileY);
            if (lead.Path.Count == 0 && (exposed || w.Tick % 40 == 0))
            {
                var hide = HideNear(w, lead, guards, near);
                if (hide != null)
                {
                    w.OrderMove(lead.Id, hide.Value.X, hide.Value.Y);
                }
            }
        }
        if (squadFollows && squad.Count > 1 && w.Tick % 40 == 0)
        {
            w.OrderGroupMove(squad.Skip(1).Select(a => a.Id).ToList(), lead.TileX, lead.TileY);
        }
    };
};

// Nächstes Versteck über einen Weg, auf dem gerade keine Wache hinsieht. Mit Ziel zählt die Nähe zur Wache dreifach:
// Sie lauert so dicht wie möglich, ohne gesehen zu werden.
(int X, int Y)? HideNear(World w, Unit a, List<Unit> guards, Unit? toward = null)
{
    var watch = Hideouts();
    (int X, int Y)? best = null;
    int bestScore = int.MaxValue;
    for (int y = Math.Max(0, a.TileY - 8); y <= Math.Min(watch.Height - 1, a.TileY + 8); y++)
    {
        for (int x = Math.Max(0, a.TileX - 8); x <= Math.Min(watch.Width - 1, a.TileX + 8); x++)
        {
            if (w.Map.IsBlocked(x, y) || !SafeSpot(w, guards, x, y))
            {
                continue;
            }
            var path = (x, y) == (a.TileX, a.TileY) ? new List<(int X, int Y)>() : w.Map.FindPath(a.TileX, a.TileY, x, y);
            if (path == null || path.Any(f => guards.Any(g => Watches(w, g, f.X, f.Y))))
            {
                continue;
            }
            int score = toward == null ? path.Count : path.Count + 3 * (Math.Abs(x - toward.TileX) + Math.Abs(y - toward.TileY));
            if (score < bestScore)
            {
                best = (x, y);
                bestScore = score;
            }
        }
    }
    return best == (a.TileX, a.TileY) ? null : best;
}

// Ein Versteck sieht gerade keine Wache, und laut Lageplan schaut auch in den nächsten 4 Sekunden keine hin.
bool SafeSpot(World w, List<Unit> guards, int x, int y)
{
    if (guards.Any(g => Watches(w, g, x, y)))
    {
        return false;
    }
    for (int t = w.Tick; t <= w.Tick + 80; t += 5)
    {
        if (Hideouts().IsWatched(x, y, t))
        {
            return false;
        }
    }
    return true;
}

WatchMap? hideouts = null;
WatchMap Hideouts() => hideouts ??= WatchMap.Measure(Loader.ParseMap(mapJson), LitMission());

// Würde die Wache einen Agenten auf diesem Feld bemerken, mit einem Feld Spielraum, weil sie weitergeht? Wie World.WatcherOf.
static bool Watches(World w, Unit g, int tx, int ty)
{
    int x = tx * World.TileUnits + World.TileUnits / 2, y = ty * World.TileUnits + World.TileUnits / 2;
    long dx = x - g.X, dy = y - g.Y;
    int dist = (int)Math.Sqrt(dx * dx + dy * dy) - World.TileUnits;
    int close = Math.Max(World.GuardCloseRange, g.Type.HearRange);
    int notice = w.NoticeRangeAt(x, y);
    if (dist > Math.Max(notice, close) || !w.Map.HasLineOfSight(g.TileX, g.TileY, tx, ty))
    {
        return false;
    }
    long fx = g.FacingX, fy = g.FacingY, dot = fx * dx + fy * dy;
    bool cone = dot > 0 && 2 * dot * dot >= (fx * fx + fy * fy) * (dx * dx + dy * dy);
    return dist <= close || (cone && dist <= notice);
}

// Erreicht der Agent die Wache auf Schlagweite, bevor sie sich umdreht und ihn bemerkt? Eine Wache auf Streife wendet am Ende ihres Wegs.
// Bemerkt sie ihn, bleiben ihr gut 8 Takte, bis sie kämpft. Knapp dahinter zählt das als frei.
static bool CatchesBeforeTurn(Unit a, Unit g)
{
    int gap = Math.Max(0, World.Distance(a, g) - 1100);
    bool away = !Facing(g, a);
    if (!away)
    {
        return gap <= World.TileUnits;
    }
    int turn = int.MaxValue;
    if (g.Path.Count > 0)
    {
        long len = 0;
        int x = g.X, y = g.Y;
        foreach (var (tx, ty) in g.Path)
        {
            int cx = tx * World.TileUnits + World.TileUnits / 2, cy = ty * World.TileUnits + World.TileUnits / 2;
            len += (long)Math.Sqrt((double)(cx - x) * (cx - x) + (double)(cy - y) * (cy - y));
            x = cx;
            y = cy;
        }
        turn = (int)(len / Math.Max(1, World.WalkSpeed(g)));
    }
    int closing = World.WalkSpeed(a) - (g.Path.Count > 0 ? World.WalkSpeed(g) : 0);
    int reach = closing <= 0 ? int.MaxValue : gap / closing;
    return reach <= (turn == int.MaxValue ? int.MaxValue : turn + 8);
}

// Steht der Agent im Blickkegel der Wache? Wie World.InCone, nur ohne Reichweite: Auch von weitem dreht sie sich sonst zu ihm.
static bool Facing(Unit g, Unit o)
{
    long fx = g.FacingX, fy = g.FacingY, dx = o.X - g.X, dy = o.Y - g.Y;
    long dot = fx * dx + fy * dy;
    return dot > 0 && 2 * dot * dot >= (fx * fx + fy * fy) * (dx * dx + dy * dy);
}

// Wer unter die Hälfte fällt, legt den Verband an, sobald ihn keine kämpfende Wache sieht. Solange gibt der Spieler ihm keine Befehle.
Func<Action<World>> Bandaged(Func<Action<World>> make) => () =>
{
    var inner = make();
    return w =>
    {
        foreach (var a in w.Units.Where(u => u.Faction == Faction.Agent && u.Alive && u.Hp * 2 < u.MaxHp && u.BandageTicks == 0))
        {
            bool underFire = w.Units.Any(g => g.Faction == Faction.Guard && g.Alive && g.GuardState == GuardState.Combat && w.CanSee(g, a, 11 * World.TileUnits));
            if (!underFire)
            {
                w.OrderUseMedkit(a.Id);
            }
        }
        var busy = w.Units.Where(u => u.BandageTicks > 0).Select(u => (u, u.BandageTicks)).ToList();
        inner(w);
        foreach (var (u, ticks) in busy)
        {
            // Der Befehl an alle soll den Verband nicht abbrechen.
            if (u.BandageTicks == 0 && u.Alive)
            {
                u.BandageTicks = ticks;
                u.Path.Clear();
                u.TargetId = -1;
            }
        }
    };
};

// ---------- Befreien ----------

// Ein Trupp, der Gefangene holt. Der Schlosser öffnet erst die Tür, die den Weg zu den Zellen sperrt, dann die Zellen,
// bleibt stehen, bis der Gefangene folgt, und führt alle zum Wagen.
// alone: Nur der Schlosser geht, der Rest wartet am Start. fightFirst: Erst alle erreichbaren Wachen ausschalten.
// bribes: Namen der Wachen, die der Buchhalter vorher kauft. plan: Der Schlosser wartet, bis der Lageplan den Weg frei zeigt.
Func<Action<World>> Rescue(bool alone, bool fightFirst, string[]? bribes = null, bool plan = false, string? gate = null, (int X, int Y)? smoke = null, bool focus = false) => () =>
{
    bool smoked = false;
    // Auf der großen Karte reißt „jeder die nächste Wache“ den Trupp auseinander. focus hält ihn zusammen.
    var attack = focus ? FocusFire() : AttackAll();
    WatchMap? watch = plan ? WatchMap.Measure(Loader.ParseMap(mapJson), LitMission()) : null;
    int stage = fightFirst ? 0 : 1, quiet = 0, bribeIndex = 0;
    bool sentHome = false;
    return w =>
    {
        var squad = w.Units.Where(u => u.Faction == Faction.Agent && u.Alive).ToList();
        if (squad.Count == 0)
        {
            return;
        }
        var guards = w.Units.Where(u => u.Faction == Faction.Guard && u.Alive && !u.Bribed).ToList();
        if (smoke is { } cloud && !smoked)
        {
            // Der Rauchtopf fliegt, sobald einer in Wurfweite ist und das Feld sieht.
            smoked = squad.Any(a => w.OrderThrowSmoke(a.Id, cloud.X, cloud.Y));
        }
        if (stage == 0)
        {
            bool near = guards.Any(g => squad.Any(a => World.Distance(a, g) <= 12 * World.TileUnits));
            quiet = near && w.Rating == MissionRating.Loud ? 0 : quiet + 1;
            if (guards.Count == 0 || (w.Rating == MissionRating.Loud && quiet >= 10) || w.Tick > 20 * 90)
            {
                stage = 1;
            }
            else
            {
                attack(w);
                return;
            }
        }
        var emil = squad.Find(a => a.Skill == World.SkillBookkeeper);
        if (bribes != null && bribeIndex < bribes.Length)
        {
            var target = w.Units.Find(u => u.Name == bribes[bribeIndex]);
            if (emil == null || target == null || !target.Alive || target.Bribed || target.GuardState == GuardState.Combat || w.Money < w.BribePrice(target, emil))
            {
                bribeIndex++;
            }
            else if (emil.BribeTarget != target.Id)
            {
                w.OrderBribe(emil.Id, target.Id);
            }
            if (bribeIndex < bribes.Length)
            {
                return;
            }
        }
        var opener = squad.Find(a => a.Skill == World.SkillLocksmith) ?? squad.Find(a => a.Keys.Count > 0) ?? squad[0];
        var captives = w.Prisoners.Where(p => p.Alive && p.Captive).ToList();
        if (captives.Count > 0)
        {
            if (opener.DoorTarget >= 0)
            {
                return;
            }
            var next = captives[0];
            if (next.CellDoor >= 0 && !w.Doors[next.CellDoor].Open)
            {
                // Erst die Zelle. Kommt er nicht hin, sperrt eine andere Tür den Weg: die nächste nehmen, die er erreicht.
                int wanted = gate == null ? -1 : w.Doors.FindIndex(d => d.Name == gate && !d.Open);
                int door = w.OrderOpenDoor(opener.Id, next.CellDoor) ? next.CellDoor
                    : wanted >= 0 && w.OrderOpenDoor(opener.Id, wanted) ? wanted
                    : Enumerable.Range(0, w.Doors.Count).Where(d => !w.Doors[d].Open && w.Doors[d].Name != "Schreibstube")
                        .OrderBy(d => Math.Abs(w.Doors[d].MarkX - opener.X) + Math.Abs(w.Doors[d].MarkY - opener.Y))
                        .FirstOrDefault(d => w.OrderOpenDoor(opener.Id, d), -1);
                if (door >= 0 && watch != null && opener.Path.Count > 0)
                {
                    // Mit Lageplan: Den Befehl zurücknehmen, solange der Weg im Blick liegt.
                    var path = opener.Path.ToList();
                    if (watch.FirstRisk(path, opener.X, opener.Y, World.WalkSpeed(opener), w.Tick, World.DoorTicks) >= 0 && w.Rating == MissionRating.Unseen)
                    {
                        w.OrderMove(opener.Id, opener.TileX, opener.TileY);
                    }
                }
                if (!alone && door >= 0)
                {
                    var rest = squad.Where(a => a != opener && a.Path.Count == 0 && World.Distance(a, opener) > 4 * World.TileUnits).Select(a => a.Id).ToList();
                    if (rest.Count > 0 && opener.Path.Count > 0)
                    {
                        var (tx, ty) = opener.Path[^1];
                        w.OrderGroupMove(rest, tx, ty);
                    }
                }
            }
            // Zelle offen: stehen bleiben, bis er folgt.
            return;
        }
        // Alle frei oder tot: zum Wagen. Die Befreiten folgen dem, der sie geholt hat.
        var (ex, ey) = (w.Exit.X0 / World.TileUnits, (w.Exit.Y0 + w.Exit.Y1) / 2 / World.TileUnits);
        bool openerHome = opener.X >= w.Exit.X0 && opener.X < w.Exit.X1 && opener.Y >= w.Exit.Y0 && opener.Y < w.Exit.Y1;
        if (!sentHome || (!openerHome && opener.Path.Count == 0))
        {
            sentHome = true;
            w.OrderGroupMove(alone ? new List<int> { opener.Id } : squad.Select(a => a.Id).ToList(), ex, ey);
        }
        foreach (var p in w.Prisoners.Where(p => p.Alive && !p.Captive && (p.FollowId < 0 || p.Waiting)))
        {
            w.OrderPrisoner(p.Id, opener.Id);
        }
    };
};

// Laut aus einer Stellung: Der Trupp geht an einen Platz mit Deckung, eröffnet dort das Feuer und lässt die Wachen kommen,
// statt ihnen über die helle Wallstraße nachzulaufen. Ist es eine Weile still, holt er die Gefangenen und schießt auf dem Weg
// nur noch auf Wachen, die er sieht. cut: Auf dem Weg in die Stellung kappt einer den Telefondraht, solange es noch still ist.
Func<Action<World>> LoudFromPost((int X, int Y) post, bool cut = false) => () =>
{
    var rescue = Rescue(false, false)();
    int stage = 0, calm = 0;
    return w =>
    {
        var squad = w.Units.Where(u => u.Faction == Faction.Agent && u.Alive).ToList();
        if (squad.Count == 0)
        {
            return;
        }
        var ids = squad.Select(a => a.Id).ToList();
        var armed = w.Units.Where(u => u.Faction == Faction.Guard && u.Alive && !u.Bribed && !u.Type.OrderOnly).ToList();
        var seen = armed.Where(g => squad.Any(a => w.CanSee(a, g, 10 * World.TileUnits))).ToList();
        int px = post.X * World.TileUnits + World.TileUnits / 2, py = post.Y * World.TileUnits + World.TileUnits / 2;
        long Far(Unit u) => Math.Abs(u.X - px) + Math.Abs(u.Y - py);
        void Fire(List<Unit> targets)
        {
            var target = targets.OrderBy(g => World.Distance(squad[0], g)).First();
            foreach (var a in squad.Where(a => a.DoorTarget < 0 && (a.TargetId < 0 || !w.Units[a.TargetId].Alive || !seen.Contains(w.Units[a.TargetId]))))
            {
                w.OrderAttack(a.Id, target.Id);
            }
        }
        if (stage == 0)
        {
            int wire = cut ? w.Doors.FindIndex(d => d.Wire && !d.Open) : -1;
            if (wire >= 0 && w.Rating != MissionRating.Loud)
            {
                if (squad[^1].DoorTarget != wire)
                {
                    w.OrderOpenDoor(squad[^1].Id, wire);
                }
                return;
            }
            if (w.Rating == MissionRating.Loud || squad.All(a => Far(a) <= 2 * World.TileUnits))
            {
                stage = 1;
            }
            else if (squad.Any(a => a.Path.Count == 0 && Far(a) > 2 * World.TileUnits))
            {
                w.OrderGroupMove(ids, post.X, post.Y);
            }
            return;
        }
        if (stage == 1)
        {
            if (seen.Count > 0)
            {
                calm = 0;
                Fire(seen);
                return;
            }
            if (w.Rating != MissionRating.Loud && armed.Count > 0)
            {
                // Noch still: die nächste Wache angreifen, das weckt die übrigen.
                Fire(armed);
                return;
            }
            var away = squad.Where(a => Far(a) > 2 * World.TileUnits).Select(a => a.Id).ToList();
            if (away.Count > 0)
            {
                foreach (var a in squad.Where(a => a.TargetId >= 0))
                {
                    a.TargetId = -1;
                }
                w.OrderGroupMove(away, post.X, post.Y);
            }
            if (++calm >= 30 || armed.Count == 0)
            {
                stage = 2;
            }
            return;
        }
        if (seen.Count > 0)
        {
            Fire(seen);
            return;
        }
        foreach (var a in squad.Where(a => a.TargetId >= 0))
        {
            a.TargetId = -1;
        }
        rescue(w);
    };
};

// Der Rückweg zum Wagen, wenn der Trupp wieder im dunklen Durchgang steht. Drei Wege und eine Abkürzung:
// "friedhof": Pforte des Totengräbers, über den Friedhof, Südpforte, die dunkle Böschung entlang nach Osten.
// "hoefe": Hoftür der Witwe, Westhof, Hoftür am Osthof, Kellerluke, Kellerhals.
// "gang": ohne Tür den Friedhofsgang hinunter und die Südgasse entlang. "platz": die Wallstraße nach Osten am Exerzierplatz vorbei.
// Der Führer öffnet, was zu ist, und wartet an jeder Station, bis alle aufgeschlossen haben.
// Ohne Angabe gilt der Friedhof: Der Friedhofsgang ist die Abkürzung, auf der die Südstreife den Trupp immer bemerkt.
string homeRoute = args.FirstOrDefault(x => x.StartsWith("--weg="))?["--weg=".Length..] ?? "friedhof";
if (args.FirstOrDefault(x => x.StartsWith("--rueckhalt=")) is { } supportArg)
{
    int support = int.Parse(supportArg["--rueckhalt=".Length..]);
    variant = m => { m.Support = support; m.Helpers = support > 0 ? m.Doors.Select(d => d.Helper).Where(h => h.Length > 0).ToList() : new List<string>(); };
}
args = args.Where(x => !x.StartsWith("--weg=") && !x.StartsWith("--rueckhalt=")).ToArray();
Func<Action<World, Unit, List<Unit>>> HomeRun = () =>
{
    int step = 0, waited = 0;
    bool walking = false;
    return (w, lead, others) =>
    {
        // Der Plan besteht aus Wegstücken. Ein Wegstück sind die Felder bis zur nächsten Tür oder bis zum nächsten Halt ("").
        // Vor jedem Wegstück wartet der Trupp an einer dunklen Stelle, bis der Lageplan es für den Führer und jeden Befreiten
        // ganz frei zeigt. Dann geht er es ohne Halt. Nach 40 s Warten geht er trotzdem.
        object[] plan = homeRoute switch
        {
            "friedhof" => new object[] { (7, 18), "Pforte des Totengräbers", (1, 24), "Südpforte des Friedhofs", (2, 29), (25, 29), (28, 23) },
            "hoefe" => new object[] { (7, 19), "Hoftür der Witwe", (11, 20), "Hoftür am Osthof", (20, 19), "Kellerluke der Kneipe", (20, 25), (28, 23) },
            "platz" => new object[] { (8, 11), (22, 11), (23, 19), (28, 23) },
            _ => new object[] { (7, 21), "", (7, 29), (25, 29), (28, 23) },
        };
        var watch = quietWatch ??= WatchMap.Measure(Loader.ParseMap(mapJson), LitMission());
        foreach (var o in others.Where(o => o.Alive && o.Path.Count == 0 && World.Distance(o, lead) > 2 * World.TileUnits))
        {
            w.OrderMove(o.Id, lead.TileX, lead.TileY);
        }
        if (step >= plan.Length)
        {
            return;
        }
        var freed = w.Prisoners.Where(p => p.Alive && !p.Captive).ToList();
        // Die Felder ab plan[from] bis zur nächsten Tür oder zum nächsten Halt.
        List<(int X, int Y)> Leg(int from)
        {
            var leg = new List<(int X, int Y)>();
            for (int i = from; i < plan.Length && plan[i] is ValueTuple<int, int> t; i++)
            {
                leg.Add(t);
            }
            return leg;
        }
        // Ist der Weg über diese Felder frei, wenn die Figur nach delay Takten bei (sx, sy) losgeht?
        bool Safe(int sx, int sy, List<(int X, int Y)> stops, int delay, int hold)
        {
            var path = new List<(int X, int Y)>();
            var (cx, cy) = (sx, sy);
            foreach (var (x, y) in stops)
            {
                var part = w.Map.FindPath(cx, cy, x, y);
                if (part == null)
                {
                    // Hinter einer Tür, die noch zu ist: Luftlinie, Feld für Feld.
                    part = new List<(int X, int Y)>();
                    for (int px = cx, py = cy; (px, py) != (x, y);)
                    {
                        px += Math.Sign(x - px);
                        py += Math.Sign(y - py);
                        part.Add((px, py));
                    }
                }
                path.AddRange(part);
                (cx, cy) = (x, y);
            }
            return path.Count == 0 || watch.FirstRisk(path, sx * World.TileUnits + 500, sy * World.TileUnits + 500, World.PrisonerSpeed, w.Tick + delay, hold) < 0;
        }
        bool Free(List<(int X, int Y)> stops, int delay, int hold, List<(int X, int Y)>? after = null)
        {
            bool free = waited > 80 || w.Rating == MissionRating.Loud
                || (Safe(lead.TileX, lead.TileY, stops, delay, hold + 30) && freed.All(p => Safe(p.TileX, p.TileY, stops, delay + 15, hold + 30))
                    && (after == null || Safe(lead.TileX, lead.TileY, after, delay + hold, 30)));
            if (!free && waited == 40 && Environment.GetEnvironmentVariable("MS_WARUM") != null)
            {
                Console.WriteLine($"       wartet bei Schritt {step}: Führer {Safe(lead.TileX, lead.TileY, stops, delay, hold + 30)}, Befreite {string.Join(",", freed.Select(p => Safe(p.TileX, p.TileY, stops, delay + 15, hold + 30)))}, danach {after == null || Safe(lead.TileX, lead.TileY, after, delay + hold, 30)}, Ziel {string.Join(" ", stops)}");
            }
            waited = free ? 0 : waited + 1;
            return free;
        }
        if (plan[step] is string doorName)
        {
            if (doorName.Length == 0)
            {
                step++;
                return;
            }
            int door = w.Doors.FindIndex(d => d.Name == doorName);
            if (w.Doors[door].Open)
            {
                step++;
                return;
            }
            // Vor dem Öffnen prüfen: die Marke für die Dauer des Öffnens und das Wegstück dahinter.
            var mark = ((int)(w.Doors[door].MarkX / World.TileUnits), (int)(w.Doors[door].MarkY / World.TileUnits));
            var beyond = new List<(int X, int Y)> { mark };
            beyond.AddRange(Leg(step + 1));
            bool gathered = freed.All(p => p.Path.Count == 0 || World.Distance(p, lead) <= 3 * World.TileUnits);
            if (lead.DoorTarget < 0 && gathered && Free(new List<(int X, int Y)> { mark }, 0, World.DoorTicks, beyond))
            {
                w.OrderOpenDoor(lead.Id, door);
            }
            return;
        }
        var (tx, ty) = ((int, int))plan[step];
        if (lead.Path.Count == 0 && (lead.TileX, lead.TileY) == (tx, ty))
        {
            step++;
            // Am Ende eines Wegstücks wieder sammeln und warten.
            walking = step < plan.Length && plan[step] is ValueTuple<int, int>;
            return;
        }
        if (lead.Path.Count > 0 && lead.Path[^1] == (tx, ty))
        {
            return;
        }
        bool together = freed.All(p => p.Path.Count == 0 || World.Distance(p, lead) <= 4 * World.TileUnits);
        if (walking || (together && Free(Leg(step), 0, 0)))
        {
            walking = true;
            w.OrderMove(lead.Id, tx, ty);
        }
    };
};

// Der leise Weg von Einsatz 2, wie ihn die Vorlage beschreibt: Otto und Emil warten im Durchgang, queren die Wallstraße im Fenster
// des Lageplans, Emil kauft Hundeführer und Schließer, beide öffnen je eine Zelle und führen die Gefangenen im nächsten Fenster zurück.
// lag: Der Klick kommt zufällig bis zu so viele Takte zu spät.
int quietRun = 0;
int passShown = 0;
Func<Action<World>> QuietWithMoney(int lag, string[]? club = null, bool withGreta = false, bool gretaWatches = false) => () =>
{
    var watch = quietWatch ??= WatchMap.Measure(Loader.ParseMap(mapJson), LitMission());
    // Jeder Lauf verspätet sich anders, sonst gäbe es nur ganz oder gar nicht.
    var rng = new Random(1927 + quietRun++);
    int stage = 0, goAt = -1, lastStage = -1;
    bool gretaHome = false;
    var home = HomeRun();
    return w =>
    {
        var otto = w.Units.Find(u => u.Skill == World.SkillLocksmith && u.Alive);
        var emil = w.Units.Find(u => u.Skill == World.SkillBookkeeper && u.Alive);
        if (otto == null || emil == null)
        {
            return;
        }
        Unit Guard(string name) => w.Units.First(u => u.Name == name);
        var handler = Guard("Hundeführer");
        var keeper = Guard("Schließer");
        int Door(string name) => w.Doors.FindIndex(d => d.Name == name);
        bool At(Unit u, int x, int y) => u.Path.Count == 0 && u.TileX == x && u.TileY == y;
        // Ist die Straße zwischen zwei Feldern frei, wenn man jetzt losgeht? Geprüft wird nur das Stück auf der Wallstraße.
        bool StreetClear(Unit u, int x, int y, int speed, int delay = 20)
        {
            var path = w.Map.FindPath(u.TileX, u.TileY, x, y);
            if (path == null)
            {
                return false;
            }
            var street = path.Where(t => t.Y >= 9 && t.Y <= 13).ToList();
            return street.Count == 0 || watch.FirstRisk(street, street[0].X * 1000 + 500, street[0].Y * 1000 + 500, speed, w.Tick + delay, 0) < 0;
        }
        bool Go(bool clear)
        {
            if (clear && goAt < 0)
            {
                goAt = w.Tick + rng.Next(0, lag + 1);
            }
            if (goAt >= 0 && w.Tick >= goAt)
            {
                goAt = -1;
                return true;
            }
            return false;
        }
        // Greta geht mit, damit die Wachen den halben Preis nehmen: immer ein paar Felder hinter Emil, nie im Hof.
        var greta = withGreta ? w.Units.Find(u => u.Skill == World.SkillLeader && u.Alive) : null;
        if (gretaWatches)
        {
            // Greta quert nicht. Sie rückt im dunklen Durchgang auf Emils Platz nach und sieht von dort die Gasse hinauf.
            var watcher = w.Units.Find(u => u.Skill == World.SkillLeader && u.Alive);
            if (watcher != null && stage >= 2 && !gretaHome)
            {
                gretaHome = true;
                w.OrderMove(watcher.Id, 6, 15);
            }
            if (watcher != null && stage == 2 && !handler.Bribed && handler.Alive && handler.GuardState == GuardState.Patrol && !w.LeaderBacks(emil, handler))
            {
                // Mit dem Geld warten, bis der Hundeführer so weit die Gasse herunter ist, dass Greta ihn sieht.
                return;
            }
        }
        void PlaceGreta()
        {
            if (greta != null && stage != lastStage && stage < 6)
            {
                var (gx, gy) = stage switch { 0 => (7, 16), 1 => (7, 16), 2 => (5, 8), 3 => (5, 8), _ => (5, 7) };
                w.OrderMove(greta.Id, gx, gy);
            }
            // Zurück geht sie für sich, in ihrem eigenen Fenster: Sie ist schneller als die Befreiten und stünde sonst zu früh auf der Straße.
            if (greta != null && stage >= 6 && !gretaHome && greta.Path.Count == 0 && StreetClear(greta, 7, 16, World.WalkSpeed(greta)))
            {
                gretaHome = true;
                w.OrderMove(greta.Id, 7, 16);
            }
            lastStage = stage;
        }
        if (greta != null && stage == 1 && greta.Path.Count > 0)
        {
            return;
        }
        switch (stage)
        {
            case 0:
                w.OrderMove(otto.Id, 7, 15);
                w.OrderMove(emil.Id, 6, 15);
                stage = 1;
                break;
            case 1:
                if (At(otto, 7, 15) && At(emil, 6, 15) && Go(StreetClear(emil, 6, 7, World.WalkSpeed(emil)) && (greta == null || StreetClear(greta, 5, 8, World.WalkSpeed(greta)))))
                {
                    // In die dunkle Gasse, nicht an ihren hellen Ausgang: Der Hundeführer ist auf seinem Rundgang nicht immer gleich da.
                    w.OrderMove(emil.Id, 6, 7);
                    w.OrderMove(otto.Id, 6, 8);
                    stage = 2;
                }
                break;
            case 2:
                bool handlerNear = handler.TileX >= 5 && World.Distance(emil, handler) <= 6 * World.TileUnits;
                if (emil.TileY <= 9 && handlerNear && emil.BribeTarget != handler.Id && !handler.Bribed && handler.Alive)
                {
                    w.OrderBribe(emil.Id, handler.Id);
                }
                if (handler.Bribed || !handler.Alive || handler.GuardState == GuardState.Combat)
                {
                    w.OrderOpenDoor(otto.Id, Door("Pforte zur Gerichtsgasse"));
                    stage = 3;
                }
                break;
            case 3:
                if (w.Doors[Door("Pforte zur Gerichtsgasse")].Open && YardDone(w, otto, emil, club, true))
                {
                    stage = 4;
                }
                break;
            case 4:
                bool ottoBusy = false;
                foreach (var p in w.Prisoners.Where(p => p.Alive && p.Captive))
                {
                    var helper = p.Name.StartsWith("Paul") && emil.Keys.Contains(p.CellDoor) ? emil : otto;
                    if (helper == otto && ottoBusy)
                    {
                        // Erst wenn der erste frei ist, geht Otto zur nächsten Zelle.
                        continue;
                    }
                    ottoBusy |= helper == otto;
                    if (!w.Doors[p.CellDoor].Open && helper.DoorTarget < 0)
                    {
                        w.OrderOpenDoor(helper.Id, p.CellDoor);
                    }
                }
                if (!w.Prisoners.Any(p => p.Alive && p.Captive))
                {
                    // Tief in der dunklen Gasse warten: Am Ausgang sähe sie die Wallstreife.
                    w.OrderMove(otto.Id, 5, 6);
                    w.OrderMove(emil.Id, 6, 6);
                    stage = 5;
                }
                break;
            case 5:
                bool gathered = w.Prisoners.Where(p => p.Alive).All(p => p.TileX <= 7 && p.Path.Count == 0 && World.Distance(p, w.Units[p.FollowId < 0 ? otto.Id : p.FollowId]) <= 2500);
                if (At(otto, 5, 6) && At(emil, 6, 6) && gathered && Go(StreetClear(emil, 7, 15, World.PrisonerSpeed)
                    && w.Prisoners.Where(p => p.Alive).All(p => StreetClear(p, 7, 15, World.PrisonerSpeed) && StreetClear(p, 6, 15, World.PrisonerSpeed, 40))))
                {
                    // Zurück gleich tiefer in den Durchgang, damit die Befreiten nicht an seinem hellen Ende stehen bleiben.
                    w.OrderMove(emil.Id, 6, 17);
                    w.OrderMove(otto.Id, 7, 17);
                    stage = 6;
                }
                break;
            case 6:
                if (At(otto, 7, 17) && At(emil, 6, 17) && w.Prisoners.Where(p => p.Alive).All(p => p.TileY >= 14))
                {
                    stage = 7;
                }
                break;
            case 7:
                home(w, otto, w.Units.Where(u => u.Faction == Faction.Agent && u.Alive && u != otto && (u == emil || u == greta)).ToList());
                break;
        }
        // Im selben Takt wie die anderen, sonst läuft sie eine halbe Sekunde hinterher.
        PlaceGreta();
    };
};

// Der leise Weg ohne Geld: Otto allein mit Totschläger. Er passt Hund und Streifen nach dem Lageplan ab, öffnet die Pforte,
// schlägt den Schließer nieder, holt beide und wartet im Hof auf das Fenster für den Rückweg.
// Mit escort wartet die Truppführerin am Wartepunkt vor der Pforte und geht am Ende zurück. Mit pass zeigt sie den Passierschein
// der ersten einfachen Wache, die jemanden bemerkt: so, wie ein Spieler das Papier als Rettung aus der Hand zieht.
// Mit ahead zeigt sie es vorab der Wallstreife 1, sobald die nah genug ist, und Otto geht los, solange die Wache wegschaut.
Func<Action<World>> QuietLocksmith(int lag, string[]? buy = null, bool escort = false, bool pass = false, bool ahead = false) => () =>
{
    var watch = quietWatch ??= WatchMap.Measure(Loader.ParseMap(mapJson), LitMission());
    var rng = new Random(4711 + quietRun++);
    int stage = 0, goAt = -1;
    bool gretaHome = false;
    var home = HomeRun();
    return w =>
    {
        var otto = w.Units.Find(u => u.Skill == World.SkillLocksmith && u.Alive);
        if (otto == null)
        {
            return;
        }
        int pforte = w.Doors.FindIndex(d => d.Name == "Pforte zur Gerichtsgasse");
        var emil = buy == null ? null : w.Units.Find(u => u.Skill == World.SkillBookkeeper && u.Alive);
        var greta = escort ? w.Units.Find(u => World.CarriesPass(u) && u.Alive) : null;
        if (greta != null)
        {
            if (stage == 0)
            {
                w.OrderMove(greta.Id, 6, 15);
            }
            else if (stage == 7 && !gretaHome && !greta.ShowsPass)
            {
                gretaHome = w.OrderMove(greta.Id, 9, 29);
            }
            if (pass && w.PassAvailable && !greta.ShowsPass)
            {
                var threat = ahead
                    ? w.Units.Find(g => stage == 1 && g.Name == "Wallstreife 1" && otto.Path.Count == 0 && greta.Path.Count == 0
                        && World.Distance(g, greta) <= 7 * World.TileUnits && w.PassCheck(greta, g) == PassAnswer.Works)
                    : w.Units.Where(g => g.Alive && g.Faction == Faction.Guard && g.GuardState == GuardState.Noticing
                    && w.PassCheck(greta, g) == PassAnswer.Works).OrderBy(g => World.Distance(g, greta)).FirstOrDefault();
                if (threat != null && w.OrderShowPass(greta.Id, threat.Id))
                {
                    passShown++;
                }
            }
        }
        bool At(int x, int y) => otto.Path.Count == 0 && otto.TileX == x && otto.TileY == y;
        bool Clear(int x, int y, int speed, int hold)
        {
            var path = w.Map.FindPath(otto.TileX, otto.TileY, x, y);
            return path != null && watch.FirstRisk(path, otto.X, otto.Y, speed, w.Tick, hold) < 0;
        }
        bool Go(bool clear)
        {
            if (clear && goAt < 0)
            {
                goAt = w.Tick + rng.Next(0, lag + 1);
            }
            if (goAt >= 0 && w.Tick >= goAt)
            {
                goAt = -1;
                return true;
            }
            return false;
        }
        switch (stage)
        {
            case 0:
                w.OrderMove(otto.Id, 7, 15);
                if (emil != null)
                {
                    w.OrderMove(emil.Id, 6, 15);
                }
                stage = 1;
                break;
            case 1:
                int doorTicks = w.HasItem(otto, ItemKind.Lockpick) ? World.DoorTicks / 2 : World.DoorTicks;
                if (At(7, 15) && (emil == null || emil.Path.Count == 0) && Go(Clear(6, 7, World.WalkSpeed(otto), doorTicks + (emil == null ? 10 : 40)) || (ahead && w.Pass == PassState.Away)))
                {
                    w.OrderOpenDoor(otto.Id, pforte);
                    if (emil != null)
                    {
                        w.OrderMove(emil.Id, 6, 8);
                    }
                    stage = 2;
                }
                break;
            case 2:
                if (w.Doors[pforte].Open)
                {
                    if (emil != null)
                    {
                        // Beide aus der Gasse in den Hof, ehe der Hund zurückkommt. Emil geht gleich zum Schließer.
                        w.OrderMove(otto.Id, 8, 6);
                    }
                    stage = 3;
                }
                break;
            case 3:
                if (YardDone(w, otto, emil, buy, false))
                {
                    stage = 4;
                }
                break;
            case 4:
                var captive = w.Prisoners.FirstOrDefault(p => p.Alive && p.Captive);
                if (captive == null)
                {
                    w.OrderMove(otto.Id, 8, 7);
                    if (emil != null)
                    {
                        w.OrderMove(emil.Id, 9, 7);
                    }
                    stage = 5;
                }
                else if (!w.Doors[captive.CellDoor].Open && otto.DoorTarget < 0)
                {
                    w.OrderOpenDoor(otto.Id, captive.CellDoor);
                }
                break;
            case 5:
                if (At(8, 7) && w.Prisoners.Where(p => p.Alive).All(p => p.Path.Count == 0) && Go(Clear(7, 17, World.PrisonerSpeed, 0)))
                {
                    w.OrderMove(otto.Id, 7, 17);
                    if (emil != null)
                    {
                        w.OrderMove(emil.Id, 6, 15);
                    }
                    stage = 6;
                }
                break;
            case 6:
                if (At(7, 17) && w.Prisoners.Where(p => p.Alive).All(p => p.TileY >= 14))
                {
                    stage = 7;
                }
                break;
            case 7:
                home(w, otto, emil == null ? new List<Unit>() : new List<Unit> { emil });
                break;
        }
    };
};

// Die Posten im Hof der Reihe nach: Wer in names steht, wird gekauft (namesAreBought false) oder niedergeschlagen (true),
// die übrigen bekommen das jeweils andere. True, sobald keiner mehr im Weg ist.
static bool YardDone(World w, Unit otto, Unit? emil, string[]? names, bool namesAreClubbed)
{
    foreach (var g in w.Units.Where(u => u.Faction == Faction.Guard && (u.Name == "Schließer" || u.Name == "Hofposten")))
    {
        if (g.Bribed || !g.Alive)
        {
            continue;
        }
        bool named = names != null && names.Contains(g.Name);
        bool clubbed = named == namesAreClubbed;
        if (!clubbed && g.GuardState == GuardState.Combat)
        {
            // Wer schon schießt, nimmt kein Geld mehr.
            continue;
        }
        if (!clubbed && (emil == null || w.Money < w.BribePrice(g, emil)))
        {
            // Kein Geld mehr: Er bleibt, wie er ist.
            continue;
        }
        if (clubbed)
        {
            int slot = otto.Items.FindIndex(i => i.Def.Kind == ItemKind.Melee);
            if (slot >= 0 && otto.Weapon != slot)
            {
                w.SelectWeapon(otto.Id, slot);
            }
            if (otto.TargetId != g.Id)
            {
                w.OrderAttack(otto.Id, g.Id);
            }
        }
        else if (emil.BribeTarget != g.Id)
        {
            w.OrderBribe(emil.Id, g.Id);
        }
        return false;
    }
    return true;
}

// Erst der leise Plan. Fliegt er auf, kämpft der ganze Trupp und befreit danach, wie ein Spieler mit einer Waffe als Absicherung.
Func<Action<World>> QuietThenFight(Func<Action<World>> quiet) => () =>
{
    var plan = quiet();
    Action<World>? fight = null;
    return w =>
    {
        if (fight == null && w.Rating == MissionRating.Loud)
        {
            fight = Rescue(false, true)();
        }
        (fight ?? plan)(w);
    };
};

void ReportRescue(string name, Func<Action<World>> make, int money = 600)
{
    int wins = 0, dead = 0, saved = 0, ticks = 0, unseen = 0, quietWins = 0, loud = 0, all = 0, paid = 0;
    for (int seed = 1; seed <= Runs; seed++)
    {
        var mission = LitMission();
        mission.Seed = seed * 7919;
        mission.Money = money;
        var w = new World(Loader.ParseMap(mapJson), mission);
        tune?.Invoke(w);
        var strategy = make();
        for (int i = 0; i < MaxTicks && w.State == MissionState.Running; i++)
        {
            if (i % 10 == 0)
            {
                strategy(w);
            }
            w.Step();
        }
        bool won = w.State == MissionState.Won;
        paid += money - w.Money;
        wins += won ? 1 : 0;
        dead += w.Units.Count(u => u.Faction == Faction.Agent && !u.Alive);
        saved += won ? w.Prisoners.Count(p => p.Alive) : 0;
        all += won && w.Prisoners.All(p => p.Alive) ? 1 : 0;
        ticks += w.Tick;
        unseen += won && w.Rating == MissionRating.Unseen ? 1 : 0;
        quietWins += won && w.Rating == MissionRating.Quiet ? 1 : 0;
        loud += w.Rating == MissionRating.Loud ? 1 : 0;
    }
    Console.WriteLine($"{name,-34} Sieg {100 * wins / Runs,3}%  beide gerettet {100 * all / Runs,3}%  tote Agenten {(double)dead / Runs:0.00}  unentdeckt {100 * unseen / Runs,3}%  leise {100 * quietWins / Runs,3}%  laut {100 * loud / Runs,3}%  Dauer {ticks / Runs / 20,3}s  bezahlt {paid / Runs,3}");
}

// Die drei Rückwege und die Abkürzung durch den Friedhofsgang: --wege. Mit zugesperrten Türen (Rückhalt 0) und mit offenen Helfertüren (Rückhalt 5).
// Der Passierschein der Truppführerin: --passierschein. Leise ohne Geld mit Otto allein, mit Greta als Begleitung ohne Papier
// und mit Papier, je mit genauem und verspätetem Klick, dazu kürzere Dauern des Wegschauens.
if (args.Length > 0 && args[0] == "--passierschein")
{
    var p0 = new[] { "pistole" };
    Console.WriteLine($"{missionId}, je {Runs} Läufe, Kasse 0");
    foreach (var (label, kit) in new[] { ("Totschläger", new[] { "pistole", "totschlaeger" }), ("Totschl. + Dietrich", new[] { "pistole", "totschlaeger", "dietrich" }) })
    {
        loadout = new[] { p0, kit, p0, p0 };
        foreach (int lag in new[] { 0, 10, 20 })
        {
            string late = $"{label}, {lag / 20.0:0.0#} s";
            ReportRescue($"Otto allein, {late}", QuietLocksmith(lag), 0);
            ReportRescue($"  mit Greta, ohne Papier", QuietLocksmith(lag, escort: true), 0);
            foreach (int seconds in new[] { 20, 10 })
            {
                tune = w => w.PassAwaySeconds = seconds;
                passShown = 0;
                ReportRescue($"  mit Greta, Papier {seconds} s", QuietLocksmith(lag, escort: true, pass: true), 0);
                Console.WriteLine($"    gezeigt in {100 * passShown / Runs}% der Läufe");
                passShown = 0;
                ReportRescue($"  Papier vorab, {seconds} s", QuietLocksmith(lag, escort: true, pass: true, ahead: true), 0);
                Console.WriteLine($"    gezeigt in {100 * passShown / Runs}% der Läufe");
            }
            tune = null;
        }
    }
    loadout = null;
    return;
}

if (args.Length > 0 && (args[0] == "--wege" || args[0] == "--laut"))
{
    bool loudOnly = args[0] == "--laut";
    var p0 = new[] { "pistole" };
    foreach (int support in new[] { 0, 5 })
    {
        variant = m => { m.Support = support; m.Helpers = support > 0 ? m.Doors.Select(d => d.Helper).Where(h => h.Length > 0).ToList() : new List<string>(); };
        quietWatch = null;
        Console.WriteLine($"== Rückhalt {support} ({missionId}, je {Runs} Läufe, Kasse 600)");
        foreach (string route in loudOnly ? Array.Empty<string>() : new[] { "gang", "friedhof", "hoefe" })
        {
            homeRoute = route;
            foreach (int lag in new[] { 0, 10, 20 })
            {
                loadout = null;
                ReportRescue($"{route}, Otto und Emil mit Geld, {lag / 20.0:0.0#} s", QuietWithMoney(lag));
            }
            loadout = new[] { p0, new[] { "pistole", "totschlaeger", "dietrich" }, p0, p0 };
            foreach (int lag in new[] { 0, 10 })
            {
                ReportRescue($"{route}, Otto allein mit Dietrich, {lag / 20.0:0.0#} s", QuietLocksmith(lag), 0);
            }
        }
        loadout = null;
        if (support == 0)
        {
            ReportRescue("Laut, jeder die nächste Wache, Pistolen", Rescue(false, true));
            ReportRescue("Laut, Trupp zusammen, Pistolen", Rescue(false, true, focus: true));
            ReportRescue("Laut, zusammen durchs Hoftor, Pistolen", Rescue(false, true, gate: "Hoftor", focus: true));
            loadout = new[] { new[] { "maschinenpistole" }, p0, p0, p0 };
            ReportRescue("Laut, Trupp zusammen, eine MP", Rescue(false, true, focus: true));
            loadout = new[] { new[] { "schrotflinte", "verbandszeug" }, new[] { "pistole", "rauchtopf" }, p0, new[] { "pistole", "verbandszeug" } };
            ReportRescue("Laut, zusammen, Schrot + Verbände", Rescue(false, true, focus: true));
            loadout = new[] { new[] { "maschinenpistole" } };
            ReportRescue("Laut, Trupp zusammen, vier MP", Rescue(false, true, focus: true));
            loadout = null;
        }
    }
    variant = null;
    return;
}

// ---------- Einsatz 3, Güterbahnhof: Sabotieren ----------

// Ein Plan für den Bahnhof: Jeder Agent hat eine eigene Liste von Schritten und arbeitet sie für sich ab.
// geh: Weg nach Lageplan, wartet an Ort und Stelle, bis er frei ist, höchstens 30 s. lauf: ohne Warten. kauf, schlag: eine Wache.
// tuer, ziel: Tür öffnen, Sabotageziel. rauch: Rauchtopf auf ein Feld. bought: Diese Wachen kauft der Plan, danach zählt der Lageplan sie nicht mehr.
Func<Action<World>> YardPlan(Dictionary<string, (string What, int X, int Y, string Name)[]> plan, int lag, string[]? bought = null) => () =>
{
    var full = WatchMap.Measure(Loader.ParseMap(mapJson), LitMission());
    WatchMap? reduced = null;
    if (bought != null)
    {
        var m = LitMission();
        m.Guards.RemoveAll(g => bought.Contains(g.Name) || bought.Contains(g.Handler));
        reduced = WatchMap.Measure(Loader.ParseMap(mapJson), m);
    }
    var rng = new Random(1927 + quietRun++);
    var at = plan.Keys.ToDictionary(k => k, _ => 0);
    var since = plan.Keys.ToDictionary(k => k, _ => 0);
    var goAt = plan.Keys.ToDictionary(k => k, _ => -1);
    bool trace = Environment.GetEnvironmentVariable("MS_SPUR") != null;
    var noted = new HashSet<string>();
    var without = new Dictionary<string, WatchMap>();
    var homes = new Dictionary<string, (int X, int Y)>();
    int lastPhase = -1;
    return w =>
    {
        if (trace)
        {
            foreach (var g in w.Units.Where(u => u.Faction == Faction.Guard && u.Alive && (u.NoticeTicks > 0 || u.GuardState != GuardState.Patrol) && noted.Add(u.Name + u.GuardState)))
            {
                Console.WriteLine($"  {w.Tick / 20.0,5:0.0}s {g.Name} {g.GuardState} bei {g.TileX},{g.TileY}  Trupp: {string.Join(" ", w.Units.Where(u => u.Faction == Faction.Agent && u.Alive).Select(u => $"{u.Name[..2]}{u.TileX},{u.TileY}"))}");
            }
        }
        bool allBought = bought != null && bought.All(n => w.Units.Any(u => u.Name == n && (u.Bribed || !u.Alive)));
        var watch = allBought ? reduced! : full;
        foreach (var (who, steps) in plan)
        {
            if (steps.Length == 0)
            {
                continue;
            }
            var a = w.Units.Find(u => u.Name == who && u.Faction == Faction.Agent);
            if (a == null || !a.Alive || at[who] >= steps.Length)
            {
                continue;
            }
            var (what, x, y, name) = steps[at[who]];
            since[who]++;
            void Next()
            {
                if (trace)
                {
                    Console.WriteLine($"  {w.Tick / 20.0,5:0.0}s {who} fertig: {what} {name} {x},{y}  bei {a.TileX},{a.TileY}  {w.Rating}");
                }
                at[who]++;
                since[who] = 0;
                goAt[who] = -1;
            }
            var guard = w.Units.Find(u => u.Name == name && u.Faction == Faction.Guard);
            bool ClearTo(Unit g)
            {
                if (!without.TryGetValue(g.Name, out var alone))
                {
                    var m = LitMission();
                    m.Guards.RemoveAll(x => x.Name == g.Name || (bought != null && bought.Contains(x.Name) && w.Units.Any(u => u.Name == x.Name && u.Bribed)));
                    without[g.Name] = alone = WatchMap.Measure(Loader.ParseMap(mapJson), m);
                }
                var way = w.Map.FindPath(a.TileX, a.TileY, g.TileX, g.TileY + 1);
                return way != null && alone.FirstRisk(way, a.X, a.Y, World.WalkSpeed(a), w.Tick + lag, 60) < 0;
            }
            switch (what)
            {
                case "geh":
                case "lauf":
                    if (a.TileX == x && a.TileY == y && a.Path.Count == 0)
                    {
                        Next();
                        break;
                    }
                    if (a.Path.Count > 0 && a.Path[^1] == (x, y))
                    {
                        break;
                    }
                    var path = w.Map.FindPath(a.TileX, a.TileY, x, y);
                    if (path == null)
                    {
                        Next();
                        break;
                    }
                    // Das Feld, auf dem er steht, zählt mit: Auf Schotter ist schon der erste Schritt lauter als das Stehen.
                    var whole = new List<(int X, int Y)> { (a.TileX, a.TileY) };
                    whole.AddRange(path);
                    // "+zx,zy,takte,ohne,vx,vy": Er geht erst, wenn auch ein späterer Weg frei sein wird, der Rückweg von vx, vy nach zx, zy,
                    // Beginn in "takte" Takten. Die Wache "ohne" zählt dabei nicht, sie steht dann im Rauch. So passt ein Spieler den
                    // Rauchtopf ab: hinein, wenn er auch wieder herauskommt.
                    bool laterFree = true;
                    if (name.StartsWith('+'))
                    {
                        var f = name[1..].Split(',');
                        if (!without.TryGetValue(f[3], out var blindMap))
                        {
                            var m = LitMission();
                            m.Guards.RemoveAll(g => g.Name == f[3]);
                            without[f[3]] = blindMap = WatchMap.Measure(Loader.ParseMap(mapJson), m);
                        }
                        var back = w.Map.FindPath(int.Parse(f[4]), int.Parse(f[5]), int.Parse(f[0]), int.Parse(f[1]));
                        int begin = w.Tick + int.Parse(f[2]) + lag;
                        int bx = int.Parse(f[4]) * 1000 + 500, by = int.Parse(f[5]) * 1000 + 500;
                        // Im Rauch kann er ein paar Sekunden auf den Rückweg warten: Es reicht, wenn er in dieser Spanne einmal sicher frei ist.
                        laterFree = back != null && new[] { -30, -15, 0, 15 }.Any(d => new[] { 0, 8, 16 }.All(e => blindMap.FirstRisk(back, bx, by, World.WalkSpeed(a), begin + d + e, 20) < 0));
                    }
                    // Steht eine Zahl im Namen, wartet er höchstens so viele halbe Sekunden, etwa im Rauch.
                    // "~ohne,geduld": Der Weg wird ohne diese Wache geprüft, sie steht im Rauch.
                    var legWatch = watch;
                    int tilde = 0;
                    if (name.StartsWith('~'))
                    {
                        var f = name[1..].Split(',');
                        tilde = int.Parse(f[1]);
                        if (!without.TryGetValue(f[0], out var smoked))
                        {
                            var m = LitMission();
                            m.Guards.RemoveAll(g => g.Name == f[0]);
                            without[f[0]] = smoked = WatchMap.Measure(Loader.ParseMap(mapJson), m);
                        }
                        legWatch = smoked;
                    }
                    bool free = what == "lauf" || w.Rating == MissionRating.Loud || since[who] > (tilde > 0 ? tilde : int.TryParse(name, out int patience) ? patience : name.StartsWith('+') ? 240 : 80)
                        || (laterFree && legWatch.FirstRisk(whole, a.X, a.Y, World.WalkSpeed(a), w.Tick + lag, 20) < 0
                            && legWatch.FirstRisk(whole, a.X, a.Y, World.WalkSpeed(a), w.Tick + lag + 10, 20) < 0);
                    if (free && goAt[who] < 0)
                    {
                        goAt[who] = w.Tick + (lag > 0 ? rng.Next(lag + 1) : 0);
                    }
                    if (goAt[who] >= 0 && w.Tick >= goAt[who])
                    {
                        w.OrderMove(a.Id, x, y);
                    }
                    break;
                case "kauf":
                    if (guard == null || guard.Bribed || !guard.Alive || guard.GuardState == GuardState.Combat || w.Money < w.BribePrice(guard, a) || since[who] > 60)
                    {
                        Next();
                    }
                    else if (a.BribeTarget != guard.Id && (x == 0 || World.Distance(a, guard) <= x * World.TileUnits) && (y == 0 || ClearTo(guard)))
                    {
                        // x ist die Entfernung, ab der er losgeht: Er läuft der Wache nicht über den halben Bahnhof nach.
                        // y = 1: erst, wenn außer dieser Wache niemand den Weg zu ihr sieht.
                        w.OrderBribe(a.Id, guard.Id);
                    }
                    else if (a.BribeTarget != guard.Id)
                    {
                        since[who] = 0;
                    }
                    break;
                case "schlag":
                    if (guard == null || !guard.Alive || since[who] > 100)
                    {
                        // Nach dem Schlag nicht auf dem Feld des Postens stehen bleiben, sondern zurück auf das geprüfte Feld hinter ihm.
                        if (x > 0 && guard != null && !guard.Alive)
                        {
                            w.OrderMove(a.Id, x, y);
                        }
                        Next();
                        break;
                    }
                    if (a.TargetId != guard.Id && w.Rating != MissionRating.Loud && since[who] <= 60)
                    {
                        // Erst losgehen, wenn außer dem Opfer niemand den Weg zu ihm und die Arbeit danach sieht (Schlag und 4 s an der Weiche,
                        // zusammen 7 s). x, y ist das Feld hinter ihm. Danach arbeitet sie sofort, ohne noch einmal im Licht zu warten.
                        if (!without.TryGetValue(name, out var alone))
                        {
                            var m = LitMission();
                            m.Guards.RemoveAll(g => g.Name == name);
                            without[name] = alone = WatchMap.Measure(Loader.ParseMap(mapJson), m);
                        }
                        var behind = w.Map.FindPath(a.TileX, a.TileY, x, y);
                        // Mit Luft: Das Fenster muss auch noch eine Sekunde später offen sein, und der Posten selbst steht einen Schritt daneben.
                        if (behind == null || new[] { 0, 10, 20 }.Any(e => alone.FirstRisk(behind, a.X, a.Y, World.WalkSpeed(a), w.Tick + lag + e, 140) >= 0)
                            || new[] { 0, 10, 20 }.Any(e => alone.IsWatched(guard.TileX, guard.TileY + 1, w.Tick + lag + 40 + e)))
                        {
                            break;
                        }
                    }
                    int slot = a.Items.FindIndex(i => i.Def.Kind == ItemKind.Melee);
                    if (slot >= 0 && a.Weapon != slot)
                    {
                        w.SelectWeapon(a.Id, slot);
                    }
                    if (a.TargetId != guard.Id)
                    {
                        w.OrderAttack(a.Id, guard.Id);
                    }
                    break;
                case "tuer":
                    int door = w.Doors.FindIndex(d => d.Name == name);
                    if (door < 0 || w.Doors[door].Open || since[who] > 40)
                    {
                        Next();
                    }
                    else if (a.DoorTarget != door)
                    {
                        // x, y ist das Feld vor der Tür: erst gehen, wenn der Weg dorthin und die Arbeit frei sind. 0, 0 heißt sofort.
                        var toDoor = x > 0 ? w.Map.FindPath(a.TileX, a.TileY, x, y) : null;
                        if (toDoor == null || w.Rating == MissionRating.Loud || since[who] > 80
                            || watch.FirstRisk(toDoor, a.X, a.Y, World.WalkSpeed(a), w.Tick + lag, 60) < 0)
                        {
                            w.OrderOpenDoor(a.Id, door);
                        }
                    }
                    break;
                case "ziel":
                    if (w.Targets[x].Done || since[who] > 160)
                    {
                        Next();
                    }
                    else if (a.SabotageTarget != x)
                    {
                        // y = 1: erst gehen, wenn der Weg ans Ziel und die Dauer der Arbeit dort frei sind.
                        // Steht ein Feld im Namen ("28,11"), arbeitet er von dort: Der Kern nähme sonst das nächste Feld, auch eines im Blick.
                        var t = w.Targets[x];
                        var spot = name.Contains(',') ? (int.Parse(name.Split(',')[0]), int.Parse(name.Split(',')[1])) : (t.X / World.TileUnits, t.Y / World.TileUnits + 1);
                        if (name.Contains(',') && (a.TileX, a.TileY) == spot && a.Path.Count == 0)
                        {
                            w.OrderSabotage(a.Id, x);
                            break;
                        }
                        if (name.Contains(',') && a.Path.Count > 0 && a.Path[^1] == spot)
                        {
                            break;
                        }
                        var toTarget = y == 1 ? w.Map.FindPath(a.TileX, a.TileY, spot.Item1, spot.Item2) : null;
                        if (toTarget == null || w.Rating == MissionRating.Loud || since[who] > 120
                            || new[] { 0, 10, 20 }.All(e => watch.FirstRisk(toTarget, a.X, a.Y, World.WalkSpeed(a), w.Tick + lag + e, t.Ticks + 30) < 0))
                        {
                            if (name.Contains(',') && toTarget != null)
                            {
                                w.OrderMove(a.Id, spot.Item1, spot.Item2);
                            }
                            else
                            {
                                w.OrderSabotage(a.Id, x);
                            }
                        }
                    }
                    break;
                case "rauch":
                    if (w.OrderThrowSmoke(a.Id, x, y) || since[who] > 10)
                    {
                        Next();
                    }
                    break;
                case "stellwerk":
                    // Das Stellwerk als Ganzes, vom sicheren Platz aus: zwei Felder vor (x, y), Rauchtopf auf den Posten, Tür, Hebelbank,
                    // zurück. Er geht erst los, wenn die ganze Kette frei sein wird: der Anlauf jetzt, der Weg zur Tür und die Arbeit
                    // im Rauch, der Rückweg danach. Der Posten zählt nicht, er steht im Rauch. Höchstens 150 s Warten.
                    {
                        if (!without.TryGetValue("Stellwerksposten", out var blindPost))
                        {
                            var m = LitMission();
                            m.Guards.RemoveAll(g => g.Name == "Stellwerksposten");
                            without["Stellwerksposten"] = blindPost = WatchMap.Measure(Loader.ParseMap(mapJson), m);
                        }
                        int phase = goAt[who] < 0 ? 0 : goAt[who];
                        int gate = w.Doors.FindIndex(d => d.Name == "Stellwerk");
                        if (trace && phase != lastPhase)
                        {
                            Console.WriteLine($"  {w.Tick / 20.0,5:0.0}s {who} Stellwerk Schritt {phase} bei {a.TileX},{a.TileY}, Rauch {w.Smokes.Count}");
                            lastPhase = phase;
                        }
                        if (phase == 0)
                        {
                            var pA = w.Map.FindPath(a.TileX, a.TileY, x, y);
                            // Gesehen werden kann er nur bis an den Rand des Rauchs bei (21, 8) und wieder ab dort.
                            var pB = w.Map.FindPath(x, y, 21, 8);
                            var pC = w.Map.FindPath(21, 8, a.TileX, a.TileY);
                            int t = w.Tick + lag, sp = World.WalkSpeed(a);
                            // Im Rauch: Tür (mit Dietrich 1 s, sonst 2 s), Hebelbank 3 s, dazu die Schritte hinein und heraus.
                            int inSmoke = a.Items.Any(i => i.Def.Kind == ItemKind.Lockpick) ? 125 : 165;
                            bool chain = pA != null && pB != null && pC != null
                                && watch.FirstRisk(pA, a.X, a.Y, sp, t, 15) < 0
                                && new[] { 0, 10, 20 }.All(e => blindPost.FirstRisk(pB, x * 1000 + 500, y * 1000 + 500, sp, t + 30 + e, 5) < 0)
                                && new[] { 0, 10, 20, 30 }.All(e => blindPost.FirstRisk(pC, 21500, 8500, sp, t + 70 + inSmoke + e, 20) < 0);
                            if (chain || since[who] > 300 || w.Rating == MissionRating.Loud)
                            {
                                if (trace)
                                {
                                    Console.WriteLine($"  {w.Tick / 20.0,5:0.0}s {who} geht ans Stellwerk, Kette {(chain ? "frei" : "nicht frei")}");
                                }
                                homes[who] = (a.TileX, a.TileY);
                                w.OrderMove(a.Id, x, y);
                                goAt[who] = 1;
                            }
                        }
                        else if (phase == 1 && a.Path.Count == 0)
                        {
                            if (w.OrderThrowSmoke(a.Id, 21, 7) || a.Items.All(i => i.Def.Kind != ItemKind.Smoke || i.Charges == 0))
                            {
                                goAt[who] = 2;
                            }
                        }
                        else if (phase == 2)
                        {
                            if (w.Doors[gate].Open)
                            {
                                goAt[who] = 3;
                            }
                            else if (a.DoorTarget != gate)
                            {
                                w.OrderOpenDoor(a.Id, gate);
                            }
                        }
                        else if (phase == 3)
                        {
                            if (w.Targets[0].Done)
                            {
                                w.OrderMove(a.Id, homes[who].X, homes[who].Y);
                                goAt[who] = 4;
                            }
                            else if (a.SabotageTarget != 0)
                            {
                                w.OrderSabotage(a.Id, 0);
                            }
                        }
                        else if (phase == 4 && a.Path.Count == 0)
                        {
                            Next();
                        }
                    }
                    break;
                case "fenster":
                    // Wartet, bis ein späterer Weg frei sein wird: von x, y nach "zx,zy", Beginn in "takte" Takten. Die Wache "ohne" zählt nicht,
                    // etwa der Posten, der dann im Rauch steht. Höchstens 60 s.
                    var parts = name.Split(',');
                    var later = w.Map.FindPath(x, y, int.Parse(parts[0]), int.Parse(parts[1]));
                    if (!without.TryGetValue(parts[3], out var blind))
                    {
                        var m = LitMission();
                        m.Guards.RemoveAll(g => g.Name == parts[3]);
                        without[parts[3]] = blind = WatchMap.Measure(Loader.ParseMap(mapJson), m);
                    }
                    int start = w.Tick + int.Parse(parts[2]) + lag;
                    if (later == null || since[who] > 120 || w.Rating == MissionRating.Loud
                        || (blind.FirstRisk(later, x * 1000 + 500, y * 1000 + 500, World.WalkSpeed(a), start, 20) < 0
                            && blind.FirstRisk(later, x * 1000 + 500, y * 1000 + 500, World.WalkSpeed(a), start + 20, 20) < 0))
                    {
                        Next();
                    }
                    break;
                case "folge":
                    // Bleibt beim Kollegen, bis der auf Otto wartet oder am Ende seines Plans ist.
                    var lead = w.Units.Find(u => u.Name == name && u.Faction == Faction.Agent);
                    if (lead == null || !lead.Alive || at[name] >= plan[name].Length || plan[name][at[name]] is ("warte", _, _, "Otto"))
                    {
                        Next();
                    }
                    else if (World.Distance(a, lead) > 2 * World.TileUnits && (a.Path.Count == 0 || since[who] % 4 == 0))
                    {
                        w.OrderMove(a.Id, lead.TileX, lead.TileY);
                    }
                    break;
                case "warte":
                    // Bis der Kollege mit diesem Namen seinen Plan zu Ende hat.
                    if (name == "Stellwerk" ? w.Targets[0].Done || w.Rating == MissionRating.Loud || since[who] > 500
                        : name == "Tür" ? w.Doors.Exists(d => d.Name == "Zaunpforte der Heizer" && d.Open) || since[who] > 400
                        : !plan.ContainsKey(name) || at[name] >= plan[name].Length || plan[name][at[name]] is ("warte", _, _, "Otto" or "Stellwerk") || since[who] > 400)
                    {
                        Next();
                    }
                    break;
            }
        }
    };
};

// Laut am Bahnhof: Stellung beziehen, das Feuer eröffnen, die Wachen kommen lassen. Ist es still, öffnet der Schlosser das Stellwerk
// und legt die Hebelbank lahm, die Kurierin die Weiche, dann alle zum Bahnübergang.
Func<Action<World>> LoudYard((int X, int Y) post) => () =>
{
    int stage = 0, calm = 0;
    return w =>
    {
        var squad = w.Units.Where(u => u.Faction == Faction.Agent && u.Alive).ToList();
        if (squad.Count == 0)
        {
            return;
        }
        var ids = squad.Select(a => a.Id).ToList();
        var armed = w.Units.Where(u => u.Faction == Faction.Guard && u.Alive && !u.Bribed && !u.Type.OrderOnly).ToList();
        var seen = armed.Where(g => squad.Any(a => w.CanSee(a, g, 10 * World.TileUnits))).ToList();
        int px = post.X * World.TileUnits + World.TileUnits / 2, py = post.Y * World.TileUnits + World.TileUnits / 2;
        long Far(Unit u) => Math.Abs(u.X - px) + Math.Abs(u.Y - py);
        void Fire(List<Unit> targets)
        {
            var target = targets.OrderBy(g => World.Distance(squad[0], g)).First();
            foreach (var a in squad.Where(a => a.TargetId < 0 || !w.Units[a.TargetId].Alive || !seen.Contains(w.Units[a.TargetId])))
            {
                w.OrderAttack(a.Id, target.Id);
            }
        }
        if (seen.Count > 0 && (stage > 0 || w.Rating == MissionRating.Loud))
        {
            calm = 0;
            Fire(seen);
            return;
        }
        switch (stage)
        {
            case 0:
                if (w.Rating == MissionRating.Loud || squad.All(a => Far(a) <= 2 * World.TileUnits))
                {
                    stage = 1;
                }
                else if (squad.Any(a => a.Path.Count == 0 && Far(a) > 2 * World.TileUnits))
                {
                    w.OrderGroupMove(ids, post.X, post.Y);
                }
                break;
            case 1:
                if (w.Rating != MissionRating.Loud && armed.Count > 0)
                {
                    Fire(armed);
                    break;
                }
                var away = squad.Where(a => Far(a) > 2 * World.TileUnits && a.Path.Count == 0).Select(a => a.Id).ToList();
                foreach (var a in squad)
                {
                    a.TargetId = -1;
                }
                if (away.Count > 0)
                {
                    w.OrderGroupMove(away, post.X, post.Y);
                }
                if (++calm >= 20 || armed.Count == 0)
                {
                    stage = 2;
                }
                break;
            case 2:
                foreach (var a in squad)
                {
                    a.TargetId = -1;
                }
                if (w.AllSabotaged)
                {
                    if (squad.Any(a => a.Path.Count == 0 && !(a.X < w.Exit.X1 && a.Y >= w.Exit.Y0 && a.Y < w.Exit.Y1)))
                    {
                        w.OrderGroupMove(ids, 1, 7);
                    }
                    break;
                }
                var smith = squad.Find(a => a.Skill == World.SkillLocksmith) ?? squad[0];
                var runner = squad.Find(a => a.Skill == World.SkillCourier && a != smith) ?? squad.Find(a => a != smith) ?? smith;
                int gate = w.Doors.FindIndex(d => d.Name == "Stellwerk");
                if (!w.Targets[0].Done && smith.SabotageTarget != 0 && smith.DoorTarget != gate && !w.OrderSabotage(smith.Id, 0))
                {
                    w.OrderOpenDoor(smith.Id, gate);
                }
                var second = w.Targets[0].Done || runner != smith ? runner : null;
                if (!w.Targets[1].Done && second != null && second.SabotageTarget != 1)
                {
                    w.OrderSabotage(second.Id, 1);
                }
                // Die übrigen bleiben beim Schlosser.
                foreach (var a in squad.Where(a => a != smith && a != second && a.Path.Count == 0 && World.Distance(a, smith) > 4 * World.TileUnits))
                {
                    w.OrderMove(a.Id, smith.TileX, smith.TileY + 2);
                }
                break;
        }
    };
};

void ReportYard(string name, Func<Action<World>> make, int money = 600)
{
    int wins = 0, dead = 0, unseen = 0, quiet = 0, late = 0, ticks = 0, left = 0, paid = 0, done = 0, doneQuiet = 0, doneAt = 0;
    for (int seed = 1; seed <= Runs; seed++)
    {
        var mission = LitMission();
        mission.Seed = seed * 7919;
        mission.Money = money;
        var w = new World(Loader.ParseMap(mapJson), mission);
        tune?.Invoke(w);
        var strategy = make();
        for (int i = 0; i < MaxTicks && w.State == MissionState.Running; i++)
        {
            if (i % 10 == 0)
            {
                strategy(w);
            }
            w.Step();
            if (w.AllDoneTick == w.Tick - 1 && w.AllDoneTick >= 0)
            {
                // Stand in dem Augenblick, in dem das zweite Ziel erledigt ist.
                doneQuiet += w.Rating != MissionRating.Loud ? 1 : 0;
            }
        }
        bool won = w.State == MissionState.Won;
        done += w.AllSabotaged ? 1 : 0;
        doneAt += w.AllSabotaged ? w.AllDoneTick / 20 : 0;
        wins += won ? 1 : 0;
        unseen += won && w.Rating == MissionRating.Unseen ? 1 : 0;
        quiet += won && w.Rating != MissionRating.Loud ? 1 : 0;
        late += w.TimeUp ? 1 : 0;
        dead += w.Units.Count(u => u.Faction == Faction.Agent && !u.Alive);
        ticks += w.Tick;
        left += won && w.AllDoneTick >= 0 ? (w.TimeLimitTicks - w.AllDoneTick) / 20 : 0;
        paid += money - w.Money;
    }
    Console.WriteLine($"{name,-44} Sieg {100 * wins / Runs,3}%  leise {100 * quiet / Runs,3}%  unentdeckt {100 * unseen / Runs,3}%  beide Ziele {100 * done / Runs,3}% (ohne Schuss {100 * doneQuiet / Runs,3}%, nach {(done > 0 ? doneAt / done : 0),3}s)  Uhr abgelaufen {100 * late / Runs,3}%  tote Agenten {(double)dead / Runs:0.00}  Dauer {ticks / Runs / 20,3}s  Rest auf der Uhr {(wins > 0 ? left / wins : 0),3}s  bezahlt {paid / Runs,3}");
}

Dictionary<string, (string, int, int, string)[]> YardSteps(bool money)
{
    var p1 = "";
    // Der leise Weg führt durch den Barackenhof, dort liegt kein Schotter: Kohlenweg, hinter den Baracken nach Osten, durch die
    // Zaunpforte der Heizer und über den Bahnweg zwischen die Wagen von Gleis 4 und 6. Wartepunkte sind Felder, die der Lageplan
    // nie im Blick zeigt. Hanna legt die Weiche lahm, Otto das Stellwerk im Rauch.
    // Mit Geld kauft Emil vorher Südstreife und Weichenposten, Greta steht dabei.
    var hanna = new List<(string, int, int, string)> { ("geh", 8, 22, p1), ("geh", 26, 22, p1) };
    var otto = new List<(string, int, int, string)> { ("geh", 8, 21, p1), ("geh", 25, 22, p1) };
    var plan = new Dictionary<string, (string, int, int, string)[]>();
    if (money)
    {
        // Emil kauft Südstreife und Weichenposten zum vollen Preis, Greta bleibt am Start. Den Hundeführer kauft er nicht:
        // Der Hund greift an, bevor Emil beim Führer ist. Zur Weiche geht es östlich am Wendepunkt des Hundes vorbei.
        plan["Emil"] = new[] { ("geh", 9, 21, p1), ("geh", 24, 22, p1), ("warte", 0, 0, "Tür"), ("geh", 25, 14, p1), ("kauf", 5, 0, "Südstreife"), ("geh", 28, 14, p1), ("kauf", 0, 1, "Weichenposten"), ("geh", 28, 14, "6") };
        plan["Tür"] = Array.Empty<(string, int, int, string)>();
        otto.Add(("tuer", 25, 18, "Zaunpforte der Heizer"));
        otto.Add(("lauf", 25, 22, p1));
        otto.Add(("warte", 0, 0, "Emil"));
        hanna.Add(("warte", 0, 0, "Emil"));
        hanna.AddRange(new[] { ("geh", 25, 13, p1), ("geh", 27, 14, p1), ("ziel", 1, 1, "28,11") });
        otto.Add(("geh", 24, 13, p1));
    }
    else
    {
        otto.AddRange(new[] { ("tuer", 25, 18, "Zaunpforte der Heizer"), ("lauf", 25, 22, p1), ("geh", 24, 13, p1) });
        hanna.AddRange(new[] { ("warte", 0, 0, "Tür"), ("geh", 25, 13, p1), ("geh", 28, 14, p1), ("schlag", 28, 10, "Weichenposten"), ("ziel", 1, 0, "28,11") });
    }
    // Emil steht mit Geld auf (28, 14), zwei passen nicht auf ein Feld: Hanna nimmt dann das Feld daneben.
    // Sie wartet östlich der Wagen von Gleis 7, bis das Stellwerk steht. Der Weg zurück nach (25, 13) führte am Wendepunkt des Hundes vorbei.
    hanna.AddRange(new[] { ("lauf", money ? 27 : 28, 14, p1), ("warte", 0, 0, "Stellwerk") });
    otto.AddRange(new[] { ("stellwerk", 22, 12, p1) });
    // Zurück, wie sie gekommen sind, dann am Kohlenlager über den Bahnweg ans Stumpfgleis und über die Bohlen des Bahnübergangs.
    // Im Barackenhof wartet jeder auf seinem eigenen Feld in Reihe 22: nie im Blick und weiter als 2,5 Felder vom Fenster der Wärterbude.
    (string, int, int, string)[] Home(int x) => new[] { ("geh", x, 22, p1), ("geh", 8, 21, p1), ("geh", 3, 15, p1), ("geh", 1, 7, p1) };
    var home = Home(24);
    otto.AddRange(Home(25));
    hanna.AddRange(Home(26));
    plan["Otto"] = otto.ToArray();
    plan["Hanna"] = hanna.ToArray();
    if (money)
    {
        plan["Emil"] = plan["Emil"].Concat(new[] { ("warte", 0, 0, "Stellwerk") }).Concat(home).ToArray();
    }
    if (yardWagons)
    {
        // Ohne Barackenhof und ohne Pforte: Kohlenweg, hinter die Wagen von Gleis 7, dann in einem Zug über den Schotter nach Osten.
        // Zurück genauso. Die Wartepunkte (8, 14), (9, 13), (8, 13) und (24 bis 26, 13) zeigt der Lageplan nie im Blick.
        (string, int, int, string)[] Swap((string, int, int, string)[] steps, (int, int) west, (int, int) east)
        {
            var kept = steps.Where(st => !(st.Item1 == "tuer" && st.Item4 == "Zaunpforte der Heizer") && !(st.Item1 == "warte" && st.Item4 == "Tür")
                && !((st.Item1 == "geh" || st.Item1 == "lauf") && st.Item3 == 22 && st.Item2 >= 24)).ToList();
            // Hinweg: nach dem ersten Schritt am Kohlenweg hinter die Wagen und nach Osten. Rückweg: vor dem Schritt nach (8, 21) dasselbe rückwärts.
            int first = kept.FindIndex(st => st.Item1 == "geh" && st.Item2 <= 9 && st.Item3 >= 21);
            kept.Insert(first + 1, ("geh", west.Item1, west.Item2, ""));
            kept.Insert(first + 2, ("geh", east.Item1, east.Item2, ""));
            int back = kept.FindLastIndex(st => st.Item1 == "geh" && st.Item2 == 8 && st.Item3 == 21);
            if (back > first)
            {
                kept[back] = ("geh", west.Item1, west.Item2, "");
                kept.Insert(back, ("geh", east.Item1, east.Item2, ""));
            }
            return kept.ToArray();
        }
        plan["Otto"] = Swap(plan["Otto"], (8, 14), (24, 13));
        plan["Hanna"] = Swap(plan["Hanna"], (9, 13), (25, 13));
        if (money)
        {
            plan["Emil"] = Swap(plan["Emil"], (8, 13), (28, 14));
            plan.Remove("Tür");
        }
    }
    return plan;
}

// Fenster am Stellwerk: --kette zählt ohne Probelauf, zu wie vielen Zeitpunkten die ganze Kette frei ist (Anlauf vom sicheren Platz,
// Weg zur Tür und Arbeit im Rauch, Rückweg), und welche Wache sie sperrt. Dazu Fassungen mit anderen Streifen.
if (args.Length > 0 && args[0] == "--kette")
{
    (double Share, int Longest, int First) Chain(MissionData m, string? ignore, (int X, int Y) from, (int X, int Y) via, int work)
    {
        var full = WatchMap.Measure(Loader.ParseMap(mapJson), m);
        var m2 = LitMission();
        variant?.Invoke(m2);
        m2.Guards = m.Guards.Where(g => g.Name != "Stellwerksposten").ToList();
        var blind = WatchMap.Measure(Loader.ParseMap(mapJson), m2);
        var map = new World(Loader.ParseMap(mapJson), LitMission()).Map;
        var pA = map.FindPath(from.X, from.Y, via.X, via.Y)!;
        // Gesehen werden kann er nur bis an den Rand des Rauchs bei (21, 8) und wieder ab dort. Tür und Hebelbank liegen im Rauch.
        var pB = map.FindPath(via.X, via.Y, 21, 8)!;
        var pC = map.FindPath(21, 8, from.X, from.Y)!;
        int free = 0, run = 0, longest = 0, first = -1, n = 0;
        for (int t = 0; t < 300 * 20; t += 5, n++)
        {
            bool ok = full.FirstRisk(pA, from.X * 1000 + 500, from.Y * 1000 + 500, 120, t, 15) < 0
                && new[] { 0, 10, 20 }.All(e => blind.FirstRisk(pB, via.X * 1000 + 500, via.Y * 1000 + 500, 120, t + 30 + e, 5) < 0)
                && new[] { 0, 10, 20, 30 }.All(e => blind.FirstRisk(pC, 21500, 8500, 120, t + 70 + work + e, 20) < 0);
            free += ok ? 1 : 0;
            run = ok ? run + 1 : 0;
            longest = Math.Max(longest, run);
            if (ok && first < 0)
            {
                first = t;
            }
        }
        return ((double)free / n, longest * 5, first);
    }
    // Fenster an der Weiche: Weg von (28, 14) hinter den Posten, Schlag und 4 s Arbeit, ohne den Weichenposten gerechnet.
    (double Share, int Longest) Points(MissionData m)
    {
        var m2 = LitMission();
        m2.Guards = m.Guards.Where(g => g.Name != "Weichenposten").ToList();
        var alone = WatchMap.Measure(Loader.ParseMap(mapJson), m2);
        var map = new World(Loader.ParseMap(mapJson), LitMission()).Map;
        var path = map.FindPath(28, 14, 28, 10)!;
        int free = 0, run = 0, longest = 0, n = 0;
        for (int t = 0; t < 300 * 20; t += 5, n++)
        {
            bool ok = alone.FirstRisk(path, 28500, 14500, 120, t, 110) < 0;
            free += ok ? 1 : 0;
            run = ok ? run + 1 : 0;
            longest = Math.Max(longest, run);
        }
        return ((double)free / n, longest * 5);
    }
    void Round(MissionData m, params double[][] route)
    {
        var g = m.Guards.First(x => x.Name == "Stellwerksrunde");
        g.X = route[^1][0] + 0.5;
        g.Y = route[^1][1] + 0.5;
        g.Patrol = route.ToList();
    }
    void Dog(MissionData m, int east) => m.Guards.First(x => x.Name == "Gleisstreife").Patrol = new() { new double[] { east, 11 }, new double[] { 2, 11 } };
    double[] P(double x, double y) => new[] { x, y };
    var versions = new (string Name, Action<MissionData> Set)[]
    {
        ("Daten wie sie sind", m => { }),
        ("Runde vor der Tür (18,7)-(25,7), wie zuerst", m => Round(m, P(25, 7), P(25, 4), P(25, 7), P(18, 7))),
        ("Runde Osthof bis x 28", m => Round(m, P(28, 7), P(28, 3), P(24, 3), P(24, 7))),
        ("Runde Osthof bis x 26", m => Round(m, P(26, 7), P(26, 3), P(24, 3), P(24, 7))),
        ("Runde Osthof Nord, y 3 bis 6", m => Round(m, P(28, 6), P(28, 3), P(24, 3), P(24, 6))),
        ("Runde Nordrand, (24,3)-(29,3)", m => Round(m, P(29, 3), P(24, 3))),
        ("Osthof bis x 26, Hund bis x 18", m => { Round(m, P(26, 7), P(26, 3), P(24, 3), P(24, 7)); Dog(m, 18); }),
        ("Osthof Nord, Hund bis x 18", m => { Round(m, P(28, 6), P(28, 3), P(24, 3), P(24, 6)); Dog(m, 18); }),
        ("Nordrand, Hund bis x 18", m => { Round(m, P(29, 3), P(24, 3)); Dog(m, 18); }),
        ("Osthof Nord, Hund bis x 22", m => { Round(m, P(28, 6), P(28, 3), P(24, 3), P(24, 6)); Dog(m, 22); }),
    };
    foreach (int work in new[] { 125, 165 })
    {
        Console.WriteLine($"== Im Rauch {work / 20.0:0.0#} s (mit Dietrich 125 Takte, ohne 165)");
        foreach (var (label, set) in versions)
        {
            variant = set;
            var m = LitMission();
            var (share, longest, first) = Chain(m, null, (24, 13), (22, 12), work);
            var (pShare, pLongest) = Points(m);
            Console.WriteLine($"{label,-44} Stellwerk frei {100 * share,5:0.0}%  längstes Fenster {longest / 20.0,4:0.0#}s  erstes bei {(first < 0 ? "nie" : (first / 20) + "s"),5}  | Weiche frei {100 * pShare,5:0.0}%  längstes {pLongest / 20.0,4:0.0#}s");
        }
    }
    variant = null;
    return;
}

// Güterbahnhof: --bahnhof misst leise ohne Geld, leise mit Geld und laut, jeweils gegen die Uhr.
if (args.Length > 0 && args[0] == "--bahnhof")
{
    var pistol = new[] { "pistole" };
    bool only(string k) => args.Length < 2 || args[1] == k;
    Console.WriteLine($"{missionId}, je {Runs} Läufe, Uhr {LitMission().TimeLimit} s");
    if (only("leise"))
    {
        loadout = new[] { pistol, new[] { "pistole", "dietrich", "rauchtopf" }, pistol, new[] { "pistole", "totschlaeger" } };
        foreach (int lag in new[] { 0, 10, 20, 40 })
        {
            ReportYard($"Leise ohne Geld, Rauch und Totschläger, {lag / 20.0:0.0#} s", YardPlan(YardSteps(false), lag), 0);
        }
    }
    if (only("geld"))
    {
        loadout = new[] { pistol, new[] { "pistole", "dietrich", "rauchtopf" }, pistol, pistol };
        foreach (int lag in new[] { 0, 10, 20, 40 })
        {
            ReportYard($"Leise mit Geld, zwei gekauft, Rauch, {lag / 20.0:0.0#} s", YardPlan(YardSteps(true), lag, new[] { "Südstreife", "Weichenposten" }));
        }
    }
    if (only("laut"))
    {
        var posts = new (string Name, (int, int) At)[] { ("Kohlenweg", (8, 19)), ("hinter Wagen Gleis 7", (11, 16)), ("Wagen Gleis 5", (16, 14)), ("Barackenhof", (17, 20)) };
        foreach (var (kitName, kit) in new (string, string[])[] { ("Pistolen", pistol), ("vier MP", new[] { "maschinenpistole" }), ("vier MP + Verband", new[] { "maschinenpistole", "verbandszeug" }) })
        {
            loadout = new[] { kit };
            foreach (var (postName, post) in posts)
            {
                ReportYard($"Laut, {postName}, {kitName}", Bandaged(LoudYard(post)));
            }
        }
    }
    loadout = null;
    return;
}

// Laut aus einer Stellung: --stellung misst Plätze, Ausrüstung und Fassungen der Verstärkung.
if (args.Length > 0 && args[0] == "--stellung")
{
    var p0 = new[] { "pistole" };
    var kits = new (string Name, string[][] Items)[]
    {
        ("Pistolen", new[] { p0 }),
        ("vier MP", new[] { new[] { "maschinenpistole" } }),
        ("vier MP + Verband", new[] { new[] { "maschinenpistole", "verbandszeug" } }),
    };
    var posts = new (string Name, (int, int) At)[] { ("Durchgang", (7, 15)), ("Friedhofsgang", (7, 20)), ("Südgasse West", (4, 27)), ("Start", (10, 29)) };
    var versions = new (string Name, Action<MissionData> Set)[]
    {
        ("Daten wie sie sind", m => { }),
        ("vier Mann, vier Betten", m => { m.Reinforcements!.Count = 4; m.Reinforcements.Beds = 4; }),
        ("fünf Mann, fünf Betten", m => { m.Reinforcements!.Count = 5; m.Reinforcements.Beds = 5; }),
        ("fünf Mann, Abstand 3 s", m => { m.Reinforcements!.Count = 5; m.Reinforcements.Beds = 5; m.Reinforcements.Interval = 3; }),
        ("sechs Mann, Abstand 3 s", m => { m.Reinforcements!.Count = 6; m.Reinforcements.Beds = 6; m.Reinforcements.Interval = 3; }),
        ("Draht gekappt: ein Mann, spät", m => { m.Reinforcements!.Phone = ""; m.Reinforcements.FirstDelay = m.Reinforcements.LateDelay; m.Reinforcements.Count = m.Reinforcements.LateCount; }),
    };
    int only = args.Length > 1 ? int.Parse(args[1]) : -1;
    for (int v = 0; v < versions.Length; v++)
    {
        if (only >= 0 && v != only)
        {
            continue;
        }
        variant = versions[v].Set;
        Console.WriteLine($"== {versions[v].Name} ({missionId}, je {Runs} Läufe)");
        foreach (var (kitName, items) in kits)
        {
            loadout = items;
            foreach (var (postName, at) in posts)
            {
                ReportRescue($"{postName}, {kitName}", Bandaged(LoudFromPost(at)));
            }
            ReportRescue($"Nachlaufen wie bisher, {kitName}", Rescue(false, true, focus: true));
        }
    }
    loadout = null;
    variant = null;
    return;
}

// Nachlass der Truppführerin: --greta misst vorher und nachher. Fassungen: kein Nachlass, halber Preis, ein Drittel, halber Preis einmal je Einsatz.
if (args.Length > 0 && args[0] == "--greta")
{
    var versions = new (string Name, int Percent, int Uses)[] { ("Voller Preis", 0, -1), ("Halber Preis", 50, -1), ("Ein Drittel Nachlass", 33, -1), ("Halber Preis, nur einmal", 50, 1) };
    var p1 = new[] { "pistole" };
    foreach (var (label, percent, uses) in versions)
    {
        if (args.Length > 1 && args[1] != percent.ToString())
        {
            continue;
        }
        tune = w => { w.LeaderDiscountPercent = percent; w.LeaderDiscountUses = uses; };
        Console.WriteLine($"== {label} ({missionId}, je {Runs} Läufe, Kasse 600)");
        if (LitMission().Prisoners.Count == 0)
        {
            // Für Einsätze mit Gegenstand gibt es noch keine Spielweise mit Bestechung, die gewinnt: Hier stehen nur die Preise.
            var sample = NewWorld(7919);
            var payer = sample.Units.Find(u => u.Skill == World.SkillBookkeeper)!;
            Console.WriteLine(string.Join(", ", sample.Units.Where(u => u.Faction == Faction.Guard && u.Type.BribeCost > 0 && u.LeashedTo < 0)
                .Select(u => $"{u.Name} {u.Type.BribeCost * (100 - percent) / 100}")));
            continue;
        }
        loadout = null;
        quietWatch = null;
        foreach (int lag in new[] { 0, 10, 20, 30, 40 })
        {
            ReportRescue($"Otto, Emil und Greta, {lag / 20.0:0.0#} s spät", QuietWithMoney(lag, withGreta: true));
        }
        foreach (int lag in new[] { 0, 10, 20, 30, 40 })
        {
            ReportRescue($"Greta sieht vom Durchgang zu, {lag / 20.0:0.0#} s spät", QuietWithMoney(lag, gretaWatches: true));
        }
        // Was vom Geld bleibt, geht in eine Waffe für den Fall, dass es laut wird.
        string[] spare = percent switch { 0 => new[] { "pistole" }, 33 => new[] { "pistole", "verbandszeug", "rauchtopf" }, _ => new[] { "schrotflinte" } };
        loadout = new[] { uses == 1 ? new[] { "pistole", "verbandszeug" } : spare, p1, p1, p1 };
        foreach (int lag in new[] { 20, 30, 40 })
        {
            ReportRescue($"  dazu Absicherung {string.Join("+", loadout[0])}, {lag / 20.0:0.0#} s", QuietThenFight(QuietWithMoney(lag, withGreta: true)));
        }
        loadout = null;
    }
    tune = null;
    return;
}

// Wer bemerkt Otto zuerst, und wo steht er dann? Zeigt, an welcher Stelle der Weg ohne Geld am engsten ist. --wer <Takte Verspätung>
if (args.Length > 0 && args[0] == "--wer")
{
    int lag = args.Length > 1 ? int.Parse(args[1]) : 10;
    var p0 = new[] { "pistole" };
    loadout = new[] { p0, new[] { "pistole", "totschlaeger", "dietrich" }, p0, p0 };
    var tally = new Dictionary<string, int>();
    int starts = 0, werWins = 0, werQuiet = 0, werTicks = 0;
    for (int seed = 1; seed <= Runs; seed++)
    {
        var mission = LitMission();
        mission.Seed = seed * 7919;
        bool three = args.Contains("greta");
        mission.Money = three ? 600 : 0;
        var w = new World(Loader.ParseMap(mapJson), mission);
        var strategy = three ? QuietWithMoney(lag, withGreta: true)() : QuietLocksmith(lag)();
        string first = "niemand";
        int started = -1;
        for (int i = 0; i < MaxTicks && w.State == MissionState.Running; i++)
        {
            if (i % 10 == 0)
            {
                strategy(w);
            }
            w.Step();
            var otto = w.Units[1];
            if (started < 0 && otto.TileY < 13)
            {
                started = w.Tick;
            }
            var seen = w.Units.Find(u => u.Faction == Faction.Guard && u.Name != "Schließer" && !u.Bribed && u.GuardState != GuardState.Patrol
                && (!args.Contains("greta") || u.GuardState == GuardState.Combat));
            if (seen != null && first == "niemand")
            {
                bool back = !w.Prisoners.Any(p => p.Captive);
                first = $"{seen.Name,-14} Otto bei {otto.TileX,2},{otto.TileY,2} {(back ? "Rückweg" : "Hinweg")}" + (args.Contains("greta") ? $", Greta bei {w.Units[0].TileX},{w.Units[0].TileY}, Emil bei {w.Units[2].TileX},{w.Units[2].TileY}" : "");
            }
        }
        starts += started < 0 ? 0 : started;
        werWins += w.State == MissionState.Won ? 1 : 0;
        werQuiet += w.State == MissionState.Won && w.Rating != MissionRating.Loud ? 1 : 0;
        werTicks += w.Tick;
        tally[first] = tally.GetValueOrDefault(first) + 1;
    }
    Console.WriteLine($"Verspätung bis {lag / 20.0:0.0#} s: Sieg {100 * werWins / Runs}%, leise {100 * werQuiet / Runs}%, Aufbruch im Mittel nach {starts / Runs / 20} s, Dauer {werTicks / Runs / 20} s");
    foreach (var (k, v) in tally.OrderByDescending(t => t.Value).Take(14))
    {
        Console.WriteLine($"{v,4}  {k}");
    }
    return;
}

// Wann ist ein Feld bewacht? --fenster x y zeigt je halbe Sekunde ein Zeichen, # bewacht, . frei.
if (args.Length > 2 && args[0] == "--fenster")
{
    var watch = WatchMap.Measure(Loader.ParseMap(mapJson), LitMission());
    for (int a = 1; a + 1 < args.Length; a += 2)
    {
        int fx = int.Parse(args[a]), fy = int.Parse(args[a + 1]);
        var line = new System.Text.StringBuilder();
        for (int t = 0; t < 1400; t += 10)
        {
            line.Append(watch.IsWatched(fx, fy, t) ? '#' : '.');
        }
        Console.WriteLine($"{fx,2},{fy,2} {line}");
    }
    return;
}

// Ein Lauf des leisen Wegs im Einzelnen: wer wann etwas bemerkt und wo der Trupp dann steht.
if (args.Length > 0 && args[0] == "--spur")
{
    // Mit dem Seed der Missionsdatei, also derselbe Ablauf wie im Spiel: Die Zeiten taugen als Vorlage für einen Prüflauf.
    // Zweites Wort wählt die Spielweise: geld (Vorgabe), schlag, kauf, allein, dazu "hof" für den zweiten Posten.
    string way = args.Length > 1 ? args[1] : "geld";
    var p1 = new[] { "pistole" };
    if (args.Contains("hof"))
    {
        variant = m => m.Guards.Add(new UnitData { Name = "Hofposten", Sprite = "Wachmann", X = 12.5, Y = 8.5, Patrol = { new double[] { 9, 8 }, new double[] { 12, 8 } } });
    }
    loadout = way == "laut" ? null : new[] { p1, new[] { "pistole", "totschlaeger", "dietrich" }, p1, p1 };
    var mission = LitMission();
    mission.Money = way == "allein" ? 0 : way == "geld" || way == "greta" || way == "blick" ? 600 : 400;
    var w = new World(Loader.ParseMap(mapJson), mission);
    var strategy = (way switch
    {
        "schlag" => QuietWithMoney(0, new[] { "Schließer", "Hofposten" }),
        "kauf" => QuietLocksmith(0, new[] { "Schließer", "Hofposten" }),
        "allein" => QuietLocksmith(0),
        "greta" => QuietWithMoney(0, withGreta: true),
        "blick" => QuietWithMoney(0, gretaWatches: true),
        "laut" => Rescue(false, true, focus: true),
        _ => QuietWithMoney(0),
    })();
    var states = w.Units.ToDictionary(u => u.Id, u => u.GuardState);
    var goals = new Dictionary<int, string>();
    string last = "";
    for (int i = 0; i < MaxTicks && w.State == MissionState.Running; i++)
    {
        if (i % 10 == 0)
        {
            strategy(w);
        }
        foreach (var a in w.Units.Where(u => u.Faction == Faction.Agent))
        {
            string goal = a.BribeTarget >= 0 ? "kauft " + w.Units[a.BribeTarget].Name : a.DoorTarget >= 0 ? "öffnet " + w.Doors[a.DoorTarget].Name
                : a.Path.Count > 0 ? $"geht nach {a.Path[^1].X},{a.Path[^1].Y}" : "";
            if (goal.Length > 0 && goals.GetValueOrDefault(a.Id) != goal)
            {
                Console.WriteLine($"{w.Tick,6} Tick: {a.Name} {goal}");
            }
            goals[a.Id] = goal;
        }
        w.Step();
        foreach (var g in w.Units.Where(u => u.Faction == Faction.Guard))
        {
            if (!states.TryGetValue(g.Id, out var old) || old != g.GuardState)
            {
                Console.WriteLine($"{w.Tick / 20.0,6:0.0}s {g.Name} {g.GuardState} bei {g.TileX},{g.TileY}{(g.Bribed ? " gekauft" : "")}  Trupp: {string.Join(" ", w.Units.Where(u => u.Faction != Faction.Guard && u.Alive && !u.Captive).Select(u => $"{u.Name[..2]}{u.TileX},{u.TileY}"))}");
                states[g.Id] = g.GuardState;
            }
        }
        string now = $"{string.Join(",", w.Doors.Where(d => d.Open).Select(d => d.Name[..6]))} | {string.Join(",", w.Prisoners.Select(p => p.Captive ? "Z" : "frei"))} | {w.Rating} | Kasse {w.Money}";
        if (now != last)
        {
            Console.WriteLine($"{w.Tick / 20.0,6:0.0}s {now}");
            last = now;
        }
    }
    Console.WriteLine($"{w.Tick / 20.0:0.0}s {w.State}");
    return;
}

bool knobsOnly = args.Length > 0 && args[0] == "--schrauben";
if (LitMission().Prisoners.Count > 0 && (args.Length == 0 || knobsOnly))
{
    Console.WriteLine($"{missionId}, je {Runs} Läufe, Kasse 600");
    var pistol = new[] { "pistole" };
    if (!knobsOnly)
    {
    ReportRescue("Alle zur Pforte, Otto öffnet", Rescue(false, false));
    ReportRescue("Erst kämpfen, dann befreien", Rescue(false, true));
    ReportRescue("Otto allein", Rescue(true, false));
    ReportRescue("Otto allein nach Lageplan", Rescue(true, false, plan: true));
    ReportRescue("Emil kauft Hundeführer, Otto allein", Rescue(true, false, new[] { "Hundeführer" }));
    ReportRescue("Emil kauft Hundef. und Schließer", Rescue(true, false, new[] { "Hundeführer", "Schließer" }));
    // Otto trägt je nach Zeile Totschläger und Dietrich, die anderen bleiben bei der Pistole.
    foreach (var (label, kit) in new[] { ("Totschläger", new[] { "pistole", "totschlaeger" }), ("Totschl. + Dietrich", new[] { "pistole", "totschlaeger", "dietrich" }) })
    {
        loadout = new[] { pistol, kit, pistol, pistol };
        ReportRescue($"Leise ohne Geld, {label}", QuietLocksmith(0), 0);
        ReportRescue($"  dasselbe, bis 0,5 s spät", QuietLocksmith(10), 0);
    }
    loadout = null;
    ReportRescue("Laut durchs Hoftor, Pistolen", Rescue(false, true, gate: "Hoftor"));
    // Der Rauch steht im Postengang zwischen Wall und Hoftor und nimmt dem Wallposten die Sicht.
    var smokeKit = new[] { new[] { "pistole", "rauchtopf" } };
    ReportRescue("Direkt zum Hoftor, Pistolen", Rescue(false, false, gate: "Hoftor"));
    loadout = smokeKit;
    ReportRescue("Direkt zum Hoftor, Rauch (240)", Rescue(false, false, gate: "Hoftor", smoke: (14, 5)));
    ReportRescue("Laut durchs Hoftor, Rauch (240)", Rescue(false, true, gate: "Hoftor", smoke: (14, 5)));
    loadout = null;
    loadout = new[] { new[] { "maschinenpistole" }, pistol, pistol, pistol };
    ReportRescue("Erst kämpfen, eine MP (600)", Rescue(false, true));
    loadout = new[] { new[] { "schrotflinte", "verbandszeug" }, new[] { "pistole", "rauchtopf" }, pistol, new[] { "pistole", "verbandszeug" } };
    ReportRescue("Erst kämpfen, Schrot + Verband (560)", Rescue(false, true));
    loadout = null;
    ReportRescue("Leise mit Geld, Klick genau", QuietWithMoney(0));
    ReportRescue("Leise mit Geld, bis 0,25 s spät", QuietWithMoney(5));
    ReportRescue("Leise mit Geld, bis 0,5 s spät", QuietWithMoney(10));
    ReportRescue("Leise mit Geld, bis 1 s spät", QuietWithMoney(20));
    foreach (var kit in new (string Name, string[] Items)[] { ("Schrotflinten", new[] { "schrotflinte" }), ("Maschinenpistolen", new[] { "maschinenpistole" }) })
    {
        loadout = new[] { kit.Items };
        ReportRescue($"Erst kämpfen, {kit.Name}", Rescue(false, true));
    }
    loadout = null;
    }

    // Stellschrauben für den leisen Weg, nur gemessen, nicht eingebaut. Allein mit --schrauben.
    var full = new[] { "pistole", "totschlaeger", "dietrich" };
    loadout = new[] { pistol, full, pistol, pistol };
    foreach (int lag in new[] { 0, 5, 10 })
    {
        ReportRescue($"Jetzt: Otto allein, {lag / 20.0:0.0#} s", QuietLocksmith(lag), 0);
    }
    loadout = null;
    foreach (int lag in new[] { 0, 10, 20, 30, 40 })
    {
        ReportRescue($"Jetzt: beide kaufen, {lag / 20.0:0.0#} s", QuietWithMoney(lag));
    }
    Console.WriteLine("Stellschraube A: Das Geld reicht nur für eine Bestechung (Schließer 350 statt 200)");
    foreach (int lag in new[] { 0, 10, 20 })
    {
        loadout = new[] { pistol, new[] { "pistole", "totschlaeger" }, pistol, pistol };
        ReportRescue($"  Hundef. kaufen, Schließer schlagen, {lag / 20.0:0.0#} s", QuietWithMoney(lag, new[] { "Schließer" }), 480);
        loadout = new[] { pistol, new[] { "pistole", "dietrich" }, pistol, pistol };
        ReportRescue($"  Schließer kaufen, Hund abpassen, {lag / 20.0:0.0#} s", QuietLocksmith(lag, new[] { "Schließer" }), 520);
    }
    Console.WriteLine("Stellschraube B: Zweiter Posten im Hof, Preise wie jetzt");
    variant = m => m.Guards.Add(new UnitData { Name = "Hofposten", Sprite = "Wachmann", X = 12.5, Y = 8.5, Patrol = { new double[] { 9, 8 }, new double[] { 12, 8 } } });
    quietWatch = null;
    foreach (int lag in new[] { 0, 10 })
    {
        loadout = new[] { pistol, pistol, pistol, pistol };
        ReportRescue($"  Hundef. und Schließer kaufen, {lag / 20.0:0.0#} s", QuietWithMoney(lag), 600);
        loadout = new[] { pistol, new[] { "pistole", "totschlaeger" }, pistol, pistol };
        ReportRescue($"  Hundef. kaufen, beide schlagen, {lag / 20.0:0.0#} s", QuietWithMoney(lag, new[] { "Schließer", "Hofposten" }), 480);
        loadout = new[] { pistol, new[] { "pistole", "dietrich" }, pistol, pistol };
        ReportRescue($"  Beide im Hof kaufen, Hund abpassen, {lag / 20.0:0.0#} s", QuietLocksmith(lag, new[] { "Schließer", "Hofposten" }), 520);
        loadout = new[] { pistol, full, pistol, pistol };
        ReportRescue($"  Otto allein, beide schlagen, {lag / 20.0:0.0#} s", QuietLocksmith(lag), 0);
    }
    Console.WriteLine("Stellschraube C: Ein Bursche im Hof, unbewaffnet, läuft zum Melden, kostet 50");
    variant = m => m.Guards.Add(new UnitData { Name = "Hofposten", Sprite = "Laufbursche", Type = "laufbursche", X = 12.5, Y = 8.5, Patrol = { new double[] { 9, 8 }, new double[] { 12, 8 } } });
    quietWatch = null;
    foreach (int lag in new[] { 0, 10, 20 })
    {
        loadout = null;
        ReportRescue($"  Alle drei kaufen (550), {lag / 20.0:0.0#} s", QuietWithMoney(lag), 600);
        ReportRescue($"  Burschen nicht beachten, {lag / 20.0:0.0#} s", QuietWithMoney(lag), 500);
        loadout = new[] { pistol, new[] { "pistole", "totschlaeger" }, pistol, pistol };
        ReportRescue($"  Hundef. + Bursche kaufen, Schließer schlagen, {lag / 20.0:0.0#} s", QuietWithMoney(lag, new[] { "Schließer" }), 350);
    }
    variant = null;
    quietWatch = null;
    loadout = null;
    return;
}

// Dritter Ausbau Arme: --arme misst die lauten Wege mit vier Maschinenpistolen je Stufe, im Einsatz der Art Befreien dazu einen leisen Weg.
if (args.Length > 0 && args[0] == "--arme")
{
    bool rescue = LitMission().Prisoners.Count > 0;
    foreach (int level in new[] { 1, 2, 3 })
    {
        variant = m =>
        {
            m.Agents.ForEach(a => a.Arms = level);
            if (rescue)
            {
                m.Support = 0;
                m.Helpers = new List<string>();
            }
        };
        quietWatch = null;
        Console.WriteLine($"== Arme Stufe {level} ({missionId}, je {Runs} Läufe)");
        loadout = new[] { new[] { "maschinenpistole" } };
        if (rescue)
        {
            ReportRescue("Laut, Trupp zusammen, vier MP", Rescue(false, true, focus: true));
            loadout = null;
            ReportRescue("Laut, Trupp zusammen, Pistolen", Rescue(false, true, focus: true));
            // Die Wege aus --wege bei Rückhalt 5 zum Vergleich: Wo nicht geschossen wird, darf sich mit den Armen nichts ändern.
            variant = m => { m.Agents.ForEach(a => a.Arms = level); m.Support = 5; m.Helpers = m.Doors.Select(d => d.Helper).Where(h => h.Length > 0).ToList(); };
            quietWatch = null;
            homeRoute = "gang";
            ReportRescue("Weg gang, Otto und Emil mit Geld", QuietWithMoney(0));
            loadout = new[] { new[] { "pistole" }, new[] { "pistole", "totschlaeger", "dietrich" }, new[] { "pistole" }, new[] { "pistole" } };
            ReportRescue("Weg gang, Otto allein mit Dietrich", QuietLocksmith(0), 0);
        }
        else
        {
            Report("Feuer bündeln, vier MP", Bandaged(FocusFire));
            Report("Alle angreifen, vier MP", Bandaged(AttackAll));
            loadout = null;
            Report("Feuer bündeln, Pistolen", Bandaged(FocusFire));
        }
    }
    loadout = null;
    variant = null;
    return;
}

if (args.Length > 0 && args[0] == "--waffen")
{
    var kits = new (string Name, string[][]? Kit, bool Catalog)[]
    {
        ("Pistole ohne Nachladen", null, false),
        ("Pistole mit Magazin", null, true),
        ("Alle Schrotflinte", new[] { new[] { "schrotflinte" } }, true),
        ("Alle MP", new[] { new[] { "maschinenpistole" } }, true),
        ("Pistole + Verband", new[] { new[] { "pistole", "verbandszeug" } }, true),
        ("Gemischt", new[] { new[] { "maschinenpistole" }, new[] { "schrotflinte", "dietrich" }, new[] { "pistole", "verbandszeug" }, new[] { "schrotflinte" } }, true),
    };
    foreach (var kit in kits)
    {
        loadout = kit.Kit;
        withCatalog = kit.Catalog;
        int cost = (kit.Kit ?? new[] { ItemCatalog.StarterKit }).Take(4).Sum(k => k.Sum(id => catalog.Get(id)?.Price ?? 0)) * (kit.Kit is { Length: 1 } || kit.Kit == null ? 4 : 1);
        Console.WriteLine($"--- {kit.Name}, Ausrüstung {cost}");
        Report("Alle angreifen", Bandaged(AttackAll));
        Report("Feuer bündeln", Bandaged(FocusFire));
        Report("Zum Buch gehen", Bandaged(Rush));
    }
    return;
}

// Kampagne mit Einkauf: Wer was kauft, mit dem Startgeld und dem Lohn der Einsätze. Gekauftes kommt in den Platz des Agenten,
// in die Hand nimmt er die teuerste Schusswaffe.
if (args.Length > 0 && args[0] == "--einkauf")
{
    var shops = new (string Name, Action<Campaign>? Shop)[]
    {
        ("nichts", null),
        ("Verbände, ohne Rückhalt", Shopper(0, "verbandszeug")),
        ("Schrot, ohne Rückhalt", Shopper(0, "schrotflinte", "verbandszeug")),
        ("Verbände, Rückhalt 250", Shopper(Campaign.RecruitCost, "verbandszeug")),
        ("Schrot, Rückhalt 250", Shopper(Campaign.RecruitCost, "schrotflinte", "verbandszeug")),
        ("Verband+Schrot, R. 250", Shopper(Campaign.RecruitCost, "verbandszeug", "schrotflinte")),
        ("Schrot, Rückhalt 350", Shopper(Campaign.RecruitCost + Campaign.DoctorCost, "schrotflinte", "verbandszeug")),
        ("MP, Rückhalt 250", Shopper(Campaign.RecruitCost, "maschinenpistole", "verbandszeug")),
        ("Knüppel, Rückhalt 250", Shopper(Campaign.RecruitCost, "totschlaeger")),
    };
    Console.WriteLine("Je 100 Kampagnen mit 6 Einsätzen, Feuer bündeln:");
    foreach (var s in shops)
    {
        CampaignRun(s.Name, Armed(Bandaged(FocusFire)), 100, 6, s.Shop);
    }
    return;
}

// Totschläger nach Plan: Wie beim Schleichplan sucht das Werkzeug für Greta den frühesten Takt, zu dem ein Angriffsbefehl auf eine Wache
// zu einem lautlosen Niederschlag führt, ohne dass eine andere Wache kämpft. Dann die nächste Wache von dort aus.
// Danach kämpft der Trupp wie bei Feuer bündeln, mit weniger Wachen gegen sich. Die Klicks kommen in der Nachprobe zufällig bis zu jitter Takte zu früh oder zu spät.
if (args.Length > 0 && args[0] == "--knueppel-plan")
{
    loadout = new[] { new[] { "totschlaeger", "pistole" } };
    var orders = new List<(int Tick, int Guard)>();
    bool Calm(World w) => w.Rating != MissionRating.Loud && !w.Units.Any(g => g.Faction == Faction.Guard && g.Alive
        && (g.GuardState == GuardState.Combat || g.GuardState == GuardState.Report));
    // Spielt die Befehle bis zum letzten Niederschlag nach. Liefert die Welt danach oder null, wenn etwas schiefgeht.
    World? Play(List<(int Tick, int Guard)> plan, int seed, int jitter, Random? rng)
    {
        var w = NewWorld(seed);
        foreach (var (tick, guard) in plan)
        {
            int at = Math.Max(0, tick + (rng?.Next(-jitter, jitter + 1) ?? 0));
            while (w.Tick < at && w.State == MissionState.Running)
            {
                w.Step();
                if (!Calm(w)) return null;
            }
            w.OrderAttack(0, guard);
            for (int i = 0; i < 400 && w.Units[guard].Alive; i++)
            {
                w.Step();
                if (!Calm(w)) return null;
            }
            if (w.Units[guard].Alive) return null;
            w.Units[0].TargetId = -1;
            w.Units[0].Path.Clear();
            // Nach dem Schlag 2 s still: Kommt dann doch jemand, zählt der Schlag nicht als gelungen.
            for (int i = 0; i < 40; i++)
            {
                w.Step();
                if (!Calm(w)) return null;
            }
        }
        return w;
    }
    while (true)
    {
        var start = Play(orders, 1927, 0, null)!;
        int from = start.Tick;
        var prey = start.Units.Where(g => g.Faction == Faction.Guard && g.Alive && g.Type != GuardType.Werkschutz && g.Type.WeaponRange > 0).Select(g => g.Id).ToList();
        (int Tick, int Guard, int Window)? best = null;
        foreach (int g in prey)
        {
            int first = -1, window = 0;
            for (int t = from; t < from + 1200 && (first < 0 || window < 200); t += 2)
            {
                bool ok = Play(new List<(int Tick, int Guard)>(orders) { (t, g) }, 1927, 0, null) != null;
                if (ok && first < 0) first = t;
                if (ok && first >= 0) window += 2;
                else if (first >= 0) break;
            }
            if (first >= 0 && (best == null || first < best.Value.Tick))
            {
                best = (first + window / 2, g, window);
            }
        }
        if (best == null) break;
        orders.Add((best.Value.Tick, best.Value.Guard));
        Console.WriteLine($"Schlag {orders.Count}: {start.Units[best.Value.Guard].Name} ({start.Units[best.Value.Guard].Type.Title}), Befehl bei {best.Value.Tick / 20.0:0.0} s, Fenster {best.Value.Window / 20.0:0.0} s");
    }
    var after = Play(orders, 1927, 0, null)!;
    Console.WriteLine($"Ohne Aufsehen niedergeschlagen: {orders.Count}, danach stehen noch {after.Units.Count(g => g.Faction == Faction.Guard && g.Alive)} Wachen, Takt {after.Tick / 20.0:0.0} s, Wertung {after.Rating}");
    foreach (int jitter in new[] { 0, 5, 10, 20 })
    {
        var rng = new Random(1927);
        int won = 0, silent = 0, failed = 0, ko = 0, deadAgents = 0;
        for (int seed = 1; seed <= Runs; seed++)
        {
            var w = Play(orders, seed * 7919, jitter, rng);
            if (w == null)
            {
                // Plan gescheitert: Ab dem Fehler kämpft der Trupp wie bei Feuer bündeln. Das spielt hier eine neue Welt mit gleichem Zufall nach.
                failed++;
                w = NewWorld(seed * 7919);
                var r2 = new Random(1927 + seed);
                foreach (var (tick, guard) in orders)
                {
                    int at = Math.Max(0, tick + r2.Next(-jitter, jitter + 1));
                    while (w.Tick < at && Calm(w)) w.Step();
                    if (!Calm(w)) break;
                    w.OrderAttack(0, guard);
                    for (int i = 0; i < 400 && w.Units[guard].Alive && Calm(w); i++) w.Step();
                    if (!Calm(w)) break;
                    w.Units[0].TargetId = -1;
                }
            }
            ko += w.Strikes.Count(k => k.KnockOut);
            var finish = Armed(Bandaged(FocusFire))();
            for (int i = 0; i < MaxTicks && w.State == MissionState.Running; i++)
            {
                if (i % 10 == 0) finish(w);
                w.Step();
            }
            won += w.State == MissionState.Won ? 1 : 0;
            silent += w.State == MissionState.Won && w.Rating != MissionRating.Loud ? 1 : 0;
            deadAgents += w.Units.Count(u => u.Faction == Faction.Agent && !u.Alive);
        }
        Console.WriteLine($"Knüppel nach Plan, Klick +-{jitter / 20.0:0.00} s: Sieg {100 * won / Runs,3}%  leise gewonnen {100 * silent / Runs,3}%  Plan gescheitert {100 * failed / Runs,3}%  geschlagen {(double)ko / Runs:0.0}  tote Agenten {(double)deadAgents / Runs:0.00}");
    }
    return;
}

// Totschläger: Greta schleicht und schlägt, der Trupp wartet oder folgt. Alle tragen Knüppel und Pistole.
// Was jede Mark bringt: gemischte Trupps, Pistole bleibt als zweite Waffe, Waffenwechsel wie ein Spieler. Gemessen mit Feuer bündeln.
if (args.Length > 0 && args[0] == "--preis")
{
    string[] P = { "pistole" }, S = { "pistole", "schrotflinte" }, M = { "pistole", "maschinenpistole" };
    var kits = new (string Name, string[][] Kit)[]
    {
        ("4 Pistolen", new[] { P }),
        ("1 Schrot", new[] { S, P, P, P }),
        ("2 Schrot", new[] { S, S, P, P }),
        ("4 Schrot", new[] { S }),
        ("1 MP", new[] { M, P, P, P }),
        ("2 MP", new[] { M, M, P, P }),
        ("4 MP", new[] { M }),
        ("1 MP, 1 Schrot", new[] { M, S, P, P }),
    };
    foreach (var kit in kits)
    {
        loadout = kit.Kit;
        int extra = Enumerable.Range(0, 4).Sum(i => kit.Kit[Math.Min(i, kit.Kit.Length - 1)].Skip(1).Sum(id => catalog.Get(id)!.Price));
        Console.Write($"{kit.Name,-16} Zukauf {extra,4}  ");
        Report("bündeln", Armed(Bandaged(FocusFire)));
    }
    loadout = null;
    return;
}

if (args.Length > 0 && args[0] == "--knueppel")
{
    loadout = new[] { new[] { "totschlaeger", "pistole" } };
    Report("Knüppel, Trupp wartet", Clubbing(false));
    Report("Knüppel, Trupp folgt", Clubbing(true));
    Report("Nur lauern, kein Schlag", Clubbing(false, false));
    loadout = null;
    Report("Lauern mit Pistole", Clubbing(false, false));
    Report("Feuer bündeln (Vergleich)", FocusFire);
    return;
}

if (args.Length > 0 && args[0] == "--kampagne")
{
    Console.WriteLine("Je 100 Kampagnen mit 6 Einsätzen derselben Mission:");
    CampaignRun("Zum Buch gehen", Rush, 100, 6);
    CampaignRun("Alle angreifen", AttackAll, 100, 6);
    CampaignRun("Feuer bündeln", FocusFire, 100, 6);
    CampaignRun("Stehen und schießen", HoldThenRush, 100, 6);
    return;
}

// Lageplan als Text: . nie bewacht, 1-9 Anteil in Zehnteln, # gesperrt, ein großes W auf dem Weg einer Wache.
if (args.Length > 0 && args[0] == "--lageplan")
{
    var mission = LitMission();
    var watch = WatchMap.Measure(Loader.ParseMap(mapJson), mission);
    var blocked = NewWorld(1927).Map;
    Console.WriteLine("    " + string.Concat(Enumerable.Range(0, watch.Width).Select(x => (x % 10).ToString())));
    for (int y = 0; y < watch.Height; y++)
    {
        var line = new System.Text.StringBuilder($"{y,3} ");
        for (int x = 0; x < watch.Width; x++)
        {
            double share = watch.Share(x, y);
            line.Append(blocked.IsBlocked(x, y) ? '#' : watch.Routes.Contains((x, y)) ? 'W' : share == 0 ? '.' : (char)('0' + Math.Clamp((int)Math.Ceiling(share * 10), 1, 9)));
        }
        Console.WriteLine(line);
    }
    foreach (int lag in new[] { 0, 5, 10, 20 })
    {
        Sneak(new[] { (6, 15), (6, 5), (6, 3), (6, 5), (6, 14), (18, 18), (18, 11) }, 100, lag);
    }
    return;
}


// Mit Feldern als Argument, etwa "6,9 6,8": Welche Wache schaut wann hin? Eine Spalte sind 5 Takte.
if (args.Length > 0)
{
    var calm = NewWorld(1927);
    var rows = args.Select(_ => new System.Text.StringBuilder()).ToList();
    for (int t = 0; t < 900; t++)
    {
        if (t >= 500 && t % 5 == 0)
        {
            for (int r = 0; r < args.Length; r++)
            {
                int fx = int.Parse(args[r].Split(',')[0]), fy = int.Parse(args[r].Split(',')[1]);
                rows[r].Append(calm.WatcherOf(fx, fy)?.Id.ToString("x") ?? ".");
            }
        }
        calm.Step();
    }
    for (int r = 0; r < args.Length; r++)
    {
        Console.WriteLine($"{args[r],6} {rows[r]}");
    }
    return;
}

Report("Zum Buch gehen", Rush);
Report("Zum Buch, Hund ab", RushShootDog);
Report("Zum Buch sprinten", RushSprint);
Report("Alle angreifen", AttackAll);
Report("Feuer bündeln", FocusFire);
Report("Hinterhalt", Ambush);
Report("Stehen und schießen", HoldThenRush);

// Schleichplan, wie ihn ein Spieler klickt: Greta allein, die anderen warten am Start.
var plan = new (int X, int Y, string Note)[]
{
    (6, 15, "Versteck vor der Straße"),
    (6, 5, "über die Straße ins Versteck in der Gasse"),
    (6, 3, "zum Buch, aufnehmen"),
    (6, 5, "zurück ins Versteck"),
    (6, 14, "zurück über die Straße"),
    (18, 18, "hinter der Lagerhalle nach Osten"),
    (18, 11, "zum Fluchtwagen"),
};

// Spielt die Klicks nach. Liefert false, sobald eine Wache etwas bemerkt.
bool Replay(World w, List<int> times, int stages)
{
    for (int stage = 0; stage < stages; stage++)
    {
        while (w.Tick < times[stage])
        {
            w.Step();
            if (w.Rating != MissionRating.Unseen)
            {
                return false;
            }
        }
        w.OrderMove(0, plan[stage].X, plan[stage].Y);
        var a = w.Units[0];
        bool book = (plan[stage].X, plan[stage].Y) == (w.ItemX / World.TileUnits, w.ItemY / World.TileUnits) && !w.ItemTaken;
        bool last = stage == plan.Length - 1;
        while ((a.Path.Count > 0 || (book && !w.ItemTaken) || last) && w.State == MissionState.Running)
        {
            w.Step();
            if (w.Rating != MissionRating.Unseen)
            {
                return false;
            }
        }
    }
    return true;
}

// Für jeden Klick das erste ausreichend große Zeitfenster suchen und dessen Mitte nehmen.
var chosen = new List<int>();
int earliest = 0;
bool planWorks = true;
for (int stage = 0; stage < plan.Length && planWorks; stage++)
{
    var good = new List<int>();
    for (int t = earliest; t < earliest + SearchTicks; t++)
    {
        var w = NewWorld(1927);
        var times = new List<int>(chosen) { t };
        if (Replay(w, times, stage + 1))
        {
            good.Add(t);
        }
    }
    var runs = new List<(int From, int To)>();
    foreach (int t in good)
    {
        if (runs.Count > 0 && runs[^1].To == t - 1)
        {
            runs[^1] = (runs[^1].From, t);
        }
        else
        {
            runs.Add((t, t));
        }
    }
    if (runs.Count == 0)
    {
        Console.WriteLine($"Klick {stage + 1} ({plan[stage].Note}): kein Zeitfenster ab Takt {earliest}");
        planWorks = false;
        break;
    }
    var pick = runs.FirstOrDefault(r => r.To - r.From + 1 >= MinWindowTicks);
    if (pick == default)
    {
        pick = runs.OrderByDescending(r => r.To - r.From).First();
    }
    int center = (pick.From + pick.To) / 2;
    chosen.Add(center);
    var after = NewWorld(1927);
    Replay(after, chosen, stage + 1);
    earliest = after.Tick;
    Console.WriteLine($"Klick {stage + 1} auf {plan[stage].X},{plan[stage].Y} bei {center / 20.0,5:0.0} s: Fenster {(pick.To - pick.From + 1) / 20.0:0.0} s  ({plan[stage].Note})");
}

if (planWorks)
{
    // Nachprobe: Jeder Klick kommt zufällig zu früh oder zu spät.
    foreach (int jitter in new[] { 0, 5, 10, 20, 40 })
    {
        var rng = new Random(1927);
        int won = 0, unseen = 0, quiet = 0, loud = 0, died = 0, ticks = 0;
        for (int seed = 1; seed <= Runs; seed++)
        {
            var w = NewWorld(seed * 7919);
            var times = chosen.Select(t => Math.Max(0, t + rng.Next(-jitter, jitter + 1))).ToList();
            int next = 0;
            bool waitBook = false;
            for (int i = 0; i < MaxTicks && w.State == MissionState.Running && w.Units[0].Alive; i++)
            {
                if (next < plan.Length && w.Units[0].Path.Count == 0 && w.Tick >= times[next] && (!waitBook || w.ItemTaken))
                {
                    waitBook = (plan[next].X, plan[next].Y) == (w.ItemX / World.TileUnits, w.ItemY / World.TileUnits) && !w.ItemTaken;
                    w.OrderMove(0, plan[next].X, plan[next].Y);
                    next++;
                }
                w.Step();
            }
            if (w.State == MissionState.Won)
            {
                won++;
                ticks += w.Tick;
                unseen += w.Rating == MissionRating.Unseen ? 1 : 0;
                quiet += w.Rating == MissionRating.Quiet ? 1 : 0;
                loud += w.Rating == MissionRating.Loud ? 1 : 0;
            }
            died += w.Units[0].Alive ? 0 : 1;
        }
        Console.WriteLine($"Schleichen, Klick +-{jitter / 20.0:0.00} s: Sieg {100 * won / Runs,3}%  Greta gefallen {100 * died / Runs,3}%  unentdeckt {unseen}, leise {quiet}, laut {loud}  Dauer {(won > 0 ? ticks / won / 20 : 0)} s");
    }
}

static string FindDataDir()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir != null && !File.Exists(Path.Combine(dir.FullName, "README.md")))
    {
        dir = dir.Parent;
    }
    return Path.Combine(dir?.FullName ?? ".", "data");
}
