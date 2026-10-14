"""Builds the Leviathan Assault set: the Caretaker war-mech (normal and enraged), the strike leader, their AI partner
drone, and the machine-hall arena they fight in.

Each unit is rendered as a sprite for the 2D operation view (most players, and every phone, see that one) and
exported as a GLB for the optional 3D view. The arena is a backdrop only. Glow is added afterwards by
tools/blender/leviathan_post.py, because Cycles has no bloom.

Run headless (from the repo root):
  blender -b --python tools/blender/leviathan_assault.py -- client/models/units <raw_render_dir> [preview]
  python tools/blender/leviathan_post.py <raw_render_dir> client/assets/operations
"""
import bpy, math, random, sys, os
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import wl_kit as K
from wl_kit import M, box, cyl, rod, torus, join, worn, glow
from mathutils import Vector, Euler

argv = sys.argv[sys.argv.index('--') + 1:]
OUT, RAW = argv[0], argv[1]
PREVIEW = len(argv) > 2 and argv[2] == 'preview'  # quick low-sample renders while iterating
TEX = os.path.join(RAW, 'tex')
os.makedirs(OUT, exist_ok=True); os.makedirs(TEX, exist_ok=True)

K.reset(); M.build()
sc = bpy.context.scene
sc.view_settings.view_transform = 'Standard'
sc.cycles.use_denoising = True
TILT = math.radians(34)  # the arena camera's angle down from overhead
# Units are shot more from the front than the arena, so they read clearly at sprite size (as in most top-down shooters).

# The mech's palette: clean white plates that go crimson when it is enraged, cold magenta Caretaker light.
WHITE = worn('mech_white', (0.86, 0.88, 0.9), rough=0.38, metal=0.1, wear=0.3, grime=0.25, scale=5, pitted=0.05)
CRIMSON = worn('mech_crimson', (0.55, 0.03, 0.03), rough=0.36, metal=0.2, wear=0.35, grime=0.3, scale=5, pitted=0.05)
FRAME = worn('mech_frame', (0.1, 0.12, 0.13), rough=0.5, metal=0.7, wear=0.6, scale=6)
CHROME = worn('mech_piston', (0.6, 0.62, 0.64), rough=0.22, metal=0.95, wear=0.1, grime=0.1)
PINK = glow('caretaker_pink', (1.0, 0.12, 0.62), 6)
RAGE = glow('caretaker_rage', (1.0, 0.1, 0.03), 9)
EYE = glow('caretaker_eye', (1.0, 0.05, 0.08), 8)
GLASS = bpy.data.materials.new('dome_glass'); GLASS.use_nodes = True
_g = GLASS.node_tree.nodes['Principled BSDF']
_g.inputs['Base Color'].default_value = (0.75, 0.9, 1.0, 1); _g.inputs['Roughness'].default_value = 0.04
_g.inputs['Alpha'].default_value = 0.22; _g.inputs['Coat Weight'].default_value = 1.0  # clear, so the core shows through
# The strike leader: red and white so they read against the dark floor.
SUIT_W = worn('suit_white', (0.95, 0.95, 0.93), rough=0.55, metal=0.05, wear=0.25, grime=0.2, scale=9)
SUIT_R = worn('suit_red', (0.62, 0.05, 0.04), rough=0.5, metal=0.1, wear=0.3, grime=0.25, scale=9)
SUIT_D = worn('suit_dark', (0.07, 0.08, 0.09), rough=0.7, metal=0.2, wear=0.2, scale=9)
VISOR = glow('visor', (0.25, 0.85, 1.0), 4)
MUZZLE = glow('muzzle', (1.0, 0.45, 0.08), 6)
# The partner: warm pale gold with a teal eye, nothing like the Caretaker's cold magenta.
GOLD = worn('partner_gold', (0.85, 0.66, 0.34), rough=0.3, metal=0.6, wear=0.2, grime=0.1, scale=6)
WARM = glow('partner_eye', (0.2, 1.0, 0.85), 7)
HALO = glow('partner_halo', (1.0, 0.75, 0.3), 4)
GLOWS = {PINK.name, RAGE.name, EYE.name, VISOR.name, MUZZLE.name, WARM.name, HALO.name, M.lamp.name, M.red.name, M.cyan.name, GLASS.name}

