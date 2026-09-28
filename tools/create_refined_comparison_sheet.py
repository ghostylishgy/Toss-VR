"""
tools/create_refined_comparison_sheet.py
Creates composite presentation and before/after comparison boards for
Toss-VR P1.1 Hero Coin Heads Bas-Relief Refinement.
"""

import os
from PIL import Image, ImageDraw, ImageFont

ROOT = r"G:\Dev\Toss-VR"
IN_DIR = os.path.join(ROOT, "Assets", "Textures", "Coin", "RefinedSculpt")
BASE_DIR = os.path.join(ROOT, "Assets", "Textures", "Coin")

def get_font(size):
    candidates = [
        r"C:\Windows\Fonts\segoeui.ttf",
        r"C:\Windows\Fonts\arial.ttf",
    ]
    for c in candidates:
        if os.path.exists(c):
            return ImageFont.truetype(c, size)
    return ImageFont.load_default()

def panel(img, title, subtitle="", width=600, height=600):
    header = 56
    canvas = Image.new("RGB", (width, height + header), (18, 19, 22))
    
    # Scale and center image
    copy = img.copy().convert("RGB")
    copy.thumbnail((width, height), Image.Resampling.LANCZOS)
    x_off = (width - copy.width) // 2
    y_off = header + (height - copy.height) // 2
    canvas.paste(copy, (x_off, y_off))
    
    draw = ImageDraw.Draw(canvas)
    draw.text((16, 10), title, fill=(235, 235, 235), font=get_font(20))
    if subtitle:
        draw.text((16, 32), subtitle, fill=(150, 155, 165), font=get_font(14))
    return canvas

def make_clay_sheet():
    files = [
        ("Clay_Front.png", "1. CLAY FRONT (ORTHOGRAPHIC)", "Evaluates exact silhouette, facial proportions & framing"),
        ("Clay_15deg.png", "2. CLAY 15° OBLIQUE", "Reveals shallow facial planar facets under gentle raking light"),
        ("Clay_30deg.png", "3. CLAY 30° OBLIQUE", "Demonstrates bas-relief elevation & contour transitions"),
        ("Clay_45deg.png", "4. CLAY 45° OBLIQUE", "Verifies true sculptural bas-relief volume & depth budget"),
        ("Clay_Macro.png", "5. CLAY MACRO CLOSE-UP", "Inspects Grecian nose prism, eyelid overhang, lips & jawline"),
    ]
    
    panels = []
    for fname, title, sub in files:
        path = os.path.join(IN_DIR, fname)
        if os.path.exists(path):
            img = Image.open(path)
            panels.append(panel(img, title, sub, 560, 560))
            
    if len(panels) < 5:
        print("Not all clay renders available yet.")
        return
        
    # Layout: Top row 3 panels (Front, 15°, 30°), Bottom row 2 panels centered (45°, Macro)
    w_p, h_p = panels[0].width, panels[0].height
    sheet = Image.new("RGB", (w_p * 3 + 40, h_p * 2 + 100), (12, 13, 15))
    
    draw = ImageDraw.Draw(sheet)
    draw.text((20, 16), "TOSS-VR // P1.1 HERO COIN — HEADS BAS-RELIEF GEOMETRY DIAGNOSTICS (MATTE CLAY)", fill=(255, 255, 255), font=get_font(24))
    draw.text((20, 46), "Pure Geometric Evaluation: Matte Neutral Clay (Roughness = 0.88, Metallic = 0.0) Under Soft Key + Directional Raking Light. No Normal Map.", fill=(160, 165, 175), font=get_font(15))
    
    top_y = 80
    sheet.paste(panels[0], (10, top_y))
    sheet.paste(panels[1], (20 + w_p, top_y))
    sheet.paste(panels[2], (30 + w_p * 2, top_y))
    
    bot_y = top_y + h_p + 15
    # Center 2 panels on bottom row
    x_start = (sheet.width - (w_p * 2 + 15)) // 2
    sheet.paste(panels[3], (x_start, bot_y))
    sheet.paste(panels[4], (x_start + w_p + 15, bot_y))
    
    out_path = os.path.join(BASE_DIR, "Heads_Clay_Diagnostics_Sheet.png")
    sheet.save(out_path, quality=95)
    print(f"Saved: {out_path}")

