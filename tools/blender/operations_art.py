"""Builds the Last Stand and Extraction Run art in the same style as Leviathan Assault: unit sprites and two arena
backdrops. Glow is added afterwards by tools/blender/leviathan_post.py's bloom (see operations_post.py).

Last Stand: raiders (bike, technical, armoured truck, mortar team) racing down an approach road at the base's gate,
and the player's turret (seen from overhead, so the game can turn it). Extraction Run: the player's recovery rover,
Caretaker patrols, sentries and drones (overhead views where the game turns them), supply crates, and the
Caretaker supply yard.

Run headless (from the repo root):
  blender -b --python tools/blender/operations_art.py -- <raw_render_dir> [preview]
  python tools/blender/operations_post.py <raw_render_dir> client/assets/operations
"""
import bpy, math, random, sys, os
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import wl_kit as K
import ops_kit as O
from wl_kit import M, box, cyl, rod, torus, worn, glow
from ops_kit import ball, sprite
from mathutils import Vector

argv = sys.argv[sys.argv.index('--') + 1:]
O.setup(argv[0], len(argv) > 1 and argv[1] == 'preview')
ONLY = set(os.environ.get('NWO_ONLY', '').split(',')) - {''}  # render just these names while iterating
want = lambda name: not ONLY or name in ONLY

# ---------- palettes ----------
RUST = worn('raider_rust', (0.42, 0.2, 0.09), rough=0.75, metal=0.45, wear=0.7, rust=(0.5, 0.22, 0.08), scale=7)
RAIDER_BLACK = worn('raider_black', (0.06, 0.06, 0.06), rough=0.7, metal=0.2, wear=0.4, scale=9)
CLOTH = worn('raider_cloth', (0.85, 0.32, 0.06), rough=0.9, wear=0.2, scale=12)
SKIN = worn('skin', (0.55, 0.38, 0.27), rough=0.8, wear=0.1, scale=20)
HEADLAMP = glow('headlamp', (1.0, 0.62, 0.2), 6)
P_WHITE = worn('player_white', (0.92, 0.92, 0.9), rough=0.5, metal=0.1, wear=0.25, grime=0.2, scale=8)
P_RED = worn('player_red', (0.62, 0.05, 0.04), rough=0.5, metal=0.15, wear=0.3, grime=0.25, scale=8)
P_DARK = worn('player_dark', (0.07, 0.08, 0.09), rough=0.6, metal=0.5, wear=0.3, scale=8)
CYAN = glow('player_cyan', (0.25, 0.85, 1.0), 6)
C_WHITE = worn('caretaker_white', (0.86, 0.88, 0.9), rough=0.38, metal=0.1, wear=0.3, grime=0.25, scale=5)
C_FRAME = worn('caretaker_frame', (0.1, 0.12, 0.13), rough=0.5, metal=0.7, wear=0.6, scale=6)
PINK = glow('caretaker_pink', (1.0, 0.12, 0.62), 6)
GLASS = worn('dark_glass2', (0.03, 0.05, 0.06), rough=0.15, metal=0.5, wear=0.05)
GREEN = glow('beacon_green', (0.25, 1.0, 0.45), 4)
HAZARD = glow('hazard_strip', (1.0, 0.72, 0.08), 4)


def wheel(loc, r=0.22, w=0.12, axis='X'):
    rot = (0, math.pi / 2, 0) if axis == 'X' else (math.pi / 2, 0, 0)
    return [cyl(r, w, loc, M.rubber, rot=rot, verts=18, bevel=0.02), cyl(r * 0.55, w + 0.02, loc, M.steel, rot=rot, verts=12, bevel=0)]


def rider(loc, lean=0.25, cloth=CLOTH):
    x, y, z = loc
    p = [box((0.3, 0.22, 0.42), (x, y, z), RAIDER_BLACK, rot=(-lean, 0, 0), bevel=0.06),
         ball(0.13, (x, y - 0.1, z + 0.32), SKIN, segs=14),
         box((0.3, 0.12, 0.1), (x, y - 0.14, z + 0.26), cloth, bevel=0.03),  # scarf over the face
         box((0.28, 0.06, 0.08), (x, y - 0.2, z + 0.36), GLASS, bevel=0.02)]  # goggles
    return p

# ---------- Last Stand: raiders, all driving towards the camera (-Y) ----------

