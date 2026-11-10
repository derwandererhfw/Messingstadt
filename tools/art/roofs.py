"""Dächer und Dachaufbauten als echte Körper: Prismen aus Halbräumen, exakt geschnitten mit dem Sehstrahl.

Ein Prisma ist die Grundfläche x0..x1, y0..y1 ab der Höhe z0, nach oben begrenzt durch Ebenen a·x + b·y + c·z <= d
(x und y in Feldern, z in Bildpunkten wie in generate_concept.py). Satteldach, Walmdach, Mansarde, Gaube und Schornstein
sind konvex und damit je ein Prisma, ein Sheddach ist eine Reihe davon. Der Lichtbaker rechnet mit denselben Ebenen
(siehe tools/Messingstadt.LightBake), deshalb liegen Licht und Bild auf denselben Flächen.

Der Strahl eines Bildpunkts: x - y = A und x + y = B + z / HH. Mit wachsendem z kommt er der Kamera näher, der sichtbare
Punkt eines Körpers ist also das größte z, das noch in allen Halbräumen liegt.
"""
import numpy as np

HW, HH = 32, 16
Z_SCALE = 39.2  # wie in bake_light.py: Bildpunkte Höhe je Feld, für echte Flächennormalen


class Prism:
    def __init__(self, x0, y0, x1, y1, z0, planes, material, name=""):
        self.x0, self.y0, self.x1, self.y1, self.z0 = x0, y0, x1, y1, z0
        self.planes = [tuple(p) for p in planes]  # (a, b, c, d) oder (a, b, c, d, Durchhang)
        self.material = material
        self.name = name
        self.info = {}

    def constraints(self):
        sides = [(-1.0, 0.0, 0.0, -self.x0), (1.0, 0.0, 0.0, self.x1), (0.0, -1.0, 0.0, -self.y0), (0.0, 1.0, 0.0, self.y1),
                 (0.0, 0.0, -1.0, -self.z0)]
        return sides + [p[:4] for p in self.planes]

    def height(self, x, y):
        """Höhe der Oberseite an einer Stelle, None außerhalb."""
        if not (self.x0 <= x <= self.x1 and self.y0 <= y <= self.y1):
            return None
        tops = [(d - a * x - b * y) / c for a, b, c, d, *_ in self.planes if c > 0]
        return min(tops) if tops else None

    def top(self):
        """Höchster Punkt, für den Bildausschnitt. Bei konvexen Körpern liegt er auf einer Ecke oder Kante: Raster reicht."""
        best = self.z0
        for i in range(9):
            for j in range(9):
                h = self.height(self.x0 + (self.x1 - self.x0) * i / 8, self.y0 + (self.y1 - self.y0) * j / 8)
                if h is not None:
                    best = max(best, h)
        return best

    def to_json(self):
        return {"x0": self.x0, "y0": self.y0, "x1": self.x1, "y1": self.y1, "z0": self.z0,
                "planes": [[round(v, 4) for v in p[:4]] for p in self.planes]}


# ---------- Ebenen ----------

def rise_from(edge, at, z_edge, k):
    """Ebene, die von einer Kante aus mit k Bildpunkten je Feld nach innen ansteigt. edge: 'x0', 'x1', 'y0' oder 'y1'."""
    if edge == "x0":
        return (-k, 0.0, 1.0, z_edge - k * at)
    if edge == "x1":
        return (k, 0.0, 1.0, z_edge + k * at)
    if edge == "y0":
        return (0.0, -k, 1.0, z_edge - k * at)
    return (0.0, k, 1.0, z_edge + k * at)


def flat(z):
    return (0.0, 0.0, 1.0, z)


def lifted(plane, dz):
    a, b, c, d, *rest = plane
    return (a, b, c, d + dz * c, *rest)


def sagging(plane, axis, s0, s1, amount):
    """Durchhang: Die Fläche sinkt zur Mitte der Spanne um amount Bildpunkte ab."""
    return tuple(plane[:4]) + ((axis, s0, s1, amount),)


def height_at(prisms, x, y):
    best = None
    for p in prisms:
        h = p.height(x, y)
        if h is not None and (best is None or h > best):
            best = h
    return best


