"""
build_perfect_relief.py
Reconstructs the Hero Coin Heads bas-relief using gradient field integration
combined with anatomical macro form and numismatic draft engineering.
Produces a pure, seam-free, sculptural relief surface.
"""

import os
import math
import numpy as np
import cv2

def build_perfect_relief():
    print("=== Building Perfect Sculptural Heads Bas-Relief ===")
    ref_path = r"G:\Dev\Toss-VR\Assets\Textures\Coin\ref_heads_crop_exact.png"
    img = cv2.imread(ref_path)
    
    # Grid resolution 1024x1024
    RES = 1024
    img_res = cv2.resize(img, (RES, RES), interpolation=cv2.INTER_LANCZOS4)
    gray = cv2.cvtColor(img_res, cv2.COLOR_BGR2GRAY).astype(np.float32) / 255.0

    # Coordinates in mm (-15.0 to +15.0)
    lin = np.linspace(-15.0, 15.0, RES, dtype=np.float32)
    X, Y = np.meshgrid(lin, -lin)
    R = np.hypot(X, Y)

    # Load clean, complete bust silhouette mask (crown, bun, collar, profile)
    mask_path = r"G:\Dev\Toss-VR\Assets\Textures\Coin\heads_clean_mask_1024.png"
    mask_bust = cv2.imread(mask_path, cv2.IMREAD_GRAYSCALE)
    if mask_bust is None:
        raise FileNotFoundError(f"Mask not found: {mask_path}")

    # Smooth contour boundary slightly
    mask_bust = cv2.GaussianBlur(mask_bust, (3, 3), 0.8)
    mask_bust = (mask_bust > 127).astype(np.uint8) * 255

    # Distance to boundary (in mm)
    scale_mm = 30.0 / RES
    dist_in = cv2.distanceTransform(mask_bust, cv2.DIST_L2, 5) * scale_mm
    dist_out = cv2.distanceTransform(255 - mask_bust, cv2.DIST_L2, 5) * scale_mm
    sdf_bust = dist_in - dist_out

    # -------------------------------------------------------------
    # 2. POISSON SURFACE INTEGRATION FROM ILLUMINATION GRADIENTS
    # -------------------------------------------------------------
    # Light direction: from upper-left, approx (-0.6, 0.6, 0.52)
    # In photometrically lit relief, surface gradient (p, q) is related to image gradient.
    # Regularized gradient estimation:
    smooth_gray = cv2.GaussianBlur(gray, (7, 7), 1.5)
    Ix = cv2.Sobel(smooth_gray, cv2.CV_32F, 1, 0, ksize=5)
    Iy = cv2.Sobel(smooth_gray, cv2.CV_32F, 0, 1, ksize=5)

    # Compute divergence of surface gradient field
    # Invert signs so highlights are positive elevation
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

    # Detrend linear plane across the bust
    bust_pts = mask_bust > 0
    A = np.column_stack([X[bust_pts], Y[bust_pts], np.ones(np.sum(bust_pts))])
    plane_fit, _, _, _ = np.linalg.lstsq(A, Z_poisson[bust_pts], rcond=None)
    Z_detrend = Z_poisson - (plane_fit[0]*X + plane_fit[1]*Y + plane_fit[2])

    # Normalize detrended relief inside bust
    z_min = np.min(Z_detrend[bust_pts])
    z_max = np.max(Z_detrend[bust_pts])
    z_int = np.where(bust_pts, (Z_detrend - z_min) / (z_max - z_min), 0.0)

    # -------------------------------------------------------------
    # 3. LEVEL 1: MACRO ANATOMICAL FORM INJECTION
    # -------------------------------------------------------------
    # Cranium dome (ellipsoid)
    # Cranium dome (smooth C-infinity Gaussian)
    macro_cran = 0.28 * np.exp(-((X - 1.2)**2 / (2.0 * 4.5**2) + (Y - 3.5)**2 / (2.0 * 3.8**2)))

    # Cheek & Zygomatic mass
    macro_chk = 0.30 * np.exp(-((X + 1.8)**2 / (2.0 * 2.8**2) + (Y - 0.5)**2 / (2.0 * 2.4**2)))

    # Forehead plane
    w_fh = np.exp(-((X + 3.5)**2 / (2.0 * 2.2**2) + (Y - 4.5)**2 / (2.0 * 2.0**2)))
    macro_fh = (0.28 - 0.012*(X + 3.5) - 0.008*(Y - 4.5)) * w_fh

    # Neck cylinder
    t_nk = np.clip(((X - (-0.5))*(1.0) + (Y - (-2.5))*(-5.0)) / (1.0 + 25.0), 0.0, 1.0)
    p_nx = -0.5 + t_nk * 1.0
    p_ny = -2.5 + t_nk * (-5.0)
    d_nk = np.hypot(X - p_nx, Y - p_ny)
    macro_nk = 0.24 * np.exp(-(d_nk**2) / (2.0 * 2.8**2))

    def smax(a, b, k=0.08):
        h = np.maximum(k - np.abs(a - b), 0.0) / k
        return np.maximum(a, b) + h * h * h * (k * (1.0 / 6.0))

    z_macro = smax(macro_cran, macro_chk, 0.08)
    z_macro = smax(z_macro, macro_fh, 0.06)
    z_macro = smax(z_macro, macro_nk, 0.08)

    # -------------------------------------------------------------
    # 4. COMBINE INTEGRATED RELIEF WITH MACRO FORM
    # -------------------------------------------------------------
    # Total relief height = Macro Base (45%) + Sculptural Features (55%)
    z_combined = 0.45 * (z_macro / np.max(z_macro)) + 0.55 * z_int

    # -------------------------------------------------------------
    # 5. CRISP NUMISMATIC DRAFT STEP & BOUNDARY
    # -------------------------------------------------------------
    # Draft chamfer width 0.32mm, height 0.12mm
    draft_w = 0.32 # mm
    draft_step = np.clip(sdf_bust / draft_w, 0.0, 1.0)
    draft_h = 0.12 # mm base shelf

    # Add draft shelf
    z_final = np.where(sdf_bust > 0.0, draft_h * draft_step + z_combined * (0.42 - draft_h), 0.0)
    # Frequency-aware finish: smooth skin areas to silky satin while preserving hair crests & features
    is_skin = (mask_bust > 0) & (sdf_bust > 0.35) & ((X < 0.2) | (Y < -1.2))
    z_smooth = cv2.bilateralFilter(z_final.astype(np.float32), 9, 0.020, 0.020)
    z_final = np.where(is_skin, z_smooth, z_final)

    # Clamp and normalize to exactly 0.420 mm
    peak = np.max(z_final)
    if peak > 0:
        z_final = (z_final / peak) * 0.420

    print(f"Relief complete! Min: {np.min(z_final):.4f} mm, Max: {np.max(z_final):.4f} mm")

    # Save master float array
    out_npy = r"G:\Dev\Toss-VR\Assets\Textures\Coin\heads_master_sculpt_float32.npy"
    np.save(out_npy, z_final.astype(np.float32))

    # Analytical shading preview
    dy, dx = np.gradient(z_final)
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
    cv2.imwrite(r"G:\Dev\Toss-VR\Assets\Textures\Coin\heads_master_perfect_shaded.png", shaded)
    print("Saved heads_master_perfect_shaded.png")

if __name__ == "__main__":
    build_perfect_relief()