def raider_bike():
    p = wheel((0, -0.48, 0.22)) + wheel((0, 0.48, 0.24), r=0.24)
    p += [box((0.22, 0.9, 0.2), (0, 0, 0.42), RUST, bevel=0.03), box((0.4, 0.42, 0.26), (0, -0.12, 0.58), RUST, bevel=0.06),
          box((0.24, 0.32, 0.08), (0, 0.18, 0.62), RAIDER_BLACK, bevel=0.03), rod((-0.28, -0.42, 0.74), (0.28, -0.42, 0.74), 0.025, M.steel),
          rod((0, -0.48, 0.24), (0, -0.4, 0.74), 0.03, M.steel), cyl(0.07, 0.06, (0, -0.58, 0.55), HEADLAMP, rot=(math.pi / 2, 0, 0), verts=12, bevel=0),
          rod((0.12, 0.3, 0.3), (0.16, 0.7, 0.42), 0.035, M.steel)]
    p += rider((0, 0.02, 0.88))
    p += [rod((-0.13, 0.0, 0.95), (-0.26, -0.4, 0.76), 0.04, RAIDER_BLACK), rod((0.13, 0.0, 0.95), (0.26, -0.4, 0.76), 0.04, RAIDER_BLACK)]
    return p


def raider_technical():
    p = []
    for x in (-0.55, 0.55):
        for y in (-0.62, 0.62): p += wheel((x, y, 0.3), r=0.3, w=0.2)
    p += [box((1.0, 1.9, 0.3), (0, 0, 0.5), RUST, bevel=0.05), box((0.9, 0.7, 0.35), (0, -0.55, 0.82), RUST, bevel=0.06),
          box((0.8, 0.05, 0.25), (0, -0.92, 0.82), GLASS, rot=(math.radians(-20), 0, 0), bevel=0.02),
          box((1.1, 0.15, 0.25), (0, -1.0, 0.45), M.steel, bevel=0.03)]
    for x in (-0.4, 0.4): p.append(cyl(0.07, 0.05, (x, -1.08, 0.55), HEADLAMP, rot=(math.pi / 2, 0, 0), verts=12, bevel=0))
    for x in (-0.48, 0.48): p.append(rod((x, -0.2, 0.68), (x, 0.4, 1.35), 0.03, M.steel))  # roll bar
    p.append(rod((-0.48, 0.4, 1.35), (0.48, 0.4, 1.35), 0.03, M.steel))
    # the mounted gun on the bed and its gunner
    p += [cyl(0.07, 0.5, (0, 0.5, 0.95), M.steel, verts=10), box((0.2, 0.5, 0.18), (0, 0.32, 1.25), RAIDER_BLACK, bevel=0.03),
          rod((0, 0.1, 1.27), (0, -0.55, 1.2), 0.045, RAIDER_BLACK), box((0.36, 0.06, 0.24), (0, 0.15, 1.3), M.steel, bevel=0.02)]
    p += rider((0, 0.75, 1.2), lean=0.1)
    return p


def raider_truck():
    p = []
    for x in (-0.75, 0.75):
        for y in (-1.0, 0.1, 0.9): p += wheel((x, y, 0.38), r=0.38, w=0.26)
    p += [box((1.4, 3.0, 0.4), (0, 0, 0.62), RAIDER_BLACK, bevel=0.05), box((1.4, 1.0, 1.0), (0, -0.95, 1.25), RUST, bevel=0.08),
          box((1.5, 1.9, 1.2), (0, 0.55, 1.35), RUST, bevel=0.08)]
    for x in (-0.35, 0.35): p.append(box((0.4, 0.05, 0.12), (x, -1.46, 1.45), GLASS, bevel=0.01))  # slit windows
    for k in range(4): p.append(box((1.52, 0.05, 0.12), (0, 0.0 + k * 0.4, 1.6), M.steel, bevel=0.01))  # bolted plates
    # the ram plough
    p += [box((1.8, 0.3, 0.6), (0, -1.75, 0.65), M.hazard, rot=(math.radians(-25), 0, 0), bevel=0.04)]
    for x in (-0.6, -0.2, 0.2, 0.6): p.append(rod((x, -1.85, 0.7), (x * 1.05, -2.25, 0.55), 0.05, M.steel))
    for x in (-0.5, 0.5): p.append(cyl(0.09, 0.05, (x, -1.48, 1.0), HEADLAMP, rot=(math.pi / 2, 0, 0), verts=12, bevel=0))
    return p


