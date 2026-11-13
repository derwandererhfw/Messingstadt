using System.Numerics;
using System.Text.Json;

// Pathtracer für die Lichtkarten. Gerufen von tools/art/bake_light.py, das auch die Eingaben schreibt.
// Einheiten: x und y in Feldern, z in Feldern (Bildpixel geteilt durch ZScale).
// Jeder Bildpunkt eines Sprites wird über den Kamerastrahl auf sein Objekt zurückgerechnet und dort beleuchtet.
// Direktes Licht und Licht über Umwege werden getrennt ausgegeben, weil nur das zweite rauscht.

const int DirectSamples = 48;
const int IndirectSamples = 256;
const int GridSamples = 96;
const int Bounces = 3;
const float Eps = 0.004f;
const float LampRadius = 0.09f;
const float WindowRange = 7f;
const float WindowCell = 0.75f;
const int BaseChannels = 10;

string workDir = args[0];
using var sceneDoc = JsonDocument.Parse(File.ReadAllText(Path.Combine(workDir, "scene.json")));
var root = sceneDoc.RootElement;

float ox = root.GetProperty("ox").GetSingle(), oy = root.GetProperty("oy").GetSingle();
float zScale = root.GetProperty("zScale").GetSingle();
int mapSize = root.GetProperty("mapSize").GetInt32();
Vector3 skyColor = Vec(root.GetProperty("sky"));
// Himmelslicht fürs Schleichen: bleibt beim alten Wert, auch wenn das Bild dunkler oder heller wird.
Vector3 rulesSky = root.TryGetProperty("rulesSky", out var frozenSky) ? Vec(frozenSky) : skyColor;
Vector3 groundAlbedo = Vec(root.GetProperty("groundAlbedo"));
Vector3 lampColor = Vec(root.GetProperty("lampColor"));
Vector3 fireColor = Vec(root.GetProperty("fireColor"));
var windowTypes = new List<(Vector3 Color, float Power)>();
foreach (var t in root.GetProperty("windowTypes").EnumerateArray())
{
    windowTypes.Add((Vec(t.GetProperty("color")), t.GetProperty("power").GetSingle()));
}
float specStrength = root.GetProperty("specStrength").GetSingle();
Vector3 flashDir = Vector3.Normalize(Vec(root.GetProperty("flashDirection")));
float flashAmbient = root.GetProperty("flashAmbient").GetSingle();
Vector3 moonDir = Vector3.Normalize(Vec(root.GetProperty("moonDirection")));
Vector3 moonColor = Vec(root.GetProperty("moonColor"));
// Nasse Dächer spiegeln den Mond zur Kamera. Nur im Bild, das Lichtgitter der Regeln sieht davon nichts.
float moonSpec = root.TryGetProperty("moonSpec", out var moonSpecElement) ? moonSpecElement.GetSingle() : 0f;
// Farbe der Laternen im Bild. Die Spielregeln rechnen weiter mit lampColor, damit eine neue Lichtfarbe das Schleichen nicht ändert.
Vector3 lampPicture = root.TryGetProperty("lampPicture", out var lampPictureElement) ? Vec(lampPictureElement) : Vec(root.GetProperty("lampColor"));
float puddleSpec = root.GetProperty("puddleSpec").GetSingle();
Vector3 viewDir = Vector3.Normalize(new Vector3(1f / 32f, 1f / 32f, 1f / zScale));

