"""
LD57 Enviro – wersja stylizowana (low-poly "polygon" look): fasetowane bryly, kolor per sciana
(gradient ciemna podstawa -> jasne konce + lekki rozrzut), korony z pojedynczych lisci, sosny z warstw
opadajacych igiel, kepy trawy, skaly z mchem, jesienne palety.

Te same typy i warianty co w grze (PropType x 4) + Extra (paproc, kwiaty) + CaveDecor (z ld57_enviro_import.py).
Uruchom w Blenderze (Scripting -> Run Script). Zastepuje kolekcje "LD57_Enviro".
Kolory sa w atrybucie koloru "Col" (Corner); materialy: "LD57 Stylized" (+ "LD57 Stylized Glow").
"""
import math
import os
import random
import importlib.util
import bpy
import bmesh
from mathutils import Matrix, Vector

V = Vector
TAU = math.tau
COLL_NAME = "LD57_Enviro"
ORIGIN_X = 40.0


# ---------------------------------------------------------------- kolory

def _lin(c):
    return c / 12.92 if c <= .04045 else ((c + .055) / 1.055) ** 2.4


def lerp(a, b, t):
    return tuple(a[i] + (b[i] - a[i]) * t for i in range(3))


def clamp01(t):
    return 0.0 if t < 0 else 1.0 if t > 1 else t


PAL = {
    "leaf":    ((.11, .26, .09), (.47, .63, .22)),
    "autumn":  ((.45, .13, .04), (.98, .55, .14)),
    "yellow":  ((.50, .36, .06), (.96, .80, .30)),
    "red":     ((.38, .07, .05), (.90, .30, .12)),
    "pine":    ((.05, .14, .08), (.22, .38, .18)),
    "bark":    ((.22, .14, .09), (.42, .29, .19)),
    "deadwood": ((.20, .17, .15), (.48, .43, .38)),
    "birch":   ((.80, .78, .74), (.95, .93, .89)),
    "grass":   ((.14, .27, .08), (.56, .71, .25)),
    "dry":     ((.40, .33, .15), (.96, .86, .56)),
    "rock":    ((.42, .40, .45), (.80, .77, .81)),
    "cave":    ((.16, .14, .19), (.42, .38, .46)),
    "moss":    ((.24, .38, .12), (.46, .60, .20)),
    "crystal": ((.15, .55, .95), (.75, 1.0, 1.0)),
    "fang":    ((.08, .03, .12), (.48, .26, .58)),
    "cocoon":  ((.62, .60, .54), (.92, .90, .84)),
    "stem":    ((.78, .72, .62), (.96, .93, .86)),
    "cap_red": ((.50, .08, .05), (.86, .22, .10)),
    "cap_brown": ((.30, .18, .10), (.62, .42, .24)),
    "cap_glow": ((.10, .70, .60), (.50, 1.0, .85)),
    "purple":  ((.30, .18, .55), (.68, .52, .95)),
    "flower_red": ((.55, .06, .05), (.95, .25, .15)),
    "flower_yel": ((.75, .55, .05), (1.0, .88, .35)),
    "flower_wht": ((.80, .80, .78), (1.0, 1.0, .98)),
}


# ---------------------------------------------------------------- budowanie siatki z kolorem per sciana

class MB:
    def __init__(self, seed, mats=("LD57 Stylized",)):
        self.bm = bmesh.new()
        self.col = self.bm.loops.layers.float_color.new("Col")
        self.uv = None
        self.rnd = random.Random(seed * 31 + 7)
        self.mats = list(mats)

    def add(self, geom, M=None, pal=None, z0=0.0, z1=1.0, mat=0, jitter=.07, face_color=None, uvs=None):
        verts, faces = geom
        M = M or Matrix.Identity(4)
        bv = [self.bm.verts.new(M @ V(v)) for v in verts]
        for f in faces:
            if len(set(f)) < 3:
                continue
            try:
                face = self.bm.faces.new([bv[i] for i in f])
            except ValueError:
                continue
            face.material_index = mat
            face.normal_update()
            t = clamp01((face.calc_center_median().z - z0) / max(z1 - z0, 1e-6))
            c = face_color(face, t) if face_color else lerp(pal[0], pal[1], t)
            if len(c) == 2 and isinstance(c[1], int):      # face_color moze zwrocic (kolor, material)
                c, face.material_index = c
            k = 1 + self.rnd.uniform(-jitter, jitter)
            c = tuple(_lin(min(1.0, x * k)) for x in c)
            for l in face.loops:
                l[self.col] = (*c, 1.0)
            if uvs:
                if self.uv is None:
                    self.uv = self.bm.loops.layers.uv.new("UVMap")
                for l, i in zip(face.loops, f):
                    l[self.uv].uv = uvs[i]


# ---------------------------------------------------------------- geometria (lokalnie, Z w gore)

def _extract(bm):
    bm.verts.index_update()
    g = ([v.co.copy() for v in bm.verts], [tuple(v.index for v in f.verts) for f in bm.faces])
    bm.free()
    return g


