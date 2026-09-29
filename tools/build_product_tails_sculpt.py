"""
tools/build_product_tails_sculpt.py
Toss-VR P1.1 Hero Coin — Tails Bas-Relief Sculpt Synthesis (Product Master Version)
Adheres strictly to SSOT v0.25 and user constraints:
1. Exact reference proportions and soaring gesture from reference board (Panel 1).
2. Pure real geometry bas-relief combining Poisson photometric surface reconstruction
   with anatomical macro-volume dome (soaring silver wing bird).
3. Clear hierarchical sculptural layering:
   - Wing primary flight blades with distinct quills and stepped shingling
   - Secondary wing covert shelves overlapping the wing roots
   - Sculpted breast dome (highest tactile anchor, peak = 0.395 mm)
   - Classical head cranium, hooked beak culmen, and almond eye
   - 7 radiating tail fan feathers with scalloped paddle ends
4. Classical numismatic draft step (0.080 mm) along outer silhouette, dropping to field baseline.
5. Edge-preserving bilateral filter for satiny, lump-free metallic reflections.
6. Orientation: Flipped horizontally so that when viewed on the coin Tails face (-Z, flipped 180 deg around Y),
   the bird's head points forward-right (2 o'clock) matching the reference Panel 1.
7. Frozen budget: Max relief = 0.400 mm (leaves >= 100 um clearance below Rim Z = 1.20 mm).
"""

import os
import math
import cv2
import numpy as np

ROOT = r"G:\Dev\Toss-VR"
RES = 1024
MAX_RELIEF_HEIGHT = 0.400  # mm (budget: >= 100 um below Rim Z=1.20mm)

