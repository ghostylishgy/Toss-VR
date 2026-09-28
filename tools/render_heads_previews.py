import bpy
import math
import os


def render_previews():
    print("=== Starting Numismatic PBR Studio Render ===")
    
    bpy.ops.wm.read_factory_settings(use_empty=True)
    
    fbx_path = r"G:\Dev\Toss-VR\Assets\Meshes\Coin\TossCoin_Heads_Relief.fbx"
    if not os.path.exists(fbx_path):
        raise FileNotFoundError(f"FBX not found: {fbx_path}")
        
    bpy.ops.import_scene.fbx(filepath=fbx_path)
    
    coin_obj = None
    for obj in bpy.context.scene.objects:
        if obj.type == 'MESH':
            coin_obj = obj
            break
            
    if coin_obj is None:
        raise RuntimeError("No mesh object imported from FBX!")
        
    bpy.context.view_layer.objects.active = coin_obj
    coin_obj.select_set(True)
    
    # Apply all transforms directly
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    coin_obj.location = (0, 0, 0)
    
    # Smooth shading
    for poly in coin_obj.data.polygons:
        poly.use_smooth = True
        
    # Weighted normal modifier for perfect numismatic shading
    mod = coin_obj.modifiers.new(name="WeightedNormal", type='WEIGHTED_NORMAL')
    mod.weight = 50
    mod.keep_sharp = True
    
    # 2. Satin Silver PBR Material
    mat = bpy.data.materials.new(name="M_TossCoin_SatinSilver")
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    
    bsdf = nodes.get("Principled BSDF")
    if bsdf:
        # Silver Base Color
        bsdf.inputs["Base Color"].default_value = (0.84, 0.85, 0.88, 1.0)
        # Metallic
        bsdf.inputs["Metallic"].default_value = 0.92
        # Roughness (Satin Silver finish)
        bsdf.inputs["Roughness"].default_value = 0.35
        if "IOR" in bsdf.inputs:
            bsdf.inputs["IOR"].default_value = 1.45
            
    coin_obj.data.materials.clear()
    coin_obj.data.materials.append(mat)
    
    # 3. World Environment Lighting (Dark Charcoal Studio Backdrop)
    world = bpy.context.scene.world
    if not world:
        world = bpy.data.worlds.new("World")
        bpy.context.scene.world = world
    world.use_nodes = True
    bg_node = world.node_tree.nodes.get("Background")
    if bg_node:
        bg_node.inputs["Color"].default_value = (0.045, 0.048, 0.055, 1.0)
        bg_node.inputs["Strength"].default_value = 0.20
        
    # 4. Studio Lighting Rig: Numismatic Directional Raking Lighting
    # Key Directional Sun - raking from top-left (reveals bas-relief contour & anatomical chisel)
    sun_key_data = bpy.data.lights.new(name="SunKey", type='SUN')
    sun_key_data.energy = 4.2
    sun_key_data.color = (1.0, 0.99, 0.97)
    sun_key_data.angle = math.radians(2.0)
    sun_key_obj = bpy.data.objects.new("SunKey", sun_key_data)
    bpy.context.collection.objects.link(sun_key_obj)
    sun_key_obj.rotation_euler = (0.78, -0.58, 0.48)
    
    # Fill/Rim Directional Sun - grazes the reeded edge & rim bevel from lower-right
    sun_rim_data = bpy.data.lights.new(name="SunRim", type='SUN')
    sun_rim_data.energy = 1.4
    sun_rim_data.color = (0.88, 0.92, 1.0)
    sun_rim_data.angle = math.radians(3.5)
    sun_rim_obj = bpy.data.objects.new("SunRim", sun_rim_data)
    bpy.context.collection.objects.link(sun_rim_obj)
    sun_rim_obj.rotation_euler = (-0.65, 0.82, -1.05)

    # 5. Camera Setup
    cam_data = bpy.data.cameras.new("RenderCamera")
    cam_data.lens = 75.0
    cam_data.clip_start = 0.001
    cam_data.clip_end = 10.0
    cam_obj = bpy.data.objects.new("RenderCamera", cam_data)
    bpy.context.collection.objects.link(cam_obj)
    bpy.context.scene.camera = cam_obj
    
    tt_cam = cam_obj.constraints.new(type='TRACK_TO')
    tt_cam.target = coin_obj
    tt_cam.track_axis = 'TRACK_NEGATIVE_Z'
    tt_cam.up_axis = 'UP_Y'
    
    # Render Settings
    scene = bpy.context.scene
    scene.render.resolution_x = 1024
    scene.render.resolution_y = 1024
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.render.image_settings.color_mode = 'RGB'
    
    scene.render.engine = 'CYCLES'
    scene.cycles.device = 'CPU'
    scene.cycles.samples = 64
    scene.cycles.use_denoising = True
    scene.view_settings.look = 'AgX - Base Contrast'

    out_dir = r"G:\Dev\Toss-VR\Assets\Textures\Coin"
    os.makedirs(out_dir, exist_ok=True)
    
    # -----------------------------------------------------------------
    # VIEW 1: 0° 正视图 (Front View)
    # -----------------------------------------------------------------
    print("Rendering View 1: 0° Front View...")
    cam_obj.location = (0.0, 0.0, 0.082)
    cam_data.lens = 68.0
    scene.render.filepath = os.path.join(out_dir, "View_1_Heads_Front.png")
    bpy.ops.render.render(write_still=True)
    print("View 1 saved!")

    # -----------------------------------------------------------------
    # VIEW 2: 45° 斜视图 (45° Oblique View)
    # -----------------------------------------------------------------
    print("Rendering View 2: 45° Oblique View...")
    cam_obj.location = (-0.045, -0.045, 0.058)
    cam_data.lens = 68.0
    scene.render.filepath = os.path.join(out_dir, "View_2_Heads_Angle45.png")
    bpy.ops.render.render(write_still=True)
    print("View 2 saved!")

    # -----------------------------------------------------------------
    # VIEW 3: 侧前方低角度视图 (Side Low-Angle View)
    # -----------------------------------------------------------------
    print("Rendering View 3: Side Low-Angle View...")
    cam_obj.location = (-0.052, -0.016, 0.020)
    cam_data.lens = 72.0
    scene.render.filepath = os.path.join(out_dir, "View_3_Heads_LowAngle.png")
    bpy.ops.render.render(write_still=True)
    print("View 3 saved!")

    # -----------------------------------------------------------------
    # VIEW 4: 近景细节图 (Close-Up Macro View)
    # -----------------------------------------------------------------
    print("Rendering View 4: Close-Up Macro View...")
    macro_target = bpy.data.objects.new("MacroTarget", None)
    bpy.context.collection.objects.link(macro_target)
    macro_target.location = (-0.0025, 0.001, 0.001)
    tt_cam.target = macro_target
    
    cam_obj.location = (-0.022, -0.010, 0.030)
    cam_data.lens = 80.0
    scene.render.filepath = os.path.join(out_dir, "View_4_Heads_Closeup.png")
    bpy.ops.render.render(write_still=True)
    print("View 4 saved!")

    print("=== All 4 Views Rendered Successfully ===")

if __name__ == "__main__":
    render_previews()