# ---------- Bauformen ----------

def mansard(x0, y0, x1, y1, h, steep=30, steep_k=60, upper=10, upper_k=8, material=None):
    """Mansarde: steiles unteres Dach rundum, darüber ein flaches. Gibt das Prisma und die Höhe der Oberkante zurück."""
    planes = [rise_from(e, a, h, steep_k) for e, a in (("x0", x0), ("x1", x1), ("y0", y0), ("y1", y1))]
    inset = steep / steep_k
    planes += [rise_from(e, a + (inset if e[1] == "0" else -inset), h + steep, upper_k)
               for e, a in (("x0", x0), ("x1", x1), ("y0", y0), ("y1", y1))]
    planes.append(flat(h + steep + upper))
    return Prism(x0, y0, x1, y1, h, planes, material, "mansard")


def gable(x0, y0, x1, y1, h, rise, ridge="y", sag=0.0, material=None):
    """Satteldach. ridge: Richtung des Firsts."""
    if ridge == "y":
        k = rise / ((x1 - x0) / 2)
        planes = [rise_from("x0", x0, h, k), rise_from("x1", x1, h, k)]
        axis, s0, s1 = "y", y0, y1
    else:
        k = rise / ((y1 - y0) / 2)
        planes = [rise_from("y0", y0, h, k), rise_from("y1", y1, h, k)]
        axis, s0, s1 = "x", x0, x1
    if sag:
        planes = [sagging(p, axis, s0, s1, sag) for p in planes]
    return Prism(x0, y0, x1, y1, h, planes, material, "gable")


def hip(x0, y0, x1, y1, h, rise, end_k, material=None):
    """Walmdach mit First längs x: Traufseiten steigen bis zur Mitte, die Enden steiler."""
    k = rise / ((y1 - y0) / 2)
    planes = [rise_from("y0", y0, h, k), rise_from("y1", y1, h, k), rise_from("x0", x0, h, end_k), rise_from("x1", x1, h, end_k)]
    return Prism(x0, y0, x1, y1, h, planes, material, "hip")


def sawtooth(x0, y0, x1, y1, h, teeth, rise, material=None, sag=0.0):
    """Sheddach: Zähne quer zu x, jede steile Seite zeigt nach +x und ist verglast."""
    pitch = (x1 - x0) / teeth
    k = rise / pitch
    out = []
    for i in range(teeth):
        xa = x0 + i * pitch
        plane = rise_from("x0", xa, h, k)
        if sag:
            plane = sagging(plane, "y", y0, y1, sag)
        out.append(Prism(xa, y0, xa + pitch, y1, h, [plane], material, f"shed{i}"))
    return out


def block(x0, y0, x1, y1, z0, z1, material=None, name="block"):
    return Prism(x0, y0, x1, y1, z0, [flat(z1)], material, name)


def dormer(xa, xb, yf, depth, h, wall, peak, facing="y", material=None):
    """Gaube mit eigenem Satteldach. Die Front steht bei yf (oder xf), der Körper reicht depth Felder ins Dach hinein."""
    k = (peak - wall) / ((xb - xa) / 2)
    if facing == "y":
        planes = [rise_from("x0", xa, wall, k), rise_from("x1", xb, wall, k)]
        return Prism(xa, yf - depth, xb, yf, h, planes, material, "dormer_y")
    planes = [rise_from("y0", xa, wall, k), rise_from("y1", xb, wall, k)]
    return Prism(yf - depth, xa, yf, xb, h, planes, material, "dormer_x")


# ---------- Sehstrahlen ----------

def screen_box(s, prisms, margin=2):
    """Bildausschnitt in Bildpunkten der aktuellen Auflösung, der alle Prismen umfasst."""
    xs, ys = [], []
    for p in prisms:
        top = p.top()
        for x in (p.x0, p.x1):
            for y in (p.y0, p.y1):
                for z in (p.z0, top):
                    sx, sy = s.to_screen(x, y, z)
                    xs.append(sx)
                    ys.append(sy)
    k = s.res
    return (int(min(xs)) - margin * k, int(min(ys)) - margin * k, int(max(xs)) + margin * k + 1, int(max(ys)) + margin * k + 1)