def g_cyl(sides, r0, r1, h, z0=0.0, cap_top=True, cap_bot=False):
    verts, faces = [], []
    for i in range(sides):
        a = TAU * i / sides
        verts.append((r0 * math.cos(a), r0 * math.sin(a), z0))
    if r1 <= 1e-5:
        verts.append((0, 0, z0 + h))
        faces += [(i, (i + 1) % sides, sides) for i in range(sides)]
    else:
        for i in range(sides):
            a = TAU * i / sides
            verts.append((r1 * math.cos(a), r1 * math.sin(a), z0 + h))
        faces += [(i, (i + 1) % sides, sides + (i + 1) % sides, sides + i) for i in range(sides)]
        if cap_top:
            faces.append(tuple(range(sides, 2 * sides)))
    if cap_bot:
        faces.append(tuple(reversed(range(sides))))
    return verts, faces


def g_tube(pts, radii, sides, cap=True):
    verts, faces, n = [], [], len(pts)
    x = None
    for i, p in enumerate(pts):
        d = (pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]).normalized()
        if x is None:                       # pierwszy pierscien: dowolny prostopadly
            x = d.cross(V((0, 0, 1)))
            if x.length < 1e-3:
                x = d.cross(V((1, 0, 0)))
        else:                               # transport rownolegly: bez skrecania miedzy pierscieniami
            x = x - d * x.dot(d)
            if x.length < 1e-6:
                x = d.orthogonal()
        x.normalize()
        y = d.cross(x)
        for k in range(sides):
            a = TAU * k / sides
            verts.append(p + (x * math.cos(a) + y * math.sin(a)) * radii[i])
    for i in range(n - 1):
        for k in range(sides):
            j = (k + 1) % sides
            faces.append((i * sides + k, i * sides + j, (i + 1) * sides + j, (i + 1) * sides + k))
    if cap:
        faces.append(tuple(range((n - 1) * sides, n * sides)))
    return verts, faces


def g_hull(points):
    bm = bmesh.new()
    vs = [bm.verts.new(p) for p in points]
    res = bmesh.ops.convex_hull(bm, input=vs)
    junk = list({g for g in res["geom_interior"] + res["geom_unused"] if isinstance(g, bmesh.types.BMVert)})
    if junk:
        bmesh.ops.delete(bm, geom=junk, context='VERTS')
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return _extract(bm)


def g_ico(rnd, radius=.5, subdiv=1, jit=.12):
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=radius)
    for v in bm.verts:
        v.co *= 1 + rnd.uniform(-jit, jit)
    return _extract(bm)


def g_leaf(L, w, fold):        # lisc w plaszczyznie XY, wzdluz +Y, zgiety wzdluz osi
    return [(0, 0, 0), (-w / 2, .42 * L, fold), (0, L, 0), (w / 2, .42 * L, fold)], [(0, 1, 2), (0, 2, 3)]


def g_blade(h, w, bend, segs=3):   # zdzblo: od (0,0,0) w gore, wygiete w +Y
    verts, faces = [], []
    for k in range(segs):
        t = k / segs
        z, y, half = h * t, bend * t * t, w * .5 * (1 - t)
        verts += [(-half, y, z), (half, y, z)]
    verts.append((0, bend, h))
    for k in range(segs - 1):
        a = 2 * k
        faces.append((a, a + 1, a + 3, a + 2))
    faces.append((2 * (segs - 1), 2 * (segs - 1) + 1, len(verts) - 1))
    return verts, faces


def g_frond(L, w, arch, n=7):      # lisc paproci: grzbiet wzdluz +Y z listkami po bokach
    spine = []
    for i in range(n + 1):
        t = i / n
        spine.append(V((0, L * t, arch * math.sin(t * math.pi) - .25 * arch * t)))
    verts = [tuple(p) for p in spine]
    faces = []
    for i in range(n):
        t = (i + .5) / n
        half = w * (1 - t) * (.6 + .4 * math.sin(t * math.pi))
        mid = (spine[i] + spine[i + 1]) / 2
        for side in (-1, 1):
            verts.append(tuple(mid + V((side * half, .1 * L / n, -.15 * half))))
            idx = len(verts) - 1
            faces.append((i, idx, i + 1) if side < 0 else (i, i + 1, idx))
    return verts, faces


def g_crystal(r, h):
    verts = [(r * math.cos(TAU * i / 6), r * math.sin(TAU * i / 6), 0) for i in range(6)]
    verts += [(r * math.cos(TAU * i / 6), r * math.sin(TAU * i / 6), .68 * h) for i in range(6)]
    verts.append((0, 0, h))
    faces = [(i, (i + 1) % 6, 6 + (i + 1) % 6, 6 + i) for i in range(6)]
    faces += [(6 + i, 6 + (i + 1) % 6, 12) for i in range(6)]
    faces.append(tuple(reversed(range(6))))
    return verts, faces


