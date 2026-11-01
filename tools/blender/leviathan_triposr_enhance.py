"""Turn the raw TripoSR Leviathan scan into a presentable game prototype.

The source remains untouched. This script repairs and optimizes a copy, preserves
its vertex colour, adds authored mechanical details, creates a preview scene, and
exports a separate GLB and .blend working file.
"""

import math
import os
import traceback

import bmesh
import bpy
from mathutils import Vector


ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SOURCE = os.path.join(ROOT, "leviathan bot.glb")
OUT_DIR = os.path.join(ROOT, "client", "models", "units")
BLEND_OUT = os.path.join(OUT_DIR, "leviathan-triposr-enhanced.blend")
GLB_OUT = os.path.join(OUT_DIR, "leviathan-triposr-enhanced.glb")
PREVIEW_OUT = os.path.join(OUT_DIR, "leviathan-triposr-enhanced-preview.png")
LOG_OUT = os.path.join(ROOT, "artifacts", "leviathan-triposr-enhance.log")


def material(name, color, metallic=0.65, roughness=0.34, emission=None, strength=0.0):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1.0)
    mat.use_nodes = True
    shader = mat.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*color, 1.0)
    shader.inputs["Metallic"].default_value = metallic
    shader.inputs["Roughness"].default_value = roughness
    if emission:
        shader.inputs["Emission Color"].default_value = (*emission, 1.0)
        shader.inputs["Emission Strength"].default_value = strength
    return mat


def bevel(obj, width=0.045, segments=2):
    mod = obj.modifiers.new("Manufactured edge", "BEVEL")
    mod.width = width
    mod.segments = segments
    return obj


def cube(name, dims, loc, mat, rotation=(0, 0, 0), edge=0.04, parent=None):
    bpy.ops.mesh.primitive_cube_add(location=loc, rotation=rotation)
    obj = active_object()
    obj.name = name
    obj.dimensions = dims
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(mat)
    if edge:
        bevel(obj, edge)
    obj.parent = parent
    return obj


def cylinder(name, radius, depth, loc, mat, rotation=(0, 0, 0), vertices=20, parent=None):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices, radius=radius, depth=depth, location=loc, rotation=rotation
    )
    obj = active_object()
    obj.name = name
    obj.data.materials.append(mat)
    bevel(obj, min(radius * 0.12, 0.035), 2)
    obj.parent = parent
    return obj


def torus(name, major, minor, loc, mat, rotation=(math.pi / 2, 0, 0), parent=None):
    bpy.ops.mesh.primitive_torus_add(
        major_radius=major,
        minor_radius=minor,
        major_segments=28,
        minor_segments=8,
        location=loc,
        rotation=rotation,
    )
    obj = active_object()
    obj.name = name
    obj.data.materials.append(mat)
    obj.parent = parent
    return obj


def sphere(name, radius, loc, mat, parent=None):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=20, ring_count=12, radius=radius, location=loc)
    obj = active_object()
    obj.name = name
    obj.data.materials.append(mat)
    obj.parent = parent
    return obj


def strut(name, start, end, radius, mat, parent=None):
    start = Vector(start)
    end = Vector(end)
    direction = end - start
    midpoint = (start + end) * 0.5
    obj = cylinder(name, radius, direction.length, midpoint, mat, vertices=14, parent=parent)
    obj.rotation_euler = direction.to_track_quat("Z", "Y").to_euler()
    return obj