def raider_mortar():
    p = [ball(0.5, (0, 0, -0.35), K.M.sandbag, scale=(1.4, 1.1, 0.6))]
    for k in range(6):
        a = k / 6 * math.tau; p.append(K.sandbag((math.cos(a) * 0.75, math.sin(a) * 0.6, 0.06), rz=a + math.pi / 2))
    p += [rod((0, 0.1, 0.1), (0, -0.25, 0.75), 0.07, RAIDER_BLACK), cyl(0.12, 0.08, (0, 0.12, 0.08), M.steel, verts=14),
          rod((0, -0.1, 0.4), (-0.18, 0.05, 0.05), 0.02, M.steel), rod((0, -0.1, 0.4), (0.18, 0.05, 0.05), 0.02, M.steel),
          box((0.3, 0.2, 0.18), (0.45, 0.25, 0.12), RUST, bevel=0.02), cyl(0.06, 0.04, (0, -0.27, 0.8), HEADLAMP, rot=(math.radians(55), 0, 0), verts=10, bevel=0)]
    p += rider((-0.35, 0.25, 0.4), lean=0.5) + rider((0.3, -0.05, 0.42), lean=0.4, cloth=RUST)
    return p


def turret():
    """The base's gun turret from overhead, barrels pointing up the screen (+Y)."""
    p = [cyl(0.95, 0.25, (0, 0, 0.12), P_DARK, verts=36), cyl(0.7, 0.35, (0, 0, 0.45), P_WHITE, verts=32),
         box((0.9, 0.9, 0.3), (0, -0.1, 0.72), P_RED, bevel=0.1), box((0.5, 0.4, 0.12), (0, -0.25, 0.92), P_WHITE, bevel=0.04)]
    for x in (-0.22, 0.22):
        p += [cyl(0.09, 1.3, (x, 0.85, 0.72), P_DARK, rot=(math.pi / 2, 0, 0), verts=14), cyl(0.12, 0.2, (x, 0.35, 0.72), P_WHITE, rot=(math.pi / 2, 0, 0), verts=14)]
    p += [cyl(0.08, 0.05, (0, 0.28, 0.9), CYAN, verts=12, bevel=0), box((0.25, 0.3, 0.2), (0.55, -0.3, 0.55), P_DARK, bevel=0.03)]
    return p

# ---------- Extraction Run: the rover and Caretaker units ----------

def rover():
    """The player's recovery rover from overhead, nose up the screen (+Y)."""
    p = []
    for x in (-0.62, 0.62):
        for y in (-0.7, 0.75): p += wheel((x, y, 0.28), r=0.3, w=0.24)
    p += [box((1.1, 2.3, 0.35), (0, 0, 0.45), P_DARK, bevel=0.05), box((1.05, 0.9, 0.5), (0, 0.6, 0.85), P_WHITE, bevel=0.1),
          box((0.85, 0.35, 0.05), (0, 0.85, 1.12), GLASS, bevel=0.02), box((1.06, 0.15, 0.52), (0, 0.15, 0.86), P_RED, bevel=0.03),
          box((1.1, 1.0, 0.18), (0, -0.55, 0.72), P_RED, bevel=0.04)]
    for x in (-0.38, 0.38): p.append(cyl(0.09, 0.05, (x, 1.16, 0.7), CYAN, rot=(math.pi / 2, 0, 0), verts=12, bevel=0))
    for x, y, c in ((-0.25, -0.4, M.wood), (0.22, -0.7, M.scrap), (0.25, -0.3, M.wood)):
        p.append(box((0.38, 0.32, 0.3), (x, y, 0.97), c, rot=(0, 0, 0.2), bevel=0.03))  # cargo on the bed
    p.append(rod((-0.5, 0.1, 1.1), (0.5, 0.1, 1.1), 0.04, M.steel))
    return p


def patrol():
    """A Caretaker patrol car from overhead, nose up the screen (+Y)."""
    p = []
    for x in (-0.66, 0.66):
        for y in (-0.75, 0.75): p += wheel((x, y, 0.28), r=0.3, w=0.22)
    p += [box((1.2, 2.4, 0.4), (0, 0, 0.5), C_FRAME, bevel=0.06), box((1.25, 2.0, 0.55), (0, 0.05, 0.9), C_WHITE, bevel=0.18),
          box((0.95, 0.7, 0.08), (0, 0.45, 1.2), GLASS, bevel=0.03), box((0.9, 0.18, 0.12), (0, -0.05, 1.28), PINK, bevel=0.02),
          box((1.15, 0.12, 0.2), (0, 1.12, 0.55), PINK, bevel=0.02), cyl(0.18, 0.08, (0, -0.6, 1.2), C_FRAME, verts=16)]
    return p