def _hits(prism, A, B, sag_s=None):
    lo = np.full(A.shape, -np.inf)
    hi = np.full(A.shape, np.inf)
    face = np.full(A.shape, -1, np.int16)
    for index, (a, b, c, d) in enumerate(prism.constraints()):
        k = (a + b) / (2 * HH) + c
        rhs = d - a * (A + B) / 2 - b * (B - A) / 2
        if index >= 5 and sag_s is not None and len(prism.planes[index - 5]) > 4:
            axis, s0, s1, amount = prism.planes[index - 5][4]
            t = np.clip((sag_s[axis] - s0) / (s1 - s0), 0, 1)
            rhs = rhs - amount * c * np.sin(np.pi * t)
        if k > 1e-9:
            bound = rhs / k
            better = bound < hi
            hi = np.where(better, bound, hi)
            face = np.where(better, index, face)
        elif k < -1e-9:
            lo = np.maximum(lo, rhs / k)
        else:
            lo = np.where(rhs < 0, np.inf, lo)
    z = np.where(lo <= hi, hi, -np.inf)
    return z, face


def trace(s, prisms, box):
    """Für jeden Bildpunkt im Ausschnitt: Höhe des sichtbaren Punkts (-inf ohne Treffer), Prisma und Fläche."""
    x0, y0, x1, y1 = box
    k = s.res
    sx, sy = np.meshgrid(np.arange(x0, x1) + 0.5, np.arange(y0, y1) + 0.5)
    A = (sx / k - s.ox) / HW
    B = (sy / k - s.oy) / HH
    best = np.full(A.shape, -np.inf)
    pid = np.full(A.shape, -1, np.int16)
    fid = np.full(A.shape, -1, np.int16)
    for i, p in enumerate(prisms):
        z, face = _hits(p, A, B)
        if any(len(q) > 4 for q in p.planes):
            fx, fy = (A + B + z / HH) / 2, (B + z / HH - A) / 2
            z2, face2 = _hits(p, A, B, {"x": np.nan_to_num(fx), "y": np.nan_to_num(fy)})
            z, face = np.where(np.isfinite(z), z2, z), np.where(np.isfinite(z), face2, face)
        nearer = z > best
        best = np.where(nearer, z, best)
        pid = np.where(nearer, i, pid)
        fid = np.where(nearer, face, fid)
    fx = (A + B + best / HH) / 2
    fy = (B + best / HH - A) / 2
    return best, pid, fid, fx, fy


def face_normal(prism, face):
    """Flächennormale in echten Maßen (z in Feldern), normiert."""
    a, b, c, d = prism.constraints()[face]
    n = np.array([a, b, c * Z_SCALE], np.float64)
    return n / np.linalg.norm(n)


def face_light(n):
    """Grundhelligkeit je Richtung, passend zu den Fassaden: links (+y) 1.0, rechts (+x) 0.74, oben 0.95."""
    nx, ny, nz = n
    return (0.74 * max(nx, 0) ** 2 + 1.0 * max(ny, 0) ** 2 + 0.95 * max(nz, 0) ** 2
            + 0.55 * (min(nx, 0) ** 2 + min(ny, 0) ** 2))


