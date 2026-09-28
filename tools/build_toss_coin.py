"""
build_toss_coin.py
Builds the Toss-VR P1.1 Hero Coin with High-Res Master (.blend) and Runtime Mesh (.fbx).
Uses the new Level 1/2 continuous bas-relief master heightfield (heads_master_sculpt_float32.npy).
Preserves frozen parameters: 30mm diameter, 2.4mm thickness, 48 reeded teeth, raised rim, recessed field.
"""

import math
import os
import sys
import bpy
import bmesh
import numpy as np

def build_coin_mesh(sectors=288, rings=130):
    # -------------------------------------------------------------
    # DIMENSIONS & SPECIFICATIONS (SSOT v0.25: 30mm diameter x 2.4mm thick)
    # -------------------------------------------------------------
    DIAMETER = 0.030          # 30.0 mm
    RADIUS = DIAMETER * 0.5   # 15.0 mm
    THICKNESS = 0.0024        # 2.4 mm
    HALF_THICK = THICKNESS * 0.5 # 1.2 mm
    
    FIELD_RADIUS = 0.0137       # 13.7 mm (inner field boundary)
    RIM_BEVEL_RADIUS = 0.0142   # 14.2 mm (inner rim bevel crest)
    RIM_CREST_RADIUS = 0.0147   # 14.7 mm (outer rim flat boundary)
    EDGE_OUTER_RADIUS = 0.0150  # 15.0 mm (reed tooth outer crest)
    EDGE_ROOT_RADIUS = 0.01468  # 14.68 mm (reed groove root, deep 48 flutes)
    
    FIELD_Z = 0.00070           # 0.70 mm (recessed field baseline)
    RIM_Z = 0.00120             # 1.20 mm (outer protective rim top)
    CHAMFER_Z = 0.00110         # 1.10 mm (outer edge bevel transition)
    CYL_WALL_Z = 0.00098        # 0.98 mm (reed tooth vertical face)
    
    NUM_TEETH = 48
    SECTORS = sectors
    RINGS = rings
    
    # Load master sculpted heightfield
    npy_path = r"G:\Dev\Toss-VR\Assets\Textures\Coin\heads_master_sculpt_float32.npy"
    if not os.path.exists(npy_path):
        raise FileNotFoundError(f"Master relief height array not found: {npy_path}")
        
    h_array = np.load(npy_path) # shape: 1024x1024, float32, in mm (0 to 0.420)
    h_rows, h_cols = h_array.shape
    
    def sample_relief(x, y):
        # x, y in meters (-0.015 to +0.015)
        # Convert to mm (-15.0 to +15.0)
        xm = x * 1000.0
        ym = y * 1000.0
        u = (xm + 15.0) / 30.0
        v = (15.0 - ym) / 30.0
        if u < 0.001 or u > 0.999 or v < 0.001 or v > 0.999:
            return 0.0
            
        col = u * (h_cols - 1)
        row = v * (h_rows - 1)
        c0 = int(math.floor(col))
        r0 = int(math.floor(row))
        c1 = min(c0 + 1, h_cols - 1)
        r1 = min(r0 + 1, h_rows - 1)
        fc = col - c0
        fr = row - r0
        
        val = (h_array[r0, c0] * (1.0 - fc) * (1.0 - fr) +
               h_array[r0, c1] * fc * (1.0 - fr) +
               h_array[r1, c0] * (1.0 - fc) * fr +
               h_array[r1, c1] * fc * fr)
        return float(val) / 1000.0 # convert mm to meters

    # -------------------------------------------------------------
    # BUILD BMESH
    # -------------------------------------------------------------
    bm = bmesh.new()
    uv_layer = bm.loops.layers.uv.new("UVMap")
    
    angles = [k * (2.0 * math.pi / SECTORS) for k in range(SECTORS)]
    cos_vals = [math.cos(a) for a in angles]
    sin_vals = [math.sin(a) for a in angles]
    
    def get_tooth_radius(k):
        a = angles[k]
        wave = math.cos(NUM_TEETH * a)
        # Trapezoidal reed tooth with flat crest and beveled root
        t_val = max(0.0, min(1.0, 0.5 - wave * 2.2))
        return EDGE_OUTER_RADIUS - (EDGE_OUTER_RADIUS - EDGE_ROOT_RADIUS) * t_val

    tooth_radii = [get_tooth_radius(k) for k in range(SECTORS)]

    # 1. Front center vertex
    z_center = FIELD_Z + sample_relief(0.0, 0.0)
    v_front_center = bm.verts.new((0.0, 0.0, z_center))
    
    # 2. Front Field concentric rings: ring 1 to RINGS
    field_rings = []
    for j in range(1, RINGS + 1):
        r = FIELD_RADIUS * (j / float(RINGS))
        ring_verts = []
        for k in range(SECTORS):
            x = r * cos_vals[k]
            y = r * sin_vals[k]
            h_elev = sample_relief(x, y)
            
            # Smoothly taper to exactly 0 at outer perimeter ring
            if j == RINGS:
                taper = 0.0
            elif j > RINGS - 4:
                taper = (RINGS - j) / 4.0
            else:
                taper = 1.0
                
            z = FIELD_Z + h_elev * taper
            v = bm.verts.new((x, y, z))
            ring_verts.append(v)
        field_rings.append(ring_verts)

    # Triangulate center fan
    for k in range(SECTORS):
        k_next = (k + 1) % SECTORS
        f = bm.faces.new([v_front_center, field_rings[0][k], field_rings[0][k_next]])
        f.smooth = True

    # Quads between concentric rings
    for j in range(RINGS - 1):
        r_inner = field_rings[j]
        r_outer = field_rings[j + 1]
        for k in range(SECTORS):
            k_next = (k + 1) % SECTORS
            f = bm.faces.new([r_inner[k], r_outer[k], r_outer[k_next], r_inner[k_next]])
            f.smooth = True

    # 3. Inner Rim Bevel
    rim_bevel_verts = []
    for k in range(SECTORS):
        x = RIM_BEVEL_RADIUS * cos_vals[k]
        y = RIM_BEVEL_RADIUS * sin_vals[k]
        v = bm.verts.new((x, y, RIM_Z))
        rim_bevel_verts.append(v)

    field_outer = field_rings[-1]
    for k in range(SECTORS):
        k_next = (k + 1) % SECTORS
        f = bm.faces.new([field_outer[k], rim_bevel_verts[k], rim_bevel_verts[k_next], field_outer[k_next]])
        f.smooth = True

    # 4. Rim Flat Top Crest
    rim_crest_verts = []
    for k in range(SECTORS):
        x = RIM_CREST_RADIUS * cos_vals[k]
        y = RIM_CREST_RADIUS * sin_vals[k]
        v = bm.verts.new((x, y, RIM_Z))
        rim_crest_verts.append(v)

    for k in range(SECTORS):
        k_next = (k + 1) % SECTORS
        f = bm.faces.new([rim_bevel_verts[k], rim_crest_verts[k], rim_crest_verts[k_next], rim_bevel_verts[k_next]])
        f.smooth = True

    # 5. Outer Rim Chamfer
    chamfer_top_verts = []
    for k in range(SECTORS):
        tr = tooth_radii[k]
        x = tr * cos_vals[k]
        y = tr * sin_vals[k]
        v = bm.verts.new((x, y, CHAMFER_Z))
        chamfer_top_verts.append(v)

    for k in range(SECTORS):
        k_next = (k + 1) % SECTORS
        f = bm.faces.new([rim_crest_verts[k], chamfer_top_verts[k], chamfer_top_verts[k_next], rim_crest_verts[k_next]])
        f.smooth = True

    # 6. Top Reed Tooth Crest
    cyl_top_verts = []
    for k in range(SECTORS):
        tr = tooth_radii[k]
        x = tr * cos_vals[k]
        y = tr * sin_vals[k]
        v = bm.verts.new((x, y, CYL_WALL_Z))
        cyl_top_verts.append(v)

    for k in range(SECTORS):
        k_next = (k + 1) % SECTORS
        f = bm.faces.new([chamfer_top_verts[k], cyl_top_verts[k], cyl_top_verts[k_next], chamfer_top_verts[k_next]])
        f.smooth = True

    # 7. Cylindrical Reeded Edge
    cyl_mid_verts = []
    cyl_bot_verts = []
    for k in range(SECTORS):
        tr = tooth_radii[k]
        x = tr * cos_vals[k]
        y = tr * sin_vals[k]
        v_mid = bm.verts.new((x, y, 0.0))
        v_bot = bm.verts.new((x, y, -CYL_WALL_Z))
        cyl_mid_verts.append(v_mid)
        cyl_bot_verts.append(v_bot)

    for k in range(SECTORS):
        k_next = (k + 1) % SECTORS
        f1 = bm.faces.new([cyl_top_verts[k], cyl_mid_verts[k], cyl_mid_verts[k_next], cyl_top_verts[k_next]])
        f2 = bm.faces.new([cyl_mid_verts[k], cyl_bot_verts[k], cyl_bot_verts[k_next], cyl_mid_verts[k_next]])
        f1.smooth = True
        f2.smooth = True

    # 8. Bottom Edge Bevels & Rim
    chamfer_bot_verts = []
    for k in range(SECTORS):
        tr = tooth_radii[k]
        x = tr * cos_vals[k]
        y = tr * sin_vals[k]
        v = bm.verts.new((x, y, -CHAMFER_Z))
        chamfer_bot_verts.append(v)

    for k in range(SECTORS):
        k_next = (k + 1) % SECTORS
        f = bm.faces.new([cyl_bot_verts[k], chamfer_bot_verts[k], chamfer_bot_verts[k_next], cyl_bot_verts[k_next]])
        f.smooth = True

    bot_flat_verts = []
    for k in range(SECTORS):
        x = RIM_CREST_RADIUS * cos_vals[k]
        y = RIM_CREST_RADIUS * sin_vals[k]
        v = bm.verts.new((x, y, -RIM_Z))
        bot_flat_verts.append(v)

    for k in range(SECTORS):
        k_next = (k + 1) % SECTORS
        f = bm.faces.new([chamfer_bot_verts[k], bot_flat_verts[k], bot_flat_verts[k_next], chamfer_bot_verts[k_next]])
        f.smooth = True

    bot_bevel_verts = []
    for k in range(SECTORS):
        x = RIM_BEVEL_RADIUS * cos_vals[k]
        y = RIM_BEVEL_RADIUS * sin_vals[k]
        v = bm.verts.new((x, y, -RIM_Z))
        bot_bevel_verts.append(v)

    for k in range(SECTORS):
        k_next = (k + 1) % SECTORS
        f = bm.faces.new([bot_flat_verts[k], bot_bevel_verts[k], bot_bevel_verts[k_next], bot_flat_verts[k_next]])
        f.smooth = True

    bot_field_edge = []
    for k in range(SECTORS):
        x = FIELD_RADIUS * cos_vals[k]
        y = FIELD_RADIUS * sin_vals[k]
        v = bm.verts.new((x, y, -FIELD_Z))
        bot_field_edge.append(v)

    for k in range(SECTORS):
        k_next = (k + 1) % SECTORS
        f = bm.faces.new([bot_bevel_verts[k], bot_field_edge[k], bot_field_edge[k_next], bot_bevel_verts[k_next]])
        f.smooth = True

    # Bottom field center vertex
    v_bot_center = bm.verts.new((0.0, 0.0, -FIELD_Z))
    for k in range(SECTORS):
        k_next = (k + 1) % SECTORS
        f = bm.faces.new([bot_field_edge[k_next], bot_field_edge[k], v_bot_center])
        f.smooth = True

    # -------------------------------------------------------------
    # UV MAPPING
    # -------------------------------------------------------------
    for f in bm.faces:
        f.smooth = True
        for loop in f.loops:
            v = loop.vert
            vx, vy, vz = v.co
            if vz > 0.0004:
                u = 0.5 + (vx / (2.0 * RADIUS)) * 0.94
                v_coord = 0.5 + (vy / (2.0 * RADIUS)) * 0.94
            elif vz < -0.0004:
                u = 0.5 + (vx / (2.0 * RADIUS)) * 0.94
                v_coord = 0.5 - (vy / (2.0 * RADIUS)) * 0.94
            else:
                ang = math.atan2(vy, vx)
                u = (ang / (2.0 * math.pi)) % 1.0
                v_coord = (vz + HALF_THICK) / THICKNESS
            loop[uv_layer].uv = (u, v_coord)

    mesh_data = bpy.data.meshes.new("Mesh_TossCoin_Heads")
    bm.to_mesh(mesh_data)
    bm.free()
    mesh_data.update()
    return mesh_data

