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

string workDir = args[0];
using var sceneDoc = JsonDocument.Parse(File.ReadAllText(Path.Combine(workDir, "scene.json")));
var root = sceneDoc.RootElement;

float ox = root.GetProperty("ox").GetSingle(), oy = root.GetProperty("oy").GetSingle();
float zScale = root.GetProperty("zScale").GetSingle();
int mapSize = root.GetProperty("mapSize").GetInt32();
Vector3 skyColor = Vec(root.GetProperty("sky"));
Vector3 groundAlbedo = Vec(root.GetProperty("groundAlbedo"));
Vector3 lampColor = Vec(root.GetProperty("lampColor"));
Vector3 fireColor = Vec(root.GetProperty("fireColor"));
Vector3 windowColor = Vec(root.GetProperty("windowColor"));
float windowPower = root.GetProperty("windowPower").GetSingle();
float specStrength = root.GetProperty("specStrength").GetSingle();
float puddleSpec = root.GetProperty("puddleSpec").GetSingle();
Vector3 viewDir = Vector3.Normalize(new Vector3(1f / 32f, 1f / 32f, 1f / zScale));

var boxes = new List<Box>();
foreach (var b in root.GetProperty("boxes").EnumerateArray())
{
    boxes.Add(new Box(
        new Vector3(b.GetProperty("x0").GetSingle(), b.GetProperty("y0").GetSingle(), b.GetProperty("z0").GetSingle() / zScale),
        new Vector3(b.GetProperty("x1").GetSingle(), b.GetProperty("y1").GetSingle(), b.GetProperty("z1").GetSingle() / zScale),
        Vec(b.GetProperty("albedo"))));
}
var boxArray = boxes.ToArray();

var lamps = new List<Light>();
foreach (var l in root.GetProperty("lights").EnumerateArray())
{
    lamps.Add(new Light(
        new Vector3(l.GetProperty("x").GetSingle(), l.GetProperty("y").GetSingle(), l.GetProperty("z").GetSingle() / zScale),
        l.GetProperty("color").GetString() == "fire" ? fireColor : lampColor,
        l.GetProperty("power").GetSingle(),
        l.GetProperty("layer").GetInt32(),
        l.GetProperty("hooded").GetBoolean()));
}
var lightArray = lamps.ToArray();

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

bool Occluded(Vector3 from, Vector3 to, int ignore)
{
    var d = to - from;
    if (from.Z * to.Z < 0)
    {
        return true;
    }
    for (int i = 0; i < boxArray.Length; i++)
    {
        if (i != ignore && boxArray[i].Hits(from, d, 1f, out _, out _))
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
        if (i != ignore && boxArray[i].Hits(from, dir, best, out float t, out var n))
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
    albedo = found < 0 ? groundAlbedo : boxArray[found].Albedo;
    return true;
}

Vector3 RandomOnSphere(ref uint rng)
{
    float z = 2f * Next(ref rng) - 1f, a = MathF.Tau * Next(ref rng), r = MathF.Sqrt(MathF.Max(0f, 1f - z * z));
    return new Vector3(r * MathF.Cos(a), r * MathF.Sin(a), z);
}

Vector3 CosineDir(Vector3 n, ref uint rng) => Vector3.Normalize(n + RandomOnSphere(ref rng) * 0.999f);

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
    return l.Power * cos / MathF.Max(dist2, 0.15f);
}