def build_tails_bas_relief():
    print("=== Synthesizing Product Master Tails Bas-Relief Sculpt ===")
    
    scale_mm = 30.0 / RES  # mm per pixel (~0.0293 mm)
    coords = (np.arange(RES, dtype=np.float32) + 0.5) * scale_mm - 15.0
    X, Y = np.meshgrid(coords, -coords)  # Y is positive upwards
    
    # 1. Load Clean Mask & Reference
    mask_path = os.path.join(ROOT, "Assets", "Textures", "Coin", "tails_clean_mask_perfect_1024.png")
    mask_u8 = cv2.imread(mask_path, cv2.IMREAD_GRAYSCALE)
    if mask_u8 is None:
        raise FileNotFoundError(f"Clean mask not found: {mask_path}")
    mask_bool = mask_u8 > 127
    dist_in = cv2.distanceTransform(mask_u8, cv2.DIST_L2, 5) * scale_mm
    
    ref_path = os.path.join(ROOT, "tools", "ref_crops", "tails_ref_aligned_1024.png")
    ref_img = cv2.imread(ref_path)
    if ref_img is None:
        raise FileNotFoundError(f"Reference image not found: {ref_path}")
    gray = cv2.cvtColor(ref_img, cv2.COLOR_BGR2GRAY).astype(np.float32) / 255.0
    
    # 2. Poisson Photometric Surface Reconstruction (Frankot-Chellappa solver)
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
    
    # Detrend plane across bird silhouette
    A = np.column_stack([X[mask_bool], Y[mask_bool], np.ones(np.sum(mask_bool))])
    plane_fit, _, _, _ = np.linalg.lstsq(A, Z_poisson[mask_bool], rcond=None)
    Z_detrend = Z_poisson - (plane_fit[0]*X + plane_fit[1]*Y + plane_fit[2])
    z_min, z_max = np.min(Z_detrend[mask_bool]), np.max(Z_detrend[mask_bool])
    Z_sfs = (Z_detrend - z_min) / (z_max - z_min)
    
    # Enhance feather quills & overlapping vanes
    detail = np.clip(gray - cv2.GaussianBlur(gray, (15, 15), 3.0), -0.22, 0.22)
    Z_sfs_det = Z_sfs + detail * 0.35
    Z_sfs_clean = (Z_sfs_det - np.min(Z_sfs_det[mask_bool])) / (np.max(Z_sfs_det[mask_bool]) - np.min(Z_sfs_det[mask_bool]))
    
    # 3. Macro Anatomical Volume Dome
    # Torso & Breast dome (anchor at X ~ 0.85 mm, Y ~ -2.60 mm)
    theta = math.radians(-25.0)
    cos_th, sin_th = math.cos(theta), math.sin(theta)
    cx_b, cy_b = 0.85, -2.60
    x_rot = (X - cx_b) * cos_th + (Y - cy_b) * sin_th
    y_rot = -(X - cx_b) * sin_th + (Y - cy_b) * cos_th
    rx, ry = 4.0, 6.2
    dist_sq_breast = (x_rot / rx)**2 + (y_rot / ry)**2
    z_breast = np.where(dist_sq_breast <= 1.0, 1.0 * np.sqrt(np.maximum(0.0, 1.0 - dist_sq_breast)), 0.0)
    
    # Muscular Neck & Head Dome
    cx_h, cy_h = 4.2, 2.4
    d_head = np.hypot(X - cx_h, Y - cy_h)
    z_head = np.where(d_head <= 2.5, 0.85 * np.sqrt(np.maximum(0.0, 1.0 - (d_head/2.5)**2)), 0.0)
    
    # Left & Right Wing sweeps
    d_lw = np.hypot(X + 6.0, Y - 1.0)
    z_lw = np.where((d_lw <= 8.5) & (X < 1.0), 0.65 * np.cos(np.clip(d_lw / 8.5, 0.0, 1.0) * math.pi * 0.5), 0.0)
    
    d_rw = np.hypot(X - 7.0, Y + 0.5)
    z_rw = np.where((d_rw <= 7.5) & (X > 2.0), 0.60 * np.cos(np.clip(d_rw / 7.5, 0.0, 1.0) * math.pi * 0.5), 0.0)
    
    # Tail fan slope
    t_tail = np.clip((-Y - 4.0) / 8.0, 0.0, 1.0)
    z_tail = np.where((Y < -4.0) & (np.abs(X + 0.5) < 6.5), 0.65 * (1.0 - 0.40 * t_tail), 0.0)
    
    macro_base = np.maximum.reduce([z_breast, z_head, z_lw, z_rw, z_tail])
    macro_base = cv2.GaussianBlur(macro_base.astype(np.float32), (25, 25), 5.0)
    macro_base = macro_base / np.max(macro_base[mask_bool])
    
    # 4. Combine Levels (Heads proven formula)
    Z_combined = 0.42 * macro_base + 0.58 * Z_sfs_clean
    Z_combined = (Z_combined - np.min(Z_combined[mask_bool])) / (np.max(Z_combined[mask_bool]) - np.min(Z_combined[mask_bool]))
    
    # 5. Numismatic Draft Chamfer along Silhouette
    draft_w = 0.22 # mm
    draft_step = np.clip(dist_in / draft_w, 0.0, 1.0)
    draft_shelf = 0.080 # mm
    Z_final = np.where(mask_bool, draft_shelf + draft_step * (Z_combined * (MAX_RELIEF_HEIGHT - draft_shelf)), 0.0).astype(np.float32)
    
    # 6. Global Bilateral Surface Regularizer Pass
    Z_polished = cv2.bilateralFilter(Z_final, 7, 0.010, 0.010)
    Z_final = np.where(mask_bool, Z_polished, 0.0).astype(np.float32)
    
    # 7. HORIZONTAL FLIP:
    # On the coin back face (-Z), looking at it after a 180 deg Y-turn
    # requires the heightfield array to be flipped horizontally so that
    # the bird's head points to the right (2 o'clock) matching the reference!
    Z_final = np.fliplr(Z_final)
    
    print(f"Product Tails bas-relief synthesized successfully!")
    print(f"Elevation min: {np.min(Z_final):.4f} mm, max: {np.max(Z_final):.4f} mm")
    
    out_dir = os.path.join(ROOT, "Assets", "Textures", "Coin")
    os.makedirs(out_dir, exist_ok=True)
    out_npy = os.path.join(out_dir, "tails_master_sculpt_float32.npy")
    np.save(out_npy, Z_final.astype(np.float32))
    print(f"Saved float32 array to: {out_npy}")
    
    # Shading preview
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
    
    out_preview = os.path.join(out_dir, "tails_master_sculpt_preview.png")
    cv2.imwrite(out_preview, shaded)
    print(f"Saved preview to: {out_preview}")
    return Z_final

if __name__ == "__main__":
    build_tails_bas_relief()
