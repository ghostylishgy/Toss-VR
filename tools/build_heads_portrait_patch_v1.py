"""Build the isolated Toss-VR Heads Portrait Patch v1 master.

This is an explicitly authored 2.5D numismatic sculpt on a uniform quad grid.
It does not sample brightness or depth from either the old beauty render or the
new multi-view sculpt reference board.  The reference board is used only as an
art-direction guide for proportions, plane hierarchy, and hair grouping.

Run with Blender 4.2 LTS:
    blender.exe --background --python tools/build_heads_portrait_patch_v1.py
"""

import math
import os

import bpy
import numpy as np


ROOT = r"G:\Dev\Toss-VR"
OUT_BLEND = os.path.join(ROOT, "Assets", "Meshes", "Coin", "HeadsPortraitPatch_v1.blend")

GRID_RES = 384
PATCH_HALF_MM = 14.0
MAX_RELIEF_MM = 0.420
MAJOR_HAIR_MASSES = 8


def smoothstep01(value):
    value = np.clip(value, 0.0, 1.0)
    return value * value * (3.0 - 2.0 * value)


def rotated_coordinates(x, y, cx, cy, angle_deg):
    angle = math.radians(angle_deg)
    ca = math.cos(angle)
    sa = math.sin(angle)
    dx = x - cx
    dy = y - cy
    return ca * dx + sa * dy, -sa * dx + ca * dy


def superellipse_bump(x, y, cx, cy, rx, ry, angle_deg=0.0, power=4.0, edge_power=2.0):
    xr, yr = rotated_coordinates(x, y, cx, cy, angle_deg)
    metric = (np.abs(xr) / rx) ** power + (np.abs(yr) / ry) ** power
    return smoothstep01(1.0 - metric) ** edge_power


def gaussian_bump(x, y, cx, cy, rx, ry, angle_deg=0.0, power=2.0):
    xr, yr = rotated_coordinates(x, y, cx, cy, angle_deg)
    metric = (np.abs(xr) / rx) ** power + (np.abs(yr) / ry) ** power
    return np.exp(-0.5 * metric)


def ellipsoid_cap(x, y, cx, cy, rx, ry, angle_deg=0.0):
    """Projected ellipsoid depth, useful as a continuous sculptural primary form."""
    xr, yr = rotated_coordinates(x, y, cx, cy, angle_deg)
    metric = (xr / rx) ** 2 + (yr / ry) ** 2
    return np.sqrt(np.clip(1.0 - metric, 0.0, 1.0))


def polygon_mask(x, y, points):
    inside = np.zeros_like(x, dtype=bool)
    xj, yj = points[-1]
    for xi, yi in points:
        crosses = ((yi > y) != (yj > y))
        x_intersection = (xj - xi) * (y - yi) / ((yj - yi) + 1.0e-12) + xi
        inside ^= crosses & (x < x_intersection)
        xj, yj = xi, yi
    return inside


def ellipse_mask(x, y, cx, cy, rx, ry, angle_deg=0.0):
    xr, yr = rotated_coordinates(x, y, cx, cy, angle_deg)
    return (xr / rx) ** 2 + (yr / ry) ** 2 <= 1.0


def cubic_bezier(points, t):
    p0, p1, p2, p3 = (np.asarray(p, dtype=np.float32) for p in points)
    omt = 1.0 - t
    return omt**3 * p0 + 3.0 * omt**2 * t * p1 + 3.0 * omt * t**2 * p2 + t**3 * p3


def cubic_bezier_tangent(points, t):
    p0, p1, p2, p3 = (np.asarray(p, dtype=np.float32) for p in points)
    omt = 1.0 - t
    tangent = 3.0 * omt**2 * (p1 - p0) + 6.0 * omt * t * (p2 - p1) + 3.0 * t**2 * (p3 - p2)
    length = float(np.hypot(tangent[0], tangent[1])) + 1.0e-8
    return tangent / length


