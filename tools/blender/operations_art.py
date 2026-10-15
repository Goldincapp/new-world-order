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

# ---------- Last Stand towers (player red and white, Lumen gold) and the raiders' War Rig ----------

def gun_nest():
    p = []
    for k in range(9):
        a = k / 9 * math.tau; p.append(K.sandbag((math.cos(a) * 0.55, math.sin(a) * 0.55, 0.06), rz=a + math.pi / 2))
        p.append(K.sandbag((math.cos(a + 0.35) * 0.55, math.sin(a + 0.35) * 0.55, 0.16), rz=a + math.pi / 2 + 0.35))
    p += [cyl(0.42, 0.06, (0, 0, 0.03), P_DARK, verts=24), cyl(0.06, 0.45, (0, 0, 0.25), M.steel, verts=10),
          box((0.3, 0.4, 0.2), (0, 0, 0.52), P_RED, bevel=0.05), box((0.32, 0.12, 0.22), (0, 0.18, 0.55), P_WHITE, bevel=0.04)]
    for x in (-0.07, 0.07): p.append(cyl(0.04, 0.6, (x, -0.42, 0.55), P_DARK, rot=(math.pi / 2, 0, 0), verts=10))
    p.append(cyl(0.05, 0.04, (0, -0.1, 0.65), CYAN, verts=10, bevel=0))
    return p


def burner():
    p = [cyl(0.45, 0.15, (0, 0, 0.08), P_DARK, verts=24)]
    for x in (-0.2, 0.2): p += [cyl(0.15, 0.7, (x, 0.12, 0.5), P_RED, verts=16, bevel=0.03), ball(0.15, (x, 0.12, 0.86), P_RED, segs=14)]
    p += [box((0.5, 0.1, 0.1), (0, 0.12, 0.6), M.hazard, bevel=0), rod((0, 0.05, 0.45), (0, -0.4, 0.42), 0.06, M.steel),
          cyl(0.1, 0.14, (0, -0.45, 0.42), P_DARK, rot=(math.pi / 2, 0, 0), verts=12), cyl(0.07, 0.03, (0, -0.53, 0.42), HEADLAMP, rot=(math.pi / 2, 0, 0), verts=12, bevel=0)]
    return p


def railgun():
    p = [cyl(0.42, 0.18, (0, 0, 0.09), P_DARK, verts=24), cyl(0.22, 0.4, (0, 0, 0.38), P_WHITE, verts=18),
         box((0.36, 0.6, 0.26), (0, 0.05, 0.72), P_WHITE, bevel=0.06)]
    for x in (-0.09, 0.09): p.append(box((0.06, 1.1, 0.08), (x, -0.55, 0.74), P_DARK, bevel=0.01))
    for k in range(4): p.append(box((0.32, 0.06, 0.16), (0, -0.2 - k * 0.22, 0.74), CYAN, bevel=0))  # coils
    p.append(box((0.38, 0.12, 0.28), (0, 0.32, 0.72), P_RED, bevel=0.03))
    return p


def beacon():
    gold = worn('beacon_gold', (0.85, 0.66, 0.34), rough=0.3, metal=0.6, wear=0.2, grime=0.1, scale=6)
    halo = glow('beacon_halo', (1.0, 0.75, 0.3), 5)
    p = [cyl(0.4, 0.12, (0, 0, 0.06), P_DARK, verts=24), cyl(0.1, 1.0, (0, 0, 0.6), gold, verts=14),
         ball(0.2, (0, 0, 1.2), gold, segs=20), ball(0.1, (0, -0.15, 1.22), glow('beacon_eye', (0.2, 1.0, 0.85), 7), scale=(1, 0.5, 1), segs=14)]
    for z, r in ((0.55, 0.3), (0.9, 0.22)): p.append(torus(r, 0.025, (0, 0, z), halo))
    return p


def war_rig():
    p = []
    for x in (-0.95, 0.95):
        for y in (-1.6, -0.5, 0.6, 1.5): p += wheel((x, y, 0.45), r=0.45, w=0.3)
    p += [box((1.8, 4.2, 0.5), (0, 0, 0.8), RAIDER_BLACK, bevel=0.06), box((1.8, 1.3, 1.2), (0, -1.4, 1.55), RUST, bevel=0.1),
          box((1.9, 2.6, 1.5), (0, 0.75, 1.7), RUST, bevel=0.1), box((2.3, 0.4, 0.9), (0, -2.35, 0.9), M.hazard, rot=(math.radians(-25), 0, 0), bevel=0.05)]
    for x in (-0.8, -0.4, 0, 0.4, 0.8): p.append(rod((x, -2.45, 0.95), (x * 1.05, -2.95, 0.75), 0.06, M.steel))
    for x in (-0.55, 0.55): p.append(cyl(0.11, 0.05, (x, -2.06, 1.4), HEADLAMP, rot=(math.pi / 2, 0, 0), verts=12, bevel=0))
    for x in (-0.6, 0.6): p += [cyl(0.12, 1.2, (x, 1.6, 2.7), M.steel, verts=12), cyl(0.09, 0.05, (x, 1.6, 3.32), HEADLAMP, verts=12, bevel=0)]
    for k in range(5): p.append(box((1.95, 0.06, 0.14), (0, 0.0 + k * 0.4, 2.0), M.steel, bevel=0.01))
    p += [cyl(0.12, 0.5, (0, 0.7, 2.6), M.steel, verts=12), rod((0, 0.5, 2.85), (0, -0.6, 2.8), 0.07, RAIDER_BLACK)]
    return p