def build_all():
    print("=== Generating Coin Assets: High-Res Master & Runtime Mesh ===")
    
    # 1. BUILD HIGH-RES MASTER (Sectors=288, Rings=130: ~38k vertices)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    master_mesh_data = build_coin_mesh(sectors=288, rings=130)
    master_obj = bpy.data.objects.new("TossCoin_Heads_Master", master_mesh_data)
    bpy.context.collection.objects.link(master_obj)
    bpy.context.view_layer.objects.active = master_obj
    master_obj.select_set(True)

    # Add Weighted Normal Modifier
    mod_m = master_obj.modifiers.new(name="WeightedNormal", type='WEIGHTED_NORMAL')
    mod_m.weight = 50
    mod_m.keep_sharp = True
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    # Save .blend master file to Assets and tools
    blend_dir_assets = r"G:\Dev\Toss-VR\Assets\Meshes\Coin"
    blend_dir_tools = r"G:\Dev\Toss-VR\tools"
    os.makedirs(blend_dir_assets, exist_ok=True)
    os.makedirs(blend_dir_tools, exist_ok=True)
    
    blend_path_assets = os.path.join(blend_dir_assets, "TossCoin_Heads_Master.blend")
    blend_path_tools = os.path.join(blend_dir_tools, "TossCoin_Heads_Master.blend")
    
    bpy.ops.wm.save_as_mainfile(filepath=blend_path_assets)
    bpy.ops.wm.save_as_mainfile(filepath=blend_path_tools)
    print(f"Saved High-Res Master .blend: {blend_path_assets}")
    print(f"Master Stats: Vertices={len(master_mesh_data.vertices)}, Polys={len(master_mesh_data.polygons)}")

    # 2. BUILD RUNTIME MESH (Optimized target: Sectors=192, Rings=90: ~17.5k vertices)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    runtime_mesh_data = build_coin_mesh(sectors=192, rings=90)
    runtime_obj = bpy.data.objects.new("TossCoin_Heads_Relief", runtime_mesh_data)
    bpy.context.collection.objects.link(runtime_obj)
    bpy.context.view_layer.objects.active = runtime_obj
    runtime_obj.select_set(True)

    mod_r = runtime_obj.modifiers.new(name="WeightedNormal", type='WEIGHTED_NORMAL')
    mod_r.weight = 50
    mod_r.keep_sharp = True
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    fbx_path = os.path.join(blend_dir_assets, "TossCoin_Heads_Relief.fbx")
    bpy.ops.export_scene.fbx(
        filepath=fbx_path,
        use_selection=True,
        axis_forward='-Z',
        axis_up='Y',
        apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_ALL',
        mesh_smooth_type='FACE',
        bake_space_transform=False
    )
    print(f"Exported Runtime FBX successfully: {fbx_path}")
    print(f"Runtime Stats: Vertices={len(runtime_mesh_data.vertices)}, Polys={len(runtime_mesh_data.polygons)}")
    print("=== Build Complete ===")

if __name__ == "__main__":
    build_all()
