"""
tools/sculpt_master_patch_v2.py
Explicitly authors the Toss-VR Hero Coin Heads portrait patch bas-relief
following classical numismatic sculpture principles aligned with the master reference.

Anatomical & Sculptural Hierarchy:
1. Exact Silhouette & Boundary Draft (80-85 deg drop to coin recessed field)
2. Level 1 Macro Masses:
   - Frontal bone plane (forehead)
   - Zygomatic / cheekbone mass & cheek hollow
   - Mandibular mass (jawline) with distinct step down to neck
   - Neck cylinder with angled base truncation
   - Cranial vault foundation
3. Level 2 Facial Planes & Features:
   - Brow ridge (superciliary arch)
   - Almond orbital depression
   - Sculpted upper eyelid with overhang ledge and deep crease
   - Sculpted lower eyelid
   - Grecian straight nose bridge prism + apex tip + alar wing with crisp alar sulcus
   - Philtrum trough
   - Upper lip vermilion wedge + lower lip cushion
   - Negative oral fissure groove + oral commissure depression
   - Labiomental groove + flattened chin pad
   - Ear relief: helix, concha, antihelix, tragus, lobule
   - Cascading curly ear tendril
4. Level 2 Hair Masses:
   - 8 primary sweeping ribbon locks with convex crests and deep shadow valleys
   - Interlaced braided chignon bun composed of 4 overlapping volumetric coils
5. Numismatic Depth Budget:
   - Field: Z = 0.00 mm (z = 0.70 mm in coin space)
   - Max Relief: Z = 0.41 mm (z = 1.11 mm in coin space, safely 90 um under 1.20 mm rim)
"""

import os
import math
import numpy as np
import cv2

# Coin & Patch Dimensions (in millimeters)
COIN_RADIUS = 15.0
PATCH_HALF_W = 12.5 # Covers X from -12.5 to +12.5 mm
PATCH_HALF_H = 13.5 # Covers Y from -13.5 to +13.5 mm
RES = 1024 # 1024x1024 high resolution grid (~25 um per pixel)

MAX_RELIEF_HEIGHT = 0.410 # mm

def smoothstep(edge0, edge1, x):
    t = np.clip((x - edge0) / (edge1 - edge0 + 1e-12), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)

def smax(a, b, k=0.08):
    """Polynomial smooth maximum of two arrays"""
    h = np.maximum(k - np.abs(a - b), 0.0) / k
    return np.maximum(a, b) + h * h * h * (k * (1.0 / 6.0))

def smin(a, b, k=0.08):
    return -smax(-a, -b, k)

def safe_taper(t, power=0.5):
    s = np.maximum(np.sin(np.pi * np.clip(t, 0.0, 1.0)), 0.0)
    return s ** power

def dist_to_segment(px, py, x1, y1, x2, y2):
    dx = x2 - x1
    dy = y2 - y1
    l2 = dx*dx + dy*dy
    if l2 == 0:
        return np.hypot(px - x1, py - y1), np.zeros_like(px)
    t = np.clip(((px - x1) * dx + (py - y1) * dy) / l2, 0.0, 1.0)
    proj_x = x1 + t * dx
    proj_y = y1 + t * dy
    dist = np.hypot(px - proj_x, py - proj_y)
    return dist, t

def dist_to_polyline(px, py, points):
    min_dist = np.full_like(px, 1e9, dtype=np.float32)
    best_t = np.zeros_like(px, dtype=np.float32)
    total_len = 0.0
    seg_lens = []
    for i in range(len(points) - 1):
        l = math.hypot(points[i+1][0] - points[i][0], points[i+1][1] - points[i][1])
        seg_lens.append(l)
        total_len += l
    
    cur_len = 0.0
    for i in range(len(points) - 1):
        x1, y1 = points[i]
        x2, y2 = points[i+1]
        d, seg_t = dist_to_segment(px, py, x1, y1, x2, y2)
        closer = d < min_dist
        min_dist = np.where(closer, d, min_dist)
        global_t = (cur_len + seg_t * seg_lens[i]) / (total_len + 1e-12)
        best_t = np.where(closer, global_t, best_t)
        cur_len += seg_lens[i]
    return min_dist, best_t