var boxes = new List<Box>();
// Dachkörper je Quader: Grundfläche, Unterkante und Ebenen a·x + b·y + c·z <= d, z in Bildpunkten. Siehe tools/art/roofs.py.
var roofs = new List<float[][]>();
// Rückstrahlung für das Licht der Spielregeln: der Stand vor dem Umbau der Grafik, damit neue Farben das Schleichen nicht ändern.
var rulesAlbedo = new List<Vector3>();
Vector3 rulesGroundAlbedo = root.TryGetProperty("rulesGroundAlbedo", out var frozenGround) ? Vec(frozenGround) : groundAlbedo;
bool rulesPass = false;
// Geist: 1 = Kleinkram, der fürs Schleichlicht keinen Schatten wirft, 2 = Durchbrochenes, das nie einen wirft.
var ghost = new List<int>();
foreach (var b in root.GetProperty("boxes").EnumerateArray())
{
    var prisms = new List<float[]>();
    if (b.TryGetProperty("roof", out var roofElement))
    {
        foreach (var prism in roofElement.EnumerateArray())
        {
            var planes = new List<float>
            {
                -1f, 0f, 0f, -prism.GetProperty("x0").GetSingle(), 1f, 0f, 0f, prism.GetProperty("x1").GetSingle(),
                0f, -1f, 0f, -prism.GetProperty("y0").GetSingle(), 0f, 1f, 0f, prism.GetProperty("y1").GetSingle(),
                0f, 0f, -1f, -prism.GetProperty("z0").GetSingle(),
            };
            foreach (var plane in prism.GetProperty("planes").EnumerateArray())
            {
                for (int i = 0; i < 4; i++)
                {
                    planes.Add(plane[i].GetSingle());
                }
            }
            prisms.Add(planes.ToArray());
        }
    }
    roofs.Add(prisms.ToArray());
    boxes.Add(new Box(
        new Vector3(b.GetProperty("x0").GetSingle(), b.GetProperty("y0").GetSingle(), b.GetProperty("z0").GetSingle() / zScale),
        new Vector3(b.GetProperty("x1").GetSingle(), b.GetProperty("y1").GetSingle(), b.GetProperty("z1").GetSingle() / zScale),
        Vec(b.GetProperty("albedo"))));
    ghost.Add(b.TryGetProperty("ghost", out var ghostElement) ? ghostElement.GetInt32() : 0);
    rulesAlbedo.Add(b.TryGetProperty("rulesAlbedo", out var frozenAlbedo) ? Vec(frozenAlbedo) : Vec(b.GetProperty("albedo")));
}
var boxArray = boxes.ToArray();

float lampScale = root.TryGetProperty("lampScale", out var lampScaleElement) ? lampScaleElement.GetSingle() : 1f;
float lampReach = root.TryGetProperty("lampReach", out var lampReachElement) ? lampReachElement.GetSingle() : 0f;
var lamps = new List<Light>();
foreach (var l in root.GetProperty("lights").EnumerateArray())
{
    lamps.Add(new Light(
        new Vector3(l.GetProperty("x").GetSingle(), l.GetProperty("y").GetSingle(), l.GetProperty("z").GetSingle() / zScale),
        l.GetProperty("color").GetString() == "fire" ? fireColor : l.TryGetProperty("rgb", out var ownColor) ? Vec(ownColor) : lampPicture,
        l.GetProperty("color").GetString() == "fire" ? fireColor : lampColor,
        l.GetProperty("power").GetSingle(),
        l.GetProperty("layer").GetInt32(),
        l.GetProperty("hooded").GetBoolean(),
        l.TryGetProperty("scale", out var ownScale) ? ownScale.GetSingle() : lampScale,
        l.TryGetProperty("cut", out var ownCut) ? ownCut.GetSingle() : lampReach));
}
var lightArray = lamps.ToArray();

// Zusatzquellen, nur fürs Bild: Fenster, die einen Lichtteppich mit Fensterkreuz werfen, Leuchtschrift, Schweißlicht.
// Gruppe -1 brennt immer und geht ins Licht ein. Jede andere Gruppe bekommt einen eigenen Kanal, aus dem bake_light.py
// einen Lichtflicken macht, den das Spiel mit der Quelle ein- und ausblendet.
var extras = new List<Extra>();
int extraGroups = root.TryGetProperty("extraGroups", out var extraGroupsElement) ? extraGroupsElement.GetInt32() : 0;
if (root.TryGetProperty("extras", out var extrasElement))
{
    foreach (var e in extrasElement.EnumerateArray())
    {
        bool window = e.GetProperty("type").GetString() == "window";
        extras.Add(new Extra(
            e.GetProperty("group").GetInt32(), window, Vec(e.GetProperty("pos")), Vec(e.GetProperty("color")),
            e.GetProperty("power").GetSingle(), e.GetProperty("reach").GetSingle(),
            window ? e.GetProperty("axis").GetInt32() : 0, window ? e.GetProperty("c").GetSingle() : 0f,
            window ? e.GetProperty("a0").GetSingle() : 0f, window ? e.GetProperty("a1").GetSingle() : 0f,
            window ? e.GetProperty("z0").GetSingle() : 0f, window ? e.GetProperty("z1").GetSingle() : 0f,
            window ? e.GetProperty("bar").GetSingle() : 0f));
    }
}
var extraArray = extras.ToArray();
int Channels = BaseChannels + extraGroups;

var puddles = new List<(Vector2 Pos, float R)>();
foreach (var p in root.GetProperty("puddles").EnumerateArray())
{
    puddles.Add((new Vector2(p[0].GetSingle(), p[1].GetSingle()), p[2].GetSingle()));
}