def g_cap(rc, hc, sides=8):        # kapelusz grzyba (z podspodem)
    verts = [(rc * math.cos(TAU * i / sides), rc * math.sin(TAU * i / sides), 0) for i in range(sides)]
    verts += [(.72 * rc * math.cos(TAU * (i + .5) / sides), .72 * rc * math.sin(TAU * (i + .5) / sides), .55 * hc) for i in range(sides)]
    verts += [(0, 0, hc), (0, 0, .12 * hc)]
    top, under = 2 * sides, 2 * sides + 1
    faces = []
    for i in range(sides):
        j = (i + 1) % sides
        faces.append((i, j, sides + i))
        faces.append((j, sides + j, sides + i))
        faces.append((sides + i, sides + j, top))
        faces.append((j, i, under))
    return verts, faces


# ---------------------------------------------------------------- transformacje

def frame(y_dir, z_hint=V((0, 0, 1))):
    y = y_dir.normalized()
    x = y.cross(z_hint)
    if x.length < 1e-4:
        x = y.cross(V((1, 0, 0)))
    x.normalize()
    z = x.cross(y)
    m = Matrix.Identity(4)
    for i in range(3):
        m[i][0], m[i][1], m[i][2] = x[i], y[i], z[i]
    return m


def T(p):
    return Matrix.Translation(V(p))


def S(s):
    if isinstance(s, (int, float)):
        s = (s, s, s)
    return Matrix.Diagonal(V(s)).to_4x4()


def Rz(a):
    return Matrix.Rotation(a, 4, 'Z')


def rnd_dir(rnd, up_bias=0.0):
    z = rnd.uniform(-1 + up_bias, 1)
    a = rnd.uniform(0, TAU)
    s = math.sqrt(max(0, 1 - z * z))
    return V((s * math.cos(a), s * math.sin(a), z))


# ---------------------------------------------------------------- elementy skladowe

class Trunk:
    """Os pnia (punkty + promienie) – pozwala przypinac galezie dokladnie do pnia."""
    def __init__(self, pts, radii):
        self.pts, self.radii = pts, radii

    @property
    def top(self):
        return self.pts[-1]

    def at(self, z):
        for i in range(len(self.pts) - 1):
            a, b = self.pts[i], self.pts[i + 1]
            if a.z <= z <= b.z:
                t = (z - a.z) / max(b.z - a.z, 1e-6)
                return a.lerp(b, t), self.radii[i] + (self.radii[i + 1] - self.radii[i]) * t
        return self.pts[-1].copy(), self.radii[-1]


def trunk(mb, rnd, h, r, pal="bark", lean=.06, segs=5, sides=6, top_r=.45, face_color=None):
    # lagodne wygiecie: jeden kierunek pochylenia + maly szum (bez zygzakow)
    lean_dir = V((rnd.uniform(-1, 1), rnd.uniform(-1, 1), 0))
    lean_dir = lean_dir.normalized() * lean * h if lean_dir.length > 1e-6 else V((0, 0, 0))
    pts, radii = [], []
    for i in range(segs + 1):
        t = i / segs
        wob = V((rnd.uniform(-1, 1), rnd.uniform(-1, 1), 0)) * lean * .25 * h / segs if 0 < i < segs else V((0, 0, 0))
        pts.append(V((0, 0, -.1 + (h + .1) * t)) + lean_dir * t * t + wob)
        radii.append(r * (1 + .3 * (1 - t) ** 3) * (1 - (1 - top_r) * t))
    mb.add(g_tube(pts, radii, sides), pal=PAL[pal], z0=0, z1=h, face_color=face_color)
    return Trunk(pts, radii)


def branch(mb, rnd, start, direction, length, r, pal="bark", segs=3, root_r=None, face_color=None):
    """Galaz zaczyna sie wewnatrz rodzica (root_r = promien rodzica w tym miejscu), wiec nie ma szczeliny."""
    d = direction.normalized()
    pts, radii = [start - d * (root_r * .6 if root_r else r)], [r]
    p = start.copy()
    for i in range(segs + 1):
        pts.append(p.copy())
        radii.append(r * (1 - .7 * i / segs))
        d = (d + V((rnd.uniform(-.12, .12), rnd.uniform(-.12, .12), rnd.uniform(0, .12)))).normalized()
        p += d * length / segs
    mb.add(g_tube(pts, radii, 5), pal=PAL[pal], z0=0, z1=start.z + length, face_color=face_color)
    return Trunk(pts, radii)