def ball(r, loc, mat, scale=(1, 1, 1), segs=24):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=r, location=loc, segments=segs, ring_count=segs // 2)
    o = bpy.context.active_object; o.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    bpy.ops.object.shade_smooth(); o.data.materials.append(mat); return o

def smooth(o):
    bpy.context.view_layer.objects.active = o; o.select_set(True); bpy.ops.object.shade_smooth(); o.select_set(False); return o

def clear():
    """Removes everything but the shared materials, ready for the next build."""
    for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)

def lights(key=3.0, tint=(0.55, 0.85, 1.0), extra=()):
    w = bpy.data.worlds.get('w') or bpy.data.worlds.new('w'); sc.world = w; w.use_nodes = True
    bg = w.node_tree.nodes['Background']; bg.inputs['Color'].default_value = (0.05, 0.09, 0.11, 1); bg.inputs['Strength'].default_value = 0.6
    bpy.ops.object.light_add(type='SUN', rotation=(math.radians(38), math.radians(-18), math.radians(-30)))
    s = bpy.context.active_object.data; s.energy = key; s.angle = math.radians(4); s.color = (1.0, 0.95, 0.88)
    bpy.ops.object.light_add(type='SUN', rotation=(math.radians(-60), math.radians(25), math.radians(20)))
    r = bpy.context.active_object.data; r.energy = key * 0.7; r.color = tint  # cool rim from behind
    for loc, col, power in extra:
        bpy.ops.object.light_add(type='POINT', location=loc); p = bpy.context.active_object.data; p.energy = power; p.color = col; p.shadow_soft_size = 0.4

def camera(target, ortho=None, lens=35, dist=30, tilt=TILT):
    d = bpy.data.cameras.new('cam')
    if ortho: d.type = 'ORTHO'; d.ortho_scale = ortho
    else: d.lens = lens
    d.clip_end = 200
    cam = bpy.data.objects.new('cam', d); sc.collection.objects.link(cam); sc.camera = cam
    rot = Euler((tilt, 0, 0)); cam.rotation_euler = rot
    cam.location = Vector(target) - (rot.to_matrix() @ Vector((0, 0, -1))) * dist
    return cam

