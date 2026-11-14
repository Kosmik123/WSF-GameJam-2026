"""
Malowanie SM_DragonHead vertex colorem ("Col", Face Corner): skalny smok w JEDNYM kolorze kamienia, tylko odcienie.
Jasnosc liczona z geometrii:
  + jasniej: grzbiet / gorne sciany, wystajace krawedzie (zeby, rogi, luski)
  - ciemniej: spod, zaglebienia i szczeliny, wnetrze pyska i gardla
  + lekki losowy rozrzut per sciana (fasetowy styl jak reszta assetow)
Material: "LD57 Stylized".
Uruchom w Blenderze z otwartym plikiem (Scripting -> Run) albo: blender -b Head.blend --python dragon_paint.py
"""
import math
import random
import bpy
from mathutils import Vector

OBJ = "SM_DragonHead"
SNOUT_DIR = Vector((0, 1, 0))      # pysk w +Y swiata (kark/szyja w -Y)

# jeden kolor kamienia (sRGB) – ta sama skala co skaly LD57; jasnosc 0..1 wybiera odcien
STONE_DARK = (.20, .19, .22)
STONE_MID = (.52, .50, .55)
STONE_LIGHT = (.82, .79, .83)

# sila poszczegolnych wplywow na jasnosc
UP_AMOUNT = .22        # grzbiet jasniejszy, spod ciemniejszy
RIDGE_AMOUNT = .22     # wystajace krawedzie jasniejsze
CAVITY_AMOUNT = .38    # zaglebienia ciemniejsze
INTERIOR_DARK = .32    # pysk / gardlo ciemniejsze
FACE_JITTER = .05      # losowy rozrzut per sciana


def lin(c):
    return tuple(x / 12.92 if x <= .04045 else ((x + .055) / 1.055) ** 2.4 for x in c)


def lerp(a, b, t):
    return tuple(a[i] + (b[i] - a[i]) * t for i in range(3))


def clamp01(x):
    return 0.0 if x < 0 else 1.0 if x > 1 else x


def sstep(e0, e1, x):
    t = clamp01((x - e0) / (e1 - e0))
    return t * t * (3 - 2 * t)


def shade(v):
    """Jasnosc 0..1 -> odcien jednego koloru kamienia."""
    v = clamp01(v)
    return lerp(STONE_DARK, STONE_MID, v * 2) if v < .5 else lerp(STONE_MID, STONE_LIGHT, (v - .5) * 2)


def _h(i, j, k, seed=7):
    n = (i * 73856093) ^ (j * 19349663) ^ (k * 83492791) ^ (seed * 2654435761)
    return ((n * 2246822519) & 0xffffffff) / 0xffffffff


def vnoise(p, seed=7):
    i, j, k = math.floor(p.x), math.floor(p.y), math.floor(p.z)
    fx, fy, fz = p.x - i, p.y - j, p.z - k
    fx, fy, fz = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy), fz * fz * (3 - 2 * fz)
    v = 0.0
    for dx in (0, 1):
        for dy in (0, 1):
            for dz in (0, 1):
                w = (fx if dx else 1 - fx) * (fy if dy else 1 - fy) * (fz if dz else 1 - fz)
                v += w * _h(i + dx, j + dy, k + dz, seed)
    return v


def pct(values, q):
    s = sorted(values)
    return s[min(len(s) - 1, max(0, int(q * (len(s) - 1))))]


def get_mat(name):
    m = bpy.data.materials.get(name)
    if m:
        return m
    m = bpy.data.materials.new(name)
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    col = nt.nodes.new("ShaderNodeVertexColor"); col.layer_name = "Col"
    sh = nt.nodes.new("ShaderNodeBsdfPrincipled"); sh.inputs["Roughness"].default_value = .9
    nt.links.new(col.outputs["Color"], sh.inputs["Base Color"]); nt.links.new(sh.outputs[0], out.inputs["Surface"])
    return m