def sentry():
    p = [cyl(0.55, 0.15, (0, 0, 0.08), C_FRAME, verts=24), box((0.9, 0.9, 0.05), (0, 0, 0.17), M.hazard, bevel=0),
         cyl(0.22, 1.1, (0, 0, 0.7), C_WHITE, verts=18), ball(0.38, (0, 0, 1.4), C_WHITE, segs=24),
         cyl(0.16, 0.12, (0, -0.32, 1.42), C_FRAME, rot=(math.pi / 2, 0, 0), verts=18), cyl(0.11, 0.05, (0, -0.4, 1.42), PINK, rot=(math.pi / 2, 0, 0), verts=18, bevel=0),
         torus(0.4, 0.03, (0, 0, 1.25), PINK)]
    return p


def drone():
    """A Caretaker drone from overhead."""
    p = [ball(0.32, (0, 0, 0), C_WHITE, scale=(1, 1, 0.55), segs=24), cyl(0.12, 0.05, (0, -0.2, 0.08), PINK, verts=16, bevel=0)]
    for k in range(4):
        a = math.pi / 4 + k * math.pi / 2; x, y = math.cos(a) * 0.62, math.sin(a) * 0.62
        p += [rod((math.cos(a) * 0.25, math.sin(a) * 0.25, 0), (x, y, 0.02), 0.05, C_FRAME), cyl(0.26, 0.02, (x, y, 0.08), GLASS, verts=24, bevel=0),
              torus(0.27, 0.02, (x, y, 0.08), PINK if k < 2 else C_WHITE)]
    return p


def crate(band):
    paint = {'fuel': (0.1, 0.45, 0.6), 'trade': (0.7, 0.5, 0.1), 'tech': (0.4, 0.22, 0.65), 'black': (0.05, 0.05, 0.06)}[band]
    p = [box((0.9, 0.7, 0.62), (0, 0, 0.31), worn('crate_' + band, paint, rough=0.6, metal=0.3, wear=0.5, grime=0.3, scale=8), bevel=0.05)]
    colour = {'fuel': glow('band_fuel', (0.25, 0.85, 1.0), 4), 'trade': glow('band_trade', (1.0, 0.78, 0.2), 4),
              'tech': glow('band_tech', (0.7, 0.45, 1.0), 4), 'black': PINK}[band]
    p += [box((0.92, 0.72, 0.1), (0, 0, 0.42), colour, bevel=0), box((0.3, 0.05, 0.22), (0, -0.37, 0.25), colour, bevel=0)]
    for x in (-0.38, 0.38): p.append(box((0.06, 0.72, 0.64), (x, 0, 0.32), M.steel, bevel=0.01))
    return p

# ---------- render the sprites ----------
units = [
    ('ls-bike', raider_bike, 1.75, (0, 0, 0.6), 300, 300, 50),
    ('ls-technical', raider_technical, 3.2, (0, -0.1, 0.8), 360, 380, 50),
    ('ls-truck', raider_truck, 4.4, (0, -0.2, 1.0), 420, 460, 50),
    ('ls-mortar', raider_mortar, 2.6, (0, 0, 0.3), 320, 300, 50),
    ('ls-turret', turret, 2.9, (0, 0.2, 0.5), 300, 300, 0),
    ('ex-rover', rover, 3.0, (0, 0, 0.6), 280, 320, 0),
    ('ex-patrol', patrol, 3.0, (0, 0, 0.6), 280, 320, 0),
    ('ex-sentry', sentry, 2.4, (0, 0, 0.8), 260, 320, 50),
    ('ex-drone', drone, 1.9, (0, 0, 0), 260, 260, 0),
]
for name, build, ortho, target, w, h, tilt in units:
    if want(name): sprite(name, build, ortho, target, w, h, tilt, extra=[((0, -2, 3), (0.9, 0.95, 1.0), 60)])
for band in ('fuel', 'trade', 'tech', 'black'):
    if want('ex-crate-' + band): sprite('ex-crate-' + band, lambda: crate(band), 1.6, (0, 0, 0.3), 220, 200, 50, extra=[((0, -2, 3), (0.9, 0.95, 1.0), 40)])

# ---------- arenas: same camera as the Leviathan's, so the floor fits the same playfield ----------

def arena_camera():
    O.camera((0, -0.4, 0), 34, lens=30, dist=15.5)