def make_silver_sheet():
    files = [
        ("Silver_Front.png", "1. SATIN SILVER FRONT", "Product baseline framing & satin silver specular response"),
        ("Silver_45deg.png", "2. SATIN SILVER 45° OBLIQUE", "Specular highlight flow over sculptural hair & facial planes"),
        ("Silver_Macro.png", "3. SATIN SILVER MACRO", "Inspects classical facial planes and hair ribbon definition"),
    ]
    
    panels = []
    for fname, title, sub in files:
        path = os.path.join(IN_DIR, fname)
        if os.path.exists(path):
            img = Image.open(path)
            panels.append(panel(img, title, sub, 600, 600))
            
    if len(panels) < 3:
        print("Not all silver renders available yet.")
        return
        
    w_p, h_p = panels[0].width, panels[0].height
    sheet = Image.new("RGB", (w_p * 3 + 40, h_p + 100), (12, 13, 15))
    
    draw = ImageDraw.Draw(sheet)
    draw.text((20, 16), "TOSS-VR // P1.1 HERO COIN — SATIN SILVER PRODUCT PRESENTATION (HEADS SIDE)", fill=(255, 255, 255), font=get_font(24))
    draw.text((20, 46), "Product Direction: Physically Based Satin Silver (Metallic = 0.96, Roughness = 0.30) with Weighted Normals", fill=(160, 165, 175), font=get_font(15))
    
    top_y = 80
    sheet.paste(panels[0], (10, top_y))
    sheet.paste(panels[1], (20 + w_p, top_y))
    sheet.paste(panels[2], (30 + w_p * 2, top_y))
    
    out_path = os.path.join(BASE_DIR, "Heads_Silver_Views_Sheet.png")
    sheet.save(out_path, quality=95)
    print(f"Saved: {out_path}")

def make_before_after():
    pairs = [
        # (Baseline path, Refined path, View title)
        (os.path.join(BASE_DIR, "Clay_View_1_Heads_Front.png"), os.path.join(IN_DIR, "Clay_Front.png"), "FRONT VIEW (CLAY)", "Left: Baseline (Flat/Soft) | Right: Refined Classical Sculpt"),
        (os.path.join(BASE_DIR, "Clay_View_2_Heads_Angle45.png"), os.path.join(IN_DIR, "Clay_45deg.png"), "45° OBLIQUE VIEW (CLAY)", "Left: Baseline (Waxy Blobs) | Right: Refined Bas-Relief Depth"),
        (os.path.join(BASE_DIR, "Clay_View_4_Heads_Closeup.png"), os.path.join(IN_DIR, "Clay_Macro.png"), "MACRO CLOSE-UP (CLAY)", "Left: Baseline (Indistinct Eyelids/Lips) | Right: Refined Eyelid Ledge, Nose Prism & Hair Ribbons"),
    ]
    
    pw, ph = 480, 480
    sheet = Image.new("RGB", (pw * 2 + 30, (ph + 56) * 3 + 110), (12, 13, 15))
    draw = ImageDraw.Draw(sheet)
    draw.text((20, 16), "TOSS-VR // P1.1 HERO COIN — HEADS SCULPT BEFORE vs AFTER COMPARISON", fill=(255, 255, 255), font=get_font(24))
    draw.text((20, 46), "Rigorous Geometric Comparison Under Matte Clay. Left: Current Baseline | Right: Refined Classical Numismatic Relief", fill=(160, 165, 175), font=get_font(15))
    
    y = 85
    for base_p, ref_p, title, sub in pairs:
        b_img = Image.open(base_p) if os.path.exists(base_p) else Image.new("RGB", (pw, ph), (30, 30, 30))
        r_img = Image.open(ref_p) if os.path.exists(ref_p) else Image.new("RGB", (pw, ph), (30, 30, 30))
        
        p_left = panel(b_img, f"BASELINE: {title}", sub.split("|")[0].strip(), pw, ph)
        p_right = panel(r_img, f"REFINED SCULPT: {title}", sub.split("|")[1].strip(), pw, ph)
        
        sheet.paste(p_left, (10, y))
        sheet.paste(p_right, (pw + 20, y))
        y += ph + 56 + 10
        
    out_path = os.path.join(BASE_DIR, "Heads_Before_After_Comparison.png")
    sheet.save(out_path, quality=95)
    print(f"Saved: {out_path}")

if __name__ == "__main__":
    make_clay_sheet()
    make_silver_sheet()
    make_before_after()
