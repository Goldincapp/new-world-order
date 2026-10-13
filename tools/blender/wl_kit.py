"""Shared Blender kit for New World Order's Wasteland art: worn materials (with baked bump detail),
modelling helpers, a library of detail parts, and baking/export.

Coordinates: Blender is Z-up; glTF export turns that into the game's Y-up (Blender -Y becomes the game's +Z).
Wall-mounted parts are built facing -Y at the origin and then turned and moved into place with `put`.
"""
import bpy, bmesh, math, random, os
from mathutils import Vector, Matrix

scene = bpy.context.scene

def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    global scene
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'; scene.cycles.device = 'CPU'

# ---------- materials ----------

def _mix(nt, a, b, fac, blend='MIX'):
    m = nt.nodes.new('ShaderNodeMix'); m.data_type = 'RGBA'; m.blend_type = blend
    if isinstance(fac, (int, float)): m.inputs[0].default_value = fac
    else: nt.links.new(fac, m.inputs[0])
    for idx, v in ((6, a), (7, b)):
        if isinstance(v, tuple): m.inputs[idx].default_value = (*v, 1)
        else: nt.links.new(v, m.inputs[idx])
    return m.outputs[2]

def _ramp(nt, src, stops):
    r = nt.nodes.new('ShaderNodeValToRGB'); nt.links.new(src, r.inputs['Fac'])
    els = r.color_ramp.elements
    while len(els) < len(stops): els.new(0.5)
    for el, (pos, col) in zip(els, stops):
        el.position = pos; el.color = (*col, 1)
    return r.outputs['Color']

def _coord(nt):
    return nt.nodes.new('ShaderNodeTexCoord').outputs['Object']

def _noise(nt, scale, detail=8, vec=None):
    n = nt.nodes.new('ShaderNodeTexNoise'); n.inputs['Scale'].default_value = scale; n.inputs['Detail'].default_value = detail
    nt.links.new(vec or _coord(nt), n.inputs['Vector'])
    return n.outputs['Fac']

def _uv_wall(nt):
    """(x + y, z): runs along a wall whichever way it faces, for courses, seams and corrugation."""
    sep = nt.nodes.new('ShaderNodeSeparateXYZ'); nt.links.new(_coord(nt), sep.inputs['Vector'])
    add = nt.nodes.new('ShaderNodeMath'); nt.links.new(sep.outputs['X'], add.inputs[0]); nt.links.new(sep.outputs['Y'], add.inputs[1])
    v = nt.nodes.new('ShaderNodeCombineXYZ'); nt.links.new(add.outputs[0], v.inputs['X']); nt.links.new(sep.outputs['Z'], v.inputs['Y'])
    return v.outputs['Vector'], sep

