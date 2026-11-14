"""
LD57 -> Blender: odtwarza dekoracje jaskini generowane w grze przez DecorPrimitives.cs
(propy podlogowe DecorLayout: 22 typy x 4 warianty + elementy CaveDecor: pajeczyna, nitka,
kokon, gniazdo, kiel gniazda, szkielet, pajak).

Uzycie: w Blenderze Scripting -> Open -> Run Script (albo blender -b plik.blend --python ten_plik.py).
Tworzy kolekcje "LD57_Enviro" z podkolekcjami. Ponowne uruchomienie zastepuje poprzednia kolekcje.

Wierne 1:1 z gra: ten sam algorytm System.Random (.NET) i te same ziarna -> te same warianty.
Uklad Unity (Y w gore, lewoskretny) -> Blender (Z w gore): (x, y, z) -> (x, z, y).
"""
import math
import bpy
import bmesh
from mathutils import Matrix, Vector

COLL_NAME = "LD57_Enviro"
ORIGIN_X = 40.0   # gdzie w scenie ulozyc katalog propow (z dala od terenu)


# ---------------------------------------------------------------- System.Random (.NET, wersja "legacy" uzywana przez Unity/Mono)

class NetRandom:
    MBIG = 2147483647
    MSEED = 161803398

    def __init__(self, seed):
        sa = [0] * 56
        sub = self.MBIG if seed == -2147483648 else abs(seed)
        mj = self.MSEED - sub
        sa[55] = mj
        mk = 1
        for i in range(1, 55):
            ii = (21 * i) % 55
            sa[ii] = mk
            mk = mj - mk
            if mk < 0:
                mk += self.MBIG
            mj = sa[ii]
        for _ in range(1, 5):
            for i in range(1, 56):
                sa[i] -= sa[1 + (i + 30) % 55]
                if sa[i] < 0:
                    sa[i] += self.MBIG
        self.sa, self.inext, self.inextp = sa, 0, 21

    def _sample_int(self):
        a = self.inext + 1
        if a >= 56:
            a = 1
        b = self.inextp + 1
        if b >= 56:
            b = 1
        v = self.sa[a] - self.sa[b]
        if v == self.MBIG:
            v -= 1
        if v < 0:
            v += self.MBIG
        self.sa[a] = v
        self.inext, self.inextp = a, b
        return v

    def next_double(self):
        return self._sample_int() * (1.0 / self.MBIG)

    def next(self, lo, hi):
        return int(self.next_double() * (hi - lo)) + lo


def R(r, a, b):
    return a + r.next_double() * (b - a)


# ---------------------------------------------------------------- matematyka w ukladzie Unity

V = Vector
UP = V((0, 1, 0))
ZERO = V((0, 0, 0))
DEG = math.pi / 180.0


def euler(x, y, z):   # Quaternion.Euler: Z, potem X, potem Y
    return (Matrix.Rotation(y * DEG, 4, 'Y') @ Matrix.Rotation(x * DEG, 4, 'X')
            @ Matrix.Rotation(z * DEG, 4, 'Z'))


def from_to(a, b):
    return a.rotation_difference(b).to_matrix().to_4x4()


def look_rotation(fwd, up=UP):
    f = fwd.normalized()
    x = up.cross(f).normalized()
    y = f.cross(x)
    m = Matrix.Identity(4)
    for i in range(3):
        m[i][0], m[i][1], m[i][2] = x[i], y[i], f[i]
    return m


IDENT = Matrix.Identity(4)


def trs(pos, rot, scale):
    return Matrix.Translation(V(pos)) @ rot @ Matrix.Diagonal(V(scale)).to_4x4()


# ---------------------------------------------------------------- ksztalty (lista trojkatow w ukladzie Unity, nawiniecie Unity)

_shape_cache = {}


def _cached(key, build):
    if key not in _shape_cache:
        _shape_cache[key] = build()
    return _shape_cache[key]


def flat_tri(o, a, b, c, inside):
    nrm = (b - a).cross(c - a)
    centroid = (a + b + c) / 3.0
    if nrm.dot(centroid - inside) < 0:
        b, c = c, b
    o.append((a.copy(), b.copy(), c.copy()))


