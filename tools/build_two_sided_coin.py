"""
tools/build_two_sided_coin.py
Builds the complete double-sided Toss-VR P1.1 Hero Coin:
1. Frozen Base Dimensions (SSOT v0.25):
   - Diameter: 30.0 mm
   - Thickness: 2.4 mm
   - 48 deep trapezoidal reeded edge flutes
   - Front Rim Z = +1.20 mm, Front Field Z = +0.70 mm
   - Back Rim Z = -1.20 mm, Back Field Z = -0.70 mm
2. Front Face (+Z): Heads Grecian Goddess Bas-Relief (sampling heads_master_sculpt_perfect_float32.npy, peak <= +1.11 mm)
3. Back Face (-Z): Tails Soaring Silver Wing Bird Bas-Relief (sampling tails_master_sculpt_float32.npy, peak <= -1.10 mm)
4. Saves high-res master scene to Assets/Meshes/Coin/TossCoin_Heads_Master.blend
5. Exports unified double-sided runtime mesh with Face-Area Weighted Normals to Assets/Meshes/Coin/TossCoin_Heads_Relief.fbx
"""

import os
import math
import bpy
import bmesh
import numpy as np

ROOT = r"G:\Dev\Toss-VR"
MASTER_BLEND = os.path.join(ROOT, "Assets", "Meshes", "Coin", "TossCoin_Heads_Master.blend")
RUNTIME_FBX = os.path.join(ROOT, "Assets", "Meshes", "Coin", "TossCoin_Heads_Relief.fbx")
HEADS_NPY = os.path.join(ROOT, "Assets", "Textures", "Coin", "heads_master_sculpt_perfect_float32.npy")
TAILS_NPY = os.path.join(ROOT, "Assets", "Textures", "Coin", "tails_master_sculpt_float32.npy")