def worn(name, base, rough=0.85, metal=0.0, rust=None, char=0.0, wear=0.55, grime=0.45, scale=7,
         bricks=False, plates=False, corrugated=False, pitted=0.25, cracks=False, stripes=None):
    """A worn surface: mottled colour, stains, dirt splashed up from the ground, lighter worn edges, optional rust and soot,
    dirt in the crevices; plus bump detail (pitting, cracks, brick courses, plate seams or corrugation) for the normal bake."""
    m = bpy.data.materials.new(name); m.use_nodes = True; nt = m.node_tree
    bsdf = nt.nodes['Principled BSDF']
    bsdf.inputs['Roughness'].default_value = rough; bsdf.inputs['Metallic'].default_value = metal
    dark = tuple(c * 0.6 for c in base); light = tuple(min(1, c * 1.22) for c in base)
    col = _ramp(nt, _noise(nt, scale), [(0.32, dark), (0.68, light)])
    if stripes:  # diagonal hazard bands
        uv, _ = _uv_wall(nt)
        w = nt.nodes.new('ShaderNodeTexWave'); w.bands_direction = 'DIAGONAL'; w.inputs['Scale'].default_value = stripes; w.inputs['Distortion'].default_value = 0
        nt.links.new(uv, w.inputs['Vector'])
        band = _ramp(nt, w.outputs['Fac'], [(0.49, (0, 0, 0)), (0.51, (1, 1, 1))])
        col = _mix(nt, (0.05, 0.05, 0.05), col, band)
    stains = _ramp(nt, _noise(nt, 1.6, 3), [(0.42, (0.55, 0.53, 0.5)), (0.62, (1, 1, 1))])
    col = _mix(nt, col, stains, 1.0, 'MULTIPLY')
    sep = nt.nodes.new('ShaderNodeSeparateXYZ'); nt.links.new(_coord(nt), sep.inputs['Vector'])
    dirt = _ramp(nt, sep.outputs['Z'], [(0.0, (0.5, 0.47, 0.43)), (0.35, (1, 1, 1))])
    col = _mix(nt, col, dirt, 1.0, 'MULTIPLY')
    height = None  # what the bump reads
    if bricks:
        uv, _ = _uv_wall(nt)
        bt = nt.nodes.new('ShaderNodeTexBrick'); nt.links.new(uv, bt.inputs['Vector'])
        bt.inputs['Scale'].default_value = 7; bt.inputs['Mortar Size'].default_value = 0.012
        bt.inputs['Color1'].default_value = (1, 1, 1, 1); bt.inputs['Color2'].default_value = (0.78, 0.74, 0.72, 1); bt.inputs['Mortar'].default_value = (0.5, 0.48, 0.45, 1)
        col = _mix(nt, col, bt.outputs['Color'], 1.0, 'MULTIPLY'); height = bt.outputs['Fac']
    if plates:  # bolted armour plates: seams between big panels
        uv, _ = _uv_wall(nt)
        bt = nt.nodes.new('ShaderNodeTexBrick'); nt.links.new(uv, bt.inputs['Vector'])
        bt.inputs['Scale'].default_value = 2.2; bt.inputs['Mortar Size'].default_value = 0.006; bt.inputs['Row Height'].default_value = 0.5
        bt.inputs['Color1'].default_value = (1, 1, 1, 1); bt.inputs['Color2'].default_value = (0.86, 0.86, 0.88, 1); bt.inputs['Mortar'].default_value = (0.25, 0.25, 0.26, 1)
        col = _mix(nt, col, bt.outputs['Color'], 1.0, 'MULTIPLY'); height = bt.outputs['Fac']
    if corrugated:
        uv, _ = _uv_wall(nt)
        w = nt.nodes.new('ShaderNodeTexWave'); w.wave_profile = 'SIN'; w.inputs['Scale'].default_value = 26; w.inputs['Distortion'].default_value = 0
        nt.links.new(uv, w.inputs['Vector']); height = w.outputs['Fac']
        col = _mix(nt, col, _ramp(nt, w.outputs['Fac'], [(0, (0.8, 0.8, 0.8)), (1, (1, 1, 1))]), 1.0, 'MULTIPLY')
    if cracks:
        vor = nt.nodes.new('ShaderNodeTexVoronoi'); vor.feature = 'DISTANCE_TO_EDGE'; vor.inputs['Scale'].default_value = 3.5
        nt.links.new(_coord(nt), vor.inputs['Vector'])
        crack = _ramp(nt, vor.outputs['Distance'], [(0.0, (0.25, 0.24, 0.22)), (0.02, (1, 1, 1))])
        col = _mix(nt, col, crack, 1.0, 'MULTIPLY')
    if rust:
        mask = _ramp(nt, _noise(nt, 2.2, 4), [(0.5, (0, 0, 0)), (0.62, (1, 1, 1))])
        col = _mix(nt, col, rust, mask)
    if char > 0:
        soot = _ramp(nt, _noise(nt, 3.5, 6), [(0.42, (0, 0, 0)), (0.6, (1, 1, 1))])
        col = _mix(nt, col, (0.05, 0.045, 0.04), soot)
    geo = nt.nodes.new('ShaderNodeNewGeometry')
    edge = _ramp(nt, geo.outputs['Pointiness'], [(0.5, (0, 0, 0)), (0.56, (wear, wear, wear))])
    col = _mix(nt, col, tuple(min(1, c * 1.45 + 0.08) for c in base), edge)
    ao = nt.nodes.new('ShaderNodeAmbientOcclusion'); ao.samples = 16; ao.inputs['Distance'].default_value = 0.5
    shade = _ramp(nt, ao.outputs['AO'], [(0.0, (grime, grime * 0.95, grime * 0.9)), (1.0, (1, 1, 1))])
    col = _mix(nt, col, shade, 1.0, 'MULTIPLY')
    nt.links.new(col, bsdf.inputs['Base Color'])
    # bump: fine pitting everywhere, plus the pattern's own relief
    bump = nt.nodes.new('ShaderNodeBump'); bump.inputs['Strength'].default_value = 0.6; bump.inputs['Distance'].default_value = 0.02
    pit = _noise(nt, 60, 4)
    if height is not None:
        mx = nt.nodes.new('ShaderNodeMath'); mx.operation = 'MULTIPLY_ADD'
        nt.links.new(pit, mx.inputs[0]); mx.inputs[1].default_value = pitted; nt.links.new(height, mx.inputs[2])
        nt.links.new(mx.outputs[0], bump.inputs['Height'])
    else:
        sc = nt.nodes.new('ShaderNodeMath'); sc.operation = 'MULTIPLY'; nt.links.new(pit, sc.inputs[0]); sc.inputs[1].default_value = pitted
        nt.links.new(sc.outputs[0], bump.inputs['Height'])
    nt.links.new(bump.outputs['Normal'], bsdf.inputs['Normal'])
    m['rough'] = rough; m['metal'] = metal
    return m