def canopy(mb, rnd, center, radius, pal, z0, z1, blobs=4, leaves=26, leaf_len=None):
    leaf_len = leaf_len or radius * .55
    centers = [center]
    for _ in range(blobs - 1):
        centers.append(center + rnd_dir(rnd, .3) * radius * rnd.uniform(.55, .85))
    for c in centers:
        rr = radius * rnd.uniform(.62, .85)
        mb.add(g_ico(rnd, .5, 1, .16), T(c) @ Rz(rnd.uniform(0, TAU)) @ S((rr * 2, rr * 2, rr * 1.7)),
               pal=PAL[pal], z0=z0, z1=z1)
        for _ in range(int(leaves * 1.4)):
            d = rnd_dir(rnd, -.35)
            p = c + V((d.x * rr, d.y * rr, d.z * rr * .85))
            out = (d * .55 + V((0, 0, -1.0))).normalized()     # liscie opadaja, przylegaja do korony
            L = leaf_len * rnd.uniform(.6, .95)
            mb.add(g_leaf(L, L * .6, L * .1), T(p - d * L * .15) @ frame(out, d),
                   pal=PAL[pal], z0=z0, z1=z1, jitter=.12)


def rock(mb, rnd, center, size, pal="rock", moss=True, pts=16, flat=.6, moss_level=.62):
    points = []
    for _ in range(pts):
        d = rnd_dir(rnd)
        points.append(V((d.x * size[0], d.y * size[1], d.z * size[2])) * rnd.uniform(.78, 1.0))
    geom = g_hull(points)
    zb = center.z - size[2] * flat
    p_rock, p_moss = PAL[pal], PAL["moss"]

    def fc(face, t):
        if moss and face.normal.z > moss_level:
            return lerp(p_moss[0], p_moss[1], .4 + .6 * t)
        return lerp(p_rock[0], p_rock[1], .25 + .75 * t * .9 + .1 * max(face.normal.z, 0))
    mb.add(geom, T(center) @ Rz(rnd.uniform(0, TAU)), z0=zb, z1=center.z + size[2], face_color=fc, jitter=.05)


def grass_clump(mb, rnd, center, n, h, spread, pal, bend=.25, w=.06):
    for _ in range(n):
        a = rnd.uniform(0, TAU)
        rr = spread * math.sqrt(rnd.random())
        p = center + V((math.cos(a) * rr, math.sin(a) * rr, -.02))
        hh = h * rnd.uniform(.65, 1.15)
        tilt = Matrix.Rotation(rnd.uniform(.05, .45), 4, 'X')
        mb.add(g_blade(hh, w * rnd.uniform(.8, 1.3), bend * hh, 3),
               T(p) @ Rz(a + math.pi / 2 + rnd.uniform(-.6, .6)) @ tilt,
               pal=PAL[pal], z0=center.z, z1=center.z + h, jitter=.1)


def flower_head(mb, rnd, p, size, pal):
    petals = 5
    for i in range(petals):
        a = TAU * i / petals + rnd.uniform(-.2, .2)
        d = V((math.cos(a), math.sin(a), .35)).normalized()
        mb.add(g_leaf(size, size * .8, size * .1), T(p) @ frame(d), pal=PAL[pal], z0=p.z - size, z1=p.z + size)
    mb.add(g_ico(rnd, size * .25, 0, .05), T(p + V((0, 0, size * .1))), pal=PAL["flower_yel"], z0=p.z, z1=p.z + 1)


# ---------------------------------------------------------------- typy propow

PROP_TYPES = ["Tree", "Bush", "Stone", "Grass", "Pebble",
              "Boulder", "DeadTree", "Rubble", "Stalagmite", "StalagmiteSmall",
              "CrystalBig", "CrystalSmall", "CrystalTiny", "Mushroom",
              "Fang", "FangSmall", "Cocoon", "WebLean",
              "LandmarkTree", "Menhir", "LandmarkCrystals", "LandmarkFang"]
EXTRA_TYPES = ["Fern", "Flowers"]

CATEGORIES = [("Las", PROP_TYPES[0:5]), ("Skala", PROP_TYPES[5:10]), ("Krysztaly", PROP_TYPES[10:14]),
              ("Otchlan", PROP_TYPES[14:18]), ("Landmarki", PROP_TYPES[18:22]), ("Extra", EXTRA_TYPES)]

PROP_NAMES = {"DeadTree": "Dead Tree", "StalagmiteSmall": "Stalagmite Small", "CrystalBig": "Crystal Big",
              "CrystalSmall": "Crystal Small", "CrystalTiny": "Crystal Tiny", "FangSmall": "Fang Small",
              "WebLean": "Web Lean", "LandmarkTree": "Landmark Tree", "Menhir": "Landmark Menhir",
              "LandmarkCrystals": "Landmark Crystals", "LandmarkFang": "Landmark Fang"}


def broadleaf(mb, rnd, h, r_trunk, crown_r, pal, bark="bark", blobs=4, leaves=26, face_color=None):
    tr = trunk(mb, rnd, h, r_trunk, bark, face_color=face_color)
    for i in range(rnd.randint(2, 3)):
        a = rnd.uniform(0, TAU)
        start, rr = tr.at(h * rnd.uniform(.55, .8))
        branch(mb, rnd, start, V((math.cos(a), math.sin(a), 1.1)), crown_r * .9, r_trunk * .45, bark, root_r=rr)
    canopy(mb, rnd, tr.top + V((0, 0, crown_r * .45)), crown_r, pal, h - crown_r * .6, h + crown_r * 1.6, blobs, leaves)