def cone_shape():
    def build():
        N, o = 8, []
        for i in range(N):
            a0, a1 = i * math.tau / N, (i + 1) * math.tau / N
            o.append((V((math.cos(a0) * .5, 0, math.sin(a0) * .5)), V((0, 1, 0)), V((math.cos(a1) * .5, 0, math.sin(a1) * .5))))
        return o
    return _cached("cone", build)


def prism_shape(sides, top):
    def build():
        o, inside = [], V((0, .5, 0))
        for i in range(sides):
            a0, a1 = i * math.tau / sides, (i + 1) * math.tau / sides
            b0 = V((math.cos(a0) * .5, 0, math.sin(a0) * .5)); b1 = V((math.cos(a1) * .5, 0, math.sin(a1) * .5))
            t0 = V((math.cos(a0) * .5 * top, 1, math.sin(a0) * .5 * top)); t1 = V((math.cos(a1) * .5 * top, 1, math.sin(a1) * .5 * top))
            flat_tri(o, b0, t0, t1, inside)
            flat_tri(o, b0, t1, b1, inside)
            flat_tri(o, ZERO, b0, b1, inside)
            flat_tri(o, V((0, 1, 0)), t0, t1, inside)
        return o
    return _cached(f"prism{sides}_{round(top * 100)}", build)


def box_shape():
    def build():
        o = []
        c = [V((-.5 if (i & 1) == 0 else .5, -.5 if (i & 2) == 0 else .5, -.5 if (i & 4) == 0 else .5)) for i in range(8)]
        for f in ((0, 1, 3, 2), (4, 5, 7, 6), (0, 1, 5, 4), (2, 3, 7, 6), (0, 2, 6, 4), (1, 3, 7, 5)):
            flat_tri(o, c[f[0]], c[f[1]], c[f[2]], ZERO)
            flat_tri(o, c[f[0]], c[f[2]], c[f[3]], ZERO)
        return o
    return _cached("box", build)


def octa_shape():
    def build():
        o = []
        ring = [V((.5, .3, 0)), V((0, .3, .5)), V((-.5, .3, 0)), V((0, .3, -.5))]
        inside = V((0, .3, 0))
        for i in range(4):
            a, b = ring[i], ring[(i + 1) % 4]
            flat_tri(o, ZERO, a, b, inside)
            flat_tri(o, V((0, 1, 0)), a, b, inside)
        return o
    return _cached("octa", build)


def crystal_shape():
    def build():
        o, inside, N = [], V((0, .4, 0)), 6
        for i in range(N):
            a0, a1 = i * math.tau / N, (i + 1) * math.tau / N
            b0 = V((math.cos(a0) * .5, 0, math.sin(a0) * .5)); b1 = V((math.cos(a1) * .5, 0, math.sin(a1) * .5))
            t0 = b0 + UP * .65; t1 = b1 + UP * .65
            flat_tri(o, b0, t0, t1, inside)
            flat_tri(o, b0, t1, b1, inside)
            flat_tri(o, ZERO, b0, b1, inside)
            flat_tri(o, V((0, 1, 0)), t0, t1, inside)
        return o
    return _cached("crystal", build)


def quad_shape():   # Unity: tris 0,2,1, 2,3,1 (normalna -Z); UV w quad_uv
    v = [V((-.5, -.5, 0)), V((.5, -.5, 0)), V((-.5, .5, 0)), V((.5, .5, 0))]
    return [(v[0], v[2], v[1]), (v[2], v[3], v[1])]


QUAD_UV = {(-.5, -.5): (0, 0), (.5, -.5): (1, 0), (-.5, .5): (0, 1), (.5, .5): (1, 1)}


def ico_shape():
    def build():
        t = (1 + math.sqrt(5)) * .5
        v = [V(p).normalized() * .5 for p in (
            (-1, t, 0), (1, t, 0), (-1, -t, 0), (1, -t, 0), (0, -1, t), (0, 1, t), (0, -1, -t), (0, 1, -t),
            (t, 0, -1), (t, 0, 1), (-t, 0, -1), (-t, 0, 1))]
        f = (0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
             3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1)
        return [(v[f[i]], v[f[i + 1]], v[f[i + 2]]) for i in range(0, len(f), 3)]
    return _cached("ico", build)


