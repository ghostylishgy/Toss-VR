"""
tools/render_refined_sculpt_views.py
Renders rigorous diagnostic views for Toss-VR P1.1 Hero Coin Heads Bas-Relief Refinement:
Group A (Clay Diagnostics - Honest Geometry Evaluation, Matte Clay, Metallic=0.0):
1. Clay Front (Orthographic)
2. Clay 15 deg
3. Clay 30 deg
4. Clay 45 deg
5. Clay Macro (Face close-up)

Group B (Satin Silver Product Direction):
6. Silver Front
7. Silver 45 deg
8. Silver Macro (Face close-up)
"""

import os
import math
import sys
import bpy

ROOT = r"G:\Dev\Toss-VR"
MASTER_BLEND = os.path.join(ROOT, "Assets", "Meshes", "Coin", "TossCoin_Heads_Master.blend")
OUT_DIR = os.path.join(ROOT, "Assets", "Textures", "Coin", "RefinedSculpt")

def setup_scene(material_type="CLAY"):
    # Load master blend containing Coin_Body and Heads_Portrait_Patch
    bpy.ops.wm.open_mainfile(filepath=MASTER_BLEND)
    scene = bpy.context.scene
    
    body_obj = bpy.data.objects.get("Coin_Body")
    patch_obj = bpy.data.objects.get("Heads_Portrait_Patch")
    if not body_obj or not patch_obj:
        raise RuntimeError("Master blend missing Coin_Body or Heads_Portrait_Patch")
        
    # Shading and Materials
    mat = bpy.data.materials.new(name=f"Mat_{material_type}")
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    
    if material_type == "CLAY":
        # Neutral Matte Clay (Roughness 0.88, Metallic 0.0)
        bsdf.inputs["Base Color"].default_value = (0.76, 0.74, 0.72, 1.0)
        bsdf.inputs["Metallic"].default_value = 0.0
        bsdf.inputs["Roughness"].default_value = 0.88
        if "Specular IOR Level" in bsdf.inputs:
            bsdf.inputs["Specular IOR Level"].default_value = 0.15
            
        # In Clay mode, do NOT use weighted normal to hide geometry
        for obj in [body_obj, patch_obj]:
            for mod in list(obj.modifiers):
                if mod.type == 'WEIGHTED_NORMAL':
                    obj.modifiers.remove(mod)
    else:
        # Satin Silver Numismatic Product Material
        bsdf.inputs["Base Color"].default_value = (0.88, 0.89, 0.92, 1.0)
        bsdf.inputs["Metallic"].default_value = 0.96
        bsdf.inputs["Roughness"].default_value = 0.30
        if "IOR" in bsdf.inputs:
            bsdf.inputs["IOR"].default_value = 1.45
            
        # Keep weighted normal for silver presentation
        for obj in [body_obj, patch_obj]:
            if not any(mod.type == 'WEIGHTED_NORMAL' for mod in obj.modifiers):
                wn = obj.modifiers.new("WeightedNormal", type='WEIGHTED_NORMAL')
                wn.mode = 'FACE_AREA'
                wn.weight = 50
                
    for obj in [body_obj, patch_obj]:
        obj.data.materials.clear()
        obj.data.materials.append(mat)
        
    # World Background
    world = bpy.data.worlds.new("World_Diagnostic")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs["Color"].default_value = (0.025, 0.026, 0.030, 1.0)
        bg.inputs["Strength"].default_value = 0.25
        
    # Studio Lighting Rig:
    # 1. Primary Raking Key (Sun, 45 deg from top-left, reveals sculptural planes)
    key_data = bpy.data.lights.new("RakingKey", type='SUN')
    key_data.energy = 3.6 if material_type == "CLAY" else 4.2
    key_data.color = (1.0, 0.98, 0.95)
    key_data.angle = math.radians(2.0)
    key_obj = bpy.data.objects.new("RakingKey", key_data)
    bpy.context.collection.objects.link(key_obj)
    key_obj.rotation_euler = (0.80, -0.60, 0.45)
    
    # 2. Gentle Fill / Rim Sun
    fill_data = bpy.data.lights.new("SoftFill", type='SUN')
    fill_data.energy = 1.0 if material_type == "CLAY" else 1.5
    fill_data.color = (0.85, 0.90, 1.0)
    fill_data.angle = math.radians(4.0)
    fill_obj = bpy.data.objects.new("SoftFill", fill_data)
    bpy.context.collection.objects.link(fill_obj)
    fill_obj.rotation_euler = (-0.60, 0.80, -1.10)
    
    # Camera
    cam_data = bpy.data.cameras.new("DiagnosticCam")
    cam_data.clip_start = 0.001
    cam_data.clip_end = 10.0
    cam_obj = bpy.data.objects.new("DiagnosticCam", cam_data)
    bpy.context.collection.objects.link(cam_obj)
    scene.camera = cam_obj
    
    # Target constraint
    target_obj = bpy.data.objects.new("CamTarget", None)
    bpy.context.collection.objects.link(target_obj)
    target_obj.location = (0.0, 0.0, 0.0007)
    
    tt = cam_obj.constraints.new(type='TRACK_TO')
    tt.target = target_obj
    tt.track_axis = 'TRACK_NEGATIVE_Z'
    tt.up_axis = 'UP_Y'
    
    # Render Settings (Cycles CPU with denoising)
    scene.render.resolution_x = 1024
    scene.render.resolution_y = 1024
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.render.image_settings.color_mode = 'RGB'
    scene.render.image_settings.color_depth = '8'
    
    scene.render.engine = 'CYCLES'
    scene.cycles.device = 'CPU'
    scene.cycles.samples = 40
    scene.cycles.use_denoising = True
    scene.view_settings.look = 'AgX - Base Contrast'
    
    return scene, cam_obj, cam_data, target_obj, tt