def build_two_sided_coin():
    print("=== Building Two-Sided Coin Master (.blend) & Runtime FBX ===")
    
    bpy.ops.wm.read_factory_settings(use_empty=True)
    
    # -------------------------------------------------------------
    # 1. SPECIFICATIONS (SSOT v0.25: 30mm x 2.4mm, 48 reeds)
    # -------------------------------------------------------------
    DIAMETER = 0.030          # 30.0 mm
    RADIUS = DIAMETER * 0.5   # 15.0 mm
    THICKNESS = 0.0024        # 2.4 mm
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
    
    angles = [k * (2.0 * math.pi / SECTORS) for k in range(SECTORS)]
    cos_vals = [math.cos(a) for a in angles]
    sin_vals = [math.sin(a) for a in angles]
    
    def get_tooth_radius(k):
        a = angles[k]
        wave = math.cos(NUM_TEETH * a)
        t_val = max(0.0, min(1.0, 0.5 - wave * 2.2))
        return EDGE_OUTER_RADIUS - (EDGE_OUTER_RADIUS - EDGE_ROOT_RADIUS) * t_val
        
    tooth_radii = [get_tooth_radius(k) for k in range(SECTORS)]
    
    # -------------------------------------------------------------
    # 2. BUILD COIN BODY (REEDED EDGE & PROTECTIVE RIMS)
    # -------------------------------------------------------------
    bm_body = bmesh.new()
    uv_layer_body = bm_body.loops.layers.uv.new("UVMap")
    
    v_front_center = bm_body.verts.new((0.0, 0.0, FIELD_Z))
    field_verts = [bm_body.verts.new((FIELD_RADIUS * cos_vals[k], FIELD_RADIUS * sin_vals[k], FIELD_Z)) for k in range(SECTORS)]
    
    # Front field disc fan
    for k in range(SECTORS):
        k_next = (k + 1) % SECTORS
        f = bm_body.faces.new([v_front_center, field_verts[k], field_verts[k_next]])
        f.smooth = True
        
    # Rim and Edge Rings
    ring_rim_bevel = [bm_body.verts.new((RIM_BEVEL_RADIUS * cos_vals[k], RIM_BEVEL_RADIUS * sin_vals[k], RIM_Z)) for k in range(SECTORS)]
    ring_rim_crest = [bm_body.verts.new((RIM_CREST_RADIUS * cos_vals[k], RIM_CREST_RADIUS * sin_vals[k], RIM_Z)) for k in range(SECTORS)]
    ring_chamfer_top = [bm_body.verts.new((tooth_radii[k] * cos_vals[k], tooth_radii[k] * sin_vals[k], CHAMFER_Z)) for k in range(SECTORS)]
    ring_cyl_top = [bm_body.verts.new((tooth_radii[k] * cos_vals[k], tooth_radii[k] * sin_vals[k], CYL_WALL_Z)) for k in range(SECTORS)]
    
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
    
    # Back field disc fan
    for k in range(SECTORS):
        k_next = (k + 1) % SECTORS
        f = bm_body.faces.new([v_back_center, ring_field_bot[k_next], ring_field_bot[k]])
        f.smooth = True
        
    # UVs for body
    for f in bm_body.faces:
        f.smooth = True
        for loop in f.loops:
            vx, vy, vz = loop.vert.co
            if vz > 0.0004:
                u = 0.5 + (vx / (2.0 * RADIUS)) * 0.94
                v_c = 0.5 + (vy / (2.0 * RADIUS)) * 0.94
            elif vz < -0.0004:
                u = 0.5 + (vx / (2.0 * RADIUS)) * 0.94
                v_c = 0.5 - (vy / (2.0 * RADIUS)) * 0.94
            else:
                ang = math.atan2(vy, vx)
                u = (ang / (2.0 * math.pi)) % 1.0
                v_c = (vz + HALF_THICK) / THICKNESS
            loop[uv_layer_body].uv = (u, v_c)
            
    mesh_body = bpy.data.meshes.new("Mesh_CoinBody")
    bm_body.to_mesh(mesh_body)
    bm_body.free()
    obj_body = bpy.data.objects.new("Coin_Body", mesh_body)
    bpy.context.collection.objects.link(obj_body)

    # -------------------------------------------------------------
    # 3. BUILD HEADS PORTRAIT PATCH (+Z FACE)
    # -------------------------------------------------------------
    print("Building Heads Portrait Patch (+Z)...")
    h_heads = np.load(HEADS_NPY)
    hh_rows, hh_cols = h_heads.shape
    
    PATCH_RES = 320
    X_MIN_H, X_MAX_H = -0.0115, +0.0115
    Y_MIN_H, Y_MAX_H = -0.0125, +0.0125
    
    xs_h = np.linspace(X_MIN_H, X_MAX_H, PATCH_RES, dtype=np.float32)
    ys_h = np.linspace(Y_MIN_H, Y_MAX_H, PATCH_RES, dtype=np.float32)
    
    bm_heads = bmesh.new()
    uv_layer_heads = bm_heads.loops.layers.uv.new("UVMap")
    heads_grid = []
    
    for j in range(PATCH_RES):
        y = float(ys_h[j])
        row_verts = []
        for i in range(PATCH_RES):
            x = float(xs_h[i])
            u = (x * 1000.0 + 15.0) / 30.0
            v = (15.0 - y * 1000.0) / 30.0
            
            if 0.0 <= u <= 1.0 and 0.0 <= v <= 1.0:
                col = u * (hh_cols - 1)
                row = v * (hh_rows - 1)
                c0 = int(math.floor(col))
                r0 = int(math.floor(row))
                c1 = min(c0 + 1, hh_cols - 1)
                r1 = min(r0 + 1, hh_rows - 1)
                fc = col - c0
                fr = row - r0
                elev_mm = (h_heads[r0, c0]*(1.0-fc)*(1.0-fr) +
                           h_heads[r0, c1]*fc*(1.0-fr) +
                           h_heads[r1, c0]*(1.0-fc)*fr +
                           h_heads[r1, c1]*fc*fr)
            else:
                elev_mm = 0.0
                
            z = FIELD_Z + (float(elev_mm) / 1000.0)
            v_vert = bm_heads.verts.new((x, y, z))
            row_verts.append(v_vert)
        heads_grid.append(row_verts)
        
    for j in range(PATCH_RES - 1):
        for i in range(PATCH_RES - 1):
            v0 = heads_grid[j][i]
            v1 = heads_grid[j][i+1]
            v2 = heads_grid[j+1][i+1]
            v3 = heads_grid[j+1][i]
            
            r_max = max(math.hypot(v.co.x, v.co.y) for v in (v0, v1, v2, v3))
            if r_max > FIELD_RADIUS:
                continue
                
            max_elev = max(v.co.z for v in (v0, v1, v2, v3)) - FIELD_Z
            if max_elev < 1.0e-5:
                continue
                
            f = bm_heads.faces.new([v0, v1, v2, v3])
            f.smooth = True
            for loop in f.loops:
                vx, vy, _ = loop.vert.co
                loop[uv_layer_heads].uv = (0.5 + (vx / (2.0 * RADIUS)) * 0.94,
                                           0.5 + (vy / (2.0 * RADIUS)) * 0.94)

    mesh_heads = bpy.data.meshes.new("Mesh_HeadsPortraitPatch")
    bm_heads.to_mesh(mesh_heads)
    bm_heads.free()
    obj_heads = bpy.data.objects.new("Heads_Portrait_Patch", mesh_heads)
    bpy.context.collection.objects.link(obj_heads)

    # -------------------------------------------------------------
    # 4. BUILD TAILS BIRD PATCH (-Z FACE)
    # -------------------------------------------------------------
    print("Building Tails Bird Patch (-Z)...")
    h_tails = np.load(TAILS_NPY)
    ht_rows, ht_cols = h_tails.shape
    
    # Tails footprint covers the full field: -13.5mm to +13.5mm
    X_MIN_T, X_MAX_T = -0.0135, +0.0135
    Y_MIN_T, Y_MAX_T = -0.0135, +0.0135
    
    xs_t = np.linspace(X_MIN_T, X_MAX_T, PATCH_RES, dtype=np.float32)
    ys_t = np.linspace(Y_MIN_T, Y_MAX_T, PATCH_RES, dtype=np.float32)
    
    bm_tails = bmesh.new()
    uv_layer_tails = bm_tails.loops.layers.uv.new("UVMap")
    tails_grid = []
    
    for j in range(PATCH_RES):
        y = float(ys_t[j])
        row_verts = []
        for i in range(PATCH_RES):
            x = float(xs_t[i])
            u = (x * 1000.0 + 15.0) / 30.0
            v = (15.0 - y * 1000.0) / 30.0
            
            if 0.0 <= u <= 1.0 and 0.0 <= v <= 1.0:
                col = u * (ht_cols - 1)
                row = v * (ht_rows - 1)
                c0 = int(math.floor(col))
                r0 = int(math.floor(row))
                c1 = min(c0 + 1, ht_cols - 1)
                r1 = min(r0 + 1, ht_rows - 1)
                fc = col - c0
                fr = row - r0
                elev_mm = (h_tails[r0, c0]*(1.0-fc)*(1.0-fr) +
                           h_tails[r0, c1]*fc*(1.0-fr) +
                           h_tails[r1, c0]*(1.0-fc)*fr +
                           h_tails[r1, c1]*fc*fr)
            else:
                elev_mm = 0.0
                
            # On -Z face, elevation extends into negative Z direction:
            # Z = -FIELD_Z - (elev_mm / 1000.0)
            z = -FIELD_Z - (float(elev_mm) / 1000.0)
            v_vert = bm_tails.verts.new((x, y, z))
            row_verts.append(v_vert)
        tails_grid.append(row_verts)
        
    for j in range(PATCH_RES - 1):
        for i in range(PATCH_RES - 1):
            v0 = tails_grid[j][i]
            v1 = tails_grid[j][i+1]
            v2 = tails_grid[j+1][i+1]
            v3 = tails_grid[j+1][i]
            
            r_max = max(math.hypot(v.co.x, v.co.y) for v in (v0, v1, v2, v3))
            if r_max > FIELD_RADIUS:
                continue
                
            min_z = min(v.co.z for v in (v0, v1, v2, v3))
            # If raised outwards towards negative Z (min_z < -FIELD_Z - 1.0e-5)
            if min_z > -FIELD_Z - 1.0e-5:
                continue
                
            # Winding order reversed on -Z face so normal points outwards in -Z direction!
            f = bm_tails.faces.new([v0, v3, v2, v1])
            f.smooth = True
            for loop in f.loops:
                vx, vy, _ = loop.vert.co
                loop[uv_layer_tails].uv = (0.5 + (vx / (2.0 * RADIUS)) * 0.94,
                                           0.5 - (vy / (2.0 * RADIUS)) * 0.94)

    mesh_tails = bpy.data.meshes.new("Mesh_TailsBirdPatch")
    bm_tails.to_mesh(mesh_tails)
    bm_tails.free()
    obj_tails = bpy.data.objects.new("Tails_Bird_Patch", mesh_tails)
    bpy.context.collection.objects.link(obj_tails)

    # -------------------------------------------------------------
    # 5. MODIFIERS & SHADING
    # -------------------------------------------------------------
    for obj in [obj_body, obj_heads, obj_tails]:
        mod = obj.modifiers.new("WeightedNormal", type='WEIGHTED_NORMAL')
        mod.mode = 'FACE_AREA'
        mod.weight = 50
        
    print(f"Stats:")
    print(f"  Coin Body: {len(mesh_body.vertices)} verts, {len(mesh_body.polygons)} polys")
    print(f"  Heads Patch: {len(mesh_heads.vertices)} verts, {len(mesh_heads.polygons)} polys")
    print(f"  Tails Patch: {len(mesh_tails.vertices)} verts, {len(mesh_tails.polygons)} polys")

    # -------------------------------------------------------------
    # 6. EXPORT UNIFIED DOUBLE-SIDED RUNTIME FBX
    # -------------------------------------------------------------
    bpy.ops.object.select_all(action='DESELECT')
    for obj in [obj_body, obj_heads, obj_tails]:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = obj_body
    
    bpy.ops.object.duplicate()
    dup_objs = bpy.context.selected_objects
    bpy.context.view_layer.objects.active = dup_objs[0]
    bpy.ops.object.join()
    combined_obj = bpy.context.active_object
    combined_obj.name = "TossCoin_Heads_Relief"
    
    # Apply Weighted Normal on combined mesh
    for mod in list(combined_obj.modifiers):
        bpy.ops.object.modifier_apply(modifier=mod.name)
    mod_wn = combined_obj.modifiers.new("WeightedNormal", type='WEIGHTED_NORMAL')
    mod_wn.mode = 'FACE_AREA'
    mod_wn.weight = 50
    bpy.ops.object.modifier_apply(modifier=mod_wn.name)
    
    print(f"Combined Runtime Mesh: {len(combined_obj.data.vertices)} verts, {len(combined_obj.data.polygons)} polys")
    
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
    print(f"Exported double-sided runtime FBX: {RUNTIME_FBX}")
    
    # Remove temporary combined mesh from scene
    bpy.ops.object.delete()
    
    # Save master blend scene with all 3 separate editable layers
    os.makedirs(os.path.dirname(MASTER_BLEND), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=MASTER_BLEND)
    print(f"Saved Master .blend to: {MASTER_BLEND}")
    print("=== Two-Sided Coin Build Complete ===")

if __name__ == "__main__":
    build_two_sided_coin()