def _from_bmesh(bm):
    """bmesh (Blender, Z w gore) -> trojkaty w ukladzie Unity z nawinieciem Unity."""
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    sw = lambda c: V((c.x, c.z, c.y))
    out = [tuple(sw(l.vert.co) for l in reversed(f.loops)) for f in bm.faces]
    bm.free()
    return out


def unity_sphere():
    def build():
        bm = bmesh.new()
        bmesh.ops.create_uvsphere(bm, u_segments=24, v_segments=16, radius=.5)
        return _from_bmesh(bm)
    return _cached("usphere", build)


def unity_cylinder():   # promien 0,5, wysokosc 2 (y -1..1)
    def build():
        bm = bmesh.new()
        bmesh.ops.create_cone(bm, cap_ends=True, segments=20, radius1=.5, radius2=.5, depth=2)
        return _from_bmesh(bm)
    return _cached("ucyl", build)


def unity_capsule():    # promien 0,5, wysokosc 2
    def build():
        bm = bmesh.new()
        bmesh.ops.create_uvsphere(bm, u_segments=24, v_segments=17, radius=.5)
        for v in bm.verts:
            v.co.z += .5 if v.co.z > 0 else -.5
        return _from_bmesh(bm)
    return _cached("ucaps", build)


def unity_cube():
    return box_shape()


# ---------------------------------------------------------------- budowanie siatek

class Builder:
    def __init__(self, slots):
        self.tris = []          # (a, b, c, slot) w ukladzie Unity
        self.slots = slots

    def add(self, shape, m, slot=0):
        for a, b, c in shape:
            self.tris.append((m @ a, m @ b, m @ c, slot))


def segment(b, shape, a, bb, radius, slot=0, parent=IDENT):
    d = bb - a
    ln = d.length
    if ln < 1e-4:
        return
    b.add(shape, parent @ trs((a + bb) * .5, from_to(UP, d / ln), (radius * 2, ln * .5, radius * 2)), slot)


def rot(r, tilt):
    return euler(R(r, -tilt, tilt), R(r, 0, 360), R(r, -tilt, tilt))


def crowns(b, r, base_y, dmin, dmax, spread, slot_a, slot_b):
    centers = [V((0, 0, 0))]
    centers.append(V((R(r, .6, 1.1), R(r, .3, .7), R(r, .4, .9))) * spread)
    centers.append(V((R(r, -1.1, -.6), R(r, .6, 1.0), R(r, -.9, -.4))) * spread)
    for i in range(3):
        d = R(r, dmin, dmax)
        pos = V((0, base_y, 0)) + centers[i]
        q = rot(r, 12)
        b.add(ico_shape(), trs(pos, q, (d, d * R(r, .82, .95), d)), slot_b if i == 1 else slot_a)


PROP_TYPES = ["Tree", "Bush", "Stone", "Grass", "Pebble",
              "Boulder", "DeadTree", "Rubble", "Stalagmite", "StalagmiteSmall",
              "CrystalBig", "CrystalSmall", "CrystalTiny", "Mushroom",
              "Fang", "FangSmall", "Cocoon", "WebLean",
              "LandmarkTree", "Menhir", "LandmarkCrystals", "LandmarkFang"]

CATEGORIES = [("Las", PROP_TYPES[0:5]), ("Skala", PROP_TYPES[5:10]), ("Krysztaly", PROP_TYPES[10:14]),
              ("Otchlan", PROP_TYPES[14:18]), ("Landmarki", PROP_TYPES[18:22])]