// ---------- Sprites einlesen und Bildpunkte in die Welt zurückrechnen ----------

var sprites = new List<Sprite>();
byte[] masks = File.ReadAllBytes(Path.Combine(workDir, "scene.bin"));
int maskOffset = 0;
foreach (var sp in root.GetProperty("sprites").EnumerateArray())
{
    int w = sp.GetProperty("w").GetInt32(), h = sp.GetProperty("h").GetInt32();
    sprites.Add(new Sprite(sp.GetProperty("box").GetInt32(), sp.GetProperty("left").GetInt32(), sp.GetProperty("top").GetInt32(), w, h, maskOffset));
    maskOffset += w * h;
}
int stride = root.GetProperty("stride").GetInt32();

Surface Unproject(Sprite s, int ix, int iy)
{
    float sx = s.Left + (ix + 0.5f) * stride, sy = s.Top + (iy + 0.5f) * stride;
    float a = (sx - ox) / 32f, b = (sy - oy) / 16f;
    Vector3 At(float t)
    {
        float m = b + t / 16f;
        return new Vector3((a + m) / 2f, (m - a) / 2f, t / zScale);
    }
    if (s.Box < 0)
    {
        return new Surface(At(0f) + new Vector3(0, 0, Eps), Vector3.UnitZ, -1, true);
    }
    var box = boxArray[s.Box];
    // Jeder Bildpixel t: mit wachsendem t kommt der Strahl der Kamera näher. Der Eintritt von vorn ist das größte t im Quader.
    float tLow = float.MinValue, tHigh = float.MaxValue;
    float scale = 16f;
    float x0T = (2f * box.Min.X - a - b) * scale, x1T = (2f * box.Max.X - a - b) * scale;
    float y0T = (2f * box.Min.Y + a - b) * scale, y1T = (2f * box.Max.Y + a - b) * scale;
    float z0T = box.Min.Z * zScale, z1T = box.Max.Z * zScale;
    tLow = MathF.Max(MathF.Max(x0T, y0T), z0T);
    tHigh = MathF.Min(MathF.Min(x1T, y1T), z1T);
    // Dach: derselbe Schnitt mit jedem Dachkörper. Der vorderste Treffer gewinnt, auch gegen den Quader darunter.
    float roofT = float.MinValue;
    Vector3 roofNormal = default;
    foreach (var prism in roofs[s.Box])
    {
        float lo = float.MinValue, hi = float.MaxValue;
        int face = -1;
        for (int i = 0; i < prism.Length; i += 4)
        {
            float pa = prism[i], pb = prism[i + 1], pc = prism[i + 2], pd = prism[i + 3];
            float k = (pa + pb) / 32f + pc;
            float rhs = pd - pa * (a + b) / 2f - pb * (b - a) / 2f;
            if (k > 1e-6f)
            {
                if (rhs / k < hi)
                {
                    hi = rhs / k;
                    face = i;
                }
            }
            else if (k < -1e-6f)
            {
                lo = MathF.Max(lo, rhs / k);
            }
            else if (rhs < 0f)
            {
                lo = float.MaxValue;
            }
        }
        if (lo <= hi && face >= 0 && hi > roofT)
        {
            roofT = hi;
            roofNormal = Vector3.Normalize(new Vector3(prism[face], prism[face + 1], prism[face + 2] * zScale));
        }
    }
    if (roofT > float.MinValue && (tLow > tHigh || roofT > tHigh + 0.01f))
    {
        return new Surface(At(roofT) + roofNormal * Eps, roofNormal, s.Box, false, roofNormal.Z > 0.05f);
    }
    if (tLow <= tHigh)
    {
        var n = tHigh == z1T ? Vector3.UnitZ : tHigh == x1T ? Vector3.UnitX : Vector3.UnitY;
        return new Surface(At(tHigh) + n * Eps, n, s.Box, false);
    }
    // Teile außerhalb des Quaders, etwa Schilder oder Wölbungen: nächster Punkt des Strahls, Fläche zur Kamera gedreht.
    float bestT = z0T, bestD = float.MaxValue;
    for (float t = z0T - 40f; t <= z1T + 60f; t += 1f)
    {
        var p = At(t);
        var q = Vector3.Clamp(p, box.Min, box.Max);
        float d = Vector3.DistanceSquared(p, q);
        if (d < bestD)
        {
            bestD = d;
            bestT = t;
        }
    }
    return new Surface(At(bestT) + viewDir * 0.02f, Vector3.Normalize(new Vector3(0.6f, 0.6f, 0.5f)), s.Box, false);
}

// ---------- Strahlen ----------

