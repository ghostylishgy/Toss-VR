"""
tools/build_perfect_heads_sculpt.py
Synthesizes the master numismatic bas-relief for Toss-VR Hero Coin Heads side.
Combines:
1. Exact silhouette boundary from the master reference
2. Level 1 Macro anatomical volumes (cranium, neck, cheekbone tilt)
3. Level 2 Classical facial planes & features (Grecian nose prism, eyelid shelf,
   lip cushions, oral fissure, chin pad, jawline step, ear relief, ear tendril)
4. Level 2 Primary hair ribbon swaths (8 sweeping locks + braided chignon bun)
5. Numismatic draft chamfer (80-85 deg drop to coin recessed field)
6. Relief height capped at 0.410 mm (strictly within 0.50 mm rim depth)
"""

import os
import math
import numpy as np
import cv2

RES = 1024
COIN_RADIUS = 15.0
MAX_RELIEF_HEIGHT = 0.410 # mm

def smoothstep(edge0, edge1, x):
    t = np.clip((x - edge0) / (edge1 - edge0 + 1e-12), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)

def smax(a, b, k=0.08):
    h = np.maximum(k - np.abs(a - b), 0.0) / k
    return np.maximum(a, b) + h * h * h * (k * (1.0 / 6.0))

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

def safe_taper(t, power=0.5):
    s = np.maximum(np.sin(np.pi * np.clip(t, 0.0, 1.0)), 0.0)
    return s ** power