def point_camera(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def active_object():
    """Return Blender's active object without relying on the removed Context.object alias."""
    return bpy.context.view_layer.objects.active


def repair_scan():
    bpy.ops.import_scene.gltf(filepath=SOURCE)
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if not meshes:
        raise RuntimeError("The source GLB did not contain a mesh")

    bpy.ops.object.select_all(action="DESELECT")
    for obj in meshes:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    scan = active_object()
    scan.name = "Leviathan.ScanShell"
    scan.parent = None

    # Bake the imported hierarchy before measuring and orienting the shell.
    world_matrix = scan.matrix_world.copy()
    scan.parent = None
    scan.matrix_world = world_matrix
    bpy.context.view_layer.objects.active = scan
    scan.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)

    bm = bmesh.new()
    bm.from_mesh(scan.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.00008)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(scan.data)
    bm.free()
    scan.data.validate(clean_customdata=False)
    scan.data.update()

    # TripoSR uses a Y-up convention; glTF import should convert it to Blender Z-up.
    # Normalize to a six-metre-wide assault unit and set its lowest point on the floor.
    bpy.context.view_layer.update()
    max_dim = max(scan.dimensions)
    scale = 6.0 / max_dim if max_dim else 1.0
    scan.scale = (scale, scale, scale)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    corners = [scan.matrix_world @ Vector(corner) for corner in scan.bound_box]
    centre = sum(corners, Vector()) / 8.0
    scan.location.x -= centre.x
    scan.location.y -= centre.y
    scan.location.z -= min(v.z for v in corners)
    bpy.ops.object.transform_apply(location=True, rotation=False, scale=False)

    # Keep the generated shell intact in this authored source file. The scan is
    # compact enough for prototyping, and engine-specific LODs can be generated
    # later without using Blender operators that depend on an interactive context.

    for polygon in scan.data.polygons:
        polygon.use_smooth = True

    # Rebuild the scan material around its vertex-colour layer.
    scan.data.materials.clear()
    scan_mat = bpy.data.materials.new("Leviathan scan armour")
    scan_mat.use_nodes = True
    nodes = scan_mat.node_tree.nodes
    links = scan_mat.node_tree.links
    shader = nodes.get("Principled BSDF")
    shader.inputs["Metallic"].default_value = 0.62
    shader.inputs["Roughness"].default_value = 0.38
    if scan.data.color_attributes:
        attr_name = scan.data.color_attributes[0].name
        attr = nodes.new("ShaderNodeVertexColor")
        attr.layer_name = attr_name
        attr.label = "Preserved TripoSR colour"
        links.new(attr.outputs["Color"], shader.inputs["Base Color"])
    else:
        shader.inputs["Base Color"].default_value = (0.06, 0.045, 0.04, 1)
    scan.data.materials.append(scan_mat)
    return scan


def build_details(scan):
    armour = material("Forged black armour", (0.028, 0.035, 0.043), 0.86, 0.25)
    steel = material("Gunmetal mechanisms", (0.14, 0.17, 0.19), 0.9, 0.22)
    dark = material("Recessed mechanisms", (0.008, 0.011, 0.015), 0.7, 0.48)
    red = material("Enemy AI red", (0.22, 0.004, 0.003), 0.45, 0.24, (0.85, 0.008, 0.003), 3.2)
    amber = material("Weapon heat", (0.25, 0.018, 0.003), 0.38, 0.30, (0.8, 0.035, 0.005), 2.2)

    root = bpy.data.objects.new("Leviathan.Enhanced", None)
    bpy.context.collection.objects.link(root)
    scan.parent = root

    bpy.context.view_layer.update()
    dims = scan.dimensions
    width, depth, height = dims.x, dims.y, dims.z
    front_y = -depth * 0.52
    body_z = height * 0.52

    # Readable front optic and protected sensor housing.
    cylinder("Sensor.Housing", 0.46, 0.28, (0, front_y - 0.05, body_z + 0.20), armour,
             rotation=(math.pi / 2, 0, 0), vertices=28, parent=root)
    torus("Sensor.Guard", 0.35, 0.075, (0, front_y - 0.21, body_z + 0.20), steel, parent=root)
    cylinder("Sensor.Eye", 0.19, 0.08, (0, front_y - 0.28, body_z + 0.20), red,
             rotation=(math.pi / 2, 0, 0), vertices=28, parent=root)

    # A visible undercarriage ties the generated shell to the authored mechanisms.
    cube("Chassis.Spine", (width * 0.58, depth * 0.58, 0.34),
         (0, 0.08, 0.58), dark, edge=0.11, parent=root)

    # Side cannon pods strengthen the assault-bot read without replacing the scan.
    for side in (-1, 1):
        sx = side * width * 0.47
        pod_z = body_z * 0.95
        cube("Weapon.Mount", (0.78, 0.78, 0.46), (side * width * 0.38, front_y + 0.35, pod_z), steel,
             rotation=(0, side * 0.05, side * -0.03), edge=0.09, parent=root)
        strut("Weapon.Brace", (side * width * 0.27, front_y + 0.38, pod_z - 0.18),
              (sx, front_y + 0.22, pod_z - 0.18), 0.12, steel, parent=root)
        cube("Weapon.Pod", (0.92, 1.30, 0.82), (sx, front_y + 0.18, pod_z), armour,
             rotation=(0, side * 0.07, side * -0.04), edge=0.10, parent=root)
        cylinder("Weapon.Pivot", 0.30, 0.70, (sx - side * 0.30, front_y + 0.15, pod_z), steel,
                 rotation=(0, math.pi / 2, 0), vertices=20, parent=root)
        for barrel_index in range(3):
            bx = sx + (barrel_index - 1) * 0.20
            cylinder("Weapon.Barrel", 0.082, 1.42, (bx, front_y - 0.88, pod_z - 0.05), dark,
                     rotation=(math.pi / 2, 0, 0), vertices=12, parent=root)
            torus("Weapon.Muzzle", 0.09, 0.026, (bx, front_y - 1.60, pod_z - 0.05), amber, parent=root)
        cube("Weapon.Status", (0.11, 0.05, 0.45), (sx + side * 0.26, front_y - 0.48, pod_z + 0.10), red,
             edge=0.015, parent=root)

    # Six independently parented legs make a believable walker and provide clean
    # future rigging anchors. Their feet are broad enough to sell the bot's mass.
    for side in (-1, 1):
        for row, y in enumerate((-depth * 0.27, 0.03, depth * 0.29)):
            hip = (side * width * 0.36, y, 0.78)
            knee = (side * width * (0.50 + 0.025 * (row == 1)), y + (row - 1) * 0.06, 0.48)
            foot = (side * width * 0.61, y - 0.08, 0.16)
            sphere("Leg.Hip", 0.24, hip, armour, parent=root)
            strut("Leg.Upper", hip, knee, 0.14, steel, parent=root)
            sphere("Leg.Knee", 0.19, knee, red, parent=root)
            strut("Leg.Lower", knee, foot, 0.13, steel, parent=root)
            cube("Leg.Foot", (0.60, 0.72, 0.18), foot, armour,
                 rotation=(0, side * 0.06, 0), edge=0.06, parent=root)

    # Low, overlapping dorsal plates sit inside the irregular scan surface. This
    # adds manufactured repetition without looking like floating accessories.
    for index, y in enumerate((-depth * 0.24, 0, depth * 0.24)):
        cube("Dorsal.Plate", (width * (0.43 - index * 0.025), depth * 0.19, 0.16),
             (0, y, height * 0.79), armour,
             rotation=(0, (index - 1) * 0.025, 0), edge=0.055, parent=root)

    # Short, protected communication vanes replace the earlier exposed antennae.
    for x in (-width * 0.14, width * 0.14):
        strut("Signal.Vane", (x, depth * 0.12, height * 0.76),
              (x, depth * 0.17, height * 0.94), 0.045, steel, parent=root)
        sphere("Signal.Tip", 0.075, (x, depth * 0.17, height * 0.94), red, parent=root)
    return root