bool Unseen(int box) => ghost[box] == 2 || (ghost[box] == 1 && rulesPass);

bool Occluded(Vector3 from, Vector3 to, int ignore)
{
    var d = to - from;
    if (from.Z * to.Z < 0)
    {
        return true;
    }
    for (int i = 0; i < boxArray.Length; i++)
    {
        if (i != ignore && !Unseen(i) && boxArray[i].Hits(from, d, 1f, out _, out _))
        {
            return true;
        }
    }
    return false;
}

bool Closest(Vector3 from, Vector3 dir, int ignore, out Vector3 pos, out Vector3 normal, out Vector3 albedo, out int hitBox)
{
    float best = float.MaxValue;
    int found = -2;
    Vector3 foundNormal = default;
    if (dir.Z < 0f)
    {
        float t = -from.Z / dir.Z;
        var g = from + dir * t;
        if (g.X >= 0 && g.Y >= 0 && g.X <= mapSize && g.Y <= mapSize)
        {
            best = t;
            found = -1;
            foundNormal = Vector3.UnitZ;
        }
    }
    for (int i = 0; i < boxArray.Length; i++)
    {
        if (i != ignore && !Unseen(i) && boxArray[i].Hits(from, dir, best, out float t, out var n))
        {
            best = t;
            found = i;
            foundNormal = n;
        }
    }
    hitBox = found;
    normal = foundNormal;
    if (found == -2)
    {
        pos = default;
        albedo = default;
        return false;
    }
    pos = from + dir * best + foundNormal * Eps;
    albedo = found < 0 ? (rulesPass ? rulesGroundAlbedo : groundAlbedo) : rulesPass ? rulesAlbedo[found] : boxArray[found].Albedo;
    return true;
}

Vector3 RandomOnSphere(ref uint rng)
{
    float z = 2f * Next(ref rng) - 1f, a = MathF.Tau * Next(ref rng), r = MathF.Sqrt(MathF.Max(0f, 1f - z * z));
    return new Vector3(r * MathF.Cos(a), r * MathF.Sin(a), z);
}

Vector3 CosineDir(Vector3 n, ref uint rng) => Vector3.Normalize(n + RandomOnSphere(ref rng) * 0.999f);

// Stärke der Feuer in diesem Durchgang: 1 fürs Bild, ein eigener Wert für das Licht der Spielregeln.
float fireScale = 1f;
// Laternen im Bild: schwächer und mit weichem Ende bei lampReach Feldern, damit zwischen ihnen Dunkel bleibt. Die Regeln
// rechnen mit voller Stärke und ohne Ende.

float LightAt(Light l, Vector3 p, Vector3 n, int ignore, ref uint rng, out Vector3 toLight)
{
    var target = l.Pos + RandomOnSphere(ref rng) * LampRadius;
    var d = target - p;
    float dist2 = d.LengthSquared();
    toLight = d / MathF.Sqrt(dist2);
    float cos = n == Vector3.Zero ? 0.6f : Vector3.Dot(n, toLight);
    // Der Hut der Laterne wirft nach oben kein Licht.
    if (cos <= 0f || (l.Hooded && toLight.Z < -0.35f) || Occluded(p, target, ignore))
    {
        return 0f;
    }
    float shape = 1f;
    if (l.Layer != 2 && !rulesPass)
    {
        float edge = l.Cut > 0f ? MathF.Max(0f, 1f - dist2 / (l.Cut * l.Cut)) : 1f;
        shape = l.Scale * edge * edge;
    }
    return l.Power * shape * (l.Layer == 2 ? fireScale : 1f) * cos / MathF.Max(dist2, 0.15f);
}