for name, build, ortho, target, w, h, tilt in [
        ('ls-tower-gun', gun_nest, 1.7, (0, 0, 0.35), 260, 260, 50), ('ls-tower-burner', burner, 1.7, (0, 0, 0.45), 260, 260, 50),
        ('ls-tower-rail', railgun, 2.1, (0, -0.25, 0.5), 260, 280, 50), ('ls-tower-beacon', beacon, 1.7, (0, 0, 0.65), 240, 280, 50),
        ('ls-warrig', war_rig, 6.4, (0, -0.4, 1.4), 440, 520, 50)]:
    if want(name): sprite(name, build, ortho, target, w, h, tilt, extra=[((0, -2, 3), (0.9, 0.95, 1.0), 60)])

# ---------- arenas: same camera as the Leviathan's, so the floor fits the same playfield ----------

def arena_camera():
    O.camera((0, -0.4, 0), 34, lens=30, dist=15.5)


# The Last Stand road and build pads, in the game's field units (x across the floor, y down the canvas).
# client/operations.js uses the same numbers (LS_PATH, LS_PADS, LS_LINE): keep them in step.
LS_PATH = [(.5, -.06), (.5, .06), (.22, .16), (.24, .27), (.74, .36), (.76, .45), (.5, .53), (.5, .69)]
LS_PADS = [(.40, .21), (.10, .27), (.56, .235), (.90, .40), (.50, .43), (.25, .48)]
LS_LINE, LS_GATE, LS_TURRET = .61, .69, (.5, .815)


def to_ground(fx, fy):
    """Where a point of the game's field sits on the arena floor: a ray from the camera through that pixel, to z=0.
    The field spans 14% to 86% of the backdrop's width; the backdrop is portrait, so the sensor fits its height."""
    bpy.context.view_layer.update(); cam = bpy.context.scene.camera; lens = cam.data.lens; sh = 36.0; sw = sh * 1080 / 1620
    u, v = .14 + .72 * fx, fy
    d = cam.matrix_world.to_3x3() @ Vector(((u - .5) * sw / lens, (.5 - v) * sh / lens, -1))
    t = -cam.location.z / d.z
    return cam.location + d * t