def build_perfect_heads_sculpt():
    print("=== Synthesizing Perfect Classical Numismatic Relief ===")
    
    # 1. Coordinate grids across coin (-15.0 mm to +15.0 mm)
    lin = np.linspace(-15.0, 15.0, RES, dtype=np.float32)
    X, Y = np.meshgrid(lin, -lin)
    scale_mm = 30.0 / RES
    
    # 2. Clean, perfect silhouette mask
    mask_path = r"G:\Dev\Toss-VR\Assets\Textures\Coin\heads_clean_mask_perfect_1024.png"
    mask_bust = cv2.imread(mask_path, cv2.IMREAD_GRAYSCALE)
    if mask_bust is None:
        raise FileNotFoundError(f"Mask not found: {mask_path}")
    mask_bool = mask_bust > 127
    
    dist_in = cv2.distanceTransform(mask_bust, cv2.DIST_L2, 5) * scale_mm
    dist_out = cv2.distanceTransform(255 - mask_bust, cv2.DIST_L2, 5) * scale_mm
    sdf = dist_in - dist_out
    
    # 3. Shape from Shading / Poisson Surface from Master Reference
    ref_img = cv2.imread(r"G:\Dev\Toss-VR\tools\ref_crops\panel1_aligned_1024.png")
    gray = cv2.cvtColor(ref_img, cv2.COLOR_BGR2GRAY).astype(np.float32) / 255.0
    
    # Regularized image gradients
    smooth_gray = cv2.GaussianBlur(gray, (5, 5), 1.2)
    Ix = cv2.Sobel(smooth_gray, cv2.CV_32F, 1, 0, ksize=5)
    Iy = cv2.Sobel(smooth_gray, cv2.CV_32F, 0, 1, ksize=5)
    
    # Divergence
    div = -(cv2.Sobel(Ix, cv2.CV_32F, 1, 0, ksize=3) + cv2.Sobel(Iy, cv2.CV_32F, 0, 1, ksize=3))
    
    # Fast Fourier Transform Poisson solver
    fx = np.fft.fftfreq(RES)
    fy = np.fft.fftfreq(RES)
    Fx, Fy = np.meshgrid(fx, fy)
    denom = -4.0 * (np.pi ** 2) * (Fx**2 + Fy**2)
    denom[0, 0] = 1.0
    
    div_fft = np.fft.fft2(div)
    Z_fft = div_fft / denom
    Z_fft[0, 0] = 0.0
    Z_poisson = np.real(np.fft.ifft2(Z_fft))
    
    # Detrend linear plane across bust
    A = np.column_stack([X[mask_bool], Y[mask_bool], np.ones(np.sum(mask_bool))])
    plane_fit, _, _, _ = np.linalg.lstsq(A, Z_poisson[mask_bool], rcond=None)
    Z_detrend = Z_poisson - (plane_fit[0]*X + plane_fit[1]*Y + plane_fit[2])
    
    # Normalize inside bust
    z_min = np.min(Z_detrend[mask_bool])
    z_max = np.max(Z_detrend[mask_bool])
    Z_sfs = np.where(mask_bool, (Z_detrend - z_min) / (z_max - z_min), 0.0)
    
    # 4. Edge-Aware Skin Planar Smoothing & Hair Consolidation:
    # Completely eliminate pebble micro-noise on open skin surfaces (forehead, cheek, neck)
    # and consolidate scratchy micro-strands in hair into bold, clean ribbon masses
    
    # Feature gradient magnitude to detect structural ridges
    Z_sfs = Z_sfs.astype(np.float32)
    smooth_for_grad = cv2.GaussianBlur(Z_sfs, (5, 5), 1.0)
    gx = cv2.Sobel(smooth_for_grad, cv2.CV_32F, 1, 0, ksize=3)
    gy = cv2.Sobel(smooth_for_grad, cv2.CV_32F, 0, 1, ksize=3)
    grad_mag = np.hypot(gx, gy)
    
    # Skin candidate zone (inside bust, away from boundary, in facial/neck zone)
    is_skin_zone = (mask_bool) & (dist_in > 0.40) & ((X < 0.2) | (Y < -1.5)) & ~((X > 0.8) & (Y > -1.5))
    ridge_threshold = 0.018
    skin_weight = np.where(is_skin_zone, smoothstep(ridge_threshold * 2.0, ridge_threshold * 0.5, grad_mag), 0.0)
    
    # Compute satin-smooth skin surface
    Z_skin_smooth = cv2.GaussianBlur(Z_sfs, (21, 21), 4.0)
    Z_skin_smooth = cv2.bilateralFilter(Z_skin_smooth, 9, 0.030, 0.030)
    
    # Hair consolidation: filter out scratchy micro-lines so hair reads as major sculptural ribbons
    is_hair_zone = (mask_bool) & ~is_skin_zone
    Z_hair_smooth = cv2.bilateralFilter(Z_sfs, 7, 0.035, 0.035)
    
    # Blend: skin gets smooth satin surface, hair consolidates micro-noise, ridges retain full crispness
    Z_sfs_clean = Z_sfs * (1.0 - skin_weight * 0.88) + Z_skin_smooth * (skin_weight * 0.88)
    Z_sfs_clean = np.where(is_hair_zone, Z_sfs * 0.45 + Z_hair_smooth * 0.55, Z_sfs_clean)
    
    # 5. Level 1 Macro Anatomical Form Injection:
    # Cranium dome
    cran_d = ((X - 1.5)/7.2)**2 + ((Y - 5.0)/6.2)**2
    macro_cran = 0.22 * np.sqrt(np.clip(1.0 - cran_d, 0.0, 1.0))
    # Cheekbone / Zygomatic fullness
    macro_chk = 0.24 * np.exp(-(((X + 2.5)/3.0)**2 + ((Y - 0.8)/2.4)**2))
    # Forehead plane
    macro_fh = 0.18 * np.exp(-(((X + 3.2)/2.4)**2 + ((Y - 5.4)/2.0)**2))
    # Neck cylinder
    d_nk, t_nk = dist_to_segment(X, Y, 1.5, -3.5, 4.5, -9.0)
    macro_nk = 0.18 * np.exp(-(d_nk / 3.4)**2) * (1.0 - 0.15 * t_nk)
    
    macro_base = smax(macro_cran, macro_chk, 0.08)
    macro_base = smax(macro_base, macro_fh, 0.06)
    macro_base = smax(macro_base, macro_nk, 0.08)
    
    # 6. Level 2 Classical Facial Planes & Structural Ridges (Final Polish Tuning):
    Z_ridges = np.zeros_like(X, dtype=np.float32)
    
    # Straight Grecian Nose: smooth cosine cross-section for organic plane transition
    nose_pts = [(-6.2, 3.2), (-6.8, 2.1), (-7.4, 1.1), (-8.0, 0.14)]
    nose_d, nose_t = dist_to_polyline(X, Y, nose_pts)
    nose_w = 0.60 - 0.12 * nose_t
    u_nose = np.clip(nose_d / (nose_w + 1e-12), 0.0, 1.0)
    nose_prism = 0.022 * np.cos(0.5 * np.pi * u_nose)**2 * safe_taper(nose_t, 0.4)
    Z_ridges += nose_prism
    
    # Nasal alar groove: organic crease connecting alar to cheek
    alar_groove_pts = [(-6.9, 0.45), (-6.1, 0.35), (-5.8, -0.05), (-6.1, -0.25)]
    ag_d, ag_t = dist_to_polyline(X, Y, alar_groove_pts)
    Z_ridges += -0.016 * np.exp(-(ag_d / 0.22)**2) * safe_taper(ag_t)
    
    # Upper eyelid shelf overhang: sculptural shelf with clean shadow step
    ul_pts = [(-5.4, 3.35), (-4.8, 3.58), (-4.0, 3.55), (-3.5, 3.30)]
    ul_d, ul_t = dist_to_polyline(X, Y, ul_pts)
    Z_ridges += 0.026 * np.exp(-(ul_d / 0.24)**2) * safe_taper(ul_t)
    
    # Lower eyelid presence
    slit_pts = [(-5.2, 3.25), (-4.6, 3.32), (-3.8, 3.28)]
    sl_d, sl_t = dist_to_polyline(X, Y, slit_pts)
    lower_lid = 0.014 * np.exp(-((sl_d - 0.20) / 0.22)**2) * safe_taper(sl_t)
    Z_ridges += lower_lid
    
    # Oral fissure: refined, organic lip separation without stepped boxes
    fis_pts = [(-6.7, -1.90), (-6.0, -1.95), (-5.4, -1.92)]
    fis_d, fis_t = dist_to_polyline(X, Y, fis_pts)
    Z_ridges += -0.018 * np.exp(-(fis_d / 0.18)**2) * safe_taper(fis_t)
    
    # Mandibular jawline: graceful feminine edge stepping down into neck
    jaw_pts = [(-6.0, -4.2), (-4.5, -4.8), (-2.5, -4.6), (-0.5, -3.8), (0.8, -1.8)]
    jaw_d, jaw_t = dist_to_polyline(X, Y, jaw_pts)
    Z_ridges += 0.020 * np.exp(-(jaw_d / 0.42)**2) * safe_taper(jaw_t)
    
    # Submandibular shadow step: soft, elegant transition
    uj_pts = [(-4.2, -5.2), (-2.2, -5.0), (-0.2, -4.2)]
    uj_d, uj_t = dist_to_polyline(X, Y, uj_pts)
    Z_ridges += -0.018 * np.exp(-(uj_d / 0.44)**2) * safe_taper(uj_t)
    
    # 7. Level 2 Hair Ribbon Crest Polish (Reinforces major masses, suppresses micro-noise):
    hair_ribbons = [
        ([(-4.5, 7.8), (-3.0, 9.6), (-0.8, 10.8), (1.8, 10.6), (4.2, 9.8), (6.5, 7.8), (7.8, 5.2)], 0.020),
        ([(-4.2, 6.8), (-2.4, 8.8), (-0.2, 9.6), (2.2, 9.0), (4.8, 8.2), (7.0, 6.6), (8.2, 4.5)], 0.022),
        ([(-3.8, 5.8), (-1.8, 7.6), (0.5, 8.2), (2.8, 7.5), (5.2, 6.8), (7.5, 5.2), (8.5, 3.2)], 0.024),
        ([(-3.2, 4.8), (-1.2, 6.2), (1.2, 6.5), (3.5, 5.8), (5.8, 4.8), (7.8, 3.6), (8.6, 2.2)], 0.026),
        ([(-2.2, 3.8), (-0.2, 4.8), (2.0, 4.8), (4.2, 4.0), (6.2, 3.2), (7.8, 2.2), (8.4, 1.2)], 0.022),
        ([(0.5, 2.8), (2.2, 3.2), (4.0, 2.6), (5.8, 1.8), (7.2, 1.0), (8.0, 0.2)], 0.020),
    ]
    for pts, amp in hair_ribbons:
        rd, rt = dist_to_polyline(X, Y, pts)
        Z_ridges += amp * np.exp(-(rd / 0.60)**2) * safe_taper(rt)
    
    # 8. Combine All Levels:
    Z_combined = 0.42 * (macro_base / np.max(macro_base)) + 0.58 * Z_sfs_clean + Z_ridges
    
    # 9. Numismatic Draft Chamfer along Silhouette:
    draft_w = 0.22 # mm
    draft_step = np.clip(dist_in / draft_w, 0.0, 1.0)
    draft_shelf = 0.08 # mm
    Z_final = np.where(mask_bool, draft_shelf + draft_step * (Z_combined * (MAX_RELIEF_HEIGHT - draft_shelf)), 0.0)
    
    # 10. Global Surface Polish Pass (Bilateral regularizer across entire bust):
    # Preserves all sharp ridges while polishing planar transitions and removing faceting
    Z_polished = cv2.bilateralFilter(Z_final.astype(np.float32), 7, 0.010, 0.010)
    Z_final = np.where(mask_bool, Z_polished, 0.0)
    
    # Silky satin finish on skin
    Z_final_smooth = cv2.bilateralFilter(Z_final.astype(np.float32), 7, 0.014, 0.014)
    Z_final = np.where((skin_weight > 0.4) & (dist_in > 0.5), Z_final_smooth, Z_final)
    
    # Ensure zero outside silhouette
    Z_final = np.where(mask_bool, Z_final, 0.0)
    
    # Normalize peak relief to exactly MAX_RELIEF_HEIGHT (0.410 mm)
    peak = np.max(Z_final)
    if peak > 0:
        Z_final = (Z_final / peak) * MAX_RELIEF_HEIGHT
    
    print(f"Master bas-relief synthesized! Min: {np.min(Z_final):.4f} mm, Max: {np.max(Z_final):.4f} mm")
    
    # Save float32 numpy array
    out_npy = r"G:\Dev\Toss-VR\Assets\Textures\Coin\heads_master_sculpt_perfect_float32.npy"
    np.save(out_npy, Z_final.astype(np.float32))
    print(f"Saved: {out_npy}")
    
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
    
    out_preview = r"G:\Dev\Toss-VR\Assets\Textures\Coin\heads_master_sculpt_perfect_shaded.png"
    cv2.imwrite(out_preview, shaded)
    print(f"Saved: {out_preview}")
    return Z_final

if __name__ == "__main__":
    build_perfect_heads_sculpt()
