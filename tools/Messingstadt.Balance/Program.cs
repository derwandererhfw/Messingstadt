using Messingstadt.Core;

// Misst die Spielweisen einer Mission in der echten Spiellogik.
// Aufruf: dotnet run -c Release --project tools/Messingstadt.Balance
// Kampagne über mehrere Einsätze: ... -- --kampagne
// Ausrüstung im Vergleich: ... -- --waffen, gemischte Trupps je Mark: -- --preis
// Kampagne mit Einkauf: -- --einkauf. Totschläger: -- --knueppel-plan (Zeitfenster wie beim Schleichplan) und -- --knueppel (ohne Plan)
// Mit --ohne-magazin vorweg gilt die Pistole der ersten Fassung, zum Vergleich mit älteren Messungen.
// Werte ausprobieren, ohne die Datei zu ändern: --wert maschinenpistole.Magazine=20, Startgeld der Kampagnen: --startgeld 600
// Anderer Einsatz: --mission=mission_02 vorweg. Einsätze der Art Befreien messen dann ihre eigenen Spielweisen.

const int Runs = 300;
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

MissionData LitMission()
{
    var mission = Loader.ParseMission(missionJson);
    mission.Light = Loader.ParseLight(lightJson);
    mission.Catalog = withCatalog ? catalog : null;
    for (int i = 0; loadout != null && i < mission.Agents.Count; i++)
    {
        mission.Agents[i].Items = loadout[Math.Min(i, loadout.Length - 1)].ToList();
    }
    return mission;
}

