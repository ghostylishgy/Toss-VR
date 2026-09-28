"""
create_composite_preview.py
Generates presentation composites:
1. Clay Diagnostic Sheet (Clay_Heads_3Views_Composite.png)
2. Satin Silver Production Sheet (View_Heads_4Views_Composite.png)
3. BEFORE / AFTER Comparison Sheet (View_Heads_Before_After_Comparison.png)
"""

import os
from PIL import Image, ImageDraw, ImageFont

COIN_DIR = r"G:\Dev\Toss-VR\Assets\Textures\Coin"

def get_font(size):
    try:
        return ImageFont.truetype("arial.ttf", size)
    except:
        return ImageFont.load_default()

def make_clay_composite():
    c1_p = os.path.join(COIN_DIR, "Clay_View_1_Heads_Front.png")
    c2_p = os.path.join(COIN_DIR, "Clay_View_2_Heads_Angle45.png")
    c3_p = os.path.join(COIN_DIR, "Clay_View_4_Heads_Closeup.png")

    for p in [c1_p, c2_p, c3_p]:
        if not os.path.exists(p):
            return False

    c1 = Image.open(c1_p).convert("RGB")
    c2 = Image.open(c2_p).convert("RGB")
    c3 = Image.open(c3_p).convert("RGB")

    w, h = 1024, 1024
    header_h = 100
    gap = 20
    border = 30
    label_h = 50

    comp_w = border * 2 + w * 3 + gap * 2
    comp_h = border * 2 + header_h + h + label_h

    comp = Image.new("RGB", (comp_w, comp_h), (22, 24, 28))
    draw = ImageDraw.Draw(comp)

    draw.text((border + 10, border + 10), "Toss-VR / P1.1 Hero Coin / Clay Diagnostic Render (Matte Gray)", fill=(245, 245, 245), font=get_font(36))
    draw.text((border + 10, border + 58), "Evaluation Criteria: Sculptural planes, nose bridge, lips/chin definition, hair ribbon flow without metallic shading disguise", fill=(170, 175, 185), font=get_font(20))

    views = [
        (c1, "1. Clay Front (0° Silhouette & Proportions)", border),
        (c2, "2. Clay 45° (Physical Bas-Relief Volume & Rim Step)", border + w + gap),
        (c3, "3. Clay Macro Close-up (Facial Planes & Hair Lock Grooves)", border + (w + gap) * 2)
    ]

    for img, label, x in views:
        y = border + header_h
        draw.rectangle([x, y, x + w, y + label_h], fill=(32, 36, 42))
        draw.text((x + 20, y + 12), label, fill=(230, 235, 245), font=get_font(24))
        comp.paste(img, (x, y + label_h))

    out_p = os.path.join(COIN_DIR, "Clay_Heads_3Views_Composite.png")
    comp.save(out_p, quality=95)
    print(f"Saved Clay Composite: {out_p}")
    return True

def make_silver_composite():
    v1_p = os.path.join(COIN_DIR, "View_1_Heads_Front.png")
    v2_p = os.path.join(COIN_DIR, "View_2_Heads_Angle45.png")
    v3_p = os.path.join(COIN_DIR, "View_3_Heads_LowAngle.png")
    v4_p = os.path.join(COIN_DIR, "View_4_Heads_Closeup.png")

    for p in [v1_p, v2_p, v3_p, v4_p]:
        if not os.path.exists(p):
            return False

    v1 = Image.open(v1_p).convert("RGB")
    v2 = Image.open(v2_p).convert("RGB")
    v3 = Image.open(v3_p).convert("RGB")
    v4 = Image.open(v4_p).convert("RGB")

    w, h = 1024, 1024
    header_h = 100
    gap = 20
    border = 30
    label_h = 50

    comp_w = border * 2 + w * 2 + gap
    comp_h = border * 2 + header_h + (h + label_h) * 2 + gap

    comp = Image.new("RGB", (comp_w, comp_h), (18, 20, 24))
    draw = ImageDraw.Draw(comp)

    draw.text((border + 10, border + 10), "Toss-VR / P1.1 Hero Coin / Heads Sculpt Refinement (Satin Silver)", fill=(240, 242, 245), font=get_font(36))
    draw.text((border + 10, border + 58), "30mm Dia x 2.4mm Thick | 48 Reeded Teeth | Raised Rim 1.20mm | Recessed Field 0.70mm | Sculpted Relief 0.42mm", fill=(160, 168, 178), font=get_font(20))

    views = [
        (v1, "1. 0° Front View (Silhouette, Framing & White Space)", border, border + header_h),
        (v2, "2. 45° Oblique View (Sculptural Bas-Relief & Rim Protection)", border + w + gap, border + header_h),
        (v3, "3. Side Low-Angle View (2.4mm Thickness & 48 Reeded Teeth)", border, border + header_h + h + label_h + gap),
        (v4, "4. Macro Close-Up (Grecian Nose, Lips, Eyelid & Hair Waves)", border + w + gap, border + header_h + h + label_h + gap)
    ]

    for img, label, x, y in views:
        draw.rectangle([x, y, x + w, y + label_h], fill=(28, 32, 38))
        draw.text((x + 20, y + 12), label, fill=(220, 225, 235), font=get_font(24))
        comp.paste(img, (x, y + label_h))

    out_p = os.path.join(COIN_DIR, "View_Heads_4Views_Composite.png")
    comp.save(out_p, quality=95)
    print(f"Saved Silver Composite: {out_p}")
    return True

