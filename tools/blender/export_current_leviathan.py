"""Export the open enhanced Leviathan .blend to a game-ready GLB.

Run this after the authored .blend has been reopened or its build script has
returned, so Blender's glTF add-on receives a normal interactive context.
"""

import os
import traceback

import bpy


ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
GLB_OUT = os.path.join(ROOT, "client", "models", "units", "leviathan-triposr-enhanced.glb")
LOG_OUT = os.path.join(ROOT, "artifacts", "leviathan-triposr-export.log")


def main():
    root = bpy.data.objects.get("Leviathan.Enhanced")
    if root is None:
        raise RuntimeError("Leviathan.Enhanced root was not found in the open Blender file")

    bpy.ops.object.select_all(action="DESELECT")
    meshes = [obj for obj in root.children_recursive if obj.type == "MESH"]
    if not meshes:
        raise RuntimeError("The enhanced Leviathan root has no mesh children")
    for obj in meshes:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]

    bpy.ops.export_scene.gltf(
        filepath=GLB_OUT,
        export_format="GLB",
        use_selection=True,
        export_apply=True,
        export_materials="EXPORT",
        export_cameras=False,
        export_lights=False,
    )
    print("LEVIATHAN_GLB_EXPORT_COMPLETE")


if __name__ == "__main__":
    try:
        main()
    except Exception:
        os.makedirs(os.path.dirname(LOG_OUT), exist_ok=True)
        with open(LOG_OUT, "w", encoding="utf-8") as log:
            log.write(traceback.format_exc())
        raise