if want('laststand-arena'):
    rng = random.Random(11)
    DIRT = worn('dirt', (0.2, 0.16, 0.12), rough=0.95, wear=0.0, grime=0.7, scale=3, pitted=0.6)
    ROAD = worn('road', (0.27, 0.23, 0.18), rough=0.95, wear=0.0, grime=0.6, scale=5, pitted=0.5)
    COURT = worn('court', (0.3, 0.31, 0.32), rough=0.85, wear=0.2, grime=0.5, scale=4, plates=True)
    bpy.ops.mesh.primitive_plane_add(size=60, location=(0, 0, 0)); bpy.context.active_object.data.materials.append(DIRT)
    bpy.ops.mesh.primitive_plane_add(size=1, location=(0, 4, 0.01)); road = bpy.context.active_object; road.scale = (3.4, 22, 1); road.data.materials.append(ROAD)
    # the base: a concrete wall across the bottom with a gate, hazard lights and the turret platform behind it
    box((20, 8, 0.05), (0, -9.0, 0.02), COURT, bevel=0)
    for x0, x1 in ((-10, -1.3), (1.3, 10)):
        wl = x1 - x0; box((wl, 0.6, 1.0), ((x0 + x1) / 2, -4.9, 0.5), M.concrete, bevel=0.04)
        for k in range(int(wl / 0.9)): box((0.32, 0.05, 0.08), (x0 + 0.45 + k * 0.9, -5.22, 0.92), HAZARD, bevel=0)
    for x in (-1.45, 1.45): box((0.35, 0.8, 1.5), (x, -4.9, 0.75), M.steel, bevel=0.04); cyl(0.12, 0.1, (x, -5.0, 1.55), M.red, verts=12, bevel=0)
    cyl(1.25, 0.3, (0, -6.0, 0.15), M.conc_dark, verts=40); torus(1.25, 0.05, (0, -6.0, 0.31), HAZARD)
    for k in range(10):  # sandbag ring around the platform
        a = math.pi * (0.1 + 0.8 * k / 9) + math.pi; K.sandbag((math.cos(a) * 1.65, -6.0 + math.sin(a) * 1.65, 0.05), rz=a + math.pi / 2)
    for x in (-5, 5): box((2.6, 2.0, 1.6), (x, -7.6, 0.8), M.armour, bevel=0.06)  # base buildings in the courtyard
    # the approach: ruins, wrecks, tank traps and craters either side of the road
    for side in (-1, 1):
        y = -3.5
        while y < 14:
            d = rng.uniform(1.2, 2.6); hgt = rng.uniform(0.6, 2.8); x = side * rng.uniform(4.3, 6.5)
            wd = rng.uniform(1.2, 2.4); mat = rng.choice([M.concrete, M.brick, M.conc_dark])
            box((wd, d, hgt), (x, y + d / 2, hgt / 2), mat, rot=(0, 0, rng.uniform(-0.1, 0.1)), bevel=0.04)
            box((wd * 0.5, d * 0.6, rng.uniform(0.3, 0.9)), (x + rng.uniform(-0.3, 0.3), y + d / 2, hgt + 0.2), mat, rot=(rng.uniform(-0.3, 0.3), rng.uniform(-0.3, 0.3), 0), bevel=0.04)  # broken top
            y += d + rng.uniform(0.4, 1.4)
        for k in range(4):
            x, y = side * rng.uniform(2.3, 3.6), rng.uniform(-3, 11)
            for a in (0.0, math.pi / 2, math.pi / 4):
                box((0.06, 0.7, 0.06), (x, y, 0.3), M.steel, rot=(math.radians(55), 0, a), bevel=0)  # tank trap
        for k in range(2):
            x, y = side * rng.uniform(2.6, 3.6), rng.uniform(0, 12)
            box((0.9, 1.8, 0.6), (x, y, 0.35), M.scrap, rot=(0, 0.15, rng.uniform(-0.8, 0.8)), bevel=0.06)  # wrecked car
    SCRUB = worn('scrub', (0.16, 0.18, 0.08), rough=1, wear=0, scale=14, pitted=0.5)
    for k in range(26):
        side = rng.choice([-1, 1]); ball(rng.uniform(0.15, 0.35), (side * rng.uniform(2.0, 4.2), rng.uniform(-3.5, 13), 0.1), SCRUB, scale=(1, 1, 0.6), segs=10)
    for y in (-2.5, 2.5, 7.5):
        for side in (-1, 1):
            cyl(0.06, 2.2, (side * 2.0, y, 1.1), M.steel, verts=8); ball(0.13, (side * 2.0, y, 2.2), M.lamp, segs=10)
    O.lights(key=0.7, tint=(0.35, 0.5, 1.0), world=(0.08, 0.07, 0.07), extra=[
        ((0, -5.5, 3), (1.0, 0.7, 0.3), 500), ((-4, -5, 2.5), (1.0, 0.5, 0.2), 200), ((4, -5, 2.5), (1.0, 0.5, 0.2), 200),
        ((0, 9, 6), (1.0, 0.55, 0.3), 900)])
    arena_camera(); O.render('laststand-arena', 1080, 1620, samples=64, transparent=False); O.clear()

