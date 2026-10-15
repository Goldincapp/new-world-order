"""Shared Blender helpers for the operation art (sprites and backdrops): one camera angle family, the same lights,
and render settings, so every operation looks like part of one game. Builds on wl_kit's materials and parts.

Factions keep one palette everywhere: the player red and white, Lumen warm gold, the Caretaker white with magenta
light, raiders rust and orange.
"""
import bpy, math, os
from mathutils import Vector, Euler
import wl_kit as K
from wl_kit import M, worn, glow

PREVIEW = False  # set by the calling script; quick low-sample renders while iterating
RAW = '.'


def setup(raw, preview):
    global PREVIEW, RAW
    RAW, PREVIEW = raw, preview
    os.makedirs(raw, exist_ok=True)
    K.reset(); M.build()
    sc = bpy.context.scene
    sc.view_settings.view_transform = 'Standard'
    sc.cycles.use_denoising = True


def ball(r, loc, mat, scale=(1, 1, 1), segs=24):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=r, location=loc, segments=segs, ring_count=segs // 2)
    o = bpy.context.active_object; o.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    bpy.ops.object.shade_smooth(); o.data.materials.append(mat); return o


def clear():
    for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)


def lights(key=3.0, tint=(0.55, 0.85, 1.0), extra=(), world=(0.05, 0.09, 0.11)):
    w = bpy.data.worlds.get('w') or bpy.data.worlds.new('w'); bpy.context.scene.world = w; w.use_nodes = True
    bg = w.node_tree.nodes['Background']; bg.inputs['Color'].default_value = (*world, 1); bg.inputs['Strength'].default_value = 0.6
    bpy.ops.object.light_add(type='SUN', rotation=(math.radians(38), math.radians(-18), math.radians(-30)))
    s = bpy.context.active_object.data; s.energy = key; s.angle = math.radians(4); s.color = (1.0, 0.95, 0.88)
    bpy.ops.object.light_add(type='SUN', rotation=(math.radians(-60), math.radians(25), math.radians(20)))
    r = bpy.context.active_object.data; r.energy = key * 0.7; r.color = tint
    for loc, col, power in extra:
        bpy.ops.object.light_add(type='POINT', location=loc); p = bpy.context.active_object.data; p.energy = power; p.color = col; p.shadow_soft_size = 0.4


def camera(target, tilt_deg, ortho=None, lens=35, dist=30):
    """tilt_deg is the angle down from straight overhead: 0 for sprites the game rotates, about 50 for units seen
    from the front, 34 for arenas."""
    d = bpy.data.cameras.new('cam')
    if ortho: d.type = 'ORTHO'; d.ortho_scale = ortho
    else: d.lens = lens
    d.clip_end = 200
    cam = bpy.data.objects.new('cam', d); bpy.context.scene.collection.objects.link(cam); bpy.context.scene.camera = cam
    rot = Euler((math.radians(tilt_deg), 0, 0)); cam.rotation_euler = rot
    cam.location = Vector(target) - (rot.to_matrix() @ Vector((0, 0, -1))) * dist
    return cam


def render(name, w, h, samples=64, transparent=True):
    sc = bpy.context.scene
    sc.render.film_transparent = transparent
    sc.render.resolution_x, sc.render.resolution_y = (w // 2, h // 2) if PREVIEW else (w, h)
    sc.cycles.samples = 12 if PREVIEW else samples
    sc.render.image_settings.file_format = 'PNG'; sc.render.image_settings.color_mode = 'RGBA'
    sc.render.filepath = os.path.join(RAW, name + '.png')
    bpy.ops.render.render(write_still=True); print('rendered', sc.render.filepath)


def sprite(name, build, ortho, target, w, h, tilt_deg, extra=()):
    """Builds a unit, renders it on a transparent background and clears the scene for the next one."""
    build(); lights(extra=extra); camera(target, tilt_deg, ortho=ortho); render(name, w, h); clear()
