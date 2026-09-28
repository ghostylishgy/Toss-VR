import os
from PIL import Image, ImageDraw, ImageFont

def make_composite():
    coin_dir = r"G:\Dev\Toss-VR\Assets\Textures\Coin"
    v1_p = os.path.join(coin_dir, "View_1_Heads_Front.png")
    v2_p = os.path.join(coin_dir, "View_2_Heads_Angle45.png")
    v3_p = os.path.join(coin_dir, "View_3_Heads_LowAngle.png")
    v4_p = os.path.join(coin_dir, "View_4_Heads_Closeup.png")

    for p in [v1_p, v2_p, v3_p, v4_p]:
        if not os.path.exists(p):
            print(f"Waiting for {p}...")
            return False

    v1 = Image.open(v1_p).convert("RGB")
    v2 = Image.open(v2_p).convert("RGB")
    v3 = Image.open(v3_p).convert("RGB")
    v4 = Image.open(v4_p).convert("RGB")

    w, h = 1024, 1024
    header_h = 100
    sub_header_h = 40
    gap = 20
    border = 30
    label_h = 50

    comp_w = border * 2 + w * 2 + gap
    comp_h = border * 2 + header_h + (h + label_h) * 2 + gap

    comp = Image.new("RGB", (comp_w, comp_h), (18, 20, 24))
    draw = ImageDraw.Draw(comp)

    # Simple title
    title_text = "Toss-VR / P1.1 Hero Coin / Heads-only Real Geometry Bas-Relief"
    subtitle_text = "Baseline: 30mm Dia x 2.4mm Thick | 48 Reeded Teeth | Raised Rim 1.2mm | Recessed Field 0.7mm | Relief 0.42mm"
    
    # Use default bitmap font or load arial if available
    try:
        font_title = ImageFont.truetype("arial.ttf", 36)
        font_sub = ImageFont.truetype("arial.ttf", 20)
        font_lbl = ImageFont.truetype("arial.ttf", 24)
    except:
        font_title = ImageFont.load_default()
        font_sub = ImageFont.load_default()
        font_lbl = ImageFont.load_default()

    draw.text((border + 10, border + 10), title_text, fill=(240, 242, 245), font=font_title)
    draw.text((border + 10, border + 58), subtitle_text, fill=(160, 168, 178), font=font_sub)

    views = [
        (v1, "1. 0° Front View (Silhouette, Framing & White Space)", border, border + header_h),
        (v2, "2. 45° Oblique View (Bas-Relief Volume & Rim Step)", border + w + gap, border + header_h),
        (v3, "3. Side Low-Angle View (2.4mm Thickness & 48 Reeded Teeth)", border, border + header_h + h + label_h + gap),
        (v4, "4. Macro Close-Up (Facial Profile & Hair Ribbons)", border + w + gap, border + header_h + h + label_h + gap)
    ]

    for img, label, x, y in views:
        # Draw label banner
        draw.rectangle([x, y, x + w, y + label_h], fill=(28, 32, 38))
        draw.text((x + 20, y + 12), label, fill=(220, 225, 235), font=font_lbl)
        # Paste image
        comp.paste(img, (x, y + label_h))

    out_p = os.path.join(coin_dir, "View_Heads_4Views_Composite.png")
    comp.save(out_p, quality=95)
    print(f"Composite saved: {out_p}")
    return True

if __name__ == "__main__":
    make_composite()
