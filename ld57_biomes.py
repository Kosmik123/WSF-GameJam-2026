"""
LD57 – assety pod biomy (Las, Pustynia, Wybrzeze, Lod, Wulkan, Bagno) w stylu ld57_enviro_stylized.py.
Kazdy typ w 4 wariantach. Kolekcja "LD57_Biomes" -> "Biome_<nazwa>" -> "<nazwa>_Landmarki/Duze/Srednie/Drobne".
Wymaga ld57_enviro_stylized.py (i ld57_enviro_import.py) w tym samym folderze. Ponowne uruchomienie zastepuje kolekcje.
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
ROOT = "LD57_Biomes"


def _load(name):
    here = os.path.dirname(os.path.abspath(__file__)) if "__file__" in globals() else bpy.path.abspath("//")
    spec = importlib.util.spec_from_file_location(name, os.path.join(here, name + ".py"))
    m = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(m)
    return m


st = _load("ld57_enviro_stylized")
MB, T, S, Rz, frame, lerp, clamp01 = st.MB, st.T, st.S, st.Rz, st.frame, st.lerp, st.clamp01
g_cyl, g_tube, g_hull, g_ico, g_leaf, g_blade, g_frond, g_cap = st.g_cyl, st.g_tube, st.g_hull, st.g_ico, st.g_leaf, st.g_blade, st.g_frond, st.g_cap
GLOW_MATS = ["LD57 Stylized", "LD57 Stylized Glow"]

st.PAL.update({
    "sand":       ((.62, .46, .28), (.93, .80, .58)),
    "terracotta": ((.50, .22, .12), (.80, .44, .26)),
    "ochre":      ((.62, .42, .16), (.90, .68, .34)),
    "cactus":     ((.18, .34, .18), (.46, .62, .36)),
    "drybrush":   ((.38, .28, .16), (.72, .58, .38)),
    "bone":       ((.70, .64, .52), (.96, .93, .84)),
    "palm":       ((.14, .34, .10), (.52, .72, .26)),
    "palmbark":   ((.36, .26, .16), (.62, .48, .32)),
    "driftwood":  ((.52, .48, .44), (.84, .82, .78)),
    "coastrock":  ((.30, .32, .36), (.62, .64, .68)),
    "seaweed":    ((.16, .22, .08), (.42, .46, .16)),
    "shell":      ((.80, .62, .52), (.99, .92, .84)),
    "starfish":   ((.80, .30, .12), (1.0, .58, .30)),
    "coral_pink": ((.70, .26, .38), (1.0, .60, .66)),
    "coral_orange": ((.80, .34, .12), (1.0, .66, .36)),
    "coral_purple": ((.38, .20, .58), (.70, .50, .90)),
    "coral_yellow": ((.78, .62, .14), (1.0, .88, .44)),
    "wood":       ((.28, .18, .10), (.52, .36, .22)),
    "snow":       ((.74, .82, .92), (.97, .98, 1.0)),
    "ice":        ((.42, .66, .86), (.82, .94, 1.0)),
    "frost":      ((.42, .46, .54), (.72, .78, .86)),
    "icerock":    ((.32, .36, .44), (.60, .64, .72)),
    "basalt":     ((.07, .07, .08), (.26, .25, .27)),
    "ash":        ((.20, .19, .19), (.46, .43, .42)),
    "lava":       ((.95, .22, .02), (1.0, .70, .18)),
    "charcoal":   ((.05, .04, .04), (.20, .17, .15)),
    "obsidian":   ((.05, .03, .08), (.30, .22, .40)),
    "swampleaf":  ((.18, .26, .08), (.48, .54, .20)),
    "swampbark":  ((.16, .13, .09), (.36, .30, .22)),
    "spanishmoss": ((.40, .44, .34), (.66, .70, .56)),
    "sedge":      ((.24, .30, .10), (.58, .60, .26)),
    "reed":       ((.30, .36, .12), (.66, .68, .32)),
    "cattail":    ((.24, .14, .08), (.44, .28, .16)),
    "lily":       ((.12, .32, .10), (.36, .58, .22)),
    "glow_purple": ((.45, .15, .90), (.85, .55, 1.0)),
    "flower_pink": ((.75, .30, .50), (1.0, .65, .80)),
    "moss_wet":   ((.16, .26, .08), (.34, .44, .14)),
})
PAL = st.PAL


def lin(c):
    return tuple(st._lin(x) for x in c)


def lum(c):
    return .3 * c[0] + .59 * c[1] + .11 * c[2]


def remap(mb, fn):
    """Przekolorowanie gotowej siatki: fn(face, kolor_liniowy) -> (kolor_sRGB|None, material|None) albo None."""
    for f in mb.bm.faces:
        f.normal_update()
        c = tuple(f.loops[0][mb.col])[:3]
        res = fn(f, c)
        if res is None:
            continue
        col, mat = res
        if mat is not None:
            f.material_index = mat
        if col is not None:
            k = 1 + mb.rnd.uniform(-.05, .05)
            cl = lin(tuple(min(1.0, x * k) for x in col))
            for l in f.loops:
                l[mb.col] = (*cl, 1.0)


def by_lum(pal, lo=.0, hi=.5):
    """Nowa paleta wedlug jasnosci starego koloru (zachowuje cieniowanie)."""
    def fn(f, c):
        return lerp(PAL[pal][0], PAL[pal][1], clamp01((lum(c) - lo) / (hi - lo))), 0
    return fn


def snow_pass(mb, th=.5, two_sided=False, chance=1.0):
    """Snieg na scianach skierowanych w gore. two_sided: dla cienkich plaszczyzn (igly, liscie) liczy |normal.z|.
    chance < 1: snieg w latach (czesc scian zostaje w oryginalnym kolorze)."""
    rnd = random.Random(len(mb.bm.faces))

    def fn(f, c):
        nz = abs(f.normal.z) if two_sided else f.normal.z
        if nz <= th or rnd.random() > chance:
            return None
        return lerp(PAL["snow"][0], PAL["snow"][1], clamp01((nz - th) / (1 - th) + .3)), None
    remap(mb, fn)


def scale_mb(mb, s):
    bmesh.ops.scale(mb.bm, vec=(s, s, s), verts=mb.bm.verts[:])


def rock2(mb, rnd, center, size, fc, pts=14):
    points = []
    for _ in range(pts):
        d = st.rnd_dir(rnd)
        points.append(V((d.x * size[0], d.y * size[1], d.z * size[2])) * rnd.uniform(.78, 1.0))
    mb.add(g_hull(points), T(center) @ Rz(rnd.uniform(0, TAU)), z0=0, z1=center.z + size[2], face_color=fc, jitter=.05)


def strata_fc(rnd, cols=("terracotta", "ochre", "sand"), band=.45):
    phase = rnd.uniform(0, 3)

    def fc(face, t):
        z = face.calc_center_median().z
        i = int((z + phase) / band) % len(cols)
        p = PAL[cols[i]]
        return lerp(p[0], p[1], .35 + .45 * clamp01(face.normal.z * .5 + .5))
    return fc


def rib_fc(pal, n=12):
    def fc(face, t):
        a = math.atan2(face.normal.y, face.normal.x)
        k = int(round((a + math.pi) / TAU * n)) % 2
        return lerp(PAL[pal][0], PAL[pal][1], .25 + .5 * t + .2 * k)
    return fc


def g_box():
    v = [(x, y, z) for z in (-.5, .5) for y in (-.5, .5) for x in (-.5, .5)]
    f = [(0, 2, 3, 1), (4, 5, 7, 6), (0, 1, 5, 4), (2, 6, 7, 3), (0, 4, 6, 2), (1, 3, 7, 5)]
    return v, f


def g_disk(rnd, r, n=14, gap=0.0, jit=.08):
    """Plaski dysk (wachlarz) w z=0; gap>0 = wyciecie (lilia)."""
    verts = [(0, 0, 0)]
    a_end = TAU - gap
    for i in range(n + 1):
        a = gap / 2 + a_end * i / n
        rr = r * (1 + rnd.uniform(-jit, jit))
        verts.append((rr * math.cos(a), rr * math.sin(a), 0))
    faces = [(0, i, i + 1) for i in range(1, n + 1)]
    return verts, faces


# ---------------------------------------------------------------- nowe elementy

def log(mb, rnd, length, r, pal="bark", moss=True, crooked=.08, stubs=2):
    pts, radii = [], []
    n = 6
    for i in range(n + 1):
        t = i / n
        pts.append(V((-length / 2 + length * t, rnd.uniform(-crooked, crooked) * length, r * .85 + rnd.uniform(-.03, .03))))
        radii.append(r * (1.08 - .2 * t))
    p_bark, p_moss = PAL[pal], PAL["moss" if moss is True else (moss or "moss")]
    ax = (pts[-1] - pts[0]).normalized()

    def fc(face, t):
        if abs(face.normal.dot(ax)) > .9:             # sloje na koncach
            return lerp((.55, .40, .26), (.78, .62, .42), t)
        if moss and face.normal.z > .55:
            return lerp(p_moss[0], p_moss[1], .5 + .5 * face.normal.z)
        return lerp(p_bark[0], p_bark[1], .3 + .5 * t)
    verts, faces = g_tube(pts, radii, 7)
    faces.append(tuple(reversed(range(7))))
    mb.add((verts, faces), z0=0, z1=2 * r, face_color=fc)
    for _ in range(stubs):
        i = rnd.randint(1, n - 1)
        a = rnd.uniform(.3, 2.8) * rnd.choice((-1, 1))
        d = V((rnd.uniform(-.3, .3), math.sin(a), abs(math.cos(a)) + .3))
        st.branch(mb, rnd, pts[i], d, r * rnd.uniform(1.2, 2.2), r * .3, pal, 2, root_r=radii[i])


def stump(mb, rnd, r, h, pal="bark", moss=True):
    tr = st.trunk(mb, rnd, h, r, pal, lean=.02, segs=2, sides=8, top_r=.92)
    p_moss = PAL["moss"]

    def top_fc(face, t):
        if face.normal.z > .9:
            return lerp((.55, .40, .26), (.80, .64, .44), t)
        return lerp(PAL[pal][0], PAL[pal][1], t)
    mb.add(g_cyl(8, r * .92, r * .92, .01, z0=h - .005), z0=0, z1=h, face_color=top_fc)
    for i in range(5):
        a = TAU * i / 5 + rnd.uniform(-.3, .3)
        st.branch(mb, rnd, V((0, 0, h * .3)), V((math.cos(a), math.sin(a), -.6)), r * 1.6, r * .45, pal, 2, root_r=r)
    if moss:
        for _ in range(3):
            a = rnd.uniform(0, TAU)
            st.rock(mb, rnd, V((math.cos(a) * r, math.sin(a) * r, h * .5)), (r * .45, r * .35, h * .35), "moss", moss=False, pts=8)


def saguaro(mb, rnd, h, arms):
    fc = rib_fc("cactus", 14)
    r = .28
    pts = [V((0, 0, -.1 + (h + .1) * i / 5)) for i in range(6)]
    mb.add(g_tube(pts, [r] * 6, 14, cap=False), z0=0, z1=h, face_color=fc)
    mb.add(g_ico(rnd, r, 1, .02), T((0, 0, h)), z0=0, z1=h + r, face_color=fc)
    a0 = rnd.uniform(0, TAU)
    for k in range(arms):
        a = a0 + TAU * k / max(arms, 1) + rnd.uniform(-.4, .4)
        out = V((math.cos(a), math.sin(a), 0))
        z0 = h * rnd.uniform(.35, .6)
        ah = h * rnd.uniform(.22, .38)
        p0 = V((0, 0, z0))
        p1 = p0 + out * .45 + V((0, 0, .05))
        p2 = p1 + out * .18 + V((0, 0, .3))
        p3 = p2 + V((0, 0, ah))
        ar = r * .7
        mb.add(g_tube([p0, p1, p2, p3], [ar] * 4, 12, cap=False), z0=0, z1=h, face_color=fc)
        mb.add(g_ico(rnd, ar, 1, .02), T(p3), z0=0, z1=h, face_color=fc)


def prickly_pear(mb, rnd, depth=2):
    fc = lambda face, t: lerp(PAL["cactus"][0], PAL["cactus"][1], .35 + .65 * t)

    def pad(center, yaw, tilt, size, d):
        R = Rz(yaw) @ Matrix.Rotation(tilt, 4, 'Y')
        mb.add(g_ico(rnd, .5, 1, .06), T(center) @ R @ S((size, size * .22, size * 1.2)), z0=0, z1=1.6, face_color=fc)
        R3 = R.to_3x3()
        up, side = R3 @ V((0, 0, 1)), R3 @ V((1, 0, 0))
        top = center + up * size * .6
        if d == 0:
            if rnd.random() < .7:
                mb.add(g_ico(rnd, .06, 0, .1), T(top), pal=PAL["flower_pink"], z0=0, z1=2)
            return
        for s in (-1, 1):
            ns = size * rnd.uniform(.7, .85)
            nt = tilt + s * rnd.uniform(.3, .7)
            dirn = (Rz(yaw) @ Matrix.Rotation(nt, 4, 'Y')).to_3x3() @ V((0, 0, 1))
            pad(top + side * s * size * .22 + dirn * ns * .5, yaw + rnd.uniform(-.5, .5), nt, ns, d - 1)
    pad(V((0, 0, .38)), rnd.uniform(0, TAU), rnd.uniform(-.15, .15), .6, depth)


def barrel(mb, rnd, n):
    fc = rib_fc("cactus", 12)
    for i in range(n):
        s = rnd.uniform(.25, .38) * (1.3 if i == 0 else 1)
        p = V((0, 0, 0)) if i == 0 else V((rnd.uniform(-.5, .5), rnd.uniform(-.5, .5), 0))
        mb.add(g_ico(rnd, .5, 2, .02), T(p + V((0, 0, s * .8))) @ S((s * 2, s * 2, s * 2.3)), z0=0, z1=1, face_color=fc)
        if rnd.random() < .7:
            st.flower_head(mb, rnd, p + V((0, 0, s * 1.95)), .07, "flower_pink")


def tumbleweed(mb, rnd, r=.45):
    c = V((0, 0, r * .92))
    for _ in range(16):
        n = st.rnd_dir(rnd)
        x = n.orthogonal().normalized()
        y = n.cross(x)
        a0 = rnd.uniform(0, TAU)
        rr = r * rnd.uniform(.7, 1.0)
        pts = [c + (x * math.cos(a0 + 3.8 * i / 9) + y * math.sin(a0 + 3.8 * i / 9)) * rr for i in range(10)]
        mb.add(g_tube(pts, [.014] * 10, 4, cap=False), pal=PAL["drybrush"], z0=0, z1=2 * r)


def arch(mb, rnd, R, fc, chunk=1.0, depth=1.0):
    n = 11
    for i in range(n):
        a = math.pi * i / (n - 1)
        p = V((R * math.cos(a), 0, R * math.sin(a) * 1.15))
        s = chunk * (1.25 - .45 * math.sin(a))
        rock2(mb, rnd, p, (s * .8, s * depth, s * .75), fc)


def ribcage(mb, rnd, scale=1.0, pal="bone"):
    """Szkielet olbrzyma lezacy na boku: kregoslup przy ziemi, zebra wychodza z ziemi i lukiem schodza sie nad nim."""
    s = scale
    spine = [V((-2.8 * s + 5.6 * s * i / 8, 0, .15 * s + .1 * s * math.sin(i / 8 * math.pi))) for i in range(9)]
    mb.add(g_tube(spine, [.16 * s] * 9, 6), pal=PAL[pal], z0=0, z1=2 * s)
    for i in range(7):
        x = -2.2 * s + i * .72 * s
        R = s * (.9 + .6 * math.sin((i + .5) / 7 * math.pi))
        for side in (-1, 1):
            pts = []
            for k in range(8):
                t = k / 7
                a = t * math.pi * .46                 # od ziemi (na boku) w gore i do srodka
                pts.append(V((x + .15 * s * t, side * R * math.cos(a) * (1 - .25 * t), -.1 * s + R * 1.5 * math.sin(a))))
            mb.add(g_tube(pts, [.12 * s * (1 - .6 * k / 7) for k in range(8)], 5), pal=PAL[pal], z0=0, z1=2 * s)
    rock2(mb, rnd, V((3.3 * s, 0, .45 * s)), (.8 * s, .6 * s, .45 * s),
          lambda f, t: lerp(PAL[pal][0], PAL[pal][1], .4 + .6 * t), 12)
    for side in (-1, 1):     # rogi / szczeka
        pts = [V((3.6 * s + .3 * s * k, side * (.35 + .25 * k) * s, (.6 + .35 * k * k) * s)) for k in range(4)]
        mb.add(g_tube(pts, [.1 * s * (1 - k / 4) + .01 for k in range(4)], 5), pal=PAL[pal], z0=0, z1=2 * s)


def palm(mb, rnd, h, lean=.25):
    lean_dir = V((math.cos(rnd.uniform(0, TAU)), math.sin(rnd.uniform(0, TAU)), 0)).normalized()
    pts = [V((0, 0, -.1 + (h + .1) * i / 9)) + lean_dir * (lean * h) * (i / 9) ** 2 for i in range(10)]

    def fc(face, t):
        z = face.calc_center_median().z
        k = int(z * 3.2) % 2
        return lerp(PAL["palmbark"][0], PAL["palmbark"][1], .3 + .4 * t + .25 * k)
    mb.add(g_tube(pts, [.2 - .06 * i / 9 for i in range(10)], 7), z0=0, z1=h, face_color=fc)
    top = pts[-1]
    n = rnd.randint(7, 9)
    a0 = rnd.uniform(0, TAU)
    for k in range(n):
        a = a0 + TAU * k / n + rnd.uniform(-.2, .2)
        d = V((math.cos(a), math.sin(a), rnd.uniform(.05, .45)))
        mb.add(g_frond(rnd.uniform(1.9, 2.4), .45, .45, 8), T(top) @ frame(d), pal=PAL["palm"], z0=h - 1, z1=h + .8, jitter=.1)
    for k in range(3):
        a = a0 + TAU * k / 3 + .5
        mb.add(g_ico(rnd, .13, 1, .05), T(top + V((math.cos(a) * .18, math.sin(a) * .18, -.15))), pal=PAL["palmbark"], z0=0, z1=h)


def shell(mb, rnd, p, size, yaw):
    pts, radii = [], []
    for i in range(14):
        t = i / 13
        a = t * TAU * 1.5
        rad = size * (.08 + .5 * t)
        pts.append(V((math.cos(a) * rad, math.sin(a) * rad, size * .9 * (1 - t))))
        radii.append(size * (.03 + .32 * t))
    M = T(p) @ Rz(yaw) @ Matrix.Rotation(math.radians(75), 4, 'X')
    mb.add(g_tube(pts, radii, 6), M, pal=PAL["shell"], z0=p.z - size, z1=p.z + size)


def starfish(mb, rnd, p, size, pal="starfish"):
    a0 = rnd.uniform(0, TAU)
    for i in range(5):
        a = a0 + TAU * i / 5
        d = V((math.cos(a), math.sin(a), .05))
        mb.add(g_leaf(size, size * .45, size * .1), T(p) @ frame(d), pal=PAL[pal], z0=p.z, z1=p.z + .1)
    mb.add(g_ico(rnd, size * .25, 0, .05), T(p) @ S((1, 1, .5)), pal=PAL[pal], z0=p.z, z1=p.z + .1)


def coral(mb, rnd, pal, size=1.0):
    st.rock(mb, rnd, V((0, 0, .08 * size)), (.3 * size, .28 * size, .15 * size), "coastrock", moss=False, pts=10)

    def grow(p, d, L, r, depth):
        br = st.branch(mb, rnd, p, d, L, r, pal, 3, root_r=r * 1.2)
        end = br.pts[-1]
        mb.add(g_ico(rnd, r * .9, 0, .05), T(end), pal=PAL[pal], z0=0, z1=size * 1.5)
        if depth:
            for _ in range(2):
                nd = (d + V((rnd.uniform(-.8, .8), rnd.uniform(-.8, .8), .4))).normalized()
                grow(end, nd, L * .7, r * .7, depth - 1)
    for _ in range(rnd.randint(3, 4)):
        d = V((rnd.uniform(-.6, .6), rnd.uniform(-.6, .6), 1)).normalized()
        grow(V((0, 0, .15 * size)), d, .45 * size, .07 * size, 2)


def wreck(mb, rnd):
    L, W, H = 7.0, 1.5, 1.7
    keel = [V((-L / 2 + L * i / 8, 0, .05 + .5 * abs(i / 8 - .5) ** 2 * 4)) for i in range(9)]
    mb.add(g_tube(keel, [.15] * 9, 6), pal=PAL["wood"], z0=0, z1=H)
    for i in range(9):
        t = i / 8
        x = -L / 2 + L * t
        w = W * (1 - (2 * t - 1) ** 4 * .7)
        for side in (-1, 1):
            if rnd.random() < .25:
                continue
            k_max = 6 if rnd.random() > .3 else rnd.randint(3, 5)
            pts = [V((x, side * w * math.sin(k / 6 * math.pi / 2), .05 + H * (1 - math.cos(k / 6 * math.pi / 2)))) for k in range(k_max + 1)]
            mb.add(g_tube(pts, [.08] * len(pts), 5), pal=PAL["wood"], z0=0, z1=H)
    for j in range(5):   # deski poszycia (jedna burta)
        z = .3 + j * .28
        x0, x1 = rnd.uniform(-2.8, -1), rnd.uniform(.5, 2.8)
        y = -W * math.sin(min(1, z / H) * math.pi / 2) * .98
        M = T(((x0 + x1) / 2, y, z)) @ Matrix.Rotation(rnd.uniform(-.06, .06), 4, 'Y') @ S((x1 - x0, .06, .24))
        mb.add(g_box(), M, pal=PAL["wood"], z0=0, z1=H)
    mast = [V((.4, 0, 0)), V((.4 + .9, .3, 1.6)), V((.4 + 1.5, .5, 2.6))]
    mb.add(g_tube(mast, [.12, .1, .08], 6), pal=PAL["wood"], z0=0, z1=H)


def g_hex_grid(radius, spacing):
    pts = []
    rows = int(radius / (spacing * .866)) + 1
    for j in range(-rows, rows + 1):
        for i in range(-rows - 1, rows + 2):
            x = (i + .5 * (j % 2)) * spacing
            y = j * spacing * .866
            if x * x + y * y <= radius * radius:
                pts.append(V((x, y, 0)))
    return pts


def basalt(mb, rnd, radius, hmax, hmin=.3, r=.32):
    def fc(face, t):
        if face.normal.z > .9:
            return lerp((.30, .29, .31), (.46, .44, .47), rnd.random())
        return lerp(PAL["basalt"][0], PAL["basalt"][1], .2 + .6 * t)
    for p in g_hex_grid(radius, r * 1.75):
        d = p.length / max(radius, .01)
        h = max(hmin, hmax * (1 - d * .75) * rnd.uniform(.6, 1.05))
        mb.add(g_cyl(6, r * .98, r * .95, h + .1, z0=-.1), T(p) @ Rz(math.pi / 6), z0=0, z1=hmax, face_color=fc)


def lava_cracks(mb, rnd, chance=.35, th=.25):
    remap(mb, lambda f, c: (lerp(PAL["lava"][0], PAL["lava"][1], rnd.random()), 1)
          if f.normal.z < th and rnd.random() < chance else None)
    mb.mats = GLOW_MATS


def lava_pool(mb, rnd, r, rim=True):
    def fc(face, t):
        d = face.calc_center_median().xy.length / r
        return lerp(PAL["lava"][1], PAL["lava"][0], clamp01(d * 1.2)), 1
    mb.add(g_disk(rnd, r, 16, 0, .12), T((0, 0, .03)), z0=0, z1=1, face_color=fc)
    mb.mats = GLOW_MATS
    if rim:
        n = int(6 + r * 5)
        for i in range(n):
            a = TAU * i / n + rnd.uniform(-.2, .2)
            s = rnd.uniform(.2, .35) * (1 + r * .2)
            st.rock(mb, rnd, V((math.cos(a) * r * 1.02, math.sin(a) * r * 1.02, s * .3)), (s, s * .9, s * .6), "basalt", moss=False, pts=9)


def vent(mb, rnd, s=1.0):
    mb.add(g_cyl(8, .55 * s, .18 * s, .7 * s, cap_top=False), pal=PAL["ash"], z0=0, z1=.7 * s)
    mb.add(g_disk(rnd, .17 * s, 8, 0, .05), T((0, 0, .62 * s)), z0=0, z1=1, face_color=lambda f, t: (PAL["lava"][1], 1))
    mb.mats = GLOW_MATS
    for _ in range(3):
        a = rnd.uniform(0, TAU)
        st.rock(mb, rnd, V((math.cos(a) * .6 * s, math.sin(a) * .6 * s, .08)), (.18 * s, .16 * s, .12 * s), "basalt", moss=False, pts=8)


def crater(mb, rnd):
    R = 2.6
    n = 14
    for i in range(n):
        a = TAU * i / n + rnd.uniform(-.1, .1)
        s = rnd.uniform(.9, 1.3)
        rock2(mb, rnd, V((math.cos(a) * R, math.sin(a) * R, s * .55)), (s * .85, s * .75, s * rnd.uniform(.8, 1.2)),
              lambda f, t: lerp(PAL["basalt"][0], PAL["basalt"][1], .3 + .6 * t), 12)
    lava_pool(mb, rnd, R * .78, rim=False)
    lava_cracks(mb, rnd, .2, .1)


def hanging_moss(mb, rnd, center, radius, n, z):
    for _ in range(n):
        a = rnd.uniform(0, TAU)
        rr = radius * math.sqrt(rnd.uniform(.15, 1))
        p = V((center.x + math.cos(a) * rr, center.y + math.sin(a) * rr, z + rnd.uniform(-.2, .2)))
        L = rnd.uniform(.7, 1.6)
        mb.add(g_blade(L, rnd.uniform(.08, .14), rnd.uniform(-.1, .1), 4), T(p) @ Rz(rnd.uniform(0, TAU)) @ Matrix.Rotation(math.pi, 4, 'X'),
               pal=PAL["spanishmoss"], z0=z - 1.6, z1=z, jitter=.1)


def stilt_tree(mb, rnd, h, r, crown):
    base = rnd.uniform(.8, 1.1)
    pts = [V((0, 0, base + (h - base) * i / 5)) + V((rnd.uniform(-.06, .06), rnd.uniform(-.06, .06), 0)) * (i > 0) for i in range(6)]
    radii = [r * (1 - .45 * i / 5) for i in range(6)]
    mb.add(g_tube(pts, radii, 7), pal=PAL["swampbark"], z0=0, z1=h)
    tr = st.Trunk(pts, radii)
    for i in range(rnd.randint(6, 8)):
        a = TAU * i / 7 + rnd.uniform(-.25, .25)
        s, rr = tr.at(base + rnd.uniform(.05, .5))
        st.branch(mb, rnd, s, V((math.cos(a), math.sin(a), -1.1)), base * rnd.uniform(1.4, 1.9), r * .32, "swampbark", 3, root_r=rr)
    st.canopy(mb, rnd, pts[-1] + V((0, 0, crown * .3)), crown, "swampleaf", h - crown, h + crown * 1.3, 4, 22)
    hanging_moss(mb, rnd, pts[-1], crown * 1.1, 22, h - crown * .3)


def cypress(mb, rnd, h):
    tr = st.trunk(mb, rnd, h, .55, "swampbark", lean=.12, segs=6, sides=8, top_r=.35)
    for i in range(7):   # "kolana" cyprysa
        a = rnd.uniform(0, TAU)
        d = rnd.uniform(1.0, 2.2)
        hh = rnd.uniform(.25, .6)
        mb.add(g_cyl(6, .14, .03, hh), T((math.cos(a) * d, math.sin(a) * d, -.05)), pal=PAL["swampbark"], z0=0, z1=1)
    tops = []
    for i in range(3):
        a = TAU * i / 3 + rnd.uniform(-.4, .4)
        s, rr = tr.at(h * rnd.uniform(.55, .8))
        br = st.branch(mb, rnd, s, V((math.cos(a), math.sin(a), .55)), rnd.uniform(1.8, 2.6), .18, "swampbark", 3, root_r=rr)
        tops.append(br.top)
    tops.append(tr.top)
    for p in tops:
        for c in range(2):
            cc = p + V((rnd.uniform(-.5, .5), rnd.uniform(-.5, .5), .3))
            rr = rnd.uniform(1.0, 1.4)
            mb.add(g_ico(rnd, .5, 1, .15), T(cc) @ S((rr * 2.2, rr * 2.2, rr * .9)), pal=PAL["swampleaf"], z0=h - 1, z1=h + 2)
        hanging_moss(mb, rnd, p, 1.3, 12, p.z - .1)


def reeds(mb, rnd, n_cat):
    st.grass_clump(mb, rnd, V((0, 0, 0)), 14, 1.3, .3, "reed", bend=.12, w=.05)
    for _ in range(n_cat):
        a = rnd.uniform(0, TAU); rr = rnd.uniform(0, .25)
        base = V((math.cos(a) * rr, math.sin(a) * rr, 0))
        h = rnd.uniform(1.2, 1.6)
        top = base + V((rnd.uniform(-.08, .08), rnd.uniform(-.08, .08), h))
        mb.add(g_tube([base, top], [.015, .01], 4), pal=PAL["reed"], z0=0, z1=h)
        mb.add(g_cyl(6, .05, .05, .28, z0=-.32), T(top), pal=PAL["cattail"], z0=h - .4, z1=h)


def lilies(mb, rnd, n, flowers, fpal):
    for i in range(n):
        a = rnd.uniform(0, TAU); rr = 0 if i == 0 else rnd.uniform(.35, .9)
        r = rnd.uniform(.22, .42)
        p = V((math.cos(a) * rr, math.sin(a) * rr, .02 + i * .002))
        mb.add(g_disk(rnd, r, 12, .5, .04), T(p) @ Rz(rnd.uniform(0, TAU)), z0=0, z1=1,
               face_color=lambda f, t: lerp(PAL["lily"][0], PAL["lily"][1], rnd.uniform(.4, 1)))
        if i < flowers:
            st.flower_head(mb, rnd, p + V((0, 0, .06)), .1, fpal)


# ---------------------------------------------------------------- definicje biomow: (kategoria, nazwa, builder(v, rnd, mb))

def reuse(type_name, post=None, scale=None, vmap=None):
    def b(v, rnd, mb):
        src = st.build(type_name, vmap[v] if vmap else v)
        if scale:
            scale_mb(src, scale)
        if post:
            post(src, rnd)
        return src
    return b


def B(fn, mats=None):
    def b(v, rnd, mb):
        fn(v, rnd, mb)
        if mats:
            mb.mats = mats
        return mb
    return b


def snowy(src, rnd):
    snow_pass(src, .45)


def icy(src, rnd):
    remap(src, lambda f, c: (lerp(PAL["ice"][0], PAL["ice"][1], clamp01(lum(c) * 1.4)), 0) if f.material_index == 1 else
          (lerp(PAL["icerock"][0], PAL["icerock"][1], clamp01(lum(c) * 3)), 0))
    snow_pass(src, .6)


def obsidian(src, rnd):
    remap(src, lambda f, c: (lerp(PAL["obsidian"][0], PAL["obsidian"][1], clamp01(lum(c) * 1.2)), 0) if f.material_index == 1 else
          (lerp(PAL["basalt"][0], PAL["basalt"][1], clamp01(lum(c) * 3)), 0))


def burnt(src, rnd):
    remap(src, lambda f, c: ((lerp(PAL["lava"][0], PAL["lava"][1], rnd.random()), 1) if rnd.random() < .07 else
                             (lerp(PAL["charcoal"][0], PAL["charcoal"][1], clamp01(lum(c) * 2.5)), 0)))
    src.mats = GLOW_MATS


def wet_dead(src, rnd):
    remap(src, lambda f, c: (lerp(PAL["moss_wet"][0], PAL["moss_wet"][1], rnd.random()), None) if f.normal.z > .5 else
          (lerp(PAL["swampbark"][0], PAL["swampbark"][1], clamp01(lum(c) * 2.5)), None))


def purple_glow(src, rnd):
    remap(src, lambda f, c: (lerp(PAL["glow_purple"][0], PAL["glow_purple"][1], clamp01(lum(c) * 1.3)), None) if f.material_index == 1 else None)


def frost_bush(src, rnd):
    remap(src, lambda f, c: (lerp(PAL["frost"][0], PAL["frost"][1], clamp01(lum(c) * 2.5)), None))
    snow_pass(src, .3)


def charcoal_rubble(src, rnd):
    burnt(src, rnd)


BIOMES = {
    "Las": [
        ("Landmarki", "Wielkie drzewo", reuse("LandmarkTree")),
        ("Landmarki", "Menhir", reuse("Menhir")),
        ("Duze", "Drzewo", reuse("Tree")),
        ("Duze", "Martwe drzewo", reuse("DeadTree")),
        ("Srednie", "Krzak", reuse("Bush")),
        ("Srednie", "Glaz", reuse("Boulder")),
        ("Srednie", "Pniak", B(lambda v, r, mb: stump(mb, r, r.uniform(.32, .45), r.uniform(.4, .8), moss=v % 2 == 0))),
        ("Srednie", "Kloda", B(lambda v, r, mb: log(mb, r, r.uniform(2.5, 3.8), r.uniform(.25, .35), moss=v != 3))),
        ("Drobne", "Trawa", reuse("Grass")),
        ("Drobne", "Paproc", reuse("Fern")),
        ("Drobne", "Kwiaty", reuse("Flowers")),
        ("Drobne", "Grzyby", reuse("Mushroom")),
        ("Drobne", "Kamyki", reuse("Pebble")),
    ],
    "Pustynia": [
        ("Landmarki", "Luk skalny", B(lambda v, r, mb: arch(mb, r, r.uniform(2.6, 3.4), strata_fc(r), 1.1))),
        ("Landmarki", "Wielki kaktus", B(lambda v, r, mb: saguaro(mb, r, r.uniform(6.5, 8), 3 + v % 2))),
        ("Duze", "Kaktus", B(lambda v, r, mb: saguaro(mb, r, r.uniform(2.8, 4.2), (0, 1, 2, 2)[v]))),
        ("Duze", "Slup skalny", B(lambda v, r, mb: (rock2(mb, r, V((0, 0, 1.5)), (.6, .55, 1.7), strata_fc(r)),
                                                      rock2(mb, r, V((0, 0, 3.3)), (.95, .85, .35), strata_fc(r))))),
        ("Duze", "Palma", B(lambda v, r, mb: palm(mb, r, r.uniform(4, 5.5), r.uniform(.15, .35)))),
        ("Srednie", "Opuncja", B(lambda v, r, mb: prickly_pear(mb, r, 2))),
        ("Srednie", "Skala warstwowa", B(lambda v, r, mb: rock2(mb, r, V((0, 0, .6)), (1.4, 1.1, .9), strata_fc(r), 16))),
        ("Srednie", "Szkielet", B(lambda v, r, mb: ribcage(mb, r, r.uniform(.45, .6)))),
        ("Drobne", "Kaktus kulisty", B(lambda v, r, mb: barrel(mb, r, 1 + v % 3))),
        ("Drobne", "Toczace zielsko", B(lambda v, r, mb: tumbleweed(mb, r, r.uniform(.35, .55)))),
        ("Drobne", "Sucha trawa", B(lambda v, r, mb: st.grass_clump(mb, r, V((0, 0, 0)), 18, .55, .3, "dry", .3, .06))),
        ("Drobne", "Kamyki", B(lambda v, r, mb: [st.rock(mb, r, V((r.uniform(-.4, .4), r.uniform(-.4, .4), .06)), (.18, .15, .1), "terracotta", moss=False, pts=9) for _ in range(3)])),
    ],
    "Wybrzeze": [
        ("Landmarki", "Wrak", B(lambda v, r, mb: wreck(mb, r))),
        ("Landmarki", "Luk morski", B(lambda v, r, mb: arch(mb, r, r.uniform(2.6, 3.2),
                                                           lambda f, t: lerp(PAL["seaweed"][0], PAL["seaweed"][1], .5) if f.normal.z > .6 and f.calc_center_median().z < 1.2
                                                           else lerp(PAL["coastrock"][0], PAL["coastrock"][1], .3 + .6 * t), 1.0, .9))),
        ("Duze", "Palma", B(lambda v, r, mb: palm(mb, r, r.uniform(4.5, 6), r.uniform(.3, .5)))),
        ("Duze", "Skala przybrzezna", B(lambda v, r, mb: [rock2(mb, r, V((x, y, z)), s, lambda f, t: lerp(PAL["seaweed"][0], PAL["seaweed"][1], .6)
                                                                if f.normal.z > .65 else lerp(PAL["coastrock"][0], PAL["coastrock"][1], .3 + .6 * t), 14)
                                                          for x, y, z, s in ((0, 0, .7, (1.3, 1.1, 1.0)), (1.2, .4, .3, (.6, .5, .45)))])),
        ("Srednie", "Wyrzucone drewno", B(lambda v, r, mb: log(mb, r, r.uniform(2, 3.2), r.uniform(.15, .22), "driftwood", moss=False, crooked=.15, stubs=3))),
        ("Srednie", "Koral", B(lambda v, r, mb: coral(mb, r, ("coral_pink", "coral_orange", "coral_purple", "coral_yellow")[v]))),
        ("Drobne", "Muszle", B(lambda v, r, mb: (shell(mb, r, V((0, 0, .08)), .25, r.uniform(0, TAU)),
                                                 shell(mb, r, V((.35, .2, .05)), .16, r.uniform(0, TAU)),
                                                 starfish(mb, r, V((-.3, .25, .02)), .2)))),
        ("Drobne", "Rozgwiazda", B(lambda v, r, mb: starfish(mb, r, V((0, 0, .02)), r.uniform(.18, .28), ("starfish", "coral_pink", "coral_purple", "coral_yellow")[v]))),
        ("Drobne", "Wodorosty", B(lambda v, r, mb: st.grass_clump(mb, r, V((0, 0, 0)), 7, 1.1, .2, "seaweed", .35, .14))),
        ("Drobne", "Trawa wydmowa", B(lambda v, r, mb: st.grass_clump(mb, r, V((0, 0, 0)), 20, .7, .3, "dry", .15, .04))),
        ("Drobne", "Otoczaki", B(lambda v, r, mb: [st.rock(mb, r, V((r.uniform(-.4, .4), r.uniform(-.4, .4), .05)), (.16, .13, .07), "coastrock", moss=False, pts=16) for _ in range(4)])),
    ],
    "Lod": [
        ("Landmarki", "Lodowa iglica", reuse("LandmarkCrystals", icy, 1.6)),
        ("Landmarki", "Zamarzniety szkielet", B(lambda v, r, mb: (ribcage(mb, r, 1.5), snow_pass(mb, .4)))),
        ("Duze", "Osniezona sosna", B(lambda v, r, mb: (st.pine(mb, r, r.uniform(5, 7), .17, r.uniform(1.6, 2.1)), snow_pass(mb, .8, True, .5)))),
        ("Duze", "Bryla lodu", B(lambda v, r, mb: (rock2(mb, r, V((0, 0, .9)), (.9, .8, 1.2), lambda f, t: lerp(PAL["ice"][0], PAL["ice"][1], .2 + .8 * t), 9),
                                                    rock2(mb, r, V((.9, .3, .4)), (.45, .4, .55), lambda f, t: lerp(PAL["ice"][0], PAL["ice"][1], .2 + .8 * t), 8),
                                                    snow_pass(mb, .75)))),
        ("Srednie", "Skala ze sniegiem", B(lambda v, r, mb: (st.rock(mb, r, V((0, 0, .5)), (1.0, .85, .7), "icerock", moss=False), snow_pass(mb, .5)))),
        ("Srednie", "Krysztaly lodu", reuse("CrystalSmall", icy)),
        ("Srednie", "Zaspa", B(lambda v, r, mb: mb.add(g_ico(r, .5, 2, .05), T((0, 0, 0)) @ Rz(r.uniform(0, TAU)) @ S((r.uniform(1.8, 2.6), r.uniform(1.2, 1.6), r.uniform(.6, .9))),
                                                       pal=PAL["snow"], z0=-.2, z1=.45))),
        ("Drobne", "Lodowe kolce", B(lambda v, r, mb: [mb.add(g_tube([V((x, y, -.1)), V((x + r.uniform(-.1, .1), y + r.uniform(-.1, .1), h))], [w, .005], 5),
                                                              pal=PAL["ice"], z0=0, z1=1) for x, y, h, w in
                                                       [(r.uniform(-.3, .3), r.uniform(-.3, .3), r.uniform(.4, 1.0), r.uniform(.08, .14)) for _ in range(5)]])),
        ("Drobne", "Oszroniony krzak", reuse("DeadTree", frost_bush, .32)),
        ("Drobne", "Kamyki", B(lambda v, r, mb: ([st.rock(mb, r, V((r.uniform(-.4, .4), r.uniform(-.4, .4), .06)), (.17, .14, .09), "icerock", moss=False, pts=9) for _ in range(3)],
                                                 snow_pass(mb, .7)))),
    ],
    "Wulkan": [
        ("Landmarki", "Krater", B(lambda v, r, mb: crater(mb, r), GLOW_MATS)),
        ("Landmarki", "Bazaltowa iglica", B(lambda v, r, mb: basalt(mb, r, 1.6, r.uniform(6, 8), 1.5))),
        ("Duze", "Kolumny bazaltowe", B(lambda v, r, mb: basalt(mb, r, r.uniform(.9, 1.3), r.uniform(2, 3.2)))),
        ("Duze", "Skala z lawa", B(lambda v, r, mb: (st.rock(mb, r, V((0, 0, .8)), (1.3, 1.1, 1.0), "basalt", moss=False),
                                                     st.rock(mb, r, V((1.0, .5, .3)), (.55, .5, .4), "basalt", moss=False),
                                                     lava_cracks(mb, r, .3, .2)))),
        ("Srednie", "Spalone drzewo", reuse("DeadTree", burnt)),
        ("Srednie", "Obsydian", reuse("CrystalSmall", obsidian)),
        ("Srednie", "Komin", B(lambda v, r, mb: vent(mb, r, r.uniform(.9, 1.4)))),
        ("Drobne", "Kaluza lawy", B(lambda v, r, mb: lava_pool(mb, r, r.uniform(.6, 1.1)))),
        ("Drobne", "Zuzel", reuse("Rubble", burnt)),
        ("Drobne", "Kamyki", B(lambda v, r, mb: [st.rock(mb, r, V((r.uniform(-.4, .4), r.uniform(-.4, .4), .06)), (.17, .14, .09), "basalt", moss=False, pts=9) for _ in range(3)])),
    ],
    "Bagno": [
        ("Landmarki", "Stary cyprys", B(lambda v, r, mb: cypress(mb, r, r.uniform(6, 7.5)))),
        ("Landmarki", "Powalony olbrzym", B(lambda v, r, mb: (log(mb, r, r.uniform(7, 9), r.uniform(.6, .8), "swampbark", "moss_wet", .06, 4),
                                                              [mb.add(g_cap(c, c * .8), T((x, r.uniform(-.2, .2), 1.35)), z0=1, z1=2, mat=1,
                                                                      face_color=lambda f, t: (lerp(PAL["glow_purple"][0], PAL["glow_purple"][1], t), 1))
                                                               for x, c in [(r.uniform(-3, 3), r.uniform(.15, .3)) for _ in range(6)]]), GLOW_MATS)),
        ("Duze", "Drzewo na korzeniach", B(lambda v, r, mb: stilt_tree(mb, r, r.uniform(4, 5.2), .22, r.uniform(1.3, 1.7)))),
        ("Duze", "Martwe drzewo", reuse("DeadTree", wet_dead)),
        ("Srednie", "Omszala kloda", B(lambda v, r, mb: log(mb, r, r.uniform(2.5, 3.5), r.uniform(.28, .38), "swampbark", "moss_wet"))),
        ("Srednie", "Swiecace grzyby", reuse("Mushroom", purple_glow, vmap=(2, 3, 2, 3))),
        ("Srednie", "Pniak", B(lambda v, r, mb: stump(mb, r, r.uniform(.35, .5), r.uniform(.5, 1.0), "swampbark", True))),
        ("Drobne", "Trzciny", B(lambda v, r, mb: reeds(mb, r, 3 + v))),
        ("Drobne", "Lilie wodne", B(lambda v, r, mb: lilies(mb, r, 4 + v, v % 3, ("flower_pink", "flower_wht", "flower_pink", "flower_wht")[v]))),
        ("Drobne", "Turzyca", B(lambda v, r, mb: st.grass_clump(mb, r, V((0, 0, 0)), 20, .6, .3, "sedge", .25, .05))),
        ("Drobne", "Paproc", reuse("Fern")),
    ],
}
CATS = ["Landmarki", "Duze", "Srednie", "Drobne"]


def build_biome_asset(biome, idx, builder, v):
    seed = sum(map(ord, biome)) * 131 + idx * 977 + v * 104729 + 23
    rnd = random.Random(seed)
    mb = MB(seed)
    res = builder(v, rnd, mb)
    if res is not mb:
        mb.bm.free()
    return res


def main():
    st.remove_collection(ROOT)
    root = bpy.data.collections.new(ROOT)
    bpy.context.scene.collection.children.link(root)
    x0, total = 40.0, 0
    y_start = -20.0
    for biome, items in BIOMES.items():
        bcol = bpy.data.collections.new(f"Biome_{biome}")
        root.children.link(bcol)
        subs = {}
        for c in CATS:
            subs[c] = bpy.data.collections.new(f"{biome}_{c}")
            bcol.children.link(subs[c])
        rows = []
        for idx, (cat, name, builder) in enumerate(items):
            objs = []
            for v in range(4):
                mb = build_biome_asset(biome, idx, builder, v)
                objs.append(st.make_object(f"{biome}_{name.replace(' ', '_')}_v{v}", mb, subs[cat]))
            rows.append(objs)
        bpy.context.view_layer.update()
        # kolumna biomu: wiersze wzdluz -Y, warianty wzdluz X
        y, right = y_start, x0
        for objs in rows:
            depth = max(max(o.dimensions.y for o in objs), 1.0)
            y -= depth * .5
            x = x0
            for o in objs:
                w = max(o.dimensions.x, 1.0)
                x += w * .5
                o.location = (x, y, 0)
                x += w * .5 + 1.5
                total += 1
            right = max(right, x)
            y -= depth * .5 + 2.0
        x0 = right + 6.0
    print(f"[LD57] Biomy: {total} obiektow w kolekcji {ROOT}")


if __name__ == "__main__":
    main()