def build_prop(type_name, variant):
    t = PROP_TYPES.index(type_name)
    r = NetRandom((t + 1) * 7919 + variant * 104729 + 17)
    ico, cone, crystal = ico_shape(), cone_shape(), crystal_shape()

    if type_name == "Tree":
        b = Builder(["Decor Trunk", "Decor Leaf A", "Decor Leaf B"])
        h = R(r, 3.3, 3.8)
        b.add(prism_shape(6, .7), trs((0, -.05, 0), rot(r, 2), (.35, h + .05, .35)), 0)
        crowns(b, r, h + .4, 2.5, 3.5, 1, 1, 2)
    elif type_name == "LandmarkTree":
        b = Builder(["Decor Trunk", "Decor Leaf A", "Decor Leaf B"])
        b.add(prism_shape(7, .65), trs((0, -.05, 0), rot(r, 1.5), (.6, 5.15, .6)), 0)
        for i in range(3):
            a = (i * 120 + R(r, -20, 20)) * DEG
            d = V((math.cos(a), 0, math.sin(a)))
            b.add(cone, trs(d * .3 + UP * .05, look_rotation(d) @ euler(65, 0, 0), (.45, 1.5, .45)), 0)
        crowns(b, r, 5.6, 3.0, 4.0, 1.25, 1, 2)
    elif type_name == "Bush":
        b = Builder(["Decor Bush"])
        d = R(r, 1.15, 1.4)
        b.add(ico, trs((0, d * .3, 0), euler(0, R(r, 0, 360), 0), (d, d * .75, d)))
        d2 = d * R(r, .55, .7)
        px = R(r, -.4, .4); pz = R(r, -.4, .4)
        b.add(ico, trs((px, d2 * .28, pz), euler(0, R(r, 0, 360), 0), (d2, d2 * .75, d2)))
    elif type_name == "Stone":
        b = Builder(["Decor Stone"])
        w = 1.2 * R(r, .82, 1)
        q = rot(r, 8)
        sx = w * R(r, .9, 1.15); sy = w * R(r, .5, .55); sz = w * R(r, .9, 1.15)
        b.add(ico, trs((0, w * .12, 0), q, (sx, sy, sz)))
    elif type_name == "Pebble":
        b = Builder(["Decor Stone"])
        q = rot(r, 6)
        sx = .3 * R(r, .85, 1); sz = .3 * R(r, .85, 1)
        b.add(ico, trs((0, .04, 0), q, (sx, .15, sz)))
        px = R(r, .15, .25); pz = R(r, -.2, .2)
        b.add(ico, trs((px, .025, pz), rot(r, 6), (.16, .09, .18)))
    elif type_name == "Grass":
        b = Builder(["Decor Grass"])
        for _ in range(5):
            h = R(r, .25, .3)
            px = R(r, -.07, .07); pz = R(r, -.07, .07)
            b.add(cone, trs((px, 0, pz), rot(r, 20), (.07, h, .07)))
    elif type_name == "Boulder":
        b = Builder(["Decor Boulder"])
        q = rot(r, 5)
        s = (2.4 * R(r, .9, 1.05), 1.8 * R(r, .9, 1), 2.2 * R(r, .9, 1.05))
        b.add(ico, trs((0, .7, 0), q, s))
        px = R(r, .9, 1.3); pz = R(r, .2, .7)
        b.add(ico, trs((px, .35, pz), rot(r, 8), (1.4, 1, 1.3)))
        px = R(r, -1.2, -.8); pz = R(r, -.8, -.4)
        b.add(ico, trs((px, .2, pz), rot(r, 8), (.9, .6, .8)))
    elif type_name == "DeadTree":
        b = Builder(["Decor Dead Wood"])
        h = R(r, 3.7, 4.1)
        b.add(prism_shape(6, .45), trs((0, -.05, 0), rot(r, 3), (.32, h + .05, .32)))
        a0 = R(r, 0, 360)
        attach = (.45, .65, .82)
        for i in range(3):
            a = (a0 + i * 120 + R(r, -25, 25)) * DEG
            cx = math.cos(a); dy = R(r, .55, 1.1)
            d = V((cx, dy, math.sin(a))).normalized()
            ln = R(r, 1.1, 1.8)
            b.add(prism_shape(5, .3), trs((0, h * attach[i], 0), from_to(UP, d), (.08, ln, .08)))
    elif type_name == "Rubble":
        b = Builder(["Decor Rubble"])
        for _ in range(r.next(4, 7)):
            s = R(r, .2, .48)
            px = R(r, -.45, .45); pz = R(r, -.45, .45)
            q = euler(R(r, 0, 360), R(r, 0, 360), R(r, 0, 360))
            b.add(box_shape(), trs((px, s * .3, pz), q, (s, s * R(r, .7, 1.1), s)))
    elif type_name in ("Stalagmite", "StalagmiteSmall"):
        b = Builder(["Decor Rock"])
        h = 1.4 if type_name == "Stalagmite" else .45
        b.add(cone, trs((0, -.05, 0), rot(r, 3), (h * .45, h + .05, h * .45)))
        for _ in range(r.next(1, 3)):
            hh = h * R(r, .35, .6); a = R(r, 0, math.tau)
            pos = V((math.cos(a), 0, math.sin(a))) * h * .22 + V((0, -.05, 0))
            b.add(cone, trs(pos, rot(r, 7), (hh * .5, hh + .05, hh * .5)))
    elif type_name in ("CrystalBig", "CrystalSmall", "CrystalTiny", "LandmarkCrystals"):
        b = Builder(["Decor Blue Crystal"])
        hs = {"CrystalBig": (3, 2, 1.6), "CrystalSmall": (1.4, .9), "CrystalTiny": (.45, .3)}.get(type_name, (4.4, 3.2, 2.5, 1.4))
        for i, h0 in enumerate(hs):
            hh = h0 * (1 if i == 0 else R(r, .92, 1.05))
            a = R(r, 0, math.tau)
            off = 0 if i == 0 else hh * .14 + .1
            pos = V((math.cos(a), 0, math.sin(a))) * off + V((0, -.05, 0))
            b.add(crystal, trs(pos, rot(r, 6 if i == 0 else 20), (hh * .27, hh + .05, hh * .27)))
    elif type_name == "Mushroom":
        b = Builder(["Decor Mushroom Cap", "Decor Mushroom Stem"])
        for i in range(3):
            h = R(r, .12, .2); cw = R(r, .14, .22)
            if i == 0:
                pos = V((0, 0, 0))
            else:
                px = R(r, -.18, .18); pz = R(r, -.18, .18); pos = V((px, 0, pz))
            b.add(prism_shape(6, .8), trs(pos, IDENT, (.05, h, .05)), 1)
            b.add(ico, trs(pos + UP * h, euler(0, R(r, 0, 360), 0), (cw, cw * .55, cw)), 0)
    elif type_name in ("Fang", "LandmarkFang", "FangSmall"):
        b = Builder(["Decor Fang"])
        h = {"Fang": 4, "LandmarkFang": 6}.get(type_name, .5)
        w = h * (.44 if type_name == "FangSmall" else .17)
        b.add(cone, trs((0, -.1, 0), rot(r, 4), (w, h + .1, w)))
        for i, sz in enumerate((.5, .33)):
            hh = h * sz * R(r, .9, 1.1); a = R(r, 0, math.tau) + i * 3
            pos = V((math.cos(a), 0, math.sin(a))) * w * .9 + V((0, -.1, 0))
            q = euler(R(r, -16, 16), R(r, 0, 360), R(r, -16, 16))
            b.add(cone, trs(pos, q, (w * .6, hh + .1, w * .6)))
    elif type_name == "Cocoon":
        b = Builder(["Decor Cocoon"])
        q = rot(r, 6)
        s = (.6 * R(r, .9, 1.1), 1.3 * R(r, .9, 1), .6 * R(r, .9, 1.1))
        b.add(ico, trs((0, .62, 0), q, s))
        b.add(ico, trs((.18, .35, .08), rot(r, 6), (.4, .7, .4)))
    elif type_name == "WebLean":
        b = Builder(["Decor Web"])
        b.add(quad_shape(), trs((0, .62, 0), euler(R(r, -35, -25), 0, R(r, -8, 8)), (1.5, 1.5, 1)))
        b.uv_quad = True
    else:   # Menhir
        b = Builder(["Decor Boulder"])
        o = octa_shape()
        b.add(o, trs((0, -.1, 0), euler(R(r, -3, 3), R(r, 0, 360), R(r, -3, 3)), (1.3, 5.1, 1.1)))
        b.add(o, trs((1.1, -.1, .4), euler(R(r, 8, 14), R(r, 0, 360), R(r, -6, 6)), (.8, 2.7, .7)))
        b.add(o, trs((-.9, -.1, -.5), euler(R(r, -14, -8), R(r, 0, 360), R(r, -6, 6)), (.55, 1.5, .5)))
    return b