// Licht einer Zusatzquelle an einem Punkt. Ein Fenster leuchtet nur durch seine Öffnung, das Kreuz wirft Schatten.
float ExtraAt(Extra x, Vector3 p, Vector3 n, int ignore, ref uint rng)
{
    const int Samples = 4;
    float sum = 0f;
    for (int i = 0; i < Samples; i++)
    {
        var source = x.Pos + RandomOnSphere(ref rng) * (x.Window ? 0.07f : 0.12f);
        var target = source;
        if (x.Window)
        {
            float side = x.Axis == 0 ? p.X - x.C : p.Y - x.C;
            float span = x.Axis == 0 ? p.X - source.X : p.Y - source.Y;
            if (side <= 0.02f || span <= side)
            {
                continue;
            }
            var q = p + (source - p) * (side / span);
            float along = x.Axis == 0 ? q.Y : q.X;
            if (along < x.A0 || along > x.A1 || q.Z < x.Z0 || q.Z > x.Z1
                || MathF.Abs(along - (x.A0 + x.A1) / 2f) < x.Bar || MathF.Abs(q.Z - (x.Z0 + x.Z1) / 2f) < x.Bar)
            {
                continue;
            }
            target = q + (x.Axis == 0 ? Vector3.UnitX : Vector3.UnitY) * 0.05f;
        }
        var d = source - p;
        float dist2 = d.LengthSquared();
        var dir = d / MathF.Sqrt(dist2);
        float cos = n == Vector3.Zero ? 0.6f : Vector3.Dot(n, dir);
        float edge = MathF.Max(0f, 1f - dist2 / (x.Reach * x.Reach));
        if (cos <= 0f || edge <= 0f || Occluded(p, target, ignore))
        {
            continue;
        }
        sum += x.Power * edge * edge * cos / MathF.Max(dist2, 0.3f);
    }
    return sum / Samples;
}

Vector3 DirectForBounce(Vector3 p, Vector3 n, int ignore, ref uint rng)
{
    var sum = Vector3.Zero;
    foreach (var l in lightArray)
    {
        sum += (rulesPass ? l.Rules : l.Color) * LightAt(l, p, n, ignore, ref rng, out _);
    }
    return sum;
}

// Licht, das aus dieser Richtung eintrifft: Himmel oder das, was eine getroffene Fläche zurückwirft.
Vector3 Incoming(Vector3 from, Vector3 dir, int ignore, ref uint rng)
{
    var throughput = Vector3.One;
    var total = Vector3.Zero;
    for (int bounce = 0; bounce < Bounces; bounce++)
    {
        if (!Closest(from, dir, ignore, out var pos, out var n, out var albedo, out int hit))
        {
            return total + throughput * (dir.Z > 0f ? (rulesPass ? rulesSky : skyColor) : Vector3.Zero);
        }
        throughput *= albedo;
        total += throughput * DirectForBounce(pos, n, hit, ref rng);
        from = pos;
        ignore = hit;
        dir = CosineDir(n, ref rng);
    }
    return total;
}

// ---------- Fenster als kleine Flächenlichter, gesammelt aus den hellen Bildpunkten ----------

var windowCells = new Dictionary<(int, int, int, int, int), (Vector3 Sum, int Count)>();
foreach (var s in sprites)
{
    for (int iy = 0; iy < s.H; iy++)
    {
        for (int ix = 0; ix < s.W; ix++)
        {
            int mask = masks[s.Offset + iy * s.W + ix];
            if (mask >= 2)
            {
                var surface = Unproject(s, ix, iy);
                var key = ((int)(surface.Pos.X / WindowCell), (int)(surface.Pos.Y / WindowCell), (int)(surface.Pos.Z / WindowCell), s.Box, mask - 2);
                windowCells.TryGetValue(key, out var cell);
                windowCells[key] = (cell.Sum + surface.Pos, cell.Count + 1);
            }
        }
    }
}
var windowList = new List<(Vector3 Pos, Vector3 Normal, Vector3 Color, float Power, int Box)>();
foreach (var (key, cell) in windowCells)
{
    var pos = cell.Sum / cell.Count;
    var box = key.Item4 >= 0 ? boxArray[key.Item4] : default;
    var n = key.Item4 < 0 ? Vector3.UnitZ
        : pos.Z >= box.Max.Z - 0.05f ? Vector3.UnitZ
        : MathF.Abs(pos.X - box.Max.X) < MathF.Abs(pos.Y - box.Max.Y) ? Vector3.UnitX : Vector3.UnitY;
    var type = windowTypes[key.Item5];
    windowList.Add((pos + n * 0.03f, n, type.Color, type.Power * cell.Count, key.Item4));
}
var windows = windowList.ToArray();
// Fensterlicht für die Spielregeln: der festgehaltene Stand (rules_windows.json), sonst das eben gesammelte.
var rulesWindows = windows;
if (root.TryGetProperty("rulesWindows", out var frozenWindows))
{
    rulesWindows = frozenWindows.EnumerateArray().Select(w => (
        new Vector3(w[0].GetSingle(), w[1].GetSingle(), w[2].GetSingle()), new Vector3(w[3].GetSingle(), w[4].GetSingle(), w[5].GetSingle()),
        new Vector3(w[6].GetSingle(), w[7].GetSingle(), w[8].GetSingle()), w[9].GetSingle(), -1)).ToArray();
}
File.WriteAllText(Path.Combine(workDir, "windows.json"), "[" + string.Join(", ", windows.Select(w => FormattableString.Invariant(
    $"[{w.Pos.X:R},{w.Pos.Y:R},{w.Pos.Z:R},{w.Normal.X:R},{w.Normal.Y:R},{w.Normal.Z:R},{w.Color.X:R},{w.Color.Y:R},{w.Color.Z:R},{w.Power:R}]"))) + "]");