def make_before_after_comparison():
    b_front_p = os.path.join(COIN_DIR, "Baseline_View_1_Heads_Front.png")
    b_45_p = os.path.join(COIN_DIR, "Baseline_View_2_Heads_Angle45.png")
    b_close_p = os.path.join(COIN_DIR, "Baseline_View_4_Heads_Closeup.png")

    a_front_p = os.path.join(COIN_DIR, "View_1_Heads_Front.png")
    a_45_p = os.path.join(COIN_DIR, "View_2_Heads_Angle45.png")
    a_close_p = os.path.join(COIN_DIR, "View_4_Heads_Closeup.png")

    for p in [b_front_p, b_45_p, b_close_p, a_front_p, a_45_p, a_close_p]:
        if not os.path.exists(p):
            print(f"Missing file for comparison: {p}")
            return False

    bf = Image.open(b_front_p).convert("RGB")
    b45 = Image.open(b_45_p).convert("RGB")
    bc = Image.open(b_close_p).convert("RGB")

    af = Image.open(a_front_p).convert("RGB")
    a45 = Image.open(a_45_p).convert("RGB")
    ac = Image.open(a_close_p).convert("RGB")

    w, h = 1024, 1024
    header_h = 110
    col_header_h = 50
    gap = 20
    border = 30
    row_label_h = 45

    comp_w = border * 2 + w * 2 + gap
    comp_h = border * 2 + header_h + col_header_h + (h + row_label_h) * 3 + gap * 2

    comp = Image.new("RGB", (comp_w, comp_h), (16, 18, 22))
    draw = ImageDraw.Draw(comp)

    draw.text((border + 10, border + 10), "Toss-VR / P1.1 Hero Coin / BEFORE vs AFTER Comparison", fill=(245, 245, 245), font=get_font(38))
    draw.text((border + 10, border + 60), "Transition from Gaussian Bumps + Blur to Directional Numismatic Sculpture & Structured Hair Flow", fill=(170, 180, 195), font=get_font(22))

    y_col = border + header_h
    # Column Headers: LEFT = BEFORE (Baseline), RIGHT = AFTER (Refined Master)
    draw.rectangle([border, y_col, border + w, y_col + col_header_h], fill=(45, 30, 30))
    draw.text((border + 30, y_col + 12), "BEFORE: Previous Baseline (Gaussian Bumps + Waxy Blur)", fill=(240, 180, 180), font=get_font(26))

    draw.rectangle([border + w + gap, y_col, border + w * 2 + gap, y_col + col_header_h], fill=(25, 45, 35))
    draw.text((border + w + gap + 30, y_col + 12), "AFTER: Refined Master (Directional Planes, Sharp Ridges & Ribbons)", fill=(180, 240, 200), font=get_font(26))

    rows = [
        (bf, af, "Row 1: 0° Front View (Profile Silhouette & White Space Balance)", y_col + col_header_h),
        (b45, a45, "Row 2: 45° Oblique View (Bas-Relief Volume, Nose Ridge & Rim Protection)", y_col + col_header_h + (h + row_label_h + gap)),
        (bc, ac, "Row 3: Macro Close-up (Facial Planes, Grecian Bridge, Eyelid & Hair Waves)", y_col + col_header_h + (h + row_label_h + gap) * 2)
    ]

    for img_b, img_a, label, y_top in rows:
        # Row banner
        draw.rectangle([border, y_top, border + w * 2 + gap, y_top + row_label_h], fill=(30, 34, 40))
        draw.text((border + 20, y_top + 10), label, fill=(225, 230, 240), font=get_font(22))
        y_img = y_top + row_label_h
        comp.paste(img_b, (border, y_img))
        comp.paste(img_a, (border + w + gap, y_img))

    out_p = os.path.join(COIN_DIR, "View_Heads_Before_After_Comparison.png")
    comp.save(out_p, quality=95)
    print(f"Saved Comparison Sheet: {out_p}")
    return True

if __name__ == "__main__":
    make_clay_composite()
    make_silver_composite()
    make_before_after_comparison()