def glow(name, color, strength=2.5):
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes['Principled BSDF']
    b.inputs['Base Color'].default_value = (*color, 1)
    b.inputs['Emission Color'].default_value = (*color, 1); b.inputs['Emission Strength'].default_value = strength
    return m

class M:
    """The palette, built once per run."""
    @classmethod
    def build(cls):
        cls.concrete = worn('concrete', (0.3, 0.3, 0.295), scale=9, cracks=True, grime=0.38)
        cls.conc_dark = worn('concrete_dark', (0.25, 0.25, 0.255), scale=9, cracks=True)
        cls.brick = worn('brick', (0.36, 0.2, 0.14), scale=14, wear=0.3, bricks=True)
        cls.armour = worn('armour', (0.2, 0.21, 0.23), rough=0.55, metal=0.6, wear=0.8, scale=5, plates=True, rust=(0.32, 0.17, 0.09))
        cls.scrap = worn('scrap', (0.34, 0.27, 0.2), rough=0.8, metal=0.4, rust=(0.42, 0.21, 0.1), corrugated=True, wear=0.45)
        cls.tin = worn('tin', (0.4, 0.41, 0.42), rough=0.6, metal=0.6, rust=(0.4, 0.2, 0.1), corrugated=True, wear=0.6)
        cls.steel = worn('steel', (0.24, 0.24, 0.26), rough=0.5, metal=0.7, wear=0.8, rust=(0.3, 0.16, 0.08))
        cls.hazard = worn('hazard', (0.85, 0.62, 0.1), rough=0.6, metal=0.2, wear=0.6, stripes=3.2, rust=(0.35, 0.18, 0.08))
        cls.yellow = worn('yellow', (0.8, 0.58, 0.1), rough=0.6, metal=0.2, wear=0.6, rust=(0.35, 0.18, 0.08))
        cls.wood = worn('wood', (0.34, 0.24, 0.15), rough=0.9, wear=0.3, scale=18, pitted=0.4)
        cls.sandbag = worn('sandbag', (0.45, 0.4, 0.3), rough=1, wear=0.2, scale=22, pitted=0.5)
        cls.rubber = worn('rubber', (0.07, 0.07, 0.075), rough=0.95, wear=0.15)
        cls.glass = worn('dark_glass', (0.05, 0.06, 0.07), rough=0.3, metal=0.2, wear=0.1)
        cls.water = worn('tank', (0.3, 0.32, 0.3), rough=0.6, metal=0.5, rust=(0.4, 0.2, 0.1), wear=0.6)
        cls.lamp = glow('lamp', (1.0, 0.85, 0.55), 3)
        cls.red = glow('red_eye', (1.0, 0.04, 0.02), 2.5)