if want('laststand-arena'):
    rng = random.Random(11)
    arena_camera()
    DIRT = worn('dirt', (0.2, 0.16, 0.12), rough=0.95, wear=0.0, grime=0.7, scale=3, pitted=0.6)
    ROAD = worn('road', (0.3, 0.25, 0.19), rough=0.95, wear=0.0, grime=0.6, scale=5, pitted=0.5)
    COURT = worn('court', (0.3, 0.31, 0.32), rough=0.85, wear=0.2, grime=0.5, scale=4, plates=True)
    PADMAT = worn('pad', (0.42, 0.42, 0.4), rough=0.8, wear=0.4, grime=0.4, scale=4, plates=True)
    bpy.ops.mesh.primitive_plane_add(size=60, location=(0, 0, 0)); bpy.context.active_object.data.materials.append(DIRT)
    # the winding road: straight slabs that run past each bend so they overlap there, each a hair higher than the last
    pts = [to_ground(x, y) for x, y in LS_PATH]
    for k, (a_, b_) in enumerate(zip(pts, pts[1:])):
        mid = (a_ + b_) / 2; d = b_ - a_; ln = d.length
        box((1.8, ln + 1.6, 0.02), (mid.x, mid.y, 0.012 + k * 0.002), ROAD, rot=(0, 0, math.atan2(d.y, d.x) - math.pi / 2), bevel=0)
    for k in range(len(pts) - 1):  # wheel ruts down the middle
        a_, b_ = pts[k], pts[k + 1]; d = b_ - a_
        for off in (-0.35, 0.35):
            n = Vector((-d.y, d.x, 0)).normalized() * off
            box((0.08, d.length, 0.005), ((a_.x + b_.x) / 2 + n.x, (a_.y + b_.y) / 2 + n.y, 0.024), M.rubber, rot=(0, 0, math.atan2(d.y, d.x) - math.pi / 2), bevel=0)
    # build pads: concrete plates with a hazard rim
    for x, y in LS_PADS:
        g = to_ground(x, y); cyl(0.62, 0.12, (g.x, g.y, 0.06), PADMAT, verts=32); torus(0.62, 0.035, (g.x, g.y, 0.13), HAZARD)
        for k in range(4): box((0.14, 0.05, 0.02), (g.x + math.cos(k * math.pi / 2) * 0.4, g.y + math.sin(k * math.pi / 2) * 0.4, 0.13), M.hazard, rot=(0, 0, k * math.pi / 2), bevel=0)
    # the squad's sandbag line across the road, just before the gate
    line = to_ground(.5, LS_LINE - .015)  # just in front of where the soldiers stand
    for k in range(9): K.sandbag((line.x - 1.6 + k * 0.4, line.y, 0.05)); K.sandbag((line.x - 1.4 + k * 0.4, line.y, 0.15))
    # the base: a concrete wall with a gate, hazard lights and the turret platform behind it
    gate = to_ground(.5, LS_GATE); tur = to_ground(*LS_TURRET)
    box((20, 8, 0.05), (0, gate.y - 4.0, 0.02), COURT, bevel=0)
    for x0, x1 in ((-10, -1.3), (1.3, 10)):
        wl = x1 - x0; box((wl, 0.6, 1.0), ((x0 + x1) / 2, gate.y, 0.5), M.concrete, bevel=0.04)
        for k in range(int(wl / 0.9)): box((0.32, 0.05, 0.08), (x0 + 0.45 + k * 0.9, gate.y - 0.32, 0.92), HAZARD, bevel=0)
    for x in (-1.45, 1.45): box((0.35, 0.8, 1.5), (x, gate.y, 0.75), M.steel, bevel=0.04); cyl(0.12, 0.1, (x, gate.y - 0.1, 1.55), M.red, verts=12, bevel=0)
    cyl(1.25, 0.3, (tur.x, tur.y, 0.15), M.conc_dark, verts=40); torus(1.25, 0.05, (tur.x, tur.y, 0.31), HAZARD)
    for x in (-5, 5): box((2.6, 2.0, 1.6), (x, tur.y - 1.6, 0.8), M.armour, bevel=0.06)
    # ruins and wrecks beyond the field's edges; scrub and tank traps only where they don't cover the road or pads
    for side in (-1, 1):
        y = gate.y + 1.0
        while y < 14:
            d = rng.uniform(1.2, 2.6); hgt = rng.uniform(0.6, 2.8); x = side * rng.uniform(4.6, 6.5)
            wd = rng.uniform(1.2, 2.4); mat = rng.choice([M.concrete, M.brick, M.conc_dark])
            box((wd, d, hgt), (x, y + d / 2, hgt / 2), mat, rot=(0, 0, rng.uniform(-0.1, 0.1)), bevel=0.04)
            box((wd * 0.5, d * 0.6, rng.uniform(0.3, 0.9)), (x + rng.uniform(-0.3, 0.3), y + d / 2, hgt + 0.2), mat, rot=(rng.uniform(-0.3, 0.3), rng.uniform(-0.3, 0.3), 0), bevel=0.04)
            y += d + rng.uniform(0.4, 1.4)
    SCRUB = worn('scrub', (0.16, 0.18, 0.08), rough=1, wear=0, scale=14, pitted=0.5)

    def clear_of_play(fx, fy):
        near_pad = any(math.hypot((fx - x) * .48, fy - y) < .07 for x, y in LS_PADS)
        near_road = any(math.hypot((fx - (x0 + (x1 - x0) * k / 10)) * .48, fy - (y0 + (y1 - y0) * k / 10)) < .06 for (x0, y0), (x1, y1) in zip(LS_PATH, LS_PATH[1:]) for k in range(11))
        return not near_pad and not near_road
    placed = 0
    while placed < 40:
        fx, fy = rng.uniform(-.05, 1.05), rng.uniform(.0, .58)
        if not clear_of_play(fx, fy): continue
        g = to_ground(fx, fy); placed += 1
        if placed % 5 == 0:
            for a2 in (0.0, math.pi / 2, math.pi / 4): box((0.06, 0.7, 0.06), (g.x, g.y, 0.3), M.steel, rot=(math.radians(55), 0, a2), bevel=0)
        else: ball(rng.uniform(0.15, 0.32), (g.x, g.y, 0.1), SCRUB, scale=(1, 1, 0.6), segs=10)
    O.lights(key=0.7, tint=(0.35, 0.5, 1.0), world=(0.08, 0.07, 0.07), extra=[
        ((tur.x, tur.y + 0.5, 3), (1.0, 0.7, 0.3), 500), ((-4, gate.y, 2.5), (1.0, 0.5, 0.2), 200), ((4, gate.y, 2.5), (1.0, 0.5, 0.2), 200),
        ((0, 9, 6), (1.0, 0.55, 0.3), 900), ((0, 2, 6), (0.9, 0.85, 0.8), 400)])
    O.render('laststand-arena', 1080, 1620, samples=64, transparent=False); O.clear()

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