PROP_NAMES = {"DeadTree": "Dead Tree", "StalagmiteSmall": "Stalagmite Small", "CrystalBig": "Crystal Big",
              "CrystalSmall": "Crystal Small", "CrystalTiny": "Crystal Tiny", "FangSmall": "Fang Small",
              "WebLean": "Web Lean", "LandmarkTree": "Landmark Tree", "Menhir": "Landmark Menhir",
              "LandmarkCrystals": "Landmark Crystals", "LandmarkFang": "Landmark Fang"}


# ---------------------------------------------------------------- elementy CaveDecor (z prymitywow Unity)

def cave_web(size=2.0):
    b = Builder(["Decor Web"])
    b.add(quad_shape(), trs(ZERO, IDENT, (size, size, size)))
    b.uv_quad = True
    return b


def cave_thread(length=1.5):
    b = Builder(["Decor Thread"])
    segment(b, unity_cylinder(), V((0, 0, 0)), V((0, -length, 0)), .012)
    return b


def cave_cocoon(size=1.0):
    b = Builder(["Decor Cocoon"])
    sph = unity_sphere()
    b.add(sph, trs(ZERO, IDENT, V((.45, 1, .45)) * size))
    b.add(sph, trs(V((.1, -.15, .05)) * size, IDENT, V((.3, .5, .3)) * size))
    return b