Vector3 DirectForBounce(Vector3 p, Vector3 n, int ignore, ref uint rng)
{
    var sum = Vector3.Zero;
    foreach (var l in lightArray)
    {
        sum += l.Color * LightAt(l, p, n, ignore, ref rng, out _);
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
            return total + throughput * (dir.Z > 0f ? skyColor : Vector3.Zero);
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

var windowCells = new Dictionary<(int, int, int, int), (Vector3 Sum, int Count)>();
foreach (var s in sprites)
{
    for (int iy = 0; iy < s.H; iy++)
    {
        for (int ix = 0; ix < s.W; ix++)
        {
            if (masks[s.Offset + iy * s.W + ix] == 2)
            {
                var surface = Unproject(s, ix, iy);
                var key = ((int)(surface.Pos.X / WindowCell), (int)(surface.Pos.Y / WindowCell), (int)(surface.Pos.Z / WindowCell), s.Box);
                windowCells.TryGetValue(key, out var cell);
                windowCells[key] = (cell.Sum + surface.Pos, cell.Count + 1);
            }
        }
    }
}
var windowList = new List<(Vector3 Pos, Vector3 Normal, float Power, int Box)>();
foreach (var (key, cell) in windowCells)
{
    var pos = cell.Sum / cell.Count;
    var box = key.Item4 >= 0 ? boxArray[key.Item4] : default;
    var n = key.Item4 < 0 ? Vector3.UnitZ
        : pos.Z >= box.Max.Z - 0.05f ? Vector3.UnitZ
        : MathF.Abs(pos.X - box.Max.X) < MathF.Abs(pos.Y - box.Max.Y) ? Vector3.UnitX : Vector3.UnitY;
    windowList.Add((pos + n * 0.03f, n, windowPower * cell.Count, key.Item4));
}
var windows = windowList.ToArray();
Console.WriteLine($"{boxArray.Length} Quader, {lightArray.Length} Lichter, {windows.Length} Fensterlichter");

Vector3 Windows(Vector3 p, Vector3 n, int ignore)
{
    var sum = Vector3.Zero;
    foreach (var w in windows)
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
            sum += windowColor * (w.Power * outCos * inCos / MathF.Max(dist2, 0.2f));
        }
    }
    return sum;
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
    var indirect = Vector3.Zero;
    for (int i = 0; i < IndirectSamples; i++)
    {
        var dir = CosineDir(s.Normal, ref rng);
        indirect += Incoming(s.Pos, dir, s.Box, ref rng);
    }
    indirect /= IndirectSamples;
    return new[] { direct.X, direct.Y, direct.Z, indirect.X, indirect.Y, indirect.Z, stutter, fire, others };
}

var started = DateTime.Now;
using var output = new BinaryWriter(File.Create(Path.Combine(workDir, "light.bin")));
foreach (var s in sprites)
{
    var result = new float[s.W * s.H * 9];
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
                result[(iy * s.W + ix) * 9] = -1f;
                continue;
            }
            float px = s.Left + (ix + 0.5f) * stride, py = s.Top + (iy + 0.5f) * stride;
            var shaded = Shade(surface, ref rng, px, py);
            Array.Copy(shaded, 0, result, (iy * s.W + ix) * 9, 9);
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
var grid = new float[gridSize * gridSize * 3];
Parallel.For(0, gridSize, gy =>
{
    for (int gx = 0; gx < gridSize; gx++)
    {
        var p = new Vector3((gx + 0.5f) / gridRes, (gy + 0.5f) / gridRes, gridZ);
        int at = (gy * gridSize + gx) * 3;
        if (Array.Exists(boxArray, b => b.Contains(p)))
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
                sum += l.Color * LightAt(l, p, Vector3.Zero, -2, ref rng, out _);
            }
            sum += Incoming(p, RandomOnSphere(ref rng), -2, ref rng);
        }
        sum = sum / GridSamples + Windows(p, Vector3.Zero, -2);
        grid[at] = sum.X;
        grid[at + 1] = sum.Y;
        grid[at + 2] = sum.Z;
    }
});
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

internal readonly record struct Light(Vector3 Pos, Vector3 Color, float Power, int Layer, bool Hooded);

internal readonly record struct Sprite(int Box, int Left, int Top, int W, int H, int Offset);

internal readonly record struct Surface(Vector3 Pos, Vector3 Normal, int Box, bool IsGround);

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
