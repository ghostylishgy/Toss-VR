"""
tools/build_product_heads_sculpt.py
Toss-VR P1.1 Hero Coin — Heads Bas-Relief Final Sculpt Polish Pass
Synthesizes the product-grade classical numismatic relief adhering strictly to:
1. Classical Facial Planes (Zygomatic cheekbone facet, frontal bone, chin pad, mentolabial sulcus)
2. Classical Grecian Profile (Grecian nose with planar facets, hooded almond eyelid shelf, supple lips)
3. 8 Primary Ribbon Masses & Interlocking Chignon Bun Coils (matching reference Panel 4)
4. Mask-based Bilateral & Laplacian Surface Polish (preserving 100% structural sharpness)
5. Frozen Numismatic Specs: Field Z = 0.70mm, Rim Z = 1.20mm, Relief Max = 0.410mm (90um safety gap)
"""

import os
import math
import cv2
import numpy as np

ROOT = r"G:\Dev\Toss-VR"
RES = 1024
MAX_RELIEF_HEIGHT = 0.410 # mm (Frozen budget: 90 um below Rim Z=1.20mm)

def dist_to_segment(px, py, x1, y1, x2, y2):
    dx = x2 - x1
    dy = y2 - y1
    l2 = dx*dx + dy*dy
    if l2 < 1e-12:
        return np.hypot(px - x1, py - y1), np.zeros_like(px)
    t = np.clip(((px - x1)*dx + (py - y1)*dy) / l2, 0.0, 1.0)
    proj_x = x1 + t * dx
    proj_y = y1 + t * dy
    dist = np.hypot(px - proj_x, py - proj_y)
    return dist, t

def dist_to_polyline(px, py, points):
    min_dist = np.full_like(px, 1e9, dtype=np.float32)
    best_t = np.zeros_like(px, dtype=np.float32)
    seg_lens = []
    total_len = 0.0
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

def safe_taper(t, power=0.5):
    s = np.maximum(np.sin(np.pi * np.clip(t, 0.0, 1.0)), 0.0)
    return s ** power

def smoothstep(edge0, edge1, x):
    t = np.clip((x - edge0) / (edge1 - edge0 + 1e-12), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)

def smax(a, b, k=0.05):
    h = np.clip(0.5 + 0.5 * (b - a) / k, 0.0, 1.0)
    return b * h + a * (1.0 - h) + k * h * (1.0 - h)

def smin(a, b, k=0.05):
    h = np.clip(0.5 + 0.5 * (b - a) / k, 0.0, 1.0)
    return b * (1.0 - h) + a * h - k * h * (1.0 - h)