def pine(mb, rnd, h, r_trunk, base_r, tiers=6):
    trunk(mb, rnd, h * .55, r_trunk, "bark", lean=.05, top_r=.6)
    z_lo, z_hi = h * .22, h
    for i in range(tiers):
        t = i / (tiers - 1)
        z = z_lo + (z_hi - z_lo) * t * .92
        R = base_r * (1 - .82 * t) + .15
        n = max(6, int(13 - 5 * t))
        a0 = rnd.uniform(0, TAU)
        for k in range(n):
            a = a0 + TAU * k / n + rnd.uniform(-.15, .15)
            d = V((math.cos(a), math.sin(a), -rnd.uniform(.45, .7))).normalized()
            L = R * rnd.uniform(1.0, 1.25)
            root = V((0, 0, z + R * .38))
            mb.add(g_leaf(L, L * .6, L * .14), T(root) @ frame(d), pal=PAL["pine"], z0=0, z1=h, jitter=.1)
    mb.add(g_cyl(6, .18, 0, h * .12, z0=h * .93), pal=PAL["pine"], z0=0, z1=h)


def birch(mb, rnd, h):
    spots = random.Random(rnd.random())

    def bark_fc(face, t):
        return (.10, .09, .09) if spots.random() < .2 else lerp(PAL["birch"][0], PAL["birch"][1], t)
    tr = trunk(mb, rnd, h, .12, "birch", lean=.04, segs=14, sides=7, top_r=.55, face_color=bark_fc)
    for _ in range(3):
        a = rnd.uniform(0, TAU)
        start, rr = tr.at(h * rnd.uniform(.6, .85))
        branch(mb, rnd, start, V((math.cos(a), math.sin(a), .9)), 1.1, .05, "birch", root_r=rr, face_color=bark_fc)
    canopy(mb, rnd, tr.top + V((0, 0, .5)), 1.0, "yellow", h - .6, h + 1.8, 3, 20, .45)


def crystal_cluster(mb, rnd, heights, spread, base_rock):
    if base_rock:
        rock(mb, rnd, V((0, 0, 0)), (spread + .3, spread + .25, (spread + .3) * .45), "cave", moss=False)
    for i, h in enumerate(heights):
        a = rnd.uniform(0, TAU)
        off = 0 if i == 0 else spread * rnd.uniform(.5, 1)
        tilt = 0 if i == 0 else rnd.uniform(.15, .5)
        d = V((math.cos(a) * math.sin(tilt), math.sin(a) * math.sin(tilt), math.cos(tilt)))
        p = V((math.cos(a) * off, math.sin(a) * off, -.08))
        mb.add(g_crystal(h * .2, h), T(p) @ frame(d, V((1, 0, 0))) @ Matrix.Rotation(-math.pi / 2, 4, 'X'),
               pal=PAL["crystal"], z0=0, z1=max(heights), mat=1, jitter=.06)


def fang(mb, rnd, h, w, pal="fang"):
    pts, radii = [], []
    bend = V((rnd.uniform(-1, 1), rnd.uniform(-1, 1), 0)).normalized() * h * .12
    for i in range(7):
        t = i / 6
        pts.append(V((0, 0, -.1 + (h + .1) * t)) + bend * t * t)
        radii.append(w * .5 * (1 - t) ** 1.2 + .005)
    mb.add(g_tube(pts, radii, 6), pal=PAL[pal], z0=0, z1=h, jitter=.06)