Console.WriteLine($"{boxArray.Length} Quader, {lightArray.Length} Lichter, {windows.Length} Fensterlichter");

Vector3 Windows(Vector3 p, Vector3 n, int ignore)
{
    var sum = Vector3.Zero;
    foreach (var w in rulesPass ? rulesWindows : windows)
    {
        var d = w.Pos - p;
        float dist2 = d.LengthSquared();
        if (dist2 > WindowRange * WindowRange)
        {
            continue;
        }
        var dir = d / MathF.Sqrt(dist2);
        float outCos = -Vector3.Dot(dir, w.Normal);
        float inCos = n == Vector3.Zero ? 0.6f : Vector3.Dot(dir, n);
        if (outCos <= 0f || inCos <= 0f)
        {
            continue;
        }
        // Das eigene Haus nur ignorieren, wenn das Licht von außen kommt: Sonst leuchtet es durch die Wand.
        if (!Occluded(p, w.Pos, ignore))
        {
            sum += w.Color * (w.Power * outCos * inCos / MathF.Max(dist2, 0.2f));
        }
    }
    return sum;
}

// Blitz: starkes Licht von weit oben aus einer Richtung, mit harten Schatten, dazu ein wenig Streulicht.
float Flash(Vector3 p, Vector3 n, int ignore, ref uint rng)
{
    const int FlashSamples = 12;
    const float FlashSpread = 0.05f;
    const float FarAway = 60f;
    float sum = 0f;
    for (int i = 0; i < FlashSamples; i++)
    {
        var dir = Vector3.Normalize(flashDir + RandomOnSphere(ref rng) * FlashSpread);
        float cos = n == Vector3.Zero ? 0.7f : Vector3.Dot(n, dir);
        if (cos > 0f && !Occluded(p, p + dir * FarAway, ignore))
        {
            sum += cos;
        }
    }
    return sum / FlashSamples + flashAmbient;
}

// Mond: nur für die Oberseiten von Objekten. Straße und Lichtgitter bekommen nichts davon, das Schleichen bleibt, wie es ist.
Vector3 Moon(Vector3 p, Vector3 n, int ignore, ref uint rng)
{
    const int MoonSamples = 12;
    const float MoonSpread = 0.04f;
    const float FarAway = 60f;
    float sum = 0f;
    for (int i = 0; i < MoonSamples; i++)
    {
        var dir = Vector3.Normalize(moonDir + RandomOnSphere(ref rng) * MoonSpread);
        float cos = Vector3.Dot(n, dir);
        if (cos > 0f && !Occluded(p, p + dir * FarAway, ignore))
        {
            sum += cos;
        }
    }
    return moonColor * (sum / MoonSamples);
}

// ---------- Beleuchten ----------

