"""
tools/build_sculpt_patch_and_coin.py
Builds the Toss-VR P1.1 Hero Coin with:
1. Coin Body: 30mm diameter, 2.4mm thickness, 48 reeded teeth, raised protective rim (Z=1.20mm), flat recessed field (Z=0.70mm).
2. Dedicated High-Res Heads Portrait Patch: 320x320 uniform quad mesh directly on the recessed field,
   sampling the master sculpted bas-relief array.
3. Saves to Assets/Meshes/Coin/TossCoin_Heads_Master.blend
4. Exports combined runtime mesh to Assets/Meshes/Coin/TossCoin_Heads_Relief.fbx
"""

import os
import math
import bpy
import bmesh
import numpy as np

ROOT = r"G:\Dev\Toss-VR"
MASTER_BLEND = os.path.join(ROOT, "Assets", "Meshes", "Coin", "TossCoin_Heads_Master.blend")
RUNTIME_FBX = os.path.join(ROOT, "Assets", "Meshes", "Coin", "TossCoin_Heads_Relief.fbx")
RELIEF_NPY = os.path.join(ROOT, "Assets", "Textures", "Coin", "heads_master_sculpt_perfect_float32.npy")

def build_scene():
    print("=== Building Coin Master & Heads Portrait Patch ===")
    
    # Reset Blender scene
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    
    # -------------------------------------------------------------
    # 1. COIN DIMENSIONS (SSOT v0.25: 30mm x 2.4mm)
    # -------------------------------------------------------------
    DIAMETER = 0.030
    RADIUS = DIAMETER * 0.5 # 15.0 mm
    THICKNESS = 0.0024      # 2.4 mm
    HALF_THICK = THICKNESS * 0.5 # 1.2 mm
    
    FIELD_RADIUS = 0.0137      # 13.7 mm
    RIM_BEVEL_RADIUS = 0.0142  # 14.2 mm
    RIM_CREST_RADIUS = 0.0147  # 14.7 mm
    EDGE_OUTER_RADIUS = 0.0150 # 15.0 mm
    EDGE_ROOT_RADIUS = 0.01468 # 14.68 mm
    
    FIELD_Z = 0.00070   # 0.70 mm
    RIM_Z = 0.00120     # 1.20 mm
    CHAMFER_Z = 0.00110 # 1.10 mm
    CYL_WALL_Z = 0.00098
    
    NUM_TEETH = 48
    SECTORS = 288
    
    # -------------------------------------------------------------
    # 2. BUILD COIN BODY MESH
    # -------------------------------------------------------------
    bm_body = bmesh.new()
    uv_layer = bm_body.loops.layers.uv.new("UVMap")
    
    angles = [k * (2.0 * math.pi / SECTORS) for k in range(SECTORS)]
    cos_vals = [math.cos(a) for a in angles]
    sin_vals = [math.sin(a) for a in angles]
    
    def get_tooth_radius(k):
        a = angles[k]
        wave = math.cos(NUM_TEETH * a)
        t_val = max(0.0, min(1.0, 0.5 - wave * 2.2))
        return EDGE_OUTER_RADIUS - (EDGE_OUTER_RADIUS - EDGE_ROOT_RADIUS) * t_val
        
    tooth_radii = [get_tooth_radius(k) for k in range(SECTORS)]
    
    # Front center vertex at FIELD_Z
    v_front_center = bm_body.verts.new((0.0, 0.0, FIELD_Z))
    
    # Field outer ring
    field_verts = []
    for k in range(SECTORS):
        x = FIELD_RADIUS * cos_vals[k]
        y = FIELD_RADIUS * sin_vals[k]
        v = bm_body.verts.new((x, y, FIELD_Z))
        field_verts.append(v)
        
    # Field disc fan
    for k in range(SECTORS):
        k_next = (k + 1) % SECTORS
        f = bm_body.faces.new([v_front_center, field_verts[k], field_verts[k_next]])
        f.smooth = True
        
    # Rim Rings
    ring_rim_bevel = [bm_body.verts.new((RIM_BEVEL_RADIUS * cos_vals[k], RIM_BEVEL_RADIUS * sin_vals[k], RIM_Z)) for k in range(SECTORS)]
    ring_rim_crest = [bm_body.verts.new((RIM_CREST_RADIUS * cos_vals[k], RIM_CREST_RADIUS * sin_vals[k], RIM_Z)) for k in range(SECTORS)]
    ring_chamfer_top = [bm_body.verts.new((tooth_radii[k] * cos_vals[k], tooth_radii[k] * sin_vals[k], CHAMFER_Z)) for k in range(SECTORS)]
    ring_cyl_top = [bm_body.verts.new((tooth_radii[k] * cos_vals[k], tooth_radii[k] * sin_vals[k], CYL_WALL_Z)) for k in range(SECTORS)]
    
    # Tails Rings
    ring_cyl_bot = [bm_body.verts.new((tooth_radii[k] * cos_vals[k], tooth_radii[k] * sin_vals[k], -CYL_WALL_Z)) for k in range(SECTORS)]
    ring_chamfer_bot = [bm_body.verts.new((tooth_radii[k] * cos_vals[k], tooth_radii[k] * sin_vals[k], -CHAMFER_Z)) for k in range(SECTORS)]
    ring_rim_crest_bot = [bm_body.verts.new((RIM_CREST_RADIUS * cos_vals[k], RIM_CREST_RADIUS * sin_vals[k], -RIM_Z)) for k in range(SECTORS)]
    ring_rim_bevel_bot = [bm_body.verts.new((RIM_BEVEL_RADIUS * cos_vals[k], RIM_BEVEL_RADIUS * sin_vals[k], -RIM_Z)) for k in range(SECTORS)]
    ring_field_bot = [bm_body.verts.new((FIELD_RADIUS * cos_vals[k], FIELD_RADIUS * sin_vals[k], -FIELD_Z)) for k in range(SECTORS)]
    v_back_center = bm_body.verts.new((0.0, 0.0, -FIELD_Z))
    
    def bridge(r1, r2):
        for k in range(SECTORS):
            k_next = (k + 1) % SECTORS
            f = bm_body.faces.new([r1[k], r2[k], r2[k_next], r1[k_next]])
            f.smooth = True
            
    bridge(field_verts, ring_rim_bevel)
    bridge(ring_rim_bevel, ring_rim_crest)
    bridge(ring_rim_crest, ring_chamfer_top)
    bridge(ring_chamfer_top, ring_cyl_top)
    bridge(ring_cyl_top, ring_cyl_bot)
    bridge(ring_cyl_bot, ring_chamfer_bot)
    bridge(ring_chamfer_bot, ring_rim_crest_bot)
    bridge(ring_rim_crest_bot, ring_rim_bevel_bot)
    bridge(ring_rim_bevel_bot, ring_field_bot)
    
    for k in range(SECTORS):
        k_next = (k + 1) % SECTORS
        f = bm_body.faces.new([v_back_center, ring_field_bot[k_next], ring_field_bot[k]])
        f.smooth = True
        
    mesh_body = bpy.data.meshes.new("Mesh_CoinBody")
    bm_body.to_mesh(mesh_body)
    bm_body.free()
    obj_body = bpy.data.objects.new("Coin_Body", mesh_body)
    bpy.context.collection.objects.link(obj_body)
    
    # -------------------------------------------------------------
    # 3. BUILD HEADS PORTRAIT PATCH MESH
    # -------------------------------------------------------------
    # Load sculpted height array
    if not os.path.exists(RELIEF_NPY):
        raise FileNotFoundError(f"Relief array not found: {RELIEF_NPY}")
    h_arr = np.load(RELIEF_NPY) # 1024x1024, float32, in mm
    h_rows, h_cols = h_arr.shape
    
    PATCH_RES = 320 # 320x320 uniform quads over portrait area
    # Bounding box of the portrait bust in meters
    X_MIN, X_MAX = -0.0115, +0.0115 # -11.5 mm to +11.5 mm
    Y_MIN, Y_MAX = -0.0125, +0.0125 # -12.5 mm to +12.5 mm
    
    xs = np.linspace(X_MIN, X_MAX, PATCH_RES, dtype=np.float32)
    ys = np.linspace(Y_MIN, Y_MAX, PATCH_RES, dtype=np.float32)
    
    bm_patch = bmesh.new()
    patch_grid = []
    
    for j in range(PATCH_RES):
        y = float(ys[j])
        row_verts = []
        for i in range(PATCH_RES):
            x = float(xs[i])
            # Sample height from relief array
            # x in [-0.015, 0.015], y in [-0.015, 0.015]
            u = (x * 1000.0 + 15.0) / 30.0
            v = (15.0 - y * 1000.0) / 30.0
            
            if 0.0 <= u <= 1.0 and 0.0 <= v <= 1.0:
                col = u * (h_cols - 1)
                row = v * (h_rows - 1)
                c0 = int(math.floor(col))
                r0 = int(math.floor(row))
                c1 = min(c0 + 1, h_cols - 1)
                r1 = min(r0 + 1, h_rows - 1)
                fc = col - c0
                fr = row - r0
                elev_mm = (h_arr[r0, c0]*(1.0-fc)*(1.0-fr) +
                           h_arr[r0, c1]*fc*(1.0-fr) +
                           h_arr[r1, c0]*(1.0-fc)*fr +
                           h_arr[r1, c1]*fc*fr)
            else:
                elev_mm = 0.0
                
            # Convert elevation to meters above field
            z = FIELD_Z + (float(elev_mm) / 1000.0)
            v_vert = bm_patch.verts.new((x, y, z))
            row_verts.append(v_vert)
        patch_grid.append(row_verts)
        
    # Create quad faces
    # Only create quads where at least one vertex is raised above field (or within bust)
    for j in range(PATCH_RES - 1):
        for i in range(PATCH_RES - 1):
            v0 = patch_grid[j][i]
            v1 = patch_grid[j][i+1]
            v2 = patch_grid[j+1][i+1]
            v3 = patch_grid[j+1][i]
            
            # If all 4 vertices are flat at field level, we can skip or keep
            # Keeping quads within radius 13.5mm ensures complete coverage
            r0 = math.hypot(v0.co.x, v0.co.y)
            r1 = math.hypot(v1.co.x, v1.co.y)
            r2 = math.hypot(v2.co.x, v2.co.y)
            r3 = math.hypot(v3.co.x, v3.co.y)
            max_r = max(r0, r1, r2, r3)
            
            # Skip quads outside the recessed field
            if max_r > FIELD_RADIUS:
                continue
                
            # Check if quad has relief elevation
            max_elev = max(v0.co.z, v1.co.z, v2.co.z, v3.co.z) - FIELD_Z
            if max_elev < 1.0e-5:
                # Flat field quad inside the patch
                continue
                
            f = bm_patch.faces.new([v0, v1, v2, v3])
            f.smooth = True
            
    mesh_patch = bpy.data.meshes.new("Mesh_HeadsPortraitPatch")
    bm_patch.to_mesh(mesh_patch)
    bm_patch.free()
    obj_patch = bpy.data.objects.new("Heads_Portrait_Patch", mesh_patch)
    bpy.context.collection.objects.link(obj_patch)
    
    # -------------------------------------------------------------
    # 4. WEIGHTED NORMAL MODIFIER FOR CRISP SHADING
    # -------------------------------------------------------------
    for obj in [obj_body, obj_patch]:
        mod = obj.modifiers.new("WeightedNormal", type='WEIGHTED_NORMAL')
        mod.mode = 'FACE_AREA'
        mod.weight = 50
        
    print(f"Coin Body: {len(mesh_body.vertices)} verts, {len(mesh_body.polygons)} polys")
    print(f"Heads Portrait Patch: {len(mesh_patch.vertices)} verts, {len(mesh_patch.polygons)} polys")
    
    # -------------------------------------------------------------
    # 5. CREATE COMBINED MESH FOR FBX EXPORT
    # -------------------------------------------------------------
    # Duplicate and join for runtime FBX
    obj_body.select_set(True)
    obj_patch.select_set(True)
    bpy.context.view_layer.objects.active = obj_body
    
    bpy.ops.object.duplicate()
    dup_body = bpy.context.selected_objects[0] if bpy.context.selected_objects[0] != obj_patch else bpy.context.selected_objects[1]
    dup_patch = bpy.context.selected_objects[1] if dup_body == bpy.context.selected_objects[0] else bpy.context.selected_objects[0]
    
    bpy.context.view_layer.objects.active = dup_body
    bpy.ops.object.join()
    combined_obj = bpy.context.active_object
    combined_obj.name = "TossCoin_Heads_Relief"
    
    # Apply weighted normal on combined mesh
    for mod in combined_obj.modifiers:
        bpy.ops.object.modifier_apply(modifier=mod.name)
    mod_wn = combined_obj.modifiers.new("WeightedNormal", type='WEIGHTED_NORMAL')
    mod_wn.mode = 'FACE_AREA'
    mod_wn.weight = 50
    bpy.ops.object.modifier_apply(modifier=mod_wn.name)
    
    print(f"Runtime Mesh: {len(combined_obj.data.vertices)} verts, {len(combined_obj.data.polygons)} polys")
    
    # Export FBX
    bpy.ops.object.select_all(action='DESELECT')
    combined_obj.select_set(True)
    bpy.context.view_layer.objects.active = combined_obj
    
    bpy.ops.export_scene.fbx(
        filepath=RUNTIME_FBX,
        use_selection=True,
        global_scale=1.0,
        apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_ALL',
        axis_forward='-Z',
        axis_up='Y',
        mesh_smooth_type='FACE'
    )
    print(f"Exported runtime FBX to: {RUNTIME_FBX}")
    
    # Delete the combined duplicate from master blend, leaving clean Coin_Body and Heads_Portrait_Patch
    bpy.ops.object.delete()
    
    # Save master blend file
    os.makedirs(os.path.dirname(MASTER_BLEND), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=MASTER_BLEND)
    print(f"Saved master blend to: {MASTER_BLEND}")

if __name__ == "__main__":
    build_scene()