def render_diagnostics():
    os.makedirs(OUT_DIR, exist_ok=True)
    
    # =============================================================
    # GROUP A: CLAY DIAGNOSTICS (5 VIEWS)
    # =============================================================
    print(">>> Rendering Group A: Clay Diagnostics...")
    scene, cam_obj, cam_data, target_obj, tt = setup_scene("CLAY")
    dist = 0.080
    
    # 1. Clay Front (Orthographic view)
    print("Rendering 1/8: Clay Front...")
    target_obj.location = (0.0, 0.0, 0.0007)
    cam_data.type = 'ORTHO'
    cam_data.ortho_scale = 0.034
    cam_obj.location = (0.0, 0.0, dist)
    scene.render.filepath = os.path.join(OUT_DIR, "Clay_Front.png")
    bpy.ops.render.render(write_still=True)
    
    # Perspective for oblique views
    cam_data.type = 'PERSP'
    cam_data.lens = 72.0
    
    # 2. Clay 15 deg
    print("Rendering 2/8: Clay 15 deg...")
    angle = math.radians(15.0)
    cam_obj.location = (-dist * math.sin(angle), 0.0, dist * math.cos(angle))
    scene.render.filepath = os.path.join(OUT_DIR, "Clay_15deg.png")
    bpy.ops.render.render(write_still=True)
    
    # 3. Clay 30 deg
    print("Rendering 3/8: Clay 30 deg...")
    angle = math.radians(30.0)
    cam_obj.location = (-dist * math.sin(angle), -dist * math.sin(angle)*0.25, dist * math.cos(angle))
    scene.render.filepath = os.path.join(OUT_DIR, "Clay_30deg.png")
    bpy.ops.render.render(write_still=True)
    
    # 4. Clay 45 deg
    print("Rendering 4/8: Clay 45 deg...")
    angle = math.radians(45.0)
    cam_obj.location = (-dist * math.sin(angle)*0.85, -dist * math.sin(angle)*0.55, dist * math.cos(angle))
    scene.render.filepath = os.path.join(OUT_DIR, "Clay_45deg.png")
    bpy.ops.render.render(write_still=True)
    
    # 5. Clay Macro (Face Close-up)
    print("Rendering 5/8: Clay Macro...")
    target_obj.location = (-0.0035, 0.0005, 0.0009) # Focus on eye, nose, lips
    cam_data.lens = 85.0
    cam_obj.location = (-0.024, -0.012, 0.032)
    scene.render.filepath = os.path.join(OUT_DIR, "Clay_Macro.png")
    bpy.ops.render.render(write_still=True)
    
    # =============================================================
    # GROUP B: SATIN SILVER PRODUCTION VIEWS (3 VIEWS)
    # =============================================================
    print(">>> Rendering Group B: Satin Silver Production Views...")
    scene, cam_obj, cam_data, target_obj, tt = setup_scene("SILVER")
    
    # 6. Silver Front
    print("Rendering 6/8: Silver Front...")
    target_obj.location = (0.0, 0.0, 0.0007)
    cam_data.type = 'ORTHO'
    cam_data.ortho_scale = 0.034
    cam_obj.location = (0.0, 0.0, dist)
    scene.render.filepath = os.path.join(OUT_DIR, "Silver_Front.png")
    bpy.ops.render.render(write_still=True)
    
    # 7. Silver 45 deg
    print("Rendering 7/8: Silver 45 deg...")
    cam_data.type = 'PERSP'
    cam_data.lens = 72.0
    angle = math.radians(45.0)
    cam_obj.location = (-dist * math.sin(angle)*0.85, -dist * math.sin(angle)*0.55, dist * math.cos(angle))
    scene.render.filepath = os.path.join(OUT_DIR, "Silver_45deg.png")
    bpy.ops.render.render(write_still=True)
    
    # 8. Silver Macro
    print("Rendering 8/8: Silver Macro...")
    target_obj.location = (-0.0035, 0.0005, 0.0009)
    cam_data.lens = 85.0
    cam_obj.location = (-0.024, -0.012, 0.032)
    scene.render.filepath = os.path.join(OUT_DIR, "Silver_Macro.png")
    bpy.ops.render.render(write_still=True)
    
    print(">>> All 8 diagnostic renders complete!")

if __name__ == "__main__":
    render_diagnostics()