def main():
    o = bpy.data.objects[OBJ]
    me = o.data
    dg = bpy.context.evaluated_depsgraph_get()
    ev = o.evaluated_get(dg)
    em = ev.to_mesh()
    src = em if (len(em.polygons) == len(me.polygons) and len(em.vertices) == len(me.vertices)) else me
    M = o.matrix_world
    N3 = M.to_3x3().inverted().transposed()

    VP = [M @ v.co for v in src.vertices]
    VN = [(N3 @ v.normal).normalized() for v in src.vertices]
    adj = [[] for _ in VP]
    for e in src.edges:
        a, b = e.vertices
        adj[a].append(b); adj[b].append(a)
    conv = []   # > 0 wklesle, < 0 wypukle
    for i, nb in enumerate(adj):
        ds = [VN[i].dot((VP[j] - VP[i]).normalized()) for j in nb if (VP[j] - VP[i]).length > 1e-9]
        conv.append(sum(ds) / len(ds) if ds else 0.0)
    for _ in range(2):   # wygladzenie, zeby odcienie przechodzily plynnie
        conv = [(conv[i] + sum(conv[j] for j in adj[i])) / (1 + len(adj[i])) for i in range(len(conv))]
    SVN = VN[:]
    for _ in range(3):
        SVN = [(SVN[i] + sum((SVN[j] for j in adj[i]), Vector())).normalized() for i in range(len(SVN))]

    polys = src.polygons
    FC = [M @ p.center for p in polys]
    FN = [(N3 @ p.normal).normalized() for p in polys]
    FCONV = [sum(conv[v] for v in p.vertices) / len(p.vertices) for p in polys]
    FUP = [sum((SVN[v] for v in p.vertices), Vector()).normalized().z for p in polys]

    mn = Vector((min(p.x for p in VP), min(p.y for p in VP), min(p.z for p in VP)))
    mx = Vector((max(p.x for p in VP), max(p.y for p in VP), max(p.z for p in VP)))
    size = mx - mn
    L = max(size)
    cen = (mn + mx) / 2
    along = lambda p: clamp01((p - mn).dot(SNOUT_DIR) / max(size.dot(SNOUT_DIR), 1e-6))
    height = lambda p: clamp01((p.z - mn.z) / max(size.z, 1e-6))

    p_cav = pct(FCONV, .97)
    p_ridge = pct(FCONV, .25)
    p_sharp = pct(FCONV, .04)

    # wnetrze pyska i tunel gardla: sciany zwrocone do osi glowy, zaslepka szyi, tylna sciana gardla
    interior = set()
    for i in range(len(polys)):
        c = FC[i]
        to_axis = Vector((cen.x - c.x, 0, cen.z - c.z))
        if to_axis.length > 1e-6 and FN[i].dot(to_axis.normalized()) > (.35 if along(c) >= .45 else .7):
            interior.add(i)
        elif abs(FN[i].dot(SNOUT_DIR)) > .8 and along(c) < .12:
            interior.add(i)
        elif FN[i].dot(SNOUT_DIR) > .55 and along(c) < .85 and abs(c.x - cen.x) < .32 * size.x and .15 < height(c) < .75:
            interior.add(i)

    rnd = random.Random(11)
    shades = []
    for i in range(len(polys)):
        c, cvx = FC[i], FCONV[i]
        up = .7 * FUP[i] + .3 * (2 * height(c) - 1)
        v = .52 + UP_AMOUNT * up
        v += RIDGE_AMOUNT * sstep(p_ridge, p_sharp, cvx)
        v -= CAVITY_AMOUNT * sstep(0, p_cav, cvx)
        if i in interior:
            v -= INTERIOR_DARK
        v += .06 * (vnoise((c - mn) / L * 4.0) - .5)          # duze, lagodne plamy tonu
        v += rnd.uniform(-FACE_JITTER, FACE_JITTER)          # fasetowy rozrzut
        shades.append(shade(v))

    attr = me.color_attributes.get("Col")
    if attr and (attr.domain != 'CORNER' or attr.data_type != 'FLOAT_COLOR'):
        me.color_attributes.remove(attr); attr = None
    if attr is None:
        attr = me.color_attributes.new("Col", 'FLOAT_COLOR', 'CORNER')
    me.color_attributes.active_color = attr
    try:
        me.color_attributes.render_color_index = me.color_attributes.active_color_index
    except AttributeError:
        pass
    data = attr.data
    for i, p in enumerate(me.polygons):
        cl = (*lin(shades[i]), 1.0)
        for li in p.loop_indices:
            data[li].color = cl
    me.materials.clear()
    me.materials.append(get_mat("LD57 Stylized"))
    me.polygons.foreach_set("material_index", [0] * len(me.polygons))
    me.update()
    ev.to_mesh_clear()
    print(f"[Dragon] kamien: {len(polys)} scian, wnetrze {len(interior)}")


if __name__ == "__main__":
    main()