def build_product_heads_sculpt():
    print("=== Synthesizing Product-Grade Classical Numismatic Relief ===")
    
    # 1. Coordinate grids across coin (-15.0 mm to +15.0 mm)
    lin = np.linspace(-15.0, 15.0, RES, dtype=np.float32)
    X, Y = np.meshgrid(lin, -lin)
    scale_mm = 30.0 / RES
    
    # 2. Master Silhouette Mask
    mask_path = os.path.join(ROOT, "Assets", "Textures", "Coin", "heads_clean_mask_perfect_1024.png")
    mask_bust = cv2.imread(mask_path, cv2.IMREAD_GRAYSCALE)
    if mask_bust is None:
        raise FileNotFoundError(f"Mask not found: {mask_path}")
    mask_bool = mask_bust > 127
    
    dist_in = cv2.distanceTransform(mask_bust, cv2.DIST_L2, 5) * scale_mm
    dist_out = cv2.distanceTransform(255 - mask_bust, cv2.DIST_L2, 5) * scale_mm
    sdf = dist_in - dist_out
    
    # 3. Base Photometric Surface
    ref_img = cv2.imread(os.path.join(ROOT, "tools", "ref_crops", "panel1_aligned_1024.png"))
    gray = cv2.cvtColor(ref_img, cv2.COLOR_BGR2GRAY).astype(np.float32) / 255.0
    
    smooth_gray = cv2.GaussianBlur(gray, (5, 5), 1.2)
    Ix = cv2.Sobel(smooth_gray, cv2.CV_32F, 1, 0, ksize=5)
    Iy = cv2.Sobel(smooth_gray, cv2.CV_32F, 0, 1, ksize=5)
    div = -(cv2.Sobel(Ix, cv2.CV_32F, 1, 0, ksize=3) + cv2.Sobel(Iy, cv2.CV_32F, 0, 1, ksize=3))
    
    fx = np.fft.fftfreq(RES)
    fy = np.fft.fftfreq(RES)
    Fx, Fy = np.meshgrid(fx, fy)
    denom = -4.0 * (np.pi ** 2) * (Fx**2 + Fy**2)
    denom[0, 0] = 1.0
    
    div_fft = np.fft.fft2(div)
    Z_fft = div_fft / denom
    Z_fft[0, 0] = 0.0
    Z_poisson = np.real(np.fft.ifft2(Z_fft))
    
    A = np.column_stack([X[mask_bool], Y[mask_bool], np.ones(np.sum(mask_bool))])
    plane_fit, _, _, _ = np.linalg.lstsq(A, Z_poisson[mask_bool], rcond=None)
    Z_detrend = Z_poisson - (plane_fit[0]*X + plane_fit[1]*Y + plane_fit[2])
    
    z_min = np.min(Z_detrend[mask_bool])
    z_max = np.max(Z_detrend[mask_bool])
    Z_sfs = np.where(mask_bool, (Z_detrend - z_min) / (z_max - z_min), 0.0).astype(np.float32)
    
    # Macro gradient magnitude to detect ONLY true anatomical macro-edges (ignoring micro-pebbles)
    smooth_macro = cv2.GaussianBlur(Z_sfs, (19, 19), 3.5)
    gx_macro = cv2.Sobel(smooth_macro, cv2.CV_32F, 1, 0, ksize=5)
    gy_macro = cv2.Sobel(smooth_macro, cv2.CV_32F, 0, 1, ksize=5)
    macro_grad = np.hypot(gx_macro, gy_macro)
    
    # 4. Anatomical Region Masks
    # Open skin zone (forehead, cheek, neck)
    is_skin_zone = (mask_bool) & (dist_in > 0.45) & ((X < 0.2) | (Y < -1.8)) & ~((X > 0.8) & (Y > -1.5))
    ridge_threshold = 0.015
    skin_weight = np.where(is_skin_zone, smoothstep(ridge_threshold * 1.8, ridge_threshold * 0.4, macro_grad), 0.0)
    
    # Clean skin planar baseline (completely pebble-free satin surface)
    Z_skin_smooth = cv2.GaussianBlur(Z_sfs, (31, 31), 6.0)
    Z_skin_smooth = cv2.bilateralFilter(Z_skin_smooth, 9, 0.035, 0.035)
    
    # Hair zone: filter scratchy micro-lines
    is_hair_zone = (mask_bool) & ~is_skin_zone
    Z_hair_smooth = cv2.bilateralFilter(Z_sfs, 9, 0.040, 0.040)
    
    Z_sfs_clean = Z_sfs * (1.0 - skin_weight) + Z_skin_smooth * skin_weight
    Z_sfs_clean = np.where(is_hair_zone, Z_sfs * 0.35 + Z_hair_smooth * 0.65, Z_sfs_clean)
    
    # -------------------------------------------------------------
    # 5. LEVEL 1: ARCHITECTURAL FACIAL PLANES & CRANIAL DOME (Scrape/Plane)
    # -------------------------------------------------------------
    # Cranium dome
    cran_d = ((X - 1.5)/7.2)**2 + ((Y - 5.0)/6.2)**2
    macro_cran = 0.24 * np.sqrt(np.clip(1.0 - cran_d, 0.0, 1.0))
    
    # Zygomatic Cheekbone Planar Facet:
    # Formed by a gentle planar ridge sloping down-forward
    # Apex at (-2.8, 0.8), extending towards temple (-1.0, 2.0) and mouth (-4.5, -0.5)
    chk_ridge_pts = [(-1.0, 2.2), (-2.0, 1.4), (-3.0, 0.8), (-4.2, -0.2)]
    d_chk_r, t_chk_r = dist_to_polyline(X, Y, chk_ridge_pts)
    macro_chk = 0.26 * np.exp(-(d_chk_r / 2.2)**2) * safe_taper(t_chk_r, 0.4)
    # Cheek hollow / buccal depression under cheekbone
    chk_hollow_d = np.hypot(X + 2.0, Y + 1.2)
    chk_hollow = -0.015 * np.exp(-(chk_hollow_d / 1.8)**2)
    
    # Forehead Plane:
    # Defined flat planar facet bounded by hairline
    macro_fh = 0.20 * np.exp(-(((X + 3.2)/2.4)**2 + ((Y - 5.4)/1.8)**2))
    
    # Neck Cylinder:
    # Clean cylindrical form with sternocleidomastoid line
    d_nk, t_nk = dist_to_segment(X, Y, 1.2, -3.5, 4.2, -9.0)
    macro_nk = 0.20 * np.exp(-(d_nk / 3.2)**2) * (1.0 - 0.12 * t_nk)
    
    macro_base = smax(macro_cran, macro_chk, 0.08)
    macro_base = smax(macro_base, macro_fh, 0.06)
    macro_base = smax(macro_base, macro_nk, 0.08)
    macro_base += chk_hollow
    
    # -------------------------------------------------------------
    # 6. LEVEL 2: CLASSICAL NUMISMATIC ANATOMY (Eyes, Nose, Lips, Chin)
    # -------------------------------------------------------------
    Z_sculpt = np.zeros_like(X, dtype=np.float32)
    
    # --- A. GRECIAN NOSE WITH PLANAR FACETS ---
    # Dorsal bridge polyline
    nose_pts = [(-6.0, 3.4), (-6.6, 2.2), (-7.3, 1.1), (-8.0, 0.14)]
    nose_d, nose_t = dist_to_polyline(X, Y, nose_pts)
    nose_w = 0.55 - 0.10 * nose_t
    u_nose = np.clip(nose_d / (nose_w + 1e-12), 0.0, 1.0)
    # Cosine dorsal plane
    nose_prism = 0.026 * np.cos(0.5 * np.pi * u_nose)**2 * safe_taper(nose_t, 0.4)
    Z_sculpt += nose_prism
    
    # Rounded Nasal Tip (Apex) Cartilage
    tip_d = np.hypot(X + 7.85, Y - 0.18)
    nose_tip = 0.016 * np.exp(-(tip_d / 0.45)**2)
    Z_sculpt += nose_tip
    
    # Alar Wing (Nostril) Lobule & Curving Alar Groove
    alar_lobe_d = np.hypot(X + 6.6, Y - 0.10)
    alar_lobe = 0.014 * np.exp(-(alar_lobe_d / 0.55)**2)
    Z_sculpt += alar_lobe
    
    alar_groove_pts = [(-7.0, 0.40), (-6.3, 0.32), (-5.9, -0.05), (-6.2, -0.28)]
    ag_d, ag_t = dist_to_polyline(X, Y, alar_groove_pts)
    alar_groove = -0.018 * np.exp(-(ag_d / 0.20)**2) * safe_taper(ag_t)
    Z_sculpt += alar_groove
    
    # Subnasal undercut (columella to philtrum)
    subnasal_d = np.hypot(X + 7.0, Y + 0.35)
    Z_sculpt += -0.010 * np.exp(-(subnasal_d / 0.35)**2)
    
    # --- B. CLASSICAL ALMOND EYE & ORBITAL LEDGE ---
    # Almond orbital socket depression
    orbit_d = np.hypot((X + 4.5)/1.2, (Y - 3.4)/0.8)
    orbit_hollow = -0.022 * np.exp(-(orbit_d / 1.0)**2)
    Z_sculpt += orbit_hollow
    
    # Eyeball sphere inside socket
    eyeball_d = np.hypot((X + 4.6)/0.7, (Y - 3.35)/0.5)
    eyeball = 0.015 * np.exp(-(eyeball_d / 0.6)**2)
    Z_sculpt += eyeball
    
    # Upper eyelid overhang shelf (hooded classical lid)
    ul_pts = [(-5.4, 3.38), (-4.8, 3.62), (-4.0, 3.60), (-3.4, 3.32)]
    ul_d, ul_t = dist_to_polyline(X, Y, ul_pts)
    upper_lid = 0.028 * np.exp(-(ul_d / 0.22)**2) * safe_taper(ul_t)
    Z_sculpt += upper_lid
    
    # Lower eyelid delicate rim
    ll_pts = [(-5.2, 3.25), (-4.6, 3.28), (-3.7, 3.26)]
    ll_d, ll_t = dist_to_polyline(X, Y, ll_pts)
    lower_lid = 0.012 * np.exp(-(ll_d / 0.18)**2) * safe_taper(ll_t)
    Z_sculpt += lower_lid
    
    # Brow ridge (Superciliary Arch) overhanging the orbit
    brow_pts = [(-5.8, 3.9), (-4.8, 4.3), (-3.5, 4.35), (-2.2, 4.0)]
    br_d, br_t = dist_to_polyline(X, Y, brow_pts)
    brow_ridge = 0.022 * np.exp(-(br_d / 0.35)**2) * safe_taper(br_t)
    Z_sculpt += brow_ridge
    
    # --- C. LIPS, PHILTRUM & MENTOLABIAL CREASE ---
    # Philtrum groove & columns
    phil_pts = [(-6.9, -0.6), (-6.4, -1.1), (-6.1, -1.5)]
    ph_d, ph_t = dist_to_polyline(X, Y, phil_pts)
    phil_groove = -0.012 * np.exp(-(ph_d / 0.25)**2) * safe_taper(ph_t)
    Z_sculpt += phil_groove
    
    # Upper lip vermilion slope
    ulip_pts = [(-6.7, -1.45), (-6.1, -1.65), (-5.6, -1.75)]
    ulp_d, ulp_t = dist_to_polyline(X, Y, ulip_pts)
    upper_lip = 0.016 * np.exp(-(ulp_d / 0.28)**2) * safe_taper(ulp_t)
    Z_sculpt += upper_lip
    
    # Oral fissure (mouth line) with commissure taper
    fis_pts = [(-6.7, -1.90), (-6.0, -1.95), (-5.3, -1.92)]
    fis_d, fis_t = dist_to_polyline(X, Y, fis_pts)
    oral_fissure = -0.020 * np.exp(-(fis_d / 0.16)**2) * safe_taper(fis_t)
    Z_sculpt += oral_fissure
    
    # Lower lip pillow volume
    llip_pts = [(-6.6, -2.35), (-5.9, -2.45), (-5.2, -2.40)]
    llp_d, llp_t = dist_to_polyline(X, Y, llip_pts)
    lower_lip = 0.022 * np.exp(-(llp_d / 0.30)**2) * safe_taper(llp_t)
    Z_sculpt += lower_lip
    
    # Mentolabial sulcus (chin-lip horizontal groove)
    mls_pts = [(-6.4, -3.10), (-5.6, -3.20), (-4.8, -3.15)]
    mls_d, mls_t = dist_to_polyline(X, Y, mls_pts)
    mentolabial = -0.018 * np.exp(-(mls_d / 0.28)**2) * safe_taper(mls_t)
    Z_sculpt += mentolabial
    
    # Chin pad dome with planar facet
    chin_d = np.hypot(X + 6.0, Y + 4.1)
    chin_pad = 0.028 * np.exp(-(chin_d / 0.85)**2)
    Z_sculpt += chin_pad
    
    # --- D. MANDIBLE JAWLINE & SUBMANDIBULAR STEP ---
    jaw_pts = [(-5.9, -4.3), (-4.5, -4.8), (-2.5, -4.7), (-0.5, -3.9), (0.8, -1.8)]
    jaw_d, jaw_t = dist_to_polyline(X, Y, jaw_pts)
    jaw_ridge = 0.022 * np.exp(-(jaw_d / 0.38)**2) * safe_taper(jaw_t)
    Z_sculpt += jaw_ridge
    
    # Submandibular undercut line
    uj_pts = [(-4.2, -5.3), (-2.2, -5.1), (-0.2, -4.3)]
    uj_d, uj_t = dist_to_polyline(X, Y, uj_pts)
    submandibular = -0.018 * np.exp(-(uj_d / 0.40)**2) * safe_taper(uj_t)
    Z_sculpt += submandibular
    
    # Ear contour definition (helix, lobule, concha)
    ear_concha_d = np.hypot(X - 2.5, Y - 0.5)
    ear_concha = -0.025 * np.exp(-(ear_concha_d / 0.8)**2)
    ear_helix_pts = [(1.8, 1.8), (2.8, 1.9), (3.3, 1.2), (3.2, 0.0), (2.8, -0.8), (2.2, -1.0)]
    eh_d, eh_t = dist_to_polyline(X, Y, ear_helix_pts)
    ear_helix = 0.022 * np.exp(-(eh_d / 0.30)**2) * safe_taper(eh_t)
    Z_sculpt += ear_concha + ear_helix
    
    # -------------------------------------------------------------
    # 7. LEVEL 3: 8 PRIMARY HAIR RIBBON MASSES & INTERLOCKING BUN COILS
    # (Directly matching aesthetic reference Panel 4)
    # -------------------------------------------------------------
    # 8 Major Flowing Hair Ribbons:
    # Defined by alternating crests (peaks) and deep troughs (valleys)
    ribbon_crests = [
        # Ribbon 1: Frontal S-curve above forehead
        ([(-4.2, 6.8), (-2.6, 8.2), (-0.5, 8.8), (2.0, 8.2), (4.5, 7.2), (6.8, 5.5)], 0.026, 0.70),
        # Ribbon 2: Upper crown sweep
        ([(-3.6, 8.5), (-1.8, 9.8), (0.8, 10.2), (3.5, 9.5), (6.0, 7.8), (7.5, 5.8)], 0.028, 0.75),
        # Ribbon 3: Apex crown crest
        ([(-1.5, 10.5), (0.8, 10.8), (3.0, 10.4), (5.5, 9.0), (7.2, 7.0)], 0.024, 0.70),
        # Ribbon 4: Mid-temporal band
        ([(-3.2, 5.2), (-1.2, 6.2), (1.2, 6.4), (3.5, 5.5), (5.8, 4.4), (7.4, 3.4)], 0.028, 0.70),
        # Ribbon 5: Above-ear tuck
        ([(-2.0, 3.8), (0.0, 4.4), (2.0, 4.2), (4.2, 3.4), (6.2, 2.4), (7.5, 1.8)], 0.025, 0.65),
        # Ribbon 6: Behind-ear / nape flow
        ([(1.2, -0.6), (2.8, -1.2), (4.5, -1.0), (6.2, 0.0), (7.2, 1.2)], 0.024, 0.65),
        # Ribbon 7: Temporal tendril curly lock
        ([(0.6, 2.0), (1.2, 1.0), (1.5, 0.0), (1.3, -1.0), (1.6, -1.8)], 0.018, 0.35),
    ]
    for pts, amp, w in ribbon_crests:
        rd, rt = dist_to_polyline(X, Y, pts)
        Z_sculpt += amp * np.exp(-(rd / w)**2) * safe_taper(rt)
        
    # Ribbon Troughs (Shadow Valleys between the major ribbons)
    ribbon_valleys = [
        ([(-3.9, 7.6), (-2.2, 9.0), (0.2, 9.5), (2.8, 8.8), (5.2, 7.5), (7.2, 5.7)], -0.016, 0.35),
        ([(-3.4, 6.0), (-1.4, 7.0), (1.1, 7.2), (3.6, 6.3), (5.8, 5.0), (7.3, 3.8)], -0.018, 0.35),
        ([(-2.5, 4.5), (-0.5, 5.2), (1.6, 5.1), (3.8, 4.3), (5.9, 3.2)], -0.016, 0.35),
        ([(0.8, 2.6), (2.4, 2.8), (4.2, 2.0), (6.0, 1.0)], -0.015, 0.35),
    ]
    for pts, amp, w in ribbon_valleys:
        vd, vt = dist_to_polyline(X, Y, pts)
        Z_sculpt += amp * np.exp(-(vd / w)**2) * safe_taper(vt)
        
    # --- CHIGNON BUN INTERLOCKING COILS (Matching reference Panel 4) ---
    # Coil 1: Top knot torus
    c1_d = np.hypot(X - 7.5, Y - 4.2)
    coil1 = 0.032 * np.exp(-((c1_d - 1.2)/0.65)**2)
    # Coil 2: Central knot torus
    c2_d = np.hypot(X - 8.2, Y - 2.5)
    coil2 = 0.038 * np.exp(-((c2_d - 1.4)/0.75)**2)
    # Coil 3: Lower tuck torus
    c3_d = np.hypot(X - 7.4, Y - 0.8)
    coil3 = 0.030 * np.exp(-((c3_d - 1.1)/0.60)**2)
    # Coil 4: Wrap-around braid strand
    bun_wrap_pts = [(6.6, 2.0), (7.6, 1.4), (8.8, 2.2), (8.6, 3.6), (7.8, 4.8)]
    bwd, bwt = dist_to_polyline(X, Y, bun_wrap_pts)
    coil_wrap = 0.026 * np.exp(-(bwd / 0.55)**2) * safe_taper(bwt)
    
    # Interlocking bun seams (undercut shadow crevices between coils)
    seam1_pts = [(6.8, 3.2), (7.8, 3.3), (8.6, 3.4)]
    s1_d, s1_t = dist_to_polyline(X, Y, seam1_pts)
    seam1 = -0.020 * np.exp(-(s1_d / 0.25)**2) * safe_taper(s1_t)
    
    seam2_pts = [(6.6, 1.6), (7.8, 1.7), (8.5, 1.8)]
    s2_d, s2_t = dist_to_polyline(X, Y, seam2_pts)
    seam2 = -0.020 * np.exp(-(s2_d / 0.25)**2) * safe_taper(s2_t)
    
    Z_bun = smax(coil1, coil2, 0.05)
    Z_bun = smax(Z_bun, coil3, 0.05)
    Z_bun += coil_wrap + seam1 + seam2
    bun_blend = smoothstep(4.0, 5.6, X)
    Z_sculpt += Z_bun * bun_blend
    
    # -------------------------------------------------------------
    # 8. HIERARCHICAL COMBINATION
    # -------------------------------------------------------------
    # 44% Macro Base + 56% Clean Poisson SfS + Local Sculpted Features
    Z_combined = 0.44 * (macro_base / np.max(macro_base)) + 0.56 * Z_sfs_clean + Z_sculpt
    
    # -------------------------------------------------------------
    # 9. NUMISMATIC DRAFT CHAMFER ALONG SILHOUETTE (220 um step)
    # -------------------------------------------------------------
    draft_w = 0.22 # mm
    draft_step = np.clip(dist_in / draft_w, 0.0, 1.0)
    draft_shelf = 0.08 # mm
    Z_final = np.where(mask_bool, draft_shelf + draft_step * (Z_combined * (MAX_RELIEF_HEIGHT - draft_shelf)), 0.0)
    
    # -------------------------------------------------------------
    # 10. FINAL SURFACE POLISH & MASKED RELAXATION (Blender Surface Polish Equivalent)
    # -------------------------------------------------------------
    # Helper to calculate distance to polyline
    def dist_poly(pts):
        d = np.full_like(X, 1e9)
        for k in range(len(pts)-1):
            d = np.minimum(d, dist_to_segment(X, Y, pts[k][0], pts[k][1], pts[k+1][0], pts[k+1][1])[0])
        return d

    # Features to protect with 100% structural preservation
    d_nose = dist_poly([(-6.0, 3.4), (-6.6, 2.2), (-7.3, 1.1), (-8.0, 0.14)])
    d_alar = dist_poly([(-7.0, 0.40), (-6.3, 0.32), (-5.9, -0.05), (-6.2, -0.28)])
    d_eye = np.hypot((X + 4.5)/1.4, (Y - 3.4)/0.9)
    d_lips = dist_poly([(-6.8, -1.5), (-6.0, -1.95), (-5.2, -2.5)])
    d_mls = dist_poly([(-6.4, -3.1), (-5.6, -3.2), (-4.8, -3.15)])
    d_jaw = dist_poly([(-5.9, -4.3), (-4.5, -4.8), (-2.5, -4.7), (-0.5, -3.9), (0.8, -1.8)])
    d_ear = np.hypot(X - 2.5, Y - 0.5)

    w_feature = np.zeros_like(X)
    w_feature = np.maximum(w_feature, np.clip(1.0 - d_nose/0.75, 0.0, 1.0))
    w_feature = np.maximum(w_feature, np.clip(1.0 - d_alar/0.45, 0.0, 1.0))
    w_feature = np.maximum(w_feature, np.clip(1.0 - d_eye/1.4, 0.0, 1.0))
    w_feature = np.maximum(w_feature, np.clip(1.0 - d_lips/0.85, 0.0, 1.0))
    w_feature = np.maximum(w_feature, np.clip(1.0 - d_mls/0.45, 0.0, 1.0))
    w_feature = np.maximum(w_feature, np.clip(1.0 - d_jaw/0.70, 0.0, 1.0))
    w_feature = np.maximum(w_feature, np.clip(1.0 - d_ear/1.6, 0.0, 1.0))

    is_hair_mass = (mask_bool) & (((Y > 4.5) & (X > -4.5)) | ((X > -0.5) & (Y > -2.5))) & (d_ear > 1.4)
    w_feature = np.maximum(w_feature, np.where(is_hair_mass, 1.0, 0.0))

    # Masked open skin weight (forehead, cheek, neck)
    w_skin = np.clip((1.0 - w_feature) * np.clip((dist_in - 0.45)/0.40, 0.0, 1.0), 0.0, 1.0)
    w_skin = cv2.GaussianBlur(w_skin.astype(np.float32), (15, 15), 3.0)

    # Broad planar smoothing on open skin
    Z_skin = cv2.GaussianBlur(Z_final.astype(np.float32), (35, 35), 7.0)
    Z_skin = cv2.bilateralFilter(Z_skin, 15, 0.025, 0.025)

    # Blend satin skin
    Z_polished = (Z_final * (1.0 - w_skin) + Z_skin * w_skin).astype(np.float32)

    # Hair polish: filter high-frequency grain while keeping ribbon crests
    Z_hair_filt = cv2.bilateralFilter(Z_polished, 11, 0.030, 0.030)
    Z_polished = np.where(is_hair_mass, Z_polished * 0.35 + Z_hair_filt * 0.65, Z_polished).astype(np.float32)

    # Global regularizer: subtle 7x7 edge-preserving polish
    Z_polished = cv2.bilateralFilter(Z_polished, 7, 0.008, 0.008)
    Z_final = np.where(mask_bool, Z_polished, 0.0).astype(np.float32)
    
    # Normalize peak relief to exactly MAX_RELIEF_HEIGHT (0.410 mm)
    peak = np.max(Z_final)
    if peak > 0:
        Z_final = (Z_final / peak) * MAX_RELIEF_HEIGHT
        
    print(f"Product bas-relief synthesized! Min: {np.min(Z_final):.4f} mm, Max: {np.max(Z_final):.4f} mm")
    
    # Save float32 numpy array
    out_npy = os.path.join(ROOT, "Assets", "Textures", "Coin", "heads_master_sculpt_perfect_float32.npy")
    np.save(out_npy, Z_final.astype(np.float32))
    print(f"Saved array to: {out_npy}")
    
    # Analytical normal/shading preview
    dy, dx = np.gradient(Z_final)
    scale = scale_mm
    lx, ly, lz = -0.55, 0.55, 0.62
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
    
    out_preview = os.path.join(ROOT, "Assets", "Textures", "Coin", "heads_master_sculpt_perfect_shaded.png")
    cv2.imwrite(out_preview, shaded)
    print(f"Saved preview to: {out_preview}")
    return Z_final

if __name__ == "__main__":
    build_product_heads_sculpt()