float[] Shade(Surface s, ref uint rng, float px, float py)
{
    var direct = Vector3.Zero;
    float stutter = 0f, fire = 0f, others = 0f;
    float spec = specStrength, shine = 40f;
    if (s.IsGround)
    {
        foreach (var (pos, r) in puddles)
        {
            float dx = (px - pos.X) / r, dy = (py - pos.Y) / (r * 0.5f);
            if (dx * dx + dy * dy < 1f)
            {
                spec = puddleSpec;
                shine = 180f;
                break;
            }
        }
    }
    for (int i = 0; i < DirectSamples; i++)
    {
        foreach (var l in lightArray)
        {
            float e = LightAt(l, s.Pos, s.Normal, s.Box, ref rng, out var toLight);
            if (e <= 0f)
            {
                continue;
            }
            float total = e;
            if (s.IsGround)
            {
                // Nasses Pflaster spiegelt die Lampe Richtung Kamera.
                var half = Vector3.Normalize(toLight + viewDir);
                total += e * spec * MathF.Pow(MathF.Max(half.Z, 0f), shine);
            }
            direct += l.Color * total;
            float luminance = total / DirectSamples;
            switch (l.Layer)
            {
                case 1: stutter += luminance; break;
                case 2: fire += luminance; break;
                default: others += luminance; break;
            }
        }
    }
    direct /= DirectSamples;
    direct += Windows(s.Pos, s.Normal, s.Box);
    if (!s.IsGround && (s.Normal == Vector3.UnitZ || s.Roof))
    {
        var moon = Moon(s.Pos, s.Normal, s.Box, ref rng);
        direct += moon;
        if (moonSpec > 0f)
        {
            // Nässeglanz: Die Fläche spiegelt den Mond, wo ihre Neigung zwischen Mond und Kamera vermittelt. Kleine Unebenheiten
            // der Deckung lassen den Glanz in Flecken aufbrechen statt als glatte Fläche zu stehen.
            float cx = MathF.Floor(px / 3f), cy = MathF.Floor(py / 2f);
            float h1 = MathF.Sin(cx * 12.9898f + cy * 78.233f) * 43758.5453f, h2 = MathF.Sin(cx * 39.346f + cy * 11.135f) * 24634.6345f;
            h1 -= MathF.Floor(h1);
            h2 -= MathF.Floor(h2);
            var bumpy = Vector3.Normalize(s.Normal + new Vector3(h1 - 0.5f, h2 - 0.5f, 0f) * 0.22f);
            var half = Vector3.Normalize(moonDir + viewDir);
            direct += moon * (moonSpec * MathF.Pow(MathF.Max(Vector3.Dot(bumpy, half), 0f), 48f));
        }
    }
    var indirect = Vector3.Zero;
    for (int i = 0; i < IndirectSamples; i++)
    {
        var dir = CosineDir(s.Normal, ref rng);
        indirect += Incoming(s.Pos, dir, s.Box, ref rng);
    }
    indirect /= IndirectSamples;
    var shaded = new float[Channels];
    foreach (var x in extraArray)
    {
        float e = ExtraAt(x, s.Pos, s.Normal, s.Box, ref rng);
        if (x.Group < 0)
        {
            direct += x.Color * e;
        }
        else
        {
            shaded[BaseChannels + x.Group] += e;
        }
    }
    shaded[0] = direct.X;
    shaded[1] = direct.Y;
    shaded[2] = direct.Z;
    shaded[3] = indirect.X;
    shaded[4] = indirect.Y;
    shaded[5] = indirect.Z;
    shaded[6] = stutter;
    shaded[7] = fire;
    shaded[8] = others;
    shaded[9] = Flash(s.Pos, s.Normal, s.Box, ref rng);
    return shaded;
}

var started = DateTime.Now;
using var output = new BinaryWriter(File.Create(Path.Combine(workDir, "light.bin")));
foreach (var s in sprites)
{
    var result = new float[s.W * s.H * Channels];
    Parallel.For(0, s.H, iy =>
    {
        for (int ix = 0; ix < s.W; ix++)
        {
            if (masks[s.Offset + iy * s.W + ix] == 0)
            {
                continue;
            }
            uint rng = (uint)(s.Offset + iy * s.W + ix) * 2654435761u | 1u;
            var surface = Unproject(s, ix, iy);
            if (s.Box < 0 && (surface.Pos.X < 0 || surface.Pos.Y < 0 || surface.Pos.X > mapSize || surface.Pos.Y > mapSize))
            {
                result[(iy * s.W + ix) * Channels] = -1f;
                continue;
            }
            float px = s.Left + (ix + 0.5f) * stride, py = s.Top + (iy + 0.5f) * stride;
            var shaded = Shade(surface, ref rng, px, py);
            Array.Copy(shaded, 0, result, (iy * s.W + ix) * Channels, Channels);
        }
    });
    foreach (float v in result)
    {
        output.Write(v);
    }
}
Console.WriteLine($"Lichtkarten fertig nach {(DateTime.Now - started).TotalSeconds:0} s");

// ---------- Lichtgitter für bewegte Figuren ----------

