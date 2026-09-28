import math
import os
import sys
import bpy
import bmesh
import numpy as np

def build_toss_coin():
    print("=== Starting Enhanced Toss Coin Heads Bas-Relief Build ===")
    
    bpy.ops.wm.read_factory_settings(use_empty=True)
    
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
    
    # 420 microns relief height: substantial, dramatic bas-relief volume
    RELIEF_MAX_HEIGHT = 0.00042 
    
    NUM_TEETH = 48
    SECTORS = 192               # 4 segments per tooth (192 total)
    RINGS = 80                  # 80 concentric rings for ultra-smooth facial topography
    
    # Load heightfield data
    npy_path = r"G:\Dev\Toss-VR\Assets\Textures\Coin\heads_relief_float32.npy"
    if not os.path.exists(npy_path):
        raise FileNotFoundError(f"Height array not found: {npy_path}")
        
    h_array = np.load(npy_path)
    h_rows, h_cols = h_array.shape
    
    def sample_relief(x, y):
        # In ref_crop_heads: cx=191, cy=180, r=184 on 374x374
        u = (191.0 / 374.0) + (x / RADIUS) * (184.0 / 374.0)
        v = (180.0 / 374.0) - (y / RADIUS) * (184.0 / 374.0)
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
        return float(val)

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
    z_center = FIELD_Z + sample_relief(0.0, 0.0) * RELIEF_MAX_HEIGHT
    v_front_center = bm.verts.new((0.0, 0.0, z_center))
    
    # 2. Front Field concentric rings: ring 1 to RINGS
    field_rings = []
    for j in range(1, RINGS + 1):
        r = FIELD_RADIUS * (j / float(RINGS))
        ring_verts = []
        for k in range(SECTORS):
            x = r * cos_vals[k]
            y = r * sin_vals[k]
            h_val = sample_relief(x, y)
            
            # Smoothly taper to exactly 0 at outer perimeter ring
            if j == RINGS:
                taper = 0.0
            elif j > RINGS - 5:
                taper = (RINGS - j) / 5.0
            else:
                taper = 1.0
                
            z = FIELD_Z + h_val * RELIEF_MAX_HEIGHT * taper
            v = bm.verts.new((x, y, z))
            ring_verts.append(v)
        field_rings.append(ring_verts)

    # Triangulate center fan
    for k in range(SECTORS):
        k_next = (k + 1) % SECTORS
        f = bm.faces.new([v_front_center, field_rings[0][k], field_rings[0][k_next]])
        f.smooth = True

    # Quads between field rings
    for j in range(RINGS - 1):
        r_curr = field_rings[j]
        r_next = field_rings[j + 1]
        for k in range(SECTORS):
            k_next = (k + 1) % SECTORS
            f = bm.faces.new([r_curr[k], r_next[k], r_next[k_next], r_curr[k_next]])
            f.smooth = True

    # 3. Front Rim Rings
    # Inner rim bevel (slopes from field edge up to rim top)
    rim_bevel_verts = []
    for k in range(SECTORS):
        x = RIM_BEVEL_RADIUS * cos_vals[k]
        y = RIM_BEVEL_RADIUS * sin_vals[k]
        v = bm.verts.new((x, y, RIM_Z))
        rim_bevel_verts.append(v)
        
    last_field_ring = field_rings[-1]
    for k in range(SECTORS):
        k_next = (k + 1) % SECTORS
        f = bm.faces.new([last_field_ring[k], rim_bevel_verts[k], rim_bevel_verts[k_next], last_field_ring[k_next]])
        f.smooth = True

    # Outer rim flat
    rim_flat_verts = []
    for k in range(SECTORS):
        x = RIM_CREST_RADIUS * cos_vals[k]
        y = RIM_CREST_RADIUS * sin_vals[k]
        v = bm.verts.new((x, y, RIM_Z))
        rim_flat_verts.append(v)
        
    for k in range(SECTORS):
        k_next = (k + 1) % SECTORS
        f = bm.faces.new([rim_bevel_verts[k], rim_flat_verts[k], rim_flat_verts[k_next], rim_bevel_verts[k_next]])
        f.smooth = True

    # Outer rim chamfer
    rim_chamfer_verts = []
    for k in range(SECTORS):
        r_e = tooth_radii[k]
        x = r_e * cos_vals[k]
        y = r_e * sin_vals[k]
        v = bm.verts.new((x, y, CHAMFER_Z))
        rim_chamfer_verts.append(v)
        
    for k in range(SECTORS):
        k_next = (k + 1) % SECTORS
        f = bm.faces.new([rim_flat_verts[k], rim_chamfer_verts[k], rim_chamfer_verts[k_next], rim_flat_verts[k_next]])
        f.smooth = True

    # 4. Reeded Edge Vertical Wall
    reed_upper_verts = []
    reed_lower_verts = []
    for k in range(SECTORS):
        r_e = tooth_radii[k]
        x = r_e * cos_vals[k]
        y = r_e * sin_vals[k]
        vu = bm.verts.new((x, y, CYL_WALL_Z))
        vl = bm.verts.new((x, y, -CYL_WALL_Z))
        reed_upper_verts.append(vu)
        reed_lower_verts.append(vl)

    # Chamfer to upper wall
    for k in range(SECTORS):
        k_next = (k + 1) % SECTORS
        f = bm.faces.new([rim_chamfer_verts[k], reed_upper_verts[k], reed_upper_verts[k_next], rim_chamfer_verts[k_next]])
        f.smooth = True

    # Vertical reed wall
    for k in range(SECTORS):
        k_next = (k + 1) % SECTORS
        f = bm.faces.new([reed_upper_verts[k], reed_lower_verts[k], reed_lower_verts[k_next], reed_upper_verts[k_next]])
        f.smooth = True

    # 5. Bottom (Tails) Side
    bot_chamfer_verts = []
    for k in range(SECTORS):
        r_e = tooth_radii[k]
        x = r_e * cos_vals[k]
        y = r_e * sin_vals[k]
        v = bm.verts.new((x, y, -CHAMFER_Z))
        bot_chamfer_verts.append(v)

    for k in range(SECTORS):
        k_next = (k + 1) % SECTORS
        f = bm.faces.new([reed_lower_verts[k], bot_chamfer_verts[k], bot_chamfer_verts[k_next], reed_lower_verts[k_next]])
        f.smooth = True

    bot_flat_verts = []
    for k in range(SECTORS):
        x = RIM_CREST_RADIUS * cos_vals[k]
        y = RIM_CREST_RADIUS * sin_vals[k]
        v = bm.verts.new((x, y, -RIM_Z))
        bot_flat_verts.append(v)

    for k in range(SECTORS):
        k_next = (k + 1) % SECTORS
        f = bm.faces.new([bot_chamfer_verts[k], bot_flat_verts[k], bot_flat_verts[k_next], bot_chamfer_verts[k_next]])
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

    # Convert to mesh
    mesh_data = bpy.data.meshes.new("Mesh_TossCoin_Heads_Relief")
    bm.to_mesh(mesh_data)
    bm.free()
    mesh_data.update()
    
    coin_obj = bpy.data.objects.new("TossCoin_Heads_Relief", mesh_data)
    bpy.context.collection.objects.link(coin_obj)
    bpy.context.view_layer.objects.active = coin_obj
    coin_obj.select_set(True)

    # Add Weighted Normal Modifier
    mod = coin_obj.modifiers.new(name="WeightedNormal", type='WEIGHTED_NORMAL')
    mod.weight = 50
    mod.keep_sharp = True

    # Apply all transforms directly
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    out_dir = r"G:\Dev\Toss-VR\Assets\Meshes\Coin"
    os.makedirs(out_dir, exist_ok=True)
    fbx_path = os.path.join(out_dir, "TossCoin_Heads_Relief.fbx")
    
    # Export FBX
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
    print(f"Exported FBX successfully to: {fbx_path}")
    print(f"Vertices: {len(mesh_data.vertices)}, Polygons: {len(mesh_data.polygons)}")
    print("=== Build Complete ===")

if __name__ == "__main__":
    build_toss_coin()