def cave_nest(size=4.0):
    b = Builder(["Decor Nest", "Decor Thread"])
    sph, cyl = unity_sphere(), unity_cylinder()
    b.add(sph, trs((0, -.65 * size, 0), IDENT, V((1, 1.3, 1)) * size))
    b.add(sph, trs(V((.35, -.9, .2)) * size, IDENT, V((1, 1, 1)) * size * .45))
    b.add(sph, trs(V((-.3, -.5, -.3)) * size, IDENT, V((1, 1, 1)) * size * .4))
    for i in range(5):
        a = i * 72 * DEG
        lo = V((math.cos(a) * .3, 0, math.sin(a) * .3)) * size
        hi = V((math.cos(a) * .6, .7, math.sin(a) * .6)) * size
        segment(b, cyl, lo, hi, .025 * max(1, size * .5), 1)
    return b


def cave_fang(height=2.5, base=.32):
    b = Builder(["Decor Fang"])
    b.add(cone_shape(), trs((0, -.1, 0), IDENT, (base, height + .1, base)))
    return b


def cave_skeleton():
    b = Builder(["Decor Bone", "Decor Metal"])
    sph, cap, box = unity_sphere(), unity_capsule(), unity_cube()
    b.add(sph, trs((0, .12, .95), IDENT, (.22, .24, .26)))
    b.add(sph, trs((0, .06, 1.05), IDENT, (.14, .07, .16)))
    segment(b, cap, V((0, .07, .8)), V((0, .07, .05)), .045)
    for i in range(4):
        z = .7 - i * .14; w = .2 - i * .025
        segment(b, cap, V((-w, .05, z)), V((w, .05, z)), .025)
    segment(b, cap, V((-.2, .06, .72)), V((-.55, .05, .5)), .03)
    segment(b, cap, V((.2, .06, .72)), V((.45, .05, 1.05)), .03)
    segment(b, cap, V((-.1, .06, .05)), V((-.25, .05, -.7)), .04)
    segment(b, cap, V((.1, .06, .05)), V((.35, .05, -.65)), .04)
    sw = trs((.7, 0, .35), euler(8, 20, 6), (1, 1, 1))
    b.add(box, sw @ trs((0, .55, 0), IDENT, (.08, 1.1, .02)), 1)
    b.add(box, sw @ trs((0, 1.1, 0), IDENT, (.3, .04, .04)), 1)
    b.add(box, sw @ trs((0, 1.25, 0), IDENT, (.04, .26, .04)), 0)
    return b


def cave_spider():
    b = Builder(["Decor Spider"])
    sph, cyl = unity_sphere(), unity_cylinder()
    b.add(sph, trs(ZERO, IDENT, (.38, .28, .5)))
    b.add(sph, trs((0, .02, .32), IDENT, (.22, .18, .2)))
    for side in (-1, 1):
        for deg in (35, 65, 100, 135):
            a = deg * DEG
            d = V((side * math.sin(a), 0, math.cos(a)))
            segment(b, cyl, d * .15, d * .55 + UP * .3, .025)
            segment(b, cyl, d * .55 + UP * .3, d * .95 - UP * .25, .02)
    return b