if want('extraction-arena'):
    rng = random.Random(5)
    PAD = worn('yard_pad', (0.4, 0.42, 0.42), rough=0.8, wear=0.3, grime=0.55, scale=3, plates=True, cracks=True)
    PAINT = worn('yard_paint', (0.85, 0.65, 0.12), rough=0.7, wear=0.5, grime=0.3, scale=6)
    bpy.ops.mesh.primitive_plane_add(size=60, location=(0, 0, 0)); bpy.context.active_object.data.materials.append(PAD)
    for x in (-3.4, 3.4): box((0.12, 22, 0.01), (x, 0, 0.006), PAINT, bevel=0)  # yard lanes
    for y in range(-8, 9, 3): box((0.9, 0.12, 0.01), (0, y, 0.006), PAINT, bevel=0)
    # the extraction pad at the top and the entry gate at the bottom
    cyl(1.3, 0.08, (0, 9.6, 0.04), M.conc_dark, verts=48); torus(1.3, 0.07, (0, 9.6, 0.1), GREEN); torus(0.9, 0.03, (0, 9.6, 0.1), GREEN)
    for x in (-0.3, 0.3): box((0.1, 0.8, 0.02), (x, 9.6, 0.11), GREEN, bevel=0)
    box((0.6, 0.1, 0.02), (0, 9.6, 0.11), GREEN, bevel=0)
    for x in (-1.6, 1.6): box((0.3, 0.3, 1.2), (x, -8.6, 0.6), C_FRAME, bevel=0.03); box((1.3, 0.08, 0.1), (x * 0.6, -8.6, 1.0), M.hazard, bevel=0)
    # container stacks, Caretaker light pylons and clutter along the edges, so the middle stays clear for crates
    CONT = [worn('cont_white', (0.75, 0.77, 0.78), rough=0.6, metal=0.4, wear=0.5, grime=0.5, corrugated=True),
            worn('cont_grey', (0.35, 0.38, 0.4), rough=0.6, metal=0.4, wear=0.5, grime=0.5, corrugated=True),
            worn('cont_teal', (0.12, 0.3, 0.32), rough=0.6, metal=0.4, wear=0.5, grime=0.5, corrugated=True)]
    for side in (-1, 1):
        y = -10.0
        while y < 12:
            stack = rng.randint(1, 3)
            for k in range(stack):
                box((1.25, 2.6, 1.2), (side * rng.uniform(4.6, 5.0), y + 1.3, 0.6 + k * 1.2), rng.choice(CONT), rot=(0, 0, rng.uniform(-0.04, 0.04)), bevel=0.03)
            box((0.05, 2.0, 0.08), (side * 4.0, y + 1.3, 1.0), PINK, bevel=0)
            y += 2.8 + rng.uniform(0, 0.4)
        for y in (-5, 0, 5):
            cyl(0.1, 2.6, (side * 3.85, y, 1.3), C_FRAME, verts=12); ball(0.18, (side * 3.85, y, 2.65), PINK, segs=14)
    for k in range(10):
        x = rng.choice([-1, 1]) * rng.uniform(3.6, 3.9); y = rng.uniform(-8, 6)
        if k % 2: cyl(0.14, 0.32, (x, y, 0.16), M.water, verts=14)
        else: box((0.5, 0.5, 0.12), (x, y, 0.06), M.wood, bevel=0.01)
    O.lights(key=1.0, tint=(0.6, 0.8, 1.0), world=(0.06, 0.08, 0.1), extra=[
        ((0, 9.6, 3), (0.3, 1.0, 0.5), 500), ((-3, -2, 4), (1.0, 0.3, 0.7), 300), ((3, 3, 4), (1.0, 0.3, 0.7), 300),
        ((0, -2, 7), (0.8, 0.9, 1.0), 900)])
    arena_camera(); O.render('extraction-arena', 1080, 1620, samples=64, transparent=False); O.clear()

print('NWO_OPERATIONS_ART_COMPLETE')
