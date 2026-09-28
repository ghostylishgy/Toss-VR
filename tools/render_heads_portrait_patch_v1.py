"""Render true-geometry diagnostics for Heads Portrait Patch v1.

The diagnostic deliberately uses no Weighted Normal modifier, no normal map,
no metal material, and one large raking area light.
"""

import math
import os

import bpy
from mathutils import Vector


ROOT = r"G:\Dev\Toss-VR"
MASTER_BLEND = os.path.join(ROOT, "Assets", "Meshes", "Coin", "HeadsPortraitPatch_v1.blend")
OUT_DIR = os.path.join(ROOT, "Assets", "Textures", "Coin", "PortraitPatch_v1")


def point_at(obj, target):
    direction = Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()


def set_shading(obj, smooth):
    for polygon in obj.data.polygons:
        polygon.use_smooth = smooth


def setup_scene():
    bpy.ops.wm.open_mainfile(filepath=MASTER_BLEND)
    patch = bpy.data.objects.get("HeadsPortraitPatch_v1")
    if patch is None:
        raise RuntimeError("HeadsPortraitPatch_v1 was not found in the master blend")
    if any(mod.type == 'WEIGHTED_NORMAL' for mod in patch.modifiers):
        raise RuntimeError("Weighted Normal must be absent for the clay checkpoint")

    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE_NEXT'
    scene.render.resolution_x = 1024
    scene.render.resolution_y = 1024
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.render.image_settings.color_mode = 'RGB'
    scene.render.film_transparent = False
    scene.render.image_settings.color_depth = '8'
    scene.render.use_file_extension = True
    scene.render.resolution_percentage = 100
    scene.view_settings.look = 'AgX - Medium High Contrast'

    world = bpy.data.worlds.new("PortraitPatchDiagnosticWorld")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.018, 0.020, 0.024, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.10
    scene.world = world

    clay = bpy.data.materials.new("M_PortraitPatch_MatteClay")
    clay.use_nodes = True
    bsdf = clay.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (0.42, 0.40, 0.38, 1.0)
    bsdf.inputs["Metallic"].default_value = 0.0
    bsdf.inputs["Roughness"].default_value = 0.88
    if "Specular IOR Level" in bsdf.inputs:
        bsdf.inputs["Specular IOR Level"].default_value = 0.18
    patch.data.materials.clear()
    patch.data.materials.append(clay)

    light_data = bpy.data.lights.new("SingleLargeRakingLight", type='AREA')
    light_data.energy = 0.08
    light_data.shape = 'DISK'
    light_data.size = 0.026
    light_data.color = (1.0, 0.97, 0.92)
    light = bpy.data.objects.new("SingleLargeRakingLight", light_data)
    bpy.context.collection.objects.link(light)
    light.location = (-0.040, 0.043, 0.021)
    point_at(light, (0.0, 0.0015, 0.0))

    camera_data = bpy.data.cameras.new("PortraitPatchDiagnosticCamera")
    camera_data.type = 'ORTHO'
    camera_data.lens = 70.0
    camera_data.clip_start = 0.001
    camera_data.clip_end = 1.0
    camera = bpy.data.objects.new("PortraitPatchDiagnosticCamera", camera_data)
    bpy.context.collection.objects.link(camera)
    scene.camera = camera

    os.makedirs(OUT_DIR, exist_ok=True)
    return scene, patch, camera, camera_data


def configure_camera(camera, camera_data, angle_deg, target=(0.0005, 0.0012, 0.0), distance=0.075, ortho_scale=0.0255):
    angle = math.radians(angle_deg)
    target_vec = Vector(target)
    camera.location = (
        target_vec.x - math.sin(angle) * distance,
        target_vec.y,
        target_vec.z + math.cos(angle) * distance,
    )
    camera_data.ortho_scale = ortho_scale
    point_at(camera, target)


def render(scene, path):
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print(f"Rendered: {path}")


def main():
    scene, patch, camera, camera_data = setup_scene()

    # Smooth clay diagnostic views.
    set_shading(patch, True)
    views = [
        ("PortraitPatch_v1_Clay_Front.png", 0.0, (0.0005, 0.0012, 0.0), 0.0255),
        ("PortraitPatch_v1_Clay_15deg.png", 15.0, (0.0005, 0.0012, 0.0), 0.0255),
        ("PortraitPatch_v1_Clay_30deg.png", 30.0, (0.0005, 0.0012, 0.0), 0.0255),
        ("PortraitPatch_v1_Clay_45deg.png", 45.0, (0.0005, 0.0012, 0.0), 0.0255),
        ("PortraitPatch_v1_Clay_Macro.png", 28.0, (-0.0018, 0.0018, 0.0), 0.0145),
    ]
    for filename, angle, target, scale in views:
        configure_camera(camera, camera_data, angle, target, ortho_scale=scale)
        render(scene, os.path.join(OUT_DIR, filename))

    # Identical front camera and light for the geometry-only flat/smooth comparison.
    configure_camera(camera, camera_data, 0.0, (0.0005, 0.0012, 0.0), ortho_scale=0.0255)
    set_shading(patch, False)
    render(scene, os.path.join(OUT_DIR, "PortraitPatch_v1_Clay_Front_Flat.png"))
    set_shading(patch, True)
    render(scene, os.path.join(OUT_DIR, "PortraitPatch_v1_Clay_Front_Smooth.png"))

    print("Weighted Normal: absent")
    print("Material: matte clay")
    print("Lighting: one large raking area light")
    print("Camera: orthographic")


if __name__ == "__main__":
    main()