def presentation(root):
    floor_mat = material("Preview floor", (0.028, 0.038, 0.052), 0.45, 0.52)
    cube("Preview.Floor", (18, 18, 0.18), (0, 0, -0.12), floor_mat, edge=0.02)

    world = bpy.data.worlds.new("Leviathan preview world")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.003, 0.005, 0.009, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.28
    bpy.context.scene.world = world

    for name, loc, energy, color, size in (
        ("Key light", (7, -8, 9), 1250, (0.62, 0.76, 1.0), 4.5),
        ("Rim light", (-7, 2, 7), 900, (0.12, 0.32, 1.0), 4.0),
        ("Threat fill", (4, 3, 3), 360, (1.0, 0.025, 0.008), 3.0),
    ):
        bpy.ops.object.light_add(type="AREA", location=loc)
        light = active_object()
        light.name = name
        light.data.energy = energy
        light.data.color = color
        light.data.shape = "DISK"
        light.data.size = size
        point_camera(light, (0, 0, 1.6))

    bpy.ops.object.camera_add(location=(8.8, -11.8, 6.6))
    camera = active_object()
    camera.name = "Preview.Camera"
    camera.data.lens = 58
    point_camera(camera, (0, -0.25, 1.55))
    bpy.context.scene.camera = camera

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 900
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.render.filepath = PREVIEW_OUT
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.view_settings.exposure = -0.15

    bpy.ops.wm.save_as_mainfile(filepath=BLEND_OUT)
    bpy.ops.render.render(write_still=True)

    # GLB export is performed as a follow-up after reopening this saved file.
    # That gives Blender a fresh interactive context and avoids a Blender 5.2
    # exporter bug after read_factory_settings is called from the Python console.


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scan = repair_scan()
    root = build_details(scan)
    presentation(root)
    print("LEVIATHAN_TRIPOSR_ENHANCEMENT_COMPLETE")


if __name__ == "__main__":
    try:
        main()
    except Exception:
        os.makedirs(os.path.dirname(LOG_OUT), exist_ok=True)
        with open(LOG_OUT, "w", encoding="utf-8") as log:
            log.write(traceback.format_exc())
        raise