# ---------- modelling helpers ----------

def _bevel(o, w):
    if w:
        mod = o.modifiers.new('bevel', 'BEVEL'); mod.width = w; mod.segments = 2; mod.limit_method = 'ANGLE'
    return o

def box(size, loc, mat, rot=(0, 0, 0), bevel=0.015):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    o = bpy.context.active_object; o.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    o.data.materials.append(mat)
    return _bevel(o, bevel)

def cyl(r, depth, loc, mat, rot=(0, 0, 0), verts=12, bevel=0.008):
    bpy.ops.mesh.primitive_cylinder_add(radius=r, depth=depth, location=loc, rotation=rot, vertices=verts)
    o = bpy.context.active_object; o.data.materials.append(mat)
    return _bevel(o, bevel)

def torus(R, r, loc, mat, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_torus_add(major_radius=R, minor_radius=r, location=loc, rotation=rot, major_segments=16, minor_segments=8)
    o = bpy.context.active_object; o.data.materials.append(mat); return o

def rod(a, b, r, mat, verts=8):
    """A cylinder from point a to point b."""
    a, b = Vector(a), Vector(b); d = b - a
    o = cyl(r, d.length, (a + b) / 2, mat, verts=verts, bevel=0)
    o.rotation_euler = d.to_track_quat('Z', 'Y').to_euler()
    return o

def put(parts, loc=(0, 0, 0), rz=0.0):
    """Turn a group of parts about the origin by rz, then move it to loc."""
    R = Matrix.Rotation(rz, 4, 'Z'); T = Matrix.Translation(loc)
    for o in parts: o.matrix_world = T @ R @ o.matrix_world
    return parts

def join(objs, name):
    dg = bpy.context.evaluated_depsgraph_get()
    for o in objs:
        if o.modifiers:
            me = bpy.data.meshes.new_from_object(o.evaluated_get(dg)); o.modifiers.clear(); o.data = me
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    o = bpy.context.active_object; o.name = name
    return o

# ---------- the detail kit (built facing -Y at the origin) ----------

def window(w=0.32, h=0.38, bars=3, depth=0.06):
    p = [box((w, 0.03, h), (0, 0.0, 0), M.glass, bevel=0)]
    t = 0.035
    p += [box((w + 2 * t, depth, t), (0, -depth / 2, h / 2 + t / 2), M.steel), box((w + 2 * t, depth, t * 1.4), (0, -depth / 2 - 0.01, -h / 2 - t / 2), M.concrete),
          box((t, depth, h), (-w / 2 - t / 2, -depth / 2, 0), M.steel), box((t, depth, h), (w / 2 + t / 2, -depth / 2, 0), M.steel)]
    for k in range(bars):
        x = -w / 2 + (k + 1) * w / (bars + 1)
        p.append(cyl(0.009, h, (x, -0.02, 0), M.steel, verts=6, bevel=0))
    return p

def door(w=0.42, h=0.72, hazard=True):
    p = [box((w, 0.05, h), (0, -0.01, h / 2), M.hazard if hazard else M.armour)]
    p += [box((w + 0.1, 0.07, 0.06), (0, -0.02, h + 0.03), M.steel), box((0.05, 0.07, h), (-w / 2 - 0.025, -0.02, h / 2), M.steel), box((0.05, 0.07, h), (w / 2 + 0.025, -0.02, h / 2), M.steel)]
    p.append(box((0.04, 0.03, 0.12), (w * 0.3, -0.05, h * 0.5), M.steel, bevel=0.005))
    return p

def ac_unit():
    p = [box((0.3, 0.22, 0.2), (0, 0, 0.1), M.tin)]
    p.append(cyl(0.08, 0.02, (0, -0.115, 0.1), M.steel, rot=(math.pi / 2, 0, 0), verts=16))
    for k in range(4): p.append(box((0.16, 0.006, 0.008), (0, -0.128, 0.04 + k * 0.04), M.steel, bevel=0))
    return p

def floodlight(h=0.6):
    p = [cyl(0.02, h, (0, 0, h / 2), M.steel, verts=8)]
    p.append(box((0.14, 0.08, 0.1), (0, -0.03, h + 0.02), M.steel, rot=(0.4, 0, 0)))
    p.append(box((0.11, 0.01, 0.07), (0, -0.075, h + 0.0), M.lamp, rot=(0.4, 0, 0), bevel=0))
    return p

def ladder(h=1.0, w=0.18):
    p = [cyl(0.012, h, (-w / 2, 0, h / 2), M.steel, verts=6, bevel=0), cyl(0.012, h, (w / 2, 0, h / 2), M.steel, verts=6, bevel=0)]
    for k in range(int(h / 0.12)): p.append(cyl(0.008, w, (0, 0, 0.08 + k * 0.12), M.steel, rot=(0, math.pi / 2, 0), verts=6, bevel=0))
    return p

def railing(length=1.0, h=0.22):
    n = max(2, int(length / 0.3)); p = []
    for k in range(n + 1): p.append(cyl(0.01, h, (-length / 2 + k * length / n, 0, h / 2), M.steel, verts=6, bevel=0))
    p.append(cyl(0.01, length, (0, 0, h), M.steel, rot=(0, math.pi / 2, 0), verts=6, bevel=0))
    p.append(cyl(0.008, length, (0, 0, h * 0.5), M.steel, rot=(0, math.pi / 2, 0), verts=6, bevel=0))
    return p

def pipe_run(points, r=0.025):
    p = []
    for a, b in zip(points, points[1:]): p.append(rod(a, b, r, M.steel))
    for q in points[1:-1]:
        bpy.ops.mesh.primitive_uv_sphere_add(radius=r * 1.25, location=q, segments=10, ring_count=6); s = bpy.context.active_object; s.data.materials.append(M.steel); p.append(s)
    return p

def sandbag(loc, rz=0.0):
    o = box((0.2, 0.11, 0.07), loc, M.sandbag, rot=(0, 0, rz), bevel=0.03)
    return o

def sandbag_wall(length, rows=3):
    p = []
    for r in range(rows):
        n = int(length / 0.19); off = 0.095 if r % 2 else 0
        for k in range(n):
            x = -length / 2 + off + k * 0.19 + 0.095
            if x > length / 2 - 0.05: continue
            p.append(sandbag((x, random.uniform(-0.01, 0.01), 0.035 + r * 0.065), random.uniform(-0.12, 0.12)))
    return p

def crate(s=0.22):
    p = [box((s, s, s), (0, 0, s / 2), M.wood, bevel=0.008)]
    for z in (0.02, s - 0.02):
        p.append(box((s + 0.012, s + 0.012, 0.025), (0, 0, z), M.wood, bevel=0.004))
    p.append(box((s + 0.012, 0.025, s * 1.1), (0, 0, s / 2), M.wood, rot=(0, 0.75, 0), bevel=0.003))
    return p

def barrel(r=0.07, h=0.2, mat=None):
    p = [cyl(r, h, (0, 0, h / 2), mat or M.water, verts=14, bevel=0.01)]
    for z in (0.04, h - 0.04): p.append(torus(r, 0.008, (0, 0, z), M.steel))
    return p

def tyre(r=0.09):
    return [torus(r, 0.035, (0, 0, 0.035), M.rubber)]

def antenna(h=1.0):
    p = [cyl(0.015, h, (0, 0, h / 2), M.steel, verts=6, bevel=0)]
    for k, w in enumerate((0.3, 0.22, 0.14)):
        p.append(cyl(0.008, w, (0, 0, h * (0.55 + k * 0.15)), M.steel, rot=(0, math.pi / 2, 0), verts=6, bevel=0))
    p.append(cyl(0.025, 0.04, (0, 0, h), M.red, verts=8, bevel=0))
    return p

def water_tank(r=0.2, h=0.32, legs=0.3):
    p = [cyl(r, h, (0, 0, legs + h / 2), M.water, verts=16, bevel=0.02)]
    for a in (0.785, 2.356, 3.927, 5.498):
        p.append(cyl(0.015, legs, (math.cos(a) * r * 0.8, math.sin(a) * r * 0.8, legs / 2), M.steel, verts=6, bevel=0))
    p.append(torus(r, 0.01, (0, 0, legs + h * 0.3), M.steel)); p.append(torus(r, 0.01, (0, 0, legs + h * 0.75), M.steel))
    return p

def armour_plates(width, height, rng):
    """Scrap and armour plates bolted over a wall face, at slightly different depths and sizes."""
    p = []
    x = -width / 2
    while x < width / 2 - 0.1:
        w = rng.uniform(0.25, 0.5); w = min(w, width / 2 - x)
        h = rng.uniform(height * 0.35, height * 0.8); z = rng.uniform(0.05, height - h)
        p.append(box((w, 0.025, h), (x + w / 2, -0.015 - rng.uniform(0, 0.012), z + h / 2), rng.choice([M.armour, M.scrap, M.tin]), rot=(0, rng.uniform(-0.03, 0.03), 0), bevel=0.006))
        x += w + rng.uniform(0.02, 0.15)
    return p

# ---------- baking and export ----------

def bake(o, size, tex_dir, normal=True):
    """Bakes colour (with crevice dirt) and, optionally, the bump detail as a normal map, into one material."""
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.015)
    bpy.ops.object.mode_set(mode='OBJECT')
    mats = [s.material for s in o.material_slots]
    out = {}
    passes = [('color', 'DIFFUSE')] + ([('normal', 'NORMAL')] if normal else [])
    for key, kind in passes:
        img = bpy.data.images.new(f'{o.name}_{key}', size, size)
        if key == 'normal': img.colorspace_settings.name = 'Non-Color'
        nodes = []
        for m in mats:
            n = m.node_tree.nodes.new('ShaderNodeTexImage'); n.image = img; m.node_tree.nodes.active = n; nodes.append((m, n))
        scene.cycles.samples = 32
        b = scene.render.bake; b.margin = 6
        if kind == 'DIFFUSE':
            b.use_pass_direct = False; b.use_pass_indirect = False; b.use_pass_color = True
            bpy.ops.object.bake(type='DIFFUSE', pass_filter={'COLOR'})
        else:
            b.normal_space = 'TANGENT'
            bpy.ops.object.bake(type='NORMAL')
        img.filepath_raw = os.path.join(tex_dir, f'{o.name}_{key}.png'); img.file_format = 'PNG'; img.save()
        for m, n in nodes: m.node_tree.nodes.remove(n)
        out[key] = img
    rough = sum(m.get('rough', 0.85) for m in mats) / len(mats); metal = max(m.get('metal', 0) for m in mats) * 0.5
    nm = bpy.data.materials.new(o.name + '_baked'); nm.use_nodes = True; nt = nm.node_tree
    pb = nt.nodes['Principled BSDF']; t = nt.nodes.new('ShaderNodeTexImage'); t.image = out['color']
    nt.links.new(t.outputs['Color'], pb.inputs['Base Color'])
    if normal:
        tn = nt.nodes.new('ShaderNodeTexImage'); tn.image = out['normal']; tn.image.colorspace_settings.name = 'Non-Color'
        nmap = nt.nodes.new('ShaderNodeNormalMap'); nt.links.new(tn.outputs['Color'], nmap.inputs['Color']); nt.links.new(nmap.outputs['Normal'], pb.inputs['Normal'])
    pb.inputs['Roughness'].default_value = rough; pb.inputs['Metallic'].default_value = metal
    o.data.materials.clear(); o.data.materials.append(nm)
    return o

def export(objs, path):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    try:
        bpy.ops.export_scene.gltf(filepath=path, export_format='GLB', use_selection=True, export_image_format='JPEG', export_apply=True)
    except TypeError:
        bpy.ops.export_scene.gltf(filepath=path, export_format='GLB', use_selection=True, export_apply=True)
    print('exported', path, os.path.getsize(path))