def render(s, prisms, only=None, shade_edges=True):
    """Zeichnet die Prismen in s. only: Menge von Prismen, deren Bildpunkte geschrieben werden (die übrigen verdecken nur)."""
    box = screen_box(s, prisms if only is None else list(only))
    box = (max(0, box[0]), max(0, box[1]), min(s.w, box[2]), min(s.h, box[3]))
    if box[2] <= box[0] or box[3] <= box[1]:
        return
    z, pid, fid, fx, fy = trace(s, prisms, box)
    hit = np.isfinite(z)
    if only is not None:
        allowed = np.array([p in only for p in prisms])
        hit &= allowed[np.clip(pid, 0, None)] & (pid >= 0)
    if not hit.any():
        return
    region = np.array(s.img.crop(box)).astype(np.float64)
    out = region.copy()
    for i, p in enumerate(prisms):
        if only is not None and p not in only:
            continue
        for face in np.unique(fid[hit & (pid == i)]):
            m = hit & (pid == i) & (fid == face)
            n = face_normal(p, face)
            col = p.material(Face(p, int(face), n, fx[m], fy[m], z[m], s.res))
            glow = None
            if isinstance(col, tuple):
                col, glow = col  # selbstleuchtende Punkte behalten ihre Farbe, daran erkennt sie der Lichtbaker
            lit = col * face_light(n)
            if glow is not None:
                lit = np.where(glow[:, None], col, lit)
            out[m, :3] = np.clip(lit, 0, 255)
            out[m, 3] = 255
    if shade_edges:
        # Kanten: Wo oben eine andere Fläche oder nichts beginnt, fängt die Kante Licht. Einen Bildpunkt breit.
        key = np.where(hit, pid.astype(np.int32) * 64 + fid, -1)
        above = np.vstack([np.full((1, key.shape[1]), -2), key[:-1]])
        edge = hit & (above != key)
        out[edge, :3] = np.clip(out[edge, :3] * 1.14 + 6, 0, 255)
        left = np.hstack([np.full((key.shape[0], 1), -2), key[:, :-1]])
        crease = hit & (left != key) & (left >= 0) & ~edge
        out[crease, :3] = out[crease, :3] * 0.8
    region[hit] = out[hit]
    from PIL import Image
    s.img.paste(Image.fromarray(region.astype(np.uint8), "RGBA"), box[:2])
    s.px = s.img.load()


class Face:
    """Ein getroffener Flächenteil: Lage im Raum, Art der Fläche und Koordinaten auf ihr, in Bildpunkten der Auflösung."""

    def __init__(self, prism, index, normal, fx, fy, z, res):
        self.prism, self.index, self.normal = prism, index, normal
        self.fx, self.fy, self.z, self.res = fx, fy, z, res
        self.kind = ("x0", "x1", "y0", "y1", "bottom")[index] if index < 5 else "plane"
        self.plane = index - 5 if index >= 5 else None

    @property
    def vertical(self):
        return abs(self.normal[2]) < 0.05

    def wall_uv(self):
        """Wandkoordinaten wie bei den Fassaden: u entlang der Wand von der vorderen Ecke, v nach unten ab der Traufe."""
        p = self.prism
        if self.kind == "x1" or (self.kind == "plane" and abs(self.normal[0]) > abs(self.normal[1])):
            u = (p.y1 - self.fy) * HW
        else:
            u = (self.fx - p.x0) * HW
        return u * self.res, -self.z * self.res

    def slope_uv(self):
        """Dachkoordinaten: u entlang der Traufe, v als Höhe (Ziegelreihen liegen waagerecht)."""
        n = self.normal
        u = (self.fy if abs(n[0]) > abs(n[1]) else self.fx) * HW
        return u * self.res, -self.z * self.res


# ---------- Rauschen, wie in generate_concept.py ----------

def hash01(ix, iy, seed=0):
    h = (np.asarray(ix).astype(np.int64) * 374761393 + np.asarray(iy).astype(np.int64) * 668265263 + seed * 2147483647) & 0xffffffff
    h = ((h ^ (h >> 13)) * 1274126177) & 0xffffffff
    return ((h ^ (h >> 16)) & 0xffff) / 65535


def noise(x, y, seed=0):
    ix, iy = np.floor(x), np.floor(y)
    fx, fy = x - ix, y - iy
    fx, fy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
    top = hash01(ix, iy, seed) * (1 - fx) + hash01(ix + 1, iy, seed) * fx
    bottom = hash01(ix, iy + 1, seed) * (1 - fx) + hash01(ix + 1, iy + 1, seed) * fx
    return top * (1 - fy) + bottom * fy


def line_mask(coord, period, res, offset=0.0):
    """Ein Bildpunkt breite Linien im Abstand period (in einfachen Bildpunkten), bei jeder Auflösung gleich dünn."""
    return np.floor(coord + offset * res) % max(1, int(round(period * res))) == 0
