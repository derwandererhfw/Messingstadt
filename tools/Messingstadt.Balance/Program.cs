using Messingstadt.Core;

// Misst die Spielweisen einer Mission in der echten Spiellogik.
// Aufruf: dotnet run -c Release --project tools/Messingstadt.Balance
// Kampagne über mehrere Einsätze: ... -- --kampagne

const int Runs = 300;
const int MaxTicks = 20 * 300;
const int SearchTicks = 400;
const int MinWindowTicks = 30;

string dataDir = FindDataDir();
string mapJson = File.ReadAllText(Path.Combine(dataDir, "map_01.json"));
string missionJson = File.ReadAllText(Path.Combine(dataDir, "mission_01.json"));
string lightJson = File.ReadAllText(Path.Combine(dataDir, "light_01.json"));

MissionData LitMission()
{
    var mission = Loader.ParseMission(missionJson);
    mission.Light = Loader.ParseLight(lightJson);
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
    int wins = 0, lost = 0, dead = 0, hp = 0, ticks = 0, loud = 0;
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
    }
    Console.WriteLine($"{name,-18} Sieg {100 * wins / Runs,3}%  verloren {100 * lost / Runs,3}%  tote Agenten {(double)dead / Runs:0.00}  LP {hp / Runs,3}/400  Dauer {ticks / Runs / 20,3}s  laut {100 * loud / Runs,3}%");
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
Action<World> FocusFire()
{
    var rush = Rush();
    return w =>
    {
        var squad = w.Units.Where(u => u.Faction == Faction.Agent && u.Alive).ToList();
        var guards = w.Units.Where(u => u.Faction == Faction.Guard && u.Alive).ToList();
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

// Kampagne: Viele Kampagnen über mehrere Einsätze mit einer Spielweise. Zwischen den Einsätzen erst Arzt, dann Anwerben, Rest in Ausbau.
void CampaignRun(string name, Func<Action<World>> make, int campaigns, int missions)
{
    int won = 0, played = 0, deaths = 0, recruits = 0, shortSquads = 0, heat = 0, money = 0;
    for (int run = 1; run <= campaigns; run++)
    {
        var c = new Campaign();
        for (int m = 0; m < missions; m++)
        {
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
            foreach (var a in c.Roster().Where(a => a.Rest > 0).ToList())
            {
                c.TryDoctor(a.Name);
            }
            foreach (var a in c.Roster().Where(a => a.Dead).ToList())
            {
                c.TryRecruit(a.Slot);
            }
            foreach (var a in c.Roster().Where(a => !a.Dead).OrderBy(a => a.Legs + a.Heart).ToList())
            {
                bool legs = a.Legs <= a.Heart;
                if (c.Money - Campaign.UpgradeCost(legs ? a.Legs : a.Heart) >= Campaign.RecruitCost)
                {
                    c.TryUpgrade(a.Name, legs);
                }
            }
        }
        deaths += c.Agents.Count(a => a.Dead);
        recruits += c.Agents.Count(a => a.Job != "");
        heat += c.Heat;
        money += c.Money;
    }
    Console.WriteLine($"{name,-18} Sieg {100 * won / played,3}%  Tote je Kampagne {(double)deaths / campaigns:0.0}  Rekruten {(double)recruits / campaigns:0.0}  "
        + $"Einsätze ohne vollen Trupp {100 * shortSquads / played,3}%  Fahndung am Ende {(double)heat / campaigns:0.0}  Kasse am Ende {money / campaigns}");
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