def eval_hair_ribbon(X, Y, points, width_start, width_end, crest_amp, valley_depth=0.04):
    """
    Evaluates a voluptuous 3D hair ribbon lock:
    - Broad convex crest across width
    - Sharp shadow valleys on both lateral borders
    """
    dist, t = dist_to_polyline(X, Y, points)
    width = width_start + t * (width_end - width_start)
    # Normalized across-distance u in [-1, 1]
    u = dist / (width + 1e-12)
    in_ribbon = u <= 1.0
    # Convex crest profile: cos^2 or (1 - u^2)^1.5
    u_clamped = np.clip(u, 0.0, 1.0)
    crest = np.where(in_ribbon, crest_amp * (np.maximum(1.0 - u_clamped*u_clamped, 0.0))**1.4, 0.0)
    # Taper ends smoothly
    sin_term = np.maximum(np.sin(np.pi * np.clip(t, 0.0, 1.0)), 0.0)
    end_taper = np.sqrt(sin_term)
    crest = crest * end_taper
    # Valley groove along borders
    valley = np.where(np.abs(u - 1.0) < 0.35, -valley_depth * np.exp(-((u - 1.0)/0.18)**2) * end_taper, 0.0)
    return crest + valley

def generate_heads_patch_relief():
    print("=== Generating Master Heads Bas-Relief Patch ===")
    
    # 1. Coordinate grids across entire coin field (-15.0 mm to +15.0 mm)
    lin = np.linspace(-15.0, 15.0, RES, dtype=np.float32)
    # Row 0 is top of image (Y = +15.0 mm), Row 1023 is bottom (Y = -15.0 mm)
    # Col 0 is left of image (X = -15.0 mm), Col 1023 is right (X = +15.0 mm)
    X, Y = np.meshgrid(lin, -lin)
    
    # 2. Load clean, aligned silhouette mask
    mask_path = r"G:\Dev\Toss-VR\Assets\Textures\Coin\heads_clean_mask_new_1024.png"
    mask_bust = cv2.imread(mask_path, cv2.IMREAD_GRAYSCALE)
    if mask_bust is None:
        raise FileNotFoundError(f"Mask not found: {mask_path}")
    
    # Clean boundary
    mask_bust = (mask_bust > 127).astype(np.uint8) * 255
    
    # Compute signed distance field for numismatic draft chamfer
    scale_mm = 30.0 / RES
    dist_in = cv2.distanceTransform(mask_bust, cv2.DIST_L2, 5) * scale_mm
    dist_out = cv2.distanceTransform(255 - mask_bust, cv2.DIST_L2, 5) * scale_mm
    sdf = dist_in - dist_out
    
    # Numismatic draft step: 80-85 deg drop to field over 0.22 mm
    draft_width = 0.22 # mm
    draft_shelf = 0.08 # mm base shelf
    draft_factor = smoothstep(0.0, draft_width, dist_in)
    
    # -------------------------------------------------------------
    # 3. LEVEL 1: MACRO ANATOMICAL VOLUMES
    # -------------------------------------------------------------
    # Cranial dome (ellipsoid foundation)
    cran_x, cran_y = 1.5, 5.0
    cran_rx, cran_ry = 6.8, 5.8
    cran_dist = ((X - cran_x)/cran_rx)**2 + ((Y - cran_y)/cran_ry)**2
    cranial_mass = 0.26 * np.sqrt(np.clip(1.0 - cran_dist, 0.0, 1.0))
    
    # Neck cylinder
    # Centerline from (1.5, -4.0) to (5.0, -9.0)
    neck_dist, neck_t = dist_to_segment(X, Y, 1.5, -4.0, 4.5, -9.5)
    neck_mass = 0.22 * np.exp(-(neck_dist / 3.6)**2) * (1.0 - 0.25 * neck_t)
    
    # Facial mass (planar tilt): Face slopes gracefully, elevated at cheek/nose, lower at ear
    face_tilt = 0.24 + 0.012 * (X + 2.0) - 0.006 * (Y - 0.0)
    face_mask_dist, _ = dist_to_segment(X, Y, -6.0, 4.0, -1.0, -3.0)
    face_foundation = np.clip(face_tilt, 0.12, 0.32) * np.exp(-(face_dist / 4.5)**2 if 'face_dist' in locals() else -(face_mask_dist / 4.8)**2)
    
    # Combine Level 1 Macro Base
    macro_base = smax(cranial_mass, neck_mass, 0.06)
    macro_base = smax(macro_base, face_foundation, 0.06)
    
    # -------------------------------------------------------------
    # 4. LEVEL 2: FACIAL PLANES & ANATOMICAL FEATURES
    # -------------------------------------------------------------
    Z_features = np.zeros_like(X, dtype=np.float32)
    
    # --- A. Forehead Plane ---
    # Broad, calm plane spanning X in [-5.5, -1.5], Y in [3.5, 7.2]
    fh_mask = smoothstep(1.8, 0.0, np.hypot(X - (-3.2), Y - 5.4) - 1.8)
    fh_plane = (0.28 - 0.010 * (X + 3.2) + 0.005 * (Y - 5.4))
    Z_features += 0.08 * fh_mask * fh_plane
    
    # --- B. Brow Ridge (Superciliary Arch) ---
    brow_pts = [(-6.2, 3.2), (-5.4, 3.6), (-4.2, 3.8), (-3.0, 3.5)]
    brow_d, brow_t = dist_to_polyline(X, Y, brow_pts)
    brow_ridge = 0.075 * np.exp(-(brow_d / 0.45)**2) * safe_taper(brow_t)
    Z_features += brow_ridge
    
    # --- C. Orbital Depression (Eye Socket) ---
    eye_cx, eye_cy = -4.6, 3.4
    orbit_d = np.hypot((X - eye_cx)/1.3, (Y - eye_cy)/0.75)
    orbit_recess = -0.055 * smoothstep(1.2, 0.0, orbit_d)
    Z_features += orbit_recess
    
    # --- D. Upper Eyelid with Overhang Ledge ---
    # Elevated ledge that drops off sharply into the eye slit
    upper_lid_pts = [(-5.4, 3.35), (-4.8, 3.58), (-4.0, 3.55), (-3.5, 3.30)]
    ul_d, ul_t = dist_to_polyline(X, Y, upper_lid_pts)
    ul_taper = safe_taper(ul_t)
    # Sharp asymmetrical profile: gentle slope up from brow, sharp cliff down to eye slit
    upper_lid = 0.065 * np.exp(-(ul_d / 0.22)**2) * ul_taper
    Z_features += upper_lid
    
    # Eyelid Crease (deep narrow groove above the lid)
    crease_pts = [(-5.3, 3.55), (-4.7, 3.78), (-3.9, 3.72), (-3.4, 3.45)]
    cr_d, cr_t = dist_to_polyline(X, Y, crease_pts)
    lid_crease = -0.028 * np.exp(-(cr_d / 0.14)**2) * safe_taper(cr_t)
    Z_features += lid_crease
    
    # Eye Slit & Lower Eyelid
    slit_pts = [(-5.2, 3.25), (-4.6, 3.32), (-3.8, 3.28)]
    sl_d, sl_t = dist_to_polyline(X, Y, slit_pts)
    eye_slit = -0.035 * np.exp(-(sl_d / 0.12)**2) * safe_taper(sl_t)
    Z_features += eye_slit
    
    lower_lid = 0.025 * np.exp(-((sl_d - 0.22) / 0.18)**2) * safe_taper(sl_t)
    Z_features += lower_lid
    
    # --- E. Grecian Nose Prism & Alar Wing ---
    # Straight bridge centerline from (-6.2, 3.2) to (-8.0, 0.14)
    nose_pts = [(-6.2, 3.2), (-6.8, 2.1), (-7.4, 1.1), (-8.0, 0.14)]
    nose_d, nose_t = dist_to_polyline(X, Y, nose_pts)
    # Triangular prism cross section: sharp crest along centerline
    nose_width = 0.65 - 0.15 * nose_t
    nose_prism = 0.110 * np.maximum(1.0 - (nose_d / nose_width), 0.0) ** 1.3
    Z_features += nose_prism
    
    # Nose tip apex
    tip_d = np.hypot(X - (-8.0), Y - 0.14)
    nose_tip = 0.075 * np.exp(-(tip_d / 0.55)**2)
    Z_features += nose_tip
    
    # Alar wing (curved nostril flare)
    alar_cx, alar_cy = -6.4, 0.05
    alar_d = np.hypot((X - alar_cx)/0.65, (Y - alar_cy)/0.38)
    alar_wing = 0.060 * np.exp(-(alar_d / 0.60)**2)
    Z_features += alar_wing
    
    # Alar groove (deep crease separating alar from cheek)
    alar_groove_pts = [(-6.9, 0.45), (-6.1, 0.35), (-5.8, -0.05), (-6.1, -0.25)]
    ag_d, ag_t = dist_to_polyline(X, Y, alar_groove_pts)
    alar_groove = -0.040 * np.exp(-(ag_d / 0.16)**2) * safe_taper(ag_t)
    Z_features += alar_groove
    
    # --- F. Philtrum & Mouth ---
    # Philtrum trough
    phil_pts = [(-6.3, -0.20), (-6.5, -0.80), (-6.8, -1.30)]
    ph_d, ph_t = dist_to_polyline(X, Y, phil_pts)
    philtrum = -0.020 * np.exp(-(ph_d / 0.25)**2) * safe_taper(ph_t)
    Z_features += philtrum
    
    # Upper lip vermilion (slanted planar wedge)
    ulip_pts = [(-7.1, -1.40), (-6.4, -1.55), (-5.6, -1.75)]
    ulip_d, ulip_t = dist_to_polyline(X, Y, ulip_pts)
    upper_lip = 0.070 * np.exp(-(ulip_d / 0.32)**2) * safe_taper(ulip_t)
    Z_features += upper_lip
    
    # Oral fissure (sharp negative groove) + Commissure depression
    fissure_pts = [(-6.7, -1.90), (-6.0, -1.95), (-5.4, -1.92)]
    fis_d, fis_t = dist_to_polyline(X, Y, fissure_pts)
    oral_fissure = -0.048 * np.exp(-(fis_d / 0.14)**2) * safe_taper(fis_t)
    commissure = -0.035 * np.exp(-((X - (-5.4))**2 + (Y - (-1.92))**2) / 0.12)
    Z_features += (oral_fissure + commissure)
    
    # Lower lip cushion
    llip_pts = [(-6.7, -2.40), (-6.1, -2.35), (-5.5, -2.25)]
    llip_d, llip_t = dist_to_polyline(X, Y, llip_pts)
    lower_lip = 0.065 * np.exp(-(llip_d / 0.36)**2) * safe_taper(llip_t)
    Z_features += lower_lip
    
    # Labiomental groove
    lmg_pts = [(-6.0, -3.10), (-5.4, -3.05), (-4.8, -2.95)]
    lmg_d, lmg_t = dist_to_polyline(X, Y, lmg_pts)
    labiomental = -0.035 * np.exp(-(lmg_d / 0.25)**2) * safe_taper(lmg_t)
    Z_features += labiomental
    
    # --- G. Chin & Mandible (Jawline Step) ---
    chin_d = np.hypot(X - (-6.4), Y - (-4.0))
    chin_pad = 0.090 * np.exp(-(chin_d / 0.85)**2)
    Z_features += chin_pad
    
    # Mandibular jawline ridge: crisp ridge that steps down into the neck
    jaw_pts = [(-6.0, -4.2), (-4.5, -4.8), (-2.5, -4.6), (-0.5, -3.8), (0.8, -1.8)]
    jaw_d, jaw_t = dist_to_polyline(X, Y, jaw_pts)
    jaw_ridge = 0.055 * np.exp(-(jaw_d / 0.35)**2) * safe_taper(jaw_t)
    # Submandibular step: negative shadow drop under the jaw into the neck
    under_jaw_pts = [(-4.2, -5.2), (-2.2, -5.0), (-0.2, -4.2)]
    uj_d, uj_t = dist_to_polyline(X, Y, under_jaw_pts)
    under_jaw_shadow = -0.045 * np.exp(-(uj_d / 0.38)**2) * safe_taper(uj_t)
    Z_features += (jaw_ridge + under_jaw_shadow)
    
    # --- H. Cheekbone (Zygomatic) & Cheek Hollow ---
    zyg_cx, zyg_cy = -3.2, 0.8
    zyg_d = np.hypot((X - zyg_cx)/1.8, (Y - zyg_cy)/1.2)
    zyg_mass = 0.070 * np.exp(-(zyg_d / 1.5)**2)
    Z_features += zyg_mass
    
    # --- I. Ear Relief & Ear Tendril ---
    # Helix outer rim
    helix_pts = [(1.5, 2.5), (2.8, 2.8), (3.6, 1.8), (3.6, -0.2), (2.5, -1.0), (1.6, -1.2)]
    h_d, h_t = dist_to_polyline(X, Y, helix_pts)
    helix = 0.060 * np.exp(-(h_d / 0.22)**2) * safe_taper(h_t)
    # Concha hollow
    concha_d = np.hypot((X - 2.4)/0.6, (Y - 0.8)/0.9)
    concha = -0.055 * np.exp(-(concha_d / 0.7)**2)
    # Antihelix
    anti_pts = [(2.2, 2.1), (2.7, 1.6), (2.8, 0.6), (2.4, -0.2)]
    a_d, a_t = dist_to_polyline(X, Y, anti_pts)
    antihelix = 0.040 * np.exp(-(a_d / 0.18)**2) * safe_taper(a_t)
    # Earlobe
    lobe_d = np.hypot(X - 2.0, Y - (-1.2))
    lobe = 0.050 * np.exp(-(lobe_d / 0.45)**2)
    Z_features += (helix + concha + antihelix + lobe)
    
    # Ear tendril (delicate S-curl in front of ear)
    tendril_pts = [(0.5, 1.5), (0.2, 0.2), (0.6, -1.2), (1.0, -2.6), (1.8, -3.8), (2.5, -4.5)]
    td_d, td_t = dist_to_polyline(X, Y, tendril_pts)
    tendril = 0.045 * np.exp(-(td_d / 0.20)**2) * safe_taper(td_t, 0.6)
    Z_features += tendril
    
    # -------------------------------------------------------------
    # 5. LEVEL 2: EIGHT MAJOR HAIR RIBBON MASSES & CHIGNON BUN
    # -------------------------------------------------------------
    Z_hair = np.zeros_like(X, dtype=np.float32)
    
    # The 8 primary hair ribbon locks (calibrated from Panel 4 close-up):
    hair_ribbons = [
        # Lock 1: Frontal hairline wave
        ([(-4.5, 7.8), (-3.0, 9.6), (-0.8, 10.8), (1.8, 10.6), (4.2, 9.8), (6.5, 7.8), (7.8, 5.2)], 0.75, 1.10, 0.095),
        # Lock 2: Parietal crest wave
        ([(-4.2, 6.8), (-2.4, 8.8), (-0.2, 9.6), (2.2, 9.0), (4.8, 8.2), (7.0, 6.6), (8.2, 4.5)], 0.85, 1.25, 0.105),
        # Lock 3: Mid-parietal sweep
        ([(-3.8, 5.8), (-1.8, 7.6), (0.5, 8.2), (2.8, 7.5), (5.2, 6.8), (7.5, 5.2), (8.5, 3.2)], 0.90, 1.30, 0.115),
        # Lock 4: Temporal major lock
        ([(-3.2, 4.8), (-1.2, 6.2), (1.2, 6.5), (3.5, 5.8), (5.8, 4.8), (7.8, 3.6), (8.6, 2.2)], 0.95, 1.35, 0.120),
        # Lock 5: Supra-auricular ribbon (curves above ear)
        ([(-2.2, 3.8), (-0.2, 4.8), (2.0, 4.8), (4.2, 4.0), (6.2, 3.2), (7.8, 2.2), (8.4, 1.2)], 0.85, 1.20, 0.110),
        # Lock 6: Post-auricular ribbon (behind ear)
        ([(0.5, 2.8), (2.2, 3.2), (4.0, 2.6), (5.8, 1.8), (7.2, 1.0), (8.0, 0.2)], 0.80, 1.15, 0.100),
        # Lock 7: Nape upward sweep
        ([(3.5, -2.0), (4.5, -0.8), (5.8, 0.2), (7.0, 0.5), (8.0, -0.2)], 0.75, 1.10, 0.090),
        # Lock 8: Temple wave
        ([(-4.0, 6.8), (-2.8, 5.4), (-1.2, 4.2), (0.5, 3.6), (2.5, 3.5)], 0.65, 0.90, 0.085),
    ]
    
    for pts, w_start, w_end, amp in hair_ribbons:
        ribbon_val = eval_hair_ribbon(X, Y, pts, w_start, w_end, amp, valley_depth=0.035)
        Z_hair = np.maximum(Z_hair, ribbon_val)
    
    # Chignon Bun: 4 interlaced spiral torus coils
    chignon_coils = [
        # Center (X, Y), Radius R, Tube radius r, Amplitude
        ((8.2, 3.2), 1.5, 0.75, 0.115), # Upper loop
        ((8.0, 0.4), 1.4, 0.70, 0.105), # Lower loop
        ((9.4, 1.8), 1.6, 0.80, 0.125), # Posterior loop
        ((8.4, 1.8), 0.9, 0.55, 0.120), # Central knot
    ]
    
    for (cx, cy), R_loop, r_tube, amp in chignon_coils:
        d_center = np.hypot(X - cx, Y - cy)
        d_tube = np.abs(d_center - R_loop)
        in_coil = d_tube <= r_tube
        d_ratio = np.clip(d_tube / r_tube, 0.0, 1.0)
        coil_h = np.where(in_coil, amp * (1.0 - d_ratio**2)**1.3, 0.0)
        # Deep shadow crevice between coils
        crevice = np.where(np.abs(d_tube - r_tube) < 0.25, -0.030 * np.exp(-((d_tube - r_tube)/0.12)**2), 0.0)
        Z_hair = np.maximum(Z_hair, coil_h + crevice)
    
    # -------------------------------------------------------------
    # 6. COMBINE VOLUMES & INTEGRATE RELIEF
    # -------------------------------------------------------------
    # Total relief = Macro Base + Facial Features + Hair Structure
    Z_combined = macro_base + Z_features + Z_hair
    
    # Apply draft chamfer step along silhouette
    Z_relief = np.where(sdf > 0.0, draft_shelf + draft_factor * (Z_combined - draft_shelf), 0.0)
    
    # Normalize peak relief to exactly MAX_RELIEF_HEIGHT (0.410 mm)
    peak = np.max(Z_relief)
    if peak > 0:
        Z_relief = (Z_relief / peak) * MAX_RELIEF_HEIGHT
    
    # Frequency-aware smoothing: smooth planar skin areas while preserving sharp ridges
    # Mask of smooth skin (forehead, cheek, throat)
    is_skin = (dist_in > 0.35) & ((X < -0.5) | (Y < -4.0)) & ~((X > 0.0) & (Y > -1.5))
    Z_bilateral = cv2.bilateralFilter(Z_relief.astype(np.float32), 7, 0.015, 0.015)
    # Blend skin softly
    skin_weight = smoothstep(0.3, 0.6, dist_in) * is_skin.astype(np.float32)
    Z_final = Z_relief * (1.0 - skin_weight * 0.5) + Z_bilateral * (skin_weight * 0.5)
    
    # Ensure zero outside silhouette
    Z_final = np.where(sdf > 0.0, Z_final, 0.0)
    
    print(f"Generated patch bas-relief: Min = {np.min(Z_final):.4f} mm, Max = {np.max(Z_final):.4f} mm")
    
    # Save float32 numpy array
    out_npy = r"G:\Dev\Toss-VR\Assets\Textures\Coin\heads_master_patch_v2_float32.npy"
    np.save(out_npy, Z_final.astype(np.float32))
    print(f"Saved: {out_npy}")
    
    # Generate shaded analytical preview
    dy, dx = np.gradient(Z_final)
    scale = scale_mm
    lx, ly, lz = -0.6, 0.6, 0.52
    ll = math.hypot(lx, ly, lz)
    lx, ly, lz = lx/ll, ly/ll, lz/ll
    nx = -dx / scale
    ny = dy / scale
    nz = np.ones_like(nx)
    nl = np.sqrt(nx*nx + ny*ny + nz*nz)
    nx, ny, nz = nx/nl, ny/nl, nz/nl
    dot = np.clip(nx*lx + ny*ly + nz*lz, 0.0, 1.0)
    shaded = (dot * 255).astype(np.uint8)
    shaded = cv2.applyColorMap(shaded, cv2.COLORMAP_BONE)
    
    out_preview = r"G:\Dev\Toss-VR\Assets\Textures\Coin\heads_master_patch_v2_preview.png"
    cv2.imwrite(out_preview, shaded)
    print(f"Saved: {out_preview}")
    return Z_final

if __name__ == "__main__":
    generate_heads_patch_relief()
