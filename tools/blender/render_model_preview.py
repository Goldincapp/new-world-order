"""Render a clean turntable-style preview of an existing GLB.

Usage:
  blender --background --python tools/blender/render_model_preview.py -- input.glb output.png
"""

import math
import os
import sys

import bpy
from mathutils import Vector


def look_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def main():
    args = sys.argv[sys.argv.index("--") + 1 :]
    if len(args) != 2:
        raise SystemExit("Expected input GLB and output PNG")
    source, output = map(os.path.abspath, args)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=source)

    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if not meshes:
        raise SystemExit(f"No mesh objects found in {source}")

    corners = [obj.matrix_world @ Vector(corner) for obj in meshes for corner in obj.bound_box]
    lower = Vector((min(p.x for p in corners), min(p.y for p in corners), min(p.z for p in corners)))
    upper = Vector((max(p.x for p in corners), max(p.y for p in corners), max(p.z for p in corners)))
    center = (lower + upper) * 0.5
    span = max(upper.x - lower.x, upper.y - lower.y, upper.z - lower.z)

    root = bpy.data.objects.new("PreviewRoot", None)
    bpy.context.scene.collection.objects.link(root)
    for obj in list(bpy.context.scene.objects):
        if obj != root and obj.parent is None:
            obj.parent = root
    root.location = -center
    root.rotation_euler[2] = math.radians(-8)

    world = bpy.data.worlds.new("Preview World")
    bpy.context.scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.008, 0.014, 0.02, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.16

    bpy.ops.mesh.primitive_plane_add(size=span * 5, location=(0, 0, -(upper.z - lower.z) * 0.5 - 0.025))
    floor = bpy.context.object
    mat = bpy.data.materials.new("Preview Floor")
    mat.diffuse_color = (0.025, 0.04, 0.05, 1)
    mat.metallic = 0.55
    mat.roughness = 0.38
    floor.data.materials.append(mat)

    def area(name, location, energy, color, size):
        bpy.ops.object.light_add(type="AREA", location=location)
        light = bpy.context.object
        light.name = name
        light.data.energy = energy
        light.data.color = color
        light.data.shape = "DISK"
        light.data.size = size
        look_at(light, (0, 0, 0))

    area("Key", (span * 1.5, -span * 2.0, span * 1.7), 1100, (0.72, 0.88, 1.0), span * 1.3)
    area("Rim", (-span * 1.7, span * 0.7, span * 1.5), 950, (0.2, 0.65, 1.0), span)
    area("Warm Fill", (span * 1.2, span * 1.6, span * 0.7), 700, (1.0, 0.24, 0.12), span * 0.9)

    bpy.ops.object.camera_add(location=(span * 1.45, -span * 2.45, span * 1.18))
    camera = bpy.context.object
    look_at(camera, (0, 0, -span * 0.02))
    camera.data.lens = 58
    bpy.context.scene.camera = camera

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1100
    scene.render.resolution_y = 900
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = output
    scene.render.film_transparent = False
    scene.render.image_settings.color_mode = "RGBA"
    scene.view_settings.look = "AgX - Medium High Contrast"
    bpy.ops.render.render(write_still=True)
    print(f"MODEL_PREVIEW_COMPLETE:{output}")


if __name__ == "__main__":
    main()