def bezier_ribbon(x, y, points, width_start, width_end, sample_count=72):
    """Return a broad directional mass and one restrained center crest."""
    body = np.zeros_like(x, dtype=np.float32)
    crest = np.zeros_like(x, dtype=np.float32)
    for t in np.linspace(0.0, 1.0, sample_count, dtype=np.float32):
        pos = cubic_bezier(points, float(t))
        tangent = cubic_bezier_tangent(points, float(t))
        normal = np.array((-tangent[1], tangent[0]), dtype=np.float32)
        dx = x - pos[0]
        dy = y - pos[1]
        along = dx * tangent[0] + dy * tangent[1]
        across = dx * normal[0] + dy * normal[1]
        width = width_start * (1.0 - t) + width_end * t
        end_fade = math.sin(math.pi * min(max(float(t), 0.0), 1.0)) ** 0.80
        local_body = np.exp(-0.5 * ((np.abs(across) / width) ** 4 + (along / 1.10) ** 2)) * end_fade
        local_crest = np.exp(-0.5 * ((across / (width * 0.24)) ** 2 + (along / 0.92) ** 2)) * end_fade
        body = np.maximum(body, local_body.astype(np.float32))
        crest = np.maximum(crest, local_crest.astype(np.float32))
    return body, crest


def compound_ribbon(x, y, points, width_start, width_end, sample_count=56):
    """Two joined cubic ribbons form a controlled S-shaped primary hair mass."""
    if len(points) != 7:
        raise ValueError("compound_ribbon requires seven control points")
    first_body, first_crest = bezier_ribbon(
        x, y, points[:4], width_start, (width_start + width_end) * 0.5, sample_count
    )
    second_body, second_crest = bezier_ribbon(
        x, y, points[3:], (width_start + width_end) * 0.5, width_end, sample_count
    )
    return np.maximum(first_body, second_body), np.maximum(first_crest, second_crest)