def build(type_name, v):
    idx = (PROP_TYPES + EXTRA_TYPES).index(type_name)
    seed = (idx + 1) * 7919 + v * 104729 + 17
    rnd = random.Random(seed)
    mb = MB(seed)

    if type_name == "Tree":
        if v == 0:
            broadleaf(mb, rnd, 3.4, .2, 1.5, "leaf")
        elif v == 1:
            broadleaf(mb, rnd, 3.6, .2, 1.55, "autumn")
        elif v == 2:
            pine(mb, rnd, 6.0, .16, 1.9)
        else:
            birch(mb, rnd, 4.6)
    elif type_name == "LandmarkTree":
        pal = ("autumn", "red", "yellow", "leaf")[v]
        broadleaf(mb, rnd, 5.4, .38, 2.6, pal, blobs=6, leaves=34)
        for i in range(3):   # korzenie
            a = TAU * i / 3 + rnd.uniform(-.3, .3)
            d = V((math.cos(a), math.sin(a), -.75))
            branch(mb, rnd, V((math.cos(a) * .2, math.sin(a) * .2, .55)), d, .8, .17, "bark", 2, root_r=.38)
    elif type_name == "Bush":
        pal = ("leaf", "leaf", "autumn", "leaf")[v]
        for i in range(3):
            c = V((rnd.uniform(-.35, .35), rnd.uniform(-.35, .35), .45)) if i else V((0, 0, .5))
            canopy(mb, rnd, c, rnd.uniform(.5, .65), pal, 0, 1.3, 1, 18, .32)
        if v >= 2:
            for _ in range(5):
                d = rnd_dir(rnd, .5)
                flower_head(mb, rnd, V((d.x * .7, d.y * .7, .55 + d.z * .45)), .1, "purple" if v == 3 else "flower_red")
    elif type_name == "Grass":
        if v < 2:
            grass_clump(mb, rnd, V((0, 0, 0)), 22, .45, .25, "grass")
        else:
            grass_clump(mb, rnd, V((0, 0, 0)), 30, .85, .35, "dry", bend=.3, w=.07)
    elif type_name == "Stone":
        rock(mb, rnd, V((0, 0, .25)), (.7, .6, .45))
        if v % 2:
            rock(mb, rnd, V((.6, .3, .1)), (.3, .28, .22))
    elif type_name == "Pebble":
        for i in range(3):
            rock(mb, rnd, V((rnd.uniform(-.25, .25), rnd.uniform(-.25, .25), .05)), (.16, .14, .09), moss=False, pts=10)
    elif type_name == "Boulder":
        rock(mb, rnd, V((0, 0, .8)), (1.4, 1.2, 1.1))
        rock(mb, rnd, V((1.2, .5, .35)), (.7, .6, .5))
        rock(mb, rnd, V((-1.1, -.6, .2)), (.45, .4, .32))
        if v % 2 == 0:
            grass_clump(mb, rnd, V((.2, -1.0, 0)), 10, .35, .25, "grass")
    elif type_name == "DeadTree":
        h = rnd.uniform(3.6, 4.2)
        tr = trunk(mb, rnd, h, .2, "deadwood", lean=.1, segs=6, top_r=.3)
        for i in range(4):
            a = rnd.uniform(0, TAU)
            s, rr = tr.at(h * rnd.uniform(.45, .9))
            br = branch(mb, rnd, s, V((math.cos(a), math.sin(a), rnd.uniform(.3, 1.0))), rnd.uniform(1.0, 1.7), .08,
                        "deadwood", root_r=rr)
            if rnd.random() < .6:
                s2, rr2 = br.at(br.pts[-1].z - .25 * (br.pts[-1].z - br.pts[1].z))
                branch(mb, rnd, s2, V((math.cos(a + .8), math.sin(a + .8), .8)), .6, .035, "deadwood", 2, root_r=rr2)
    elif type_name == "Rubble":
        for _ in range(rnd.randint(4, 6)):
            s = rnd.uniform(.15, .3)
            rock(mb, rnd, V((rnd.uniform(-.45, .45), rnd.uniform(-.45, .45), s * .4)), (s, s * .9, s * .7), "cave", moss=False, pts=10)
    elif type_name in ("Stalagmite", "StalagmiteSmall"):
        h = 1.6 if type_name == "Stalagmite" else .55
        z = 0.0
        for i in range(3):
            rw = h * (.32 - .09 * i)
            seg = h * (.42 - .08 * i)
            rock(mb, rnd, V((rnd.uniform(-.04, .04) * h, rnd.uniform(-.04, .04) * h, z + seg * .5)), (rw, rw, seg * .62),
                 "cave", moss=False, pts=12)
            z += seg * .8
        mb.add(g_cyl(6, h * .1, 0, h * .3, z0=z - h * .05), pal=PAL["cave"], z0=0, z1=h)
        for _ in range(rnd.randint(1, 2)):
            a = rnd.uniform(0, TAU)
            hh = h * rnd.uniform(.3, .5)
            mb.add(g_cyl(6, hh * .3, 0, hh), T((math.cos(a) * h * .3, math.sin(a) * h * .3, -.03)), pal=PAL["cave"], z0=0, z1=h)
    elif type_name in ("CrystalBig", "CrystalSmall", "CrystalTiny", "LandmarkCrystals"):
        hs = {"CrystalBig": [3, 2, 1.6, 1.0], "CrystalSmall": [1.4, .9, .6],
              "CrystalTiny": [.45, .3, .22]}.get(type_name, [4.4, 3.2, 2.5, 1.8, 1.4])
        spread = hs[0] * .2
        crystal_cluster(mb, rnd, [h * (1 if i == 0 else rnd.uniform(.9, 1.05)) for i, h in enumerate(hs)], spread,
                        type_name != "CrystalTiny")
        mb.mats = ["LD57 Stylized", "LD57 Stylized Glow"]
    elif type_name == "Mushroom":
        glow = v >= 2
        cap = "cap_glow" if glow else ("cap_red" if v == 0 else "cap_brown")
        stem = PAL["stem"]
        for i in range(3):
            p = V((0, 0, 0)) if i == 0 else V((rnd.uniform(-.2, .2), rnd.uniform(-.2, .2), 0))
            h = rnd.uniform(.14, .26) * (1.3 if i == 0 else 1)
            cw = rnd.uniform(.12, .2) * (1.3 if i == 0 else 1)
            mb.add(g_cyl(6, .035, .028, h), T(p), pal=stem, z0=0, z1=h)
            pc = PAL[cap]

            def fc(face, t, pc=pc):
                return stem[1] if face.normal.z < -.2 else lerp(pc[0], pc[1], t)
            mb.add(g_cap(cw, cw * .8), T(p + V((0, 0, h * .92))) @ Rz(rnd.uniform(0, 1)), z0=h, z1=h + cw * .8,
                   face_color=fc, mat=1 if glow else 0)
        grass_clump(mb, rnd, V((0, 0, 0)), 6, .18, .2, "grass", w=.04)
        mb.mats = ["LD57 Stylized", "LD57 Stylized Glow"]
    elif type_name in ("Fang", "LandmarkFang", "FangSmall"):
        h = {"Fang": 4, "LandmarkFang": 6}.get(type_name, .5)
        w = h * (.44 if type_name == "FangSmall" else .17)
        fang(mb, rnd, h, w)
        for i in range(2):
            a = rnd.uniform(0, TAU)
            hh = h * (.5, .33)[i] * rnd.uniform(.9, 1.1)
            mbm = Matrix.Translation(V((math.cos(a) * w * .9, math.sin(a) * w * .9, 0))) @ Matrix.Rotation(rnd.uniform(-.3, .3), 4, 'X')
            pts = [mbm @ V((0, 0, -.1 + (hh + .1) * k / 5)) for k in range(6)]
            mb.add(g_tube(pts, [w * .3 * (1 - k / 5) + .004 for k in range(6)], 6), pal=PAL["fang"], z0=0, z1=h)
        rock(mb, rnd, V((0, 0, 0)), (w * 1.2, w * 1.1, w * .5), "cave", moss=False, pts=10)
    elif type_name == "Cocoon":
        mb.add(g_ico(rnd, .5, 2, .07), T((0, 0, .65)) @ S((.62, .58, 1.3)), pal=PAL["cocoon"], z0=0, z1=1.3)
        mb.add(g_ico(rnd, .5, 1, .1), T((.2, .1, .35)) @ S((.38, .36, .62)), pal=PAL["cocoon"], z0=0, z1=1.3)
        for k in range(4):   # nici owijajace
            z = .3 + k * .22
            rr = .32 * math.sin(math.pi * (z / 1.3)) + .02
            pts = [V((math.cos(a) * rr, math.sin(a) * rr, z + .06 * math.sin(a * 2 + k))) for a in [TAU * i / 12 for i in range(13)]]
            mb.add(g_tube(pts, [.012] * 13, 4, cap=False), pal=((.85, .84, .8), (.95, .95, .92)), z0=0, z1=1.3)
    elif type_name == "WebLean":
        mb.mats = ["Decor Web"]
        quad = ([(-.75, 0, -.75), (.75, 0, -.75), (.75, 0, .75), (-.75, 0, .75)], [(0, 1, 2, 3)])
        uvs = [(0, 0), (1, 0), (1, 1), (0, 1)]
        M = T((0, 0, .7)) @ Matrix.Rotation(math.radians(rnd.uniform(25, 35)), 4, 'X') @ Matrix.Rotation(rnd.uniform(-.15, .15), 4, 'Y')
        mb.add(quad, M, pal=((1, 1, 1), (1, 1, 1)), jitter=0, uvs=uvs)
    elif type_name == "Menhir":
        rock(mb, rnd, V((0, 0, 2.4)), (1.05, .85, 2.6), moss=True, pts=18, flat=.95, moss_level=.5)
        rock(mb, rnd, V((1.3, .4, 1.0)), (.6, .55, 1.2), moss=True, pts=14, flat=.9, moss_level=.5)
        rock(mb, rnd, V((-1.1, -.55, .55)), (.5, .45, .65), moss=True, pts=12, flat=.9, moss_level=.5)
        grass_clump(mb, rnd, V((0, 0, 0)), 16, .5, .9, "dry" if v % 2 else "grass")
    elif type_name == "Fern":
        n = 7 + v
        for i in range(n):
            a = TAU * i / n + rnd.uniform(-.2, .2)
            L = rnd.uniform(.7, 1.0)
            d = V((math.cos(a), math.sin(a), rnd.uniform(.25, .6))).normalized()
            mb.add(g_frond(L, .22, .18), frame(d), pal=PAL["leaf" if v < 3 else "yellow"], z0=0, z1=.6, jitter=.1)
    elif type_name == "Flowers":
        pal = ("purple", "flower_red", "flower_yel", "flower_wht")[v]
        grass_clump(mb, rnd, V((0, 0, 0)), 10, .4, .25, "grass", w=.04)
        for _ in range(6 + v):
            a = rnd.uniform(0, TAU); rr = rnd.uniform(0, .3)
            h = rnd.uniform(.35, .6)
            base = V((math.cos(a) * rr, math.sin(a) * rr, 0))
            top = base + V((rnd.uniform(-.06, .06), rnd.uniform(-.06, .06), h))
            mb.add(g_tube([base, top], [.012, .008], 4), pal=PAL["grass"], z0=0, z1=h)
            if pal == "purple":   # lawenda: klos
                for k in range(4):
                    mb.add(g_ico(rnd, .035, 0, .1), T(top - V((0, 0, k * .05))), pal=PAL[pal], z0=h - .2, z1=h)
            else:
                flower_head(mb, rnd, top, .07, pal)
    return mb