CAVE_ITEMS = [("Web", cave_web), ("Thread", cave_thread), ("Cocoon Wall", cave_cocoon), ("Nest", cave_nest),
              ("Nest Fang", cave_fang), ("Skeleton", cave_skeleton), ("Spider Prop", cave_spider)]


# ---------------------------------------------------------------- materialy

def srgb_to_lin(c):
    return c / 12.92 if c <= .04045 else ((c + .055) / 1.055) ** 2.4


MATS = {   # nazwa: (kolor sRGB jak w Unity, unlit, przezroczysty, smoothness, metallic)
    "Decor Trunk": ((.26, .16, .09), False, False, .05, 0),
    "Decor Dead Wood": ((.2, .17, .14), False, False, .05, 0),
    "Decor Leaf A": ((.12, .38, .1), False, False, .08, 0),
    "Decor Leaf B": ((.18, .46, .12), False, False, .08, 0),
    "Decor Bush": ((.06, .24, .07), False, False, .05, 0),
    "Decor Stone": ((.4, .4, .42), False, False, .1, 0),
    "Decor Grass": ((.22, .58, .15), False, False, .05, 0),
    "Decor Blue Crystal": ((.4, .95, 1.5), True, False, 0, 0),
    "Decor Boulder": ((.3, .3, .33), False, False, .1, 0),
    "Decor Mushroom Cap": ((.2, 1.2, .9), True, False, 0, 0),
    "Decor Mushroom Stem": ((.75, .78, .7), False, False, .1, 0),
    "Decor Web": ((1, 1, 1), True, True, 0, 0),
    "Decor Thread": ((.8, .8, .78), True, False, 0, 0),
    "Decor Cocoon": ((.78, .77, .72), False, False, .12, 0),
    "Decor Nest": ((.13, .05, .2), False, False, .2, 0),
    "Decor Bone": ((.8, .76, .65), False, False, .15, 0),
    "Decor Metal": ((.45, .47, .5), False, False, .5, .7),
    "Decor Rock": ((.1, .095, .11), False, False, .1, 0),
    "Decor Rubble": ((.16, .15, .16), False, False, .08, 0),
    "Decor Fang": ((.11, .03, .15), False, False, .35, 0),
    "Decor Spider": ((.22, .08, .32), False, False, .25, 0),
}


def web_image():
    name = "Decor Web"
    img = bpy.data.images.get(name)
    if img:
        return img
    N, spokes = 128, 8
    seg = 2 * math.pi / spokes

    def smooth(e0, e1, x):
        t = min(max((x - e0) / (e1 - e0), 0), 1)
        return t * t * (3 - 2 * t)
    px = []
    for y in range(N):
        for x in range(N):
            u = (x + .5) / N * 2 - 1; v = (y + .5) / N * 2 - 1
            r = math.sqrt(u * u + v * v); a = 0.0
            if r < .99:
                th = math.atan2(v, u) + math.pi
                s = (th % seg) / seg
                sag = 1 - .09 * 4 * s * (1 - s)
                spoke = min(s, 1 - s) * seg * r * (N * .5)
                a = max(a, 1 - smooth(.4, 1.4, spoke))
                for k in range(1, 6):
                    d = abs(r - k * .19 * sag) * (N * .5)
                    a = max(a, 1 - smooth(.4, 1.3, d))
            px += (1, 1, 1, round(a * .5 * 255) / 255)
    img = bpy.data.images.new(name, N, N, alpha=True)
    img.pixels[:] = px
    img.pack()
    return img