def build_height_field():
    lin = np.linspace(-PATCH_HALF_MM, PATCH_HALF_MM, GRID_RES, dtype=np.float32)
    x, y = np.meshgrid(lin, lin)

    # Front-view silhouette authority: explicitly placed landmarks, not image luminance.
    face_outline = [
        (-4.25, 7.55), (-4.85, 6.70), (-5.15, 5.55), (-5.28, 4.50),
        (-5.58, 3.65), (-6.05, 2.75), (-7.12, 1.82), (-7.00, 1.38),
        (-6.30, 1.08), (-6.02, 0.55), (-6.55, 0.18), (-6.14, -0.05),
        (-6.50, -0.48), (-6.07, -0.86), (-5.73, -1.78), (-5.10, -2.48),
        (-3.82, -2.94), (-2.42, -3.06), (-3.18, -5.48), (-4.52, -8.16),
        (-1.90, -7.78), (2.60, -6.92), (5.02, -5.83), (3.78, -4.55),
        (3.30, -2.72), (4.18, -1.25), (4.55, 1.55), (3.82, 4.62),
        (2.15, 7.12), (-0.80, 8.05),
    ]
    face_mask = polygon_mask(x, y, face_outline)

    cran_mask = ellipse_mask(x, y, 1.10, 4.55, 6.15, 6.35, -4.0)
    back_hair_mask = ellipse_mask(x, y, 4.10, 2.60, 3.55, 4.85, -8.0)
    bun_mask = ellipse_mask(x, y, 7.15, 2.62, 2.75, 3.35, 2.0)
    lower_coil_mask = ellipse_mask(x, y, 5.75, -0.10, 2.20, 2.35, -20.0)
    hair_silhouette = cran_mask | back_hair_mask | bun_mask | lower_coil_mask
    # A soft, explicitly controlled hairline avoids a sticker-like polygon seam.
    hairline_y = np.where(x < -2.20, 1.80 - 1.27 * x, 4.60 - 0.90 * (x + 2.20))
    scalp_weight = smoothstep01((y - hairline_y + 0.55) / 1.10) * hair_silhouette.astype(np.float32)
    bun_weight = gaussian_bump(x, y, 7.15, 2.62, 2.55, 3.15, 2.0, 4.0) * hair_silhouette
    lower_coil_weight = gaussian_bump(x, y, 5.75, -0.10, 2.05, 2.20, -20.0, 4.0) * hair_silhouette
    hair_weight = np.maximum.reduce([scalp_weight, bun_weight, lower_coil_weight])
    hair_region = hair_weight > 0.001
    silhouette = face_mask | hair_silhouette

    z = np.zeros_like(x, dtype=np.float32)
    z += silhouette.astype(np.float32) * 0.028

    # Continuous primary facial volume. Overlapping directional fields are deliberately
    # broad enough that their mathematical boundaries never become visible as rings.
    face_macro = 0.082 * gaussian_bump(x, y, -0.85, 1.70, 6.80, 8.20, -4.0, 2.6)
    forehead_form = 0.047 * gaussian_bump(x, y, -2.55, 5.20, 3.20, 3.05, -8.0, 3.0)
    cheek_form = 0.084 * gaussian_bump(x, y, -1.55, 1.08, 3.45, 2.70, -12.0, 3.0)
    maxillary_form = 0.035 * gaussian_bump(x, y, -3.62, 0.66, 2.02, 2.22, -7.0, 3.0)
    chin_form = 0.044 * gaussian_bump(x, y, -4.55, -1.55, 1.72, 1.15, -5.0, 3.0)
    jaw_form = 0.034 * gaussian_bump(x, y, -1.52, -2.18, 4.10, 1.42, -4.0, 3.2)
    neck_form = 0.052 * gaussian_bump(x, y, 0.12, -4.60, 3.85, 4.30, -12.0, 3.0)
    primary_face = face_macro + forehead_form + cheek_form + maxillary_form + chin_form + jaw_form + neck_form
    # Let the cranial under-form continue beneath hair so the face polygon never
    # appears as an internal cut line. Hair masses are layered over this base.
    z += np.where(silhouette, primary_face, 0.0)

    # Explicit brow, orbit, eyelids, and eye socket.
    brow_body, _ = bezier_ribbon(
        x, y, [(-5.00, 4.30), (-4.25, 4.63), (-3.05, 4.55), (-2.20, 4.18)], 0.48, 0.62, 28
    )
    orbit = gaussian_bump(x, y, -3.62, 3.60, 1.48, 0.68, -8.0, 3.2)
    upper_lid, _ = bezier_ribbon(
        x, y, [(-4.78, 3.70), (-4.28, 3.91), (-3.48, 3.88), (-2.88, 3.58)], 0.13, 0.17, 28
    )
    lid_crease, _ = bezier_ribbon(
        x, y, [(-4.72, 4.00), (-4.15, 4.18), (-3.38, 4.13), (-2.82, 3.84)], 0.12, 0.15, 24
    )
    eye_fissure, _ = bezier_ribbon(
        x, y, [(-4.68, 3.58), (-4.20, 3.48), (-3.48, 3.48), (-3.00, 3.58)], 0.065, 0.075, 24
    )
    z += np.where(face_mask, 0.078 * brow_body - 0.024 * orbit + 0.054 * upper_lid - 0.014 * lid_crease - 0.015 * eye_fissure, 0.0)

    # Explicit nose bridge, tip, alar/base structure.
    nose_bridge, _ = bezier_ribbon(
        x, y, [(-4.68, 4.34), (-4.95, 3.56), (-5.12, 2.78), (-5.55, 2.03)], 0.52, 0.42, 28
    )
    nose_tip = gaussian_bump(x, y, -5.78, 1.82, 0.78, 0.60, -8.0, 2.6)
    nose_alar = gaussian_bump(x, y, -5.54, 1.26, 0.72, 0.36, -4.0, 2.6)
    nose_base_cut = gaussian_bump(x, y, -5.82, 0.98, 0.54, 0.23, 0.0, 2.0)
    z += np.where(face_mask, 0.095 * nose_bridge + 0.082 * nose_tip + 0.040 * nose_alar - 0.023 * nose_base_cut, 0.0)

    # Two shallow lip volumes, a restrained fissure, philtrum, and labiomental break.
    upper_lip = gaussian_bump(x, y, -5.68, 0.22, 0.90, 0.24, -5.0, 2.8)
    lower_lip = gaussian_bump(x, y, -5.64, -0.35, 0.92, 0.29, 4.0, 2.8)
    mouth_fissure = gaussian_bump(x, y, -5.78, -0.06, 0.92, 0.105, 0.0, 2.0)
    philtrum = gaussian_bump(x, y, -5.40, 0.57, 0.34, 0.46, 0.0, 2.4)
    labiomental = gaussian_bump(x, y, -5.28, -0.82, 0.72, 0.20, 0.0, 2.4)
    z += np.where(face_mask, 0.043 * upper_lip + 0.048 * lower_lip - 0.024 * mouth_fissure + 0.014 * philtrum - 0.016 * labiomental, 0.0)

    # Under-jaw separation and controlled neck turn.
    underjaw, _ = bezier_ribbon(
        x, y, [(-4.88, -2.38), (-3.52, -2.82), (-1.78, -2.97), (-0.18, -2.76)], 0.18, 0.28, 28
    )
    neck_turn, _ = bezier_ribbon(
        x, y, [(-2.08, -3.18), (-1.58, -4.45), (-1.62, -6.10), (-2.62, -7.35)], 0.46, 0.62, 28
    )
    z += np.where(face_mask, -0.018 * underjaw + 0.020 * neck_turn, 0.0)

    # Explicit ear relief: an open helix, concha, and restrained antihelix.
    helix, _ = bezier_ribbon(
        x, y, [(1.82, 2.58), (3.20, 2.72), (3.42, 0.42), (2.05, 0.02)], 0.16, 0.18, 30
    )
    concha = gaussian_bump(x, y, 2.42, 1.22, 0.50, 0.68, -8.0, 2.6)
    antihelix, _ = bezier_ribbon(
        x, y, [(2.03, 2.10), (2.76, 1.82), (2.72, 0.88), (2.14, 0.55)], 0.12, 0.14, 24
    )
    z += np.where(silhouette, 0.042 * helix - 0.021 * concha + 0.018 * antihelix, 0.0)

    # Hair foundation and eight major masses: six swept scalp masses and two chignon masses.
    hair_foundation = 0.055 * gaussian_bump(x, y, 1.20, 4.55, 6.40, 6.65, -4.0, 3.2)
    hair_foundation += 0.043 * gaussian_bump(x, y, 4.15, 2.50, 3.65, 4.85, -8.0, 3.2)
    z += hair_weight * hair_foundation

    scalp_masses = [
        ([(-4.38, 7.40), (-3.35, 9.70), (-0.60, 10.62), (1.55, 9.55), (3.28, 8.70), (5.28, 8.78), (6.62, 7.42)], 1.12, 1.42, 0.094),
        ([(-4.58, 6.50), (-3.12, 8.78), (-0.42, 9.50), (1.58, 8.45), (3.36, 7.58), (5.48, 7.76), (6.96, 6.10)], 1.28, 1.58, 0.108),
        ([(-4.38, 5.56), (-2.62, 7.58), (0.12, 8.15), (2.02, 7.08), (3.74, 6.16), (5.82, 6.28), (7.08, 4.64)], 1.42, 1.70, 0.118),
        ([(-3.92, 4.66), (-1.98, 6.48), (0.72, 6.72), (2.44, 5.62), (3.92, 4.70), (5.70, 4.74), (6.82, 3.08)], 1.34, 1.60, 0.110),
        ([(-3.18, 3.70), (-1.08, 5.18), (1.38, 5.18), (2.88, 4.12), (4.00, 3.30), (5.32, 3.34), (6.16, 1.50)], 1.20, 1.48, 0.100),
        ([(-2.02, 2.90), (0.02, 3.86), (1.92, 3.48), (3.08, 2.72), (4.00, 2.08), (4.82, 1.82), (5.38, 0.14)], 1.02, 1.30, 0.090),
    ]
    hair_body = np.zeros_like(x, dtype=np.float32)
    hair_crest = np.zeros_like(x, dtype=np.float32)
    for points, width_a, width_b, amplitude in scalp_masses:
        body, crest = compound_ribbon(x, y, points, width_a, width_b, 64)
        hair_body += amplitude * body
        hair_crest += 0.018 * crest
    hair_body = np.minimum(hair_body, 0.170)
    hair_crest = np.minimum(hair_crest, 0.036)
    z += hair_weight * (hair_body + hair_crest)

    chignon_upper, chignon_upper_crest = bezier_ribbon(
        x, y, [(5.72, 4.78), (7.18, 5.72), (8.92, 4.10), (7.30, 2.78)], 0.84, 1.02, 36
    )
    chignon_lower, chignon_lower_crest = bezier_ribbon(
        x, y, [(6.08, 2.70), (8.58, 2.42), (8.52, 0.20), (6.32, -0.88)], 0.92, 1.10, 36
    )
    bun_knot = 0.040 * gaussian_bump(x, y, 6.48, 2.68, 0.74, 1.08, 4.0, 2.8)
    chignon_form = 0.124 * chignon_upper + 0.112 * chignon_lower
    chignon_form += 0.018 * np.maximum(chignon_upper_crest, chignon_lower_crest) + bun_knot
    z += np.where(silhouette, chignon_form, 0.0)

    # Broad separation valleys communicate overlap without strand-by-strand engraving.
    valley_curves = [
        [(-4.35, 6.90), (-1.45, 8.25), (3.40, 8.28), (6.72, 5.60)],
        [(-4.02, 5.72), (-1.10, 6.92), (3.25, 6.86), (6.60, 4.02)],
        [(-3.45, 4.58), (-0.55, 5.48), (3.15, 5.22), (6.15, 2.48)],
        [(-2.55, 3.45), (0.02, 4.08), (3.05, 3.76), (5.58, 1.12)],
    ]
    for points in valley_curves:
        valley, _ = bezier_ribbon(x, y, points, 0.16, 0.22, 34)
        z -= hair_weight * (0.015 * valley)

    hairline, _ = bezier_ribbon(
        x, y, [(-4.32, 7.18), (-3.28, 6.35), (-2.66, 5.50), (-2.05, 4.58)], 0.13, 0.18, 24
    )
    z -= np.where(face_mask, hair_weight * (0.016 * hairline), 0.0)

    z = np.where(silhouette, np.maximum(z, 0.018), 0.0)
    current_max = float(np.max(z))
    if current_max > 0.0:
        z *= MAX_RELIEF_MM / current_max

    return x, y, z.astype(np.float32), silhouette