# ---------------------------------------------------------------- materialy

def stylized_material(name):
    m = bpy.data.materials.get(name)
    if m:
        return m
    m = bpy.data.materials.new(name)
    try:
        m.use_nodes = True
    except Exception:
        pass
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial"); out.location = (400, 0)
    col = nt.nodes.new("ShaderNodeVertexColor"); col.layer_name = "Col"; col.location = (-400, 0)
    if name.endswith("Glow"):
        em = nt.nodes.new("ShaderNodeEmission")
        em.inputs["Strength"].default_value = 2.0
        nt.links.new(col.outputs["Color"], em.inputs["Color"])
        nt.links.new(em.outputs[0], out.inputs["Surface"])
    else:
        sh = nt.nodes.new("ShaderNodeBsdfPrincipled")
        nt.links.new(col.outputs["Color"], sh.inputs["Base Color"])
        sh.inputs["Roughness"].default_value = .9
        for k in ("Specular IOR Level", "Specular"):
            if k in sh.inputs:
                sh.inputs[k].default_value = .15
                break
        nt.links.new(sh.outputs[0], out.inputs["Surface"])
    m.use_backface_culling = False
    return m


_old = None


def old_module():
    global _old
    if _old is None:
        here = os.path.dirname(os.path.abspath(__file__)) if "__file__" in globals() else bpy.path.abspath("//")
        path = os.path.join(here, "ld57_enviro_import.py")
        spec = importlib.util.spec_from_file_location("ld57_enviro_import", path)
        _old = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(_old)
    return _old