def render(name, w, h, samples=64, transparent=True):
    sc.render.film_transparent = transparent
    sc.render.resolution_x, sc.render.resolution_y = (w // 2, h // 2) if PREVIEW else (w, h)
    sc.cycles.samples = 12 if PREVIEW else samples
    sc.render.image_settings.file_format = 'PNG'; sc.render.image_settings.color_mode = 'RGBA'
    sc.render.filepath = os.path.join(RAW, name + '.png')
    bpy.ops.render.render(write_still=True); print('rendered', sc.render.filepath)

def swap(objs, pairs):
    for o in objs:
        for slot in o.material_slots:
            if slot.material in pairs: slot.material = pairs[slot.material]

def finish(name, parts, size):
    """Bake the solid parts into one textured mesh; glowing parts and the glass dome stay separate meshes."""
    keep = [o for o in parts if o.data.materials and o.data.materials[0].name in GLOWS]
    solid = [o for o in parts if o not in keep]
    objs = [K.bake(join(solid, name), size, TEX, normal=True)]
    by_mat = {}
    for o in keep: by_mat.setdefault(o.data.materials[0].name, []).append(o)
    for mat, group in by_mat.items(): objs.append(join(group, name + '_' + mat))
    return objs

# ---------- the Caretaker war-mech: upper body, facing the player (-Y) ----------

def mech():
    p = []
    # waist and hips, half hidden behind the ledge
    p.append(box((1.3, 0.9, 0.55), (0, 0, 0.3), FRAME, bevel=0.06))
    for k in range(3):  # segmented abdomen
        p.append(box((1.0 - k * 0.08, 0.75, 0.2), (0, -0.04, 0.68 + k * 0.2), FRAME if k % 2 else CHROME, bevel=0.04))
    # chest: a broad white shell with angled front plates and magenta seams
    p.append(box((2.0, 1.2, 1.25), (0, 0.05, 1.62), WHITE, bevel=0.22))
    p.append(box((0.5, 0.3, 1.0), (0, -0.62, 1.55), WHITE, rot=(math.radians(-10), 0, 0), bevel=0.1))  # keel plate
    for side in (-1, 1):
        p.append(box((0.92, 0.18, 0.78), (side * 0.52, -0.62, 1.5), WHITE, rot=(math.radians(-14), 0, side * math.radians(-12)), bevel=0.06))
        p.append(box((0.07, 0.05, 0.62), (side * 0.08, -0.72, 1.5), PINK, rot=(math.radians(-14), 0, 0), bevel=0))
        p.append(box((0.55, 0.05, 0.05), (side * 0.6, -0.74, 1.12), PINK, bevel=0))
        # vents under the chest plates
        for v in range(3): p.append(box((0.5, 0.06, 0.04), (side * 0.55, -0.66, 0.98 - v * 0.08), FRAME, bevel=0))
    p.append(box((0.7, 0.7, 0.3), (0, 0.1, 2.3), FRAME, bevel=0.06))  # neck collar base
    # the dome: the Caretaker's core under glass, ringed by a collar, with one red monocle eye
    p.append(smooth(torus(0.72, 0.13, (0, -0.05, 2.45), WHITE)))
    p.append(ball(0.7, (0, -0.05, 2.62), GLASS, scale=(1, 1, 0.95), segs=32))
    p.append(ball(0.36, (0, -0.05, 2.62), PINK, segs=24))
    for k, (r, tilt) in enumerate(((0.4, 25), (0.36, -40))):
        p.append(torus(r * 1.2, 0.025, (0, -0.05, 2.62), PINK, rot=(math.radians(tilt), math.radians(30 * k), 0)))
    p.append(cyl(0.18, 0.22, (0.45, -0.68, 2.4), FRAME, rot=(math.radians(90), 0, 0), verts=20))
    p.append(cyl(0.13, 0.06, (0.45, -0.8, 2.4), EYE, rot=(math.radians(90), 0, 0), verts=20, bevel=0))
    p.append(rod((0.65, -0.45, 2.45), (0.95, -0.2, 3.2), 0.025, CHROME))  # antenna by the eye
    p.append(ball(0.05, (0.95, -0.2, 3.2), EYE, segs=10))
    for side in (-1, 1):
        sx = side
        # shoulder pauldrons
        p.append(ball(0.62, (sx * 1.38, 0.05, 1.95), WHITE, scale=(1.0, 0.95, 0.72)))
        p.append(ball(0.4, (sx * 1.42, 0.05, 2.25), FRAME, scale=(1.1, 1.0, 0.5)))
        p.append(box((0.08, 0.7, 0.06), (sx * 1.62, -0.05, 2.06), PINK, rot=(0, sx * math.radians(-30), 0), bevel=0))
        # upper arm, elbow and hydraulic pistons
        p.append(rod((sx * 1.5, 0.05, 1.7), (sx * 1.75, -0.25, 0.95), 0.24, FRAME, verts=14))
        p.append(ball(0.3, (sx * 1.75, -0.25, 0.92), FRAME))
        p.append(rod((sx * 1.2, -0.25, 1.85), (sx * 1.55, -0.55, 0.95), 0.05, CHROME))
        p.append(rod((sx * 1.95, 0.3, 1.8), (sx * 1.95, -0.2, 1.0), 0.05, CHROME))
        # forearm cannon pointing down the arena
        p.append(smooth(cyl(0.36, 1.5, (sx * 1.8, -1.0, 0.8), WHITE, rot=(math.radians(84), 0, 0), verts=24, bevel=0.04)))
        p.append(cyl(0.28, 0.25, (sx * 1.8, -1.78, 0.72), FRAME, rot=(math.radians(84), 0, 0), verts=24))
        p.append(torus(0.22, 0.05, (sx * 1.8, -1.92, 0.71), PINK, rot=(math.radians(84), 0, 0)))
        p.append(cyl(0.16, 0.08, (sx * 1.8, -1.93, 0.71), M.rubber, rot=(math.radians(84), 0, 0), verts=16, bevel=0))
        for band in (-0.6, -0.15): p.append(cyl(0.38, 0.1, (sx * 1.8, band - 0.35, 0.85 + band * 0.1), FRAME, rot=(math.radians(84), 0, 0), verts=24, bevel=0.01))
        p.append(box((0.1, 1.0, 0.05), (sx * 1.8, -1.05, 1.17), PINK, rot=(math.radians(-6), 0, 0), bevel=0))
        # exhaust stacks and hoses on the back
        p.append(cyl(0.14, 0.9, (sx * 0.7, 0.75, 2.3), FRAME, rot=(math.radians(-15), 0, 0), verts=14))
        p.append(cyl(0.1, 0.06, (sx * 0.7, 0.87, 2.74), M.red, rot=(math.radians(-15), 0, 0), verts=14, bevel=0))
        p.extend(K.pipe_run([(sx * 0.9, 0.6, 1.4), (sx * 1.3, 0.75, 1.2), (sx * 1.7, 0.4, 1.0)], r=0.06))
    return p

# ---------- the strike leader: seen from behind, facing the mech (+Y) ----------

def leader():
    p = []
    for side in (-1, 1):
        p.append(rod((side * 0.12, 0, 0.1), (side * 0.13, 0.02, 0.48), 0.075, SUIT_D, verts=10))  # shins
        p.append(rod((side * 0.13, 0.02, 0.48), (side * 0.12, 0.0, 0.82), 0.085, SUIT_W, verts=10))  # thighs
        p.append(box((0.15, 0.24, 0.1), (side * 0.12, 0.04, 0.05), SUIT_D, bevel=0.03))  # boots
        p.append(ball(0.075, (side * 0.13, -0.01, 0.48), SUIT_R, segs=12))  # knee pads
    p.append(box((0.4, 0.25, 0.2), (0, 0, 0.88), SUIT_D, bevel=0.05))  # hips
    p.append(box((0.46, 0.3, 0.45), (0, 0, 1.18), SUIT_W, bevel=0.09))  # torso
    p.append(box((0.48, 0.06, 0.14), (0, -0.16, 1.12), SUIT_R, bevel=0.02))  # back stripe
    p.append(box((0.32, 0.18, 0.36), (0, -0.22, 1.2), SUIT_R, bevel=0.06))  # pack
    p.append(cyl(0.05, 0.03, (0, -0.32, 1.28), VISOR, rot=(math.radians(90), 0, 0), verts=12, bevel=0))  # partner uplink light
    for side in (-1, 1):
        p.append(ball(0.12, (side * 0.3, 0, 1.36), SUIT_R, segs=14))  # shoulders
        p.append(rod((side * 0.3, 0, 1.32), (side * 0.2, 0.25, 1.1), 0.06, SUIT_W, verts=10))
        p.append(rod((side * 0.2, 0.25, 1.1), (side * 0.06, 0.42, 1.12), 0.055, SUIT_D, verts=10))
    p.append(ball(0.23, (0, 0.02, 1.62), SUIT_W, segs=20))  # helmet
    p.append(box((0.06, 0.42, 0.05), (0, -0.02, 1.84), SUIT_R, bevel=0.02))  # crest stripe
    p.append(box((0.3, 0.08, 0.1), (0, 0.2, 1.62), VISOR, bevel=0.02))
    # rifle held towards the mech
    p.append(box((0.08, 0.62, 0.1), (0, 0.55, 1.12), SUIT_D, bevel=0.02))
    p.append(box((0.06, 0.2, 0.14), (0, 0.35, 1.03), SUIT_D, bevel=0.015))
    p.append(cyl(0.035, 0.05, (0, 0.88, 1.13), MUZZLE, rot=(math.radians(90), 0, 0), verts=10, bevel=0))
    return p

# ---------- the AI partner drone ----------

def partner():
    p = []
    p.append(ball(0.32, (0, 0, 0), GOLD, scale=(1, 1, 0.85), segs=28))
    p.append(smooth(torus(0.46, 0.035, (0, 0, -0.02), HALO)))  # light ring around its middle
    p.append(cyl(0.17, 0.08, (0, -0.28, 0.02), SUIT_D, rot=(math.radians(90), 0, 0), verts=24, bevel=0.01))
    p.append(ball(0.12, (0, -0.31, 0.02), WARM, scale=(1, 0.5, 1)))
    for side in (-1, 1):  # little thruster fins
        p.append(box((0.22, 0.18, 0.05), (side * 0.38, 0.05, -0.05), GOLD, rot=(0, side * math.radians(25), 0), bevel=0.02))
        p.append(cyl(0.05, 0.08, (side * 0.48, 0.05, -0.12), HALO, verts=10, bevel=0))
    p.append(rod((0, 0.05, 0.25), (0, 0.1, 0.48), 0.015, CHROME))
    p.append(ball(0.035, (0, 0.1, 0.48), WARM, segs=10))
    return p

def unit(name, parts, ortho, target, size, w, h, tilt, extra=(), variants=()):
    lights(extra=extra); camera(target, ortho=ortho, tilt=math.radians(tilt))
    render(name, w, h)
    for suffix, pairs in variants:
        swap(parts, pairs); render(name + suffix, w, h); swap(parts, {v: k for k, v in pairs.items()})
    K.export(finish(name, parts, size), os.path.join(OUT, name + '.glb'))
    clear()

unit('caretaker-mech', mech(), 6.6, (0, -0.4, 1.4), 1024, 1000, 820, 62,
     extra=[((0, -2.5, 3.2), (0.85, 0.95, 1.0), 160), ((0, -0.9, 2.6), (1.0, 0.2, 0.6), 25), ((0, -3, 0.5), (0.3, 0.8, 1.0), 60)],
     variants=[('-rage', {WHITE: CRIMSON, PINK: RAGE})])
unit('strike-leader', leader(), 2.3, (0, 0.15, 0.85), 512, 360, 420, 50, extra=[((0, -1.2, 2.2), (0.4, 0.8, 1.0), 25)])
unit('partner-drone', partner(), 1.35, (0, 0, 0), 512, 280, 280, 56, extra=[((0, -1.2, 0.8), (1.0, 0.8, 0.5), 18)])

# ---------- the arena: a Caretaker machine hall, backdrop only ----------

rng = random.Random(7)
FLOOR = worn('hall_floor', (0.05, 0.1, 0.11), rough=0.55, metal=0.6, wear=0.45, grime=0.5, scale=3, plates=True)
DECK = worn('hall_deck', (0.07, 0.13, 0.14), rough=0.5, metal=0.65, wear=0.5, grime=0.45, scale=4, plates=True)
WALL = worn('hall_wall', (0.05, 0.1, 0.11), rough=0.6, metal=0.55, wear=0.55, grime=0.5, scale=4, plates=True)
PIPE = worn('hall_pipe', (0.14, 0.24, 0.24), rough=0.4, metal=0.8, wear=0.6, grime=0.3, scale=6)
HAZARD = glow('hazard_strip', (1.0, 0.72, 0.08), 5)
TEAL = glow('hall_teal', (0.1, 0.9, 0.85), 3)
MARK = glow('floor_mark', (0.15, 0.7, 0.75), 0.6)
p = []
bpy.ops.mesh.primitive_plane_add(size=40, location=(0, 0, -0.35)); bpy.context.active_object.data.materials.append(FLOOR)
# the fighting floor: one rounded slab with hazard light strips down both sides and a faint ring marking
box((7.0, 11.5, 0.35), (0, -2.55, -0.175), DECK, bevel=0)
for side in (-1, 1):
    for k in range(7): box((0.08, 0.55, 0.05), (side * 3.32, -7.4 + k * 1.45, 0.02), HAZARD, bevel=0)
torus(2.7, 0.025, (0, -1.3, 0.005), MARK).scale = (1, 1, 0.2)
torus(2.1, 0.012, (0, -1.3, 0.005), MARK).scale = (1, 1, 0.2)
for x in (-1.8, 0, 1.8):
    for y in (-7.6, -5.8, -4.0, -2.2, -0.4, 1.4):
        box((1.6, 1.6, 0.01), (x, y, 0.004), FLOOR, bevel=0)  # deck plates, slightly proud
# the ledge the mech stands behind, hazard-striped, with railings each side
box((9.0, 0.6, 0.6), (0, 3.4, 0.3), WALL, bevel=0.03)
for k in range(18): box((0.32, 0.04, 0.08), (-4.25 + k * 0.5, 3.08, 0.52), HAZARD, bevel=0)
box((9.0, 3.2, 0.6), (0, 5.2, 0.3), DECK, bevel=0)
for side in (-1, 1):
    for o in K.railing(2.6, 0.5): o.location += Vector((side * 3.0, 3.25, 0.6)); o.scale = (1, 1, 1)
cyl(2.0, 0.18, (0, 5.0, 0.69), WALL, verts=48)
torus(2.0, 0.05, (0, 5.0, 0.79), M.red)
# back wall: a great vent behind the mech, pipes and teal status lights
box((16, 0.8, 7), (0, 7.4, 3.0), WALL, bevel=0.05)
torus(2.6, 0.35, (0, 6.95, 3.4), PIPE, rot=(math.radians(90), 0, 0))
cyl(2.3, 0.2, (0, 7.0, 3.4), FLOOR, rot=(math.radians(90), 0, 0), verts=48)
for r in (1.6, 1.0): torus(r, 0.08, (0, 6.88, 3.4), PIPE, rot=(math.radians(90), 0, 0))
cyl(0.45, 0.1, (0, 6.88, 3.4), M.red, rot=(math.radians(90), 0, 0), verts=32, bevel=0)
for x in (-6.5, -5.2, 5.2, 6.5): cyl(0.28, 7, (x, 6.8, 3.0), PIPE, verts=16)
for x in (-4.2, 4.2):
    for z in (1.4, 2.4, 3.4): box((0.9, 0.05, 0.12), (x, 6.98, z), TEAL, bevel=0)
# side walls: stacked machinery, tanks and pipe runs, receding into the dark
for side in (-1, 1):
    y = -9.0
    while y < 7:
        d = rng.uniform(1.2, 2.4); hgt = rng.uniform(1.4, 4.2); x = side * rng.uniform(4.6, 5.4)
        box((rng.uniform(1.2, 2.0), d, hgt), (x, y + d / 2, hgt / 2 - 0.35), rng.choice([WALL, WALL, PIPE]), bevel=0.04)
        if rng.random() < 0.6: box((0.05, d * 0.5, 0.1), (x - side * 0.65, y + d / 2, hgt * 0.6), rng.choice([TEAL, TEAL, M.red]), bevel=0)
        if rng.random() < 0.35: cyl(0.5, hgt * 0.8, (x + side * 0.3, y + d / 2, hgt * 0.4), PIPE, verts=20)
        y += d + rng.uniform(0.05, 0.3)
    for z in (0.3, 0.9):
        K.pipe_run([(side * 3.9, -9, z), (side * 3.9, 2.8, z), (side * 4.3, 3.4, z + 0.6), (side * 4.3, 7, z + 0.6)], r=0.12)
for o in bpy.context.scene.objects:
    if o.type == 'MESH' and o.data.materials and o.data.materials[0] in (M.steel,): o.data.materials[0] = PIPE
bpy.ops.object.light_add(type='SPOT', location=(0, -2.5, 7)); spot = bpy.context.active_object
spot.data.energy = 1500; spot.data.spot_size = math.radians(70); spot.data.spot_blend = 0.8; spot.data.color = (0.75, 0.95, 1.0)
lights(key=0.6, extra=[
    ((0, 5.0, 3.5), (1.0, 0.25, 0.5), 900),   # the mech's glow spilling over the ledge
    ((-3.6, -1, 2.5), (0.2, 0.8, 1.0), 260), ((3.6, -1, 2.5), (0.2, 0.8, 1.0), 260),
    ((0, -2, 5), (0.7, 0.9, 1.0), 700)])
camera((0, -0.4, 0), lens=30, dist=15.5)
render('leviathan-arena', 1080, 1620, samples=96, transparent=False)
print('NWO_LEVIATHAN_COMPLETE')