int gridRes = root.GetProperty("gridRes").GetInt32();
float gridZ = root.GetProperty("gridZ").GetSingle() / zScale;
int gridSize = mapSize * gridRes;
// Je Zelle: Licht fürs Bild (3), Blitz (1) und Licht für die Spielregeln (3). Die Regeln rechnen Feuer mit eigener Stärke.
float rulesFire = root.TryGetProperty("rulesFireScale", out var rulesFireElement) ? rulesFireElement.GetSingle() : 1f;
const int GridChannels = 7;
var grid = new float[gridSize * gridSize * GridChannels];
for (int pass = 0; pass < 2; pass++)
{
    // Zweiter Durchgang mit denselben Zufallszahlen, nur die Feuer so stark, wie die Spielregeln sie kennen.
    fireScale = pass == 0 ? 1f : rulesFire;
    rulesPass = pass == 1;
    int first = pass == 0 ? 0 : 4;
    Parallel.For(0, gridSize, gy =>
    {
        for (int gx = 0; gx < gridSize; gx++)
        {
            var p = new Vector3((gx + 0.5f) / gridRes, (gy + 0.5f) / gridRes, gridZ);
            int at = (gy * gridSize + gx) * GridChannels;
            if (Enumerable.Range(0, boxArray.Length).Any(i => ghost[i] == 0 && boxArray[i].Contains(p)))
            {
                grid[at] = -1f;
                continue;
            }
            uint rng = (uint)(gy * gridSize + gx + 1) * 2654435761u | 1u;
            var sum = Vector3.Zero;
            for (int i = 0; i < GridSamples; i++)
            {
                foreach (var l in lightArray)
                {
                    sum += (rulesPass ? l.Rules : l.Color) * LightAt(l, p, Vector3.Zero, -2, ref rng, out _);
                }
                sum += Incoming(p, RandomOnSphere(ref rng), -2, ref rng);
            }
            sum = sum / GridSamples + Windows(p, Vector3.Zero, -2);
            grid[at + first] = sum.X;
            grid[at + first + 1] = sum.Y;
            grid[at + first + 2] = sum.Z;
            if (pass == 0)
            {
                grid[at + 3] = Flash(p, Vector3.Zero, -2, ref rng);
            }
        }
    });
}
fireScale = 1f;
rulesPass = false;
using (var gridOut = new BinaryWriter(File.Create(Path.Combine(workDir, "grid.bin"))))
{
    foreach (float v in grid)
    {
        gridOut.Write(v);
    }
}
Console.WriteLine($"Lichtgitter fertig nach {(DateTime.Now - started).TotalSeconds:0} s");

static Vector3 Vec(JsonElement e) => new(e[0].GetSingle(), e[1].GetSingle(), e[2].GetSingle());

static float Next(ref uint rng)
{
    rng ^= rng << 13;
    rng ^= rng >> 17;
    rng ^= rng << 5;
    return rng / 4294967296f;
}

internal readonly record struct Extra(int Group, bool Window, Vector3 Pos, Vector3 Color, float Power, float Reach, int Axis, float C, float A0, float A1, float Z0, float Z1, float Bar);
internal readonly record struct Light(Vector3 Pos, Vector3 Color, Vector3 Rules, float Power, int Layer, bool Hooded, float Scale, float Cut);

internal readonly record struct Sprite(int Box, int Left, int Top, int W, int H, int Offset);

internal readonly record struct Surface(Vector3 Pos, Vector3 Normal, int Box, bool IsGround, bool Roof = false);

internal readonly record struct Box(Vector3 Min, Vector3 Max, Vector3 Albedo)
{
    public bool Contains(Vector3 p) => p.X > Min.X && p.Y > Min.Y && p.Z > Min.Z && p.X < Max.X && p.Y < Max.Y && p.Z < Max.Z;

    /// <summary>Schnitt mit einem Strahl von from in Richtung dir, gültig bis maxT. Nur Eintritte von außen zählen.</summary>
    public bool Hits(Vector3 from, Vector3 dir, float maxT, out float t, out Vector3 normal)
    {
        float tMin = 0f, tMax = maxT;
        int axis = -1;
        float sign = 0f;
        for (int a = 0; a < 3; a++)
        {
            float o = a == 0 ? from.X : a == 1 ? from.Y : from.Z;
            float d = a == 0 ? dir.X : a == 1 ? dir.Y : dir.Z;
            float lo = a == 0 ? Min.X : a == 1 ? Min.Y : Min.Z;
            float hi = a == 0 ? Max.X : a == 1 ? Max.Y : Max.Z;
            if (MathF.Abs(d) < 1e-7f)
            {
                if (o < lo || o > hi)
                {
                    t = 0f;
                    normal = default;
                    return false;
                }
                continue;
            }
            float t0 = (lo - o) / d, t1 = (hi - o) / d, s = -1f;
            if (t0 > t1)
            {
                (t0, t1) = (t1, t0);
                s = 1f;
            }
            if (t0 > tMin)
            {
                tMin = t0;
                axis = a;
                sign = s;
            }
            tMax = MathF.Min(tMax, t1);
            if (tMin > tMax)
            {
                t = 0f;
                normal = default;
                return false;
            }
        }
        t = tMin;
        normal = axis == 0 ? new Vector3(sign, 0, 0) : axis == 1 ? new Vector3(0, sign, 0) : new Vector3(0, 0, sign);
        return axis >= 0 || maxT == 1f;
    }
}