def get_material(name):
    m = bpy.data.materials.get(name)
    if m:
        return m
    col, unlit, transp, smooth, metal = MATS[name]
    lin = [srgb_to_lin(c) if c <= 1 else c ** 2.2 for c in col]
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*[min(c, 1) for c in lin], 1)
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial"); out.location = (400, 0)
    if unlit:
        sh = nt.nodes.new("ShaderNodeEmission")
        sh.inputs["Color"].default_value = (*lin, 1)
        sh.inputs["Strength"].default_value = 1.0
        if transp:
            tex = nt.nodes.new("ShaderNodeTexImage"); tex.image = web_image(); tex.location = (-500, 0)
            tr = nt.nodes.new("ShaderNodeBsdfTransparent"); tr.location = (-100, -150)
            mix = nt.nodes.new("ShaderNodeMixShader"); mix.location = (200, 0)
            nt.links.new(tex.outputs["Alpha"], mix.inputs["Fac"])
            nt.links.new(tr.outputs[0], mix.inputs[1])
            nt.links.new(sh.outputs[0], mix.inputs[2])
            nt.links.new(mix.outputs[0], out.inputs["Surface"])
            try:
                m.surface_render_method = 'BLENDED'
            except AttributeError:
                m.blend_method = 'BLEND'
            m.use_backface_culling = False
        else:
            nt.links.new(sh.outputs[0], out.inputs["Surface"])
    else:
        sh = nt.nodes.new("ShaderNodeBsdfPrincipled")
        sh.inputs["Base Color"].default_value = (*lin, 1)
        sh.inputs["Roughness"].default_value = 1 - smooth
        sh.inputs["Metallic"].default_value = metal
        nt.links.new(sh.outputs[0], out.inputs["Surface"])
    return m


# ---------------------------------------------------------------- tworzenie obiektow

def to_blender(p):
    return (p.x, p.z, p.y)


def make_object(name, b, collection):
    verts, faces, mat_idx, uvs = [], [], [], []
    index = {}
    for a, bb, c, slot in b.tris:
        f = []
        for p in (a, c, bb):   # odbicie osi -> odwrocone nawiniecie
            key = (round(p.x, 5), round(p.y, 5), round(p.z, 5))
            if key not in index:
                index[key] = len(verts)
                verts.append(to_blender(p))
            f.append(index[key])
        if len(set(f)) == 3:
            faces.append(f); mat_idx.append(slot)
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], faces)
    for s in b.slots:
        me.materials.append(get_material(s))
    me.polygons.foreach_set("material_index", mat_idx)
    me.polygons.foreach_set("use_smooth", [False] * len(faces))
    if getattr(b, "uv_quad", False):
        uv = me.uv_layers.new(name="UVMap")
        # quad: UV z lokalnego polozenia przed transformacja -> rzut na plaszczyzne quada
        cs = [V(v) for v in verts]
        cen = sum(cs, V()) / len(cs)
        e1 = (cs[1] - cs[0]) if len(cs) > 1 else V((1, 0, 0))
        n = me.polygons[0].normal
        ax = e1.normalized(); ay = n.cross(ax).normalized()
        pr = [((c - cen).dot(ax), (c - cen).dot(ay)) for c in cs]
        mnx, mxx = min(p[0] for p in pr), max(p[0] for p in pr)
        mny, mxy = min(p[1] for p in pr), max(p[1] for p in pr)
        for loop in me.loops:
            x, y = pr[loop.vertex_index]
            uv.data[loop.index].uv = ((x - mnx) / max(mxx - mnx, 1e-6), (y - mny) / max(mxy - mny, 1e-6))
    me.validate()
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


def place_rows(rows, x0):
    """Blok kategorii: wiersze (typy) wzdluz Y, warianty wzdluz X, zaczynajac od x0. Zwraca prawa krawedz bloku."""
    y, right = 0.0, x0
    for _, objs in rows:
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
        rows = []
        for t in types:
            objs = [make_object(f"{PROP_NAMES.get(t, t)}_v{v}", build_prop(t, v), sub) for v in range(4)]
            rows.append((t, objs))
        bpy.context.view_layer.update()
        x = place_rows(rows, x) + 4.0
    sub = bpy.data.collections.new(f"{COLL_NAME}_CaveDecor")
    root.children.link(sub)
    objs = [make_object(n, fn(), sub) for n, fn in CAVE_ITEMS]
    bpy.context.view_layer.update()
    place_rows([(n, [o]) for n, o in zip([c[0] for c in CAVE_ITEMS], objs)], x)
    for o in objs:   # w katalogu stawiamy wszystko na ziemi (pivot zostaje taki jak w grze)
        o.location.z = -min(v.co.z for v in o.data.vertices)
    n = sum(len(c.objects) for c in [root] + list(root.children_recursive))
    print(f"[LD57] Utworzono {n} obiektow w kolekcji {COLL_NAME}")


if __name__ == "__main__":
    main()