World NewWorld(int seed)
{
    var mission = LitMission();
    mission.Seed = seed;
    return new World(Loader.ParseMap(mapJson), mission);
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
Func<Action<World>> Rescue(bool alone, bool fightFirst, string[]? bribes = null, bool plan = false, string? gate = null) => () =>
{
    var attack = AttackAll();
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
            if (emil == null || target == null || !target.Alive || target.Bribed || target.GuardState == GuardState.Combat || w.Money < target.Type.BribeCost)
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

// Der leise Weg von Einsatz 2, wie ihn die Vorlage beschreibt: Otto und Emil warten im Durchgang, queren die Wallstraße im Fenster
// des Lageplans, Emil kauft Hundeführer und Schließer, beide öffnen je eine Zelle und führen die Gefangenen im nächsten Fenster zurück.
// lag: Der Klick kommt zufällig bis zu so viele Takte zu spät.
int quietRun = 0;
WatchMap? quietWatch = null;
Func<Action<World>> QuietWithMoney(int lag) => () =>
{
    var watch = quietWatch ??= WatchMap.Measure(Loader.ParseMap(mapJson), LitMission());
    // Jeder Lauf verspätet sich anders, sonst gäbe es nur ganz oder gar nicht.
    var rng = new Random(1927 + quietRun++);
    int stage = 0, goAt = -1;
    return w =>
    {
        var otto = w.Units.Find(u => u.Skill == World.SkillLocksmith && u.Alive);
        var emil = w.Units.Find(u => u.Skill == World.SkillBookkeeper && u.Alive);
        if (otto == null || emil == null)
        {
            return;
        }
        Unit Guard(string name) => w.Units.First(u => u.Name == name);
        int Door(string name) => w.Doors.FindIndex(d => d.Name == name);
        bool At(Unit u, int x, int y) => u.Path.Count == 0 && u.TileX == x && u.TileY == y;
        // Ist die Straße zwischen zwei Feldern frei, wenn man jetzt losgeht? Geprüft wird nur das Stück auf der Wallstraße.
        bool StreetClear(Unit u, int x, int y, int speed)
        {
            var path = w.Map.FindPath(u.TileX, u.TileY, x, y);
            if (path == null)
            {
                return false;
            }
            var street = path.Where(t => t.Y >= 9 && t.Y <= 13).ToList();
            return street.Count == 0 || watch.FirstRisk(street, street[0].X * 1000 + 500, street[0].Y * 1000 + 500, speed, w.Tick + 20, 0) < 0;
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
        var handler = Guard("Hundeführer");
        var keeper = Guard("Schließer");
        switch (stage)
        {
            case 0:
                w.OrderMove(otto.Id, 7, 15);
                w.OrderMove(emil.Id, 6, 15);
                stage = 1;
                break;
            case 1:
                if (At(otto, 7, 15) && At(emil, 6, 15) && Go(StreetClear(emil, 6, 9, World.WalkSpeed(emil))))
                {
                    w.OrderMove(emil.Id, 6, 9);
                    w.OrderMove(otto.Id, 6, 10);
                    stage = 2;
                }
                break;
            case 2:
                if (emil.TileY <= 10 && emil.BribeTarget != handler.Id && !handler.Bribed && handler.Alive)
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
                if (w.Doors[Door("Pforte zur Gerichtsgasse")].Open)
                {
                    if (!keeper.Bribed && keeper.Alive && keeper.GuardState != GuardState.Combat && emil.BribeTarget != keeper.Id)
                    {
                        w.OrderBribe(emil.Id, keeper.Id);
                    }
                    else if (keeper.Bribed || !keeper.Alive || keeper.GuardState == GuardState.Combat)
                    {
                        stage = 4;
                    }
                }
                break;
            case 4:
                foreach (var p in w.Prisoners.Where(p => p.Alive && p.Captive))
                {
                    var helper = p.Name.StartsWith("Paul") && emil.Keys.Contains(p.CellDoor) ? emil : otto;
                    if (!w.Doors[p.CellDoor].Open && helper.DoorTarget < 0)
                    {
                        w.OrderOpenDoor(helper.Id, p.CellDoor);
                        if (helper == otto)
                        {
                            break;
                        }
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
                if (At(otto, 5, 6) && At(emil, 6, 6) && gathered && Go(StreetClear(emil, 7, 15, World.PrisonerSpeed)))
                {
                    w.OrderMove(emil.Id, 6, 15);
                    w.OrderMove(otto.Id, 7, 15);
                    stage = 6;
                }
                break;
            case 6:
                if (At(otto, 7, 15) && At(emil, 6, 15) && w.Prisoners.Where(p => p.Alive).All(p => p.TileY >= 13))
                {
                    w.OrderMove(otto.Id, 1, 18);
                    w.OrderMove(emil.Id, 0, 18);
                    stage = 7;
                }
                break;
        }
    };
};

// Der leise Weg ohne Geld: Otto allein mit Totschläger. Er passt Hund und Streifen nach dem Lageplan ab, öffnet die Pforte,
// schlägt den Schließer nieder, holt beide und wartet im Hof auf das Fenster für den Rückweg.
Func<Action<World>> QuietLocksmith(int lag) => () =>
{
    var watch = quietWatch ??= WatchMap.Measure(Loader.ParseMap(mapJson), LitMission());
    var rng = new Random(4711 + quietRun++);
    int stage = 0, goAt = -1;
    return w =>
    {
        var otto = w.Units.Find(u => u.Skill == World.SkillLocksmith && u.Alive);
        if (otto == null)
        {
            return;
        }
        int pforte = w.Doors.FindIndex(d => d.Name == "Pforte zur Gerichtsgasse");
        var keeper = w.Units.First(u => u.Name == "Schließer");
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
                stage = 1;
                break;
            case 1:
                int doorTicks = w.HasItem(otto, ItemKind.Lockpick) ? World.DoorTicks / 2 : World.DoorTicks;
                if (At(7, 15) && Go(Clear(6, 7, World.WalkSpeed(otto), doorTicks + 10)))
                {
                    w.OrderOpenDoor(otto.Id, pforte);
                    stage = 2;
                }
                break;
            case 2:
                if (w.Doors[pforte].Open)
                {
                    int club = otto.Items.FindIndex(i => i.Def.Kind == ItemKind.Melee);
                    if (club >= 0)
                    {
                        w.SelectWeapon(otto.Id, club);
                    }
                    w.OrderAttack(otto.Id, keeper.Id);
                    stage = 3;
                }
                break;
            case 3:
                if (!keeper.Alive || w.Tick % 200 == 0)
                {
                    stage = keeper.Alive ? 3 : 4;
                }
                break;
            case 4:
                var captive = w.Prisoners.FirstOrDefault(p => p.Alive && p.Captive);
                if (captive == null)
                {
                    w.OrderMove(otto.Id, 8, 7);
                    stage = 5;
                }
                else if (!w.Doors[captive.CellDoor].Open && otto.DoorTarget < 0)
                {
                    w.OrderOpenDoor(otto.Id, captive.CellDoor);
                }
                break;
            case 5:
                if (At(8, 7) && w.Prisoners.Where(p => p.Alive).All(p => p.Path.Count == 0) && Go(Clear(7, 15, World.PrisonerSpeed, 0)))
                {
                    w.OrderMove(otto.Id, 7, 15);
                    stage = 6;
                }
                break;
            case 6:
                if (At(7, 15) && w.Prisoners.Where(p => p.Alive).All(p => p.TileY >= 13))
                {
                    w.OrderMove(otto.Id, 1, 18);
                    stage = 7;
                }
                break;
        }
    };
};

void ReportRescue(string name, Func<Action<World>> make, int money = 600)
{
    int wins = 0, dead = 0, saved = 0, ticks = 0, unseen = 0, quietWins = 0, loud = 0, all = 0;
    for (int seed = 1; seed <= Runs; seed++)
    {
        var mission = LitMission();
        mission.Seed = seed * 7919;
        mission.Money = money;
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
        bool won = w.State == MissionState.Won;
        wins += won ? 1 : 0;
        dead += w.Units.Count(u => u.Faction == Faction.Agent && !u.Alive);
        saved += won ? w.Prisoners.Count(p => p.Alive) : 0;
        all += won && w.Prisoners.All(p => p.Alive) ? 1 : 0;
        ticks += w.Tick;
        unseen += won && w.Rating == MissionRating.Unseen ? 1 : 0;
        quietWins += won && w.Rating == MissionRating.Quiet ? 1 : 0;
        loud += w.Rating == MissionRating.Loud ? 1 : 0;
    }
    Console.WriteLine($"{name,-34} Sieg {100 * wins / Runs,3}%  beide gerettet {100 * all / Runs,3}%  tote Agenten {(double)dead / Runs:0.00}  unentdeckt {100 * unseen / Runs,3}%  leise {100 * quietWins / Runs,3}%  laut {100 * loud / Runs,3}%  Dauer {ticks / Runs / 20,3}s");
}

// Ein Lauf des leisen Wegs im Einzelnen: wer wann etwas bemerkt und wo der Trupp dann steht.
if (args.Length > 0 && args[0] == "--spur")
{
    var mission = LitMission();
    mission.Seed = 7919;
    mission.Money = 600;
    var w = new World(Loader.ParseMap(mapJson), mission);
    var strategy = QuietWithMoney(0)();
    var states = w.Units.ToDictionary(u => u.Id, u => u.GuardState);
    string last = "";
    for (int i = 0; i < MaxTicks && w.State == MissionState.Running; i++)
    {
        if (i % 10 == 0)
        {
            strategy(w);
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

if (LitMission().Prisoners.Count > 0 && args.Length == 0)
{
    Console.WriteLine($"{missionId}, je {Runs} Läufe, Kasse 600");
    ReportRescue("Alle zur Pforte, Otto öffnet", Rescue(false, false));
    ReportRescue("Erst kämpfen, dann befreien", Rescue(false, true));
    ReportRescue("Otto allein", Rescue(true, false));
    ReportRescue("Otto allein nach Lageplan", Rescue(true, false, plan: true));
    ReportRescue("Emil kauft Hundeführer, Otto allein", Rescue(true, false, new[] { "Hundeführer" }));
    ReportRescue("Emil kauft Hundef. und Schließer", Rescue(true, false, new[] { "Hundeführer", "Schließer" }));
    // Otto trägt je nach Zeile Totschläger und Dietrich, die anderen bleiben bei der Pistole.
    var pistol = new[] { "pistole" };
    foreach (var (label, kit) in new[] { ("Totschläger", new[] { "pistole", "totschlaeger" }), ("Totschl. + Dietrich", new[] { "pistole", "totschlaeger", "dietrich" }) })
    {
        loadout = new[] { pistol, kit, pistol, pistol };
        ReportRescue($"Leise ohne Geld, {label}", QuietLocksmith(0), 0);
        ReportRescue($"  dasselbe, bis 0,5 s spät", QuietLocksmith(10), 0);
    }
    loadout = null;
    ReportRescue("Laut durchs Hoftor, Pistolen", Rescue(false, true, gate: "Hoftor"));
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