def build_uniform_quad_mesh(x, y, z):
    rows, cols = z.shape
    verts = [
        (float(x[row, col]) / 1000.0, float(y[row, col]) / 1000.0, float(z[row, col]) / 1000.0)
        for row in range(rows)
        for col in range(cols)
    ]
    faces = [
        (row * cols + col, row * cols + col + 1, (row + 1) * cols + col + 1, (row + 1) * cols + col)
        for row in range(rows - 1)
        for col in range(cols - 1)
    ]
    mesh = bpy.data.meshes.new("Mesh_HeadsPortraitPatch_v1")
    mesh.from_pydata(verts, [], faces)
    mesh.update(calc_edges=True)
    return mesh


def main():
    print("=== Building isolated Heads Portrait Patch v1 ===")
    bpy.ops.wm.read_factory_settings(use_empty=True)

    x, y, z, silhouette = build_height_field()
    mesh = build_uniform_quad_mesh(x, y, z)
    patch = bpy.data.objects.new("HeadsPortraitPatch_v1", mesh)
    bpy.context.collection.objects.link(patch)

    # One applied Catmull-Clark pass turns the authored quad cage into the actual
    # high-resolution sculpt master. This is geometric refinement, not normal editing.
    bpy.context.view_layer.objects.active = patch
    patch.select_set(True)
    subdivision = patch.modifiers.new(name="AppliedSculptSubdivision", type='SUBSURF')
    subdivision.subdivision_type = 'CATMULL_CLARK'
    subdivision.levels = 1
    subdivision.render_levels = 1
    bpy.ops.object.modifier_apply(modifier=subdivision.name)
    mesh = patch.data

    for polygon in mesh.polygons:
        polygon.use_smooth = False

    patch["portrait_patch_version"] = "v1"
    patch["modeling_method"] = "uniform_quad_explicit_numismatic_height_sculpt"
    patch["old_poisson_usage"] = "none"
    patch["major_hair_masses"] = MAJOR_HAIR_MASSES
    patch["max_relief_mm"] = float(np.max(z))
    patch["grid_resolution"] = GRID_RES
    patch["reference_role"] = "multi_view_art_direction_only_not_depth"

    # The checkpoint contains real subdivided geometry and no shading modifiers.
    assert len(patch.modifiers) == 0

    os.makedirs(os.path.dirname(OUT_BLEND), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND)

    print(f"Saved: {OUT_BLEND}")
    print(f"Grid: {GRID_RES} x {GRID_RES}")
    print(f"Vertices: {len(mesh.vertices)}")
    print(f"Polygons: {len(mesh.polygons)}")
    print(f"Silhouette coverage: {float(np.mean(silhouette)) * 100.0:.2f}%")
    print(f"Max relief: {float(np.max(z)):.4f} mm")
    print(f"Major hair masses: {MAJOR_HAIR_MASSES}")
    print("Weighted Normal: disabled / absent")


if __name__ == "__main__":
    main()