def get_material(name):
    if name.startswith("LD57 Stylized"):
        return stylized_material(name)
    return old_module().get_material(name)


def make_object(name, mb, collection):
    me = bpy.data.meshes.new(name)
    bmesh.ops.remove_doubles(mb.bm, verts=mb.bm.verts[:], dist=1e-5)
    mb.bm.to_mesh(me)
    mb.bm.free()
    for s in mb.mats:
        me.materials.append(get_material(s))
    me.polygons.foreach_set("use_smooth", [False] * len(me.polygons))
    if me.color_attributes.get("Col"):
        me.color_attributes.active_color = me.color_attributes["Col"]
        try:
            me.color_attributes.render_color_index = me.color_attributes.active_color_index
        except AttributeError:
            pass
    me.update()
    ob = bpy.data.objects.new(name, me)
    collection.objects.link(ob)
    return ob


def remove_collection(name):
    col = bpy.data.collections.get(name)
    if not col:
        return
    for c in [col] + list(col.children_recursive):
        for ob in list(c.objects):
            bpy.data.objects.remove(ob, do_unlink=True)
    for c in reversed(list(col.children_recursive)):
        bpy.data.collections.remove(c)
    bpy.data.collections.remove(col)
    for me in [m for m in bpy.data.meshes if m.users == 0]:
        bpy.data.meshes.remove(me)


def place_rows(rows, x0):
    y, right = 0.0, x0
    for objs in rows:
        depth = max(max(o.dimensions.y for o in objs), 1.0)
        x = x0
        y += depth * .5
        for o in objs:
            w = max(o.dimensions.x, 1.0)
            x += w * .5
            o.location = (x, y, 0)
            x += w * .5 + 1.5
        right = max(right, x)
        y += depth * .5 + 2.0
    return right


def main():
    remove_collection(COLL_NAME)
    root = bpy.data.collections.new(COLL_NAME)
    bpy.context.scene.collection.children.link(root)
    x = ORIGIN_X
    for cat, types in CATEGORIES:
        sub = bpy.data.collections.new(f"{COLL_NAME}_{cat}")
        root.children.link(sub)
        rows = [[make_object(f"{PROP_NAMES.get(t, t)}_v{v}", build(t, v), sub) for v in range(4)] for t in types]
        bpy.context.view_layer.update()
        x = place_rows(rows, x) + 4.0
    old = old_module()
    sub = bpy.data.collections.new(f"{COLL_NAME}_CaveDecor")
    root.children.link(sub)
    objs = [old.make_object(n, fn(), sub) for n, fn in old.CAVE_ITEMS]
    bpy.context.view_layer.update()
    place_rows([[o] for o in objs], x)
    for o in objs:
        o.location.z = -min(v.co.z for v in o.data.vertices)
    n = sum(len(c.objects) for c in [root] + list(root.children_recursive))
    print(f"[LD57] Stylized: {n} obiektow w kolekcji {COLL_NAME}")


if __name__ == "__main__":
    main()
