"""Create checkpoint comparison boards for Heads Portrait Patch v1."""

import os

from PIL import Image, ImageDraw, ImageFont


ROOT = r"G:\Dev\Toss-VR"
OUT_DIR = os.path.join(ROOT, "Assets", "Textures", "Coin", "PortraitPatch_v1")
REFERENCE = r"C:\Users\steve\Downloads\ChatGPT 图像 2026年9月28日 13_10_35.png"


def font(size):
    candidates = [
        r"C:\Windows\Fonts\segoeui.ttf",
        r"C:\Windows\Fonts\arial.ttf",
    ]
    for candidate in candidates:
        if os.path.exists(candidate):
            return ImageFont.truetype(candidate, size)
    return ImageFont.load_default()


def fit(image, width, height):
    copy = image.copy()
    copy.thumbnail((width, height), Image.Resampling.LANCZOS)
    canvas = Image.new("RGB", (width, height), (22, 23, 26))
    canvas.paste(copy, ((width - copy.width) // 2, (height - copy.height) // 2))
    return canvas


def panel(image, title, width=720, height=720):
    header = 52
    result = Image.new("RGB", (width, height + header), (22, 23, 26))
    result.paste(fit(image.convert("RGB"), width, height), (0, header))
    draw = ImageDraw.Draw(result)
    draw.text((18, 13), title, fill=(232, 232, 232), font=font(24))
    return result


def make_flat_smooth():
    flat = Image.open(os.path.join(OUT_DIR, "PortraitPatch_v1_Clay_Front_Flat.png"))
    smooth = Image.open(os.path.join(OUT_DIR, "PortraitPatch_v1_Clay_Front_Smooth.png"))
    left = panel(flat, "FLAT SHADING — NO WEIGHTED NORMAL")
    right = panel(smooth, "SMOOTH SHADING — NO WEIGHTED NORMAL")
    board = Image.new("RGB", (left.width + right.width, left.height), (15, 16, 18))
    board.paste(left, (0, 0))
    board.paste(right, (left.width, 0))
    path = os.path.join(OUT_DIR, "PortraitPatch_v1_Flat_vs_Smooth.png")
    board.save(path, quality=95)
    print(path)


def make_reference_comparison():
    reference = Image.open(REFERENCE).convert("RGB")
    w, h = reference.size
    reference_front = reference.crop((0, 0, w // 2, int(h * 0.47)))
    reference_face = reference.crop((0, int(h * 0.47), w // 2, h))
    current_front = Image.open(os.path.join(OUT_DIR, "PortraitPatch_v1_Clay_Front.png"))
    current_macro = Image.open(os.path.join(OUT_DIR, "PortraitPatch_v1_Clay_Macro.png"))

    panels = [
        panel(reference_front, "PRIMARY REFERENCE — FRONT / SILHOUETTE", 700, 630),
        panel(current_front, "PATCH v1 — CLAY FRONT", 700, 630),
        panel(reference_face, "PRIMARY REFERENCE — FACIAL PLANES", 700, 630),
        panel(current_macro, "PATCH v1 — CLAY MACRO", 700, 630),
    ]
    board = Image.new("RGB", (1400, 1364), (15, 16, 18))
    board.paste(panels[0], (0, 0))
    board.paste(panels[1], (700, 0))
    board.paste(panels[2], (0, 682))
    board.paste(panels[3], (700, 682))
    path = os.path.join(OUT_DIR, "PortraitPatch_v1_Reference_Comparison.png")
    board.save(path, quality=95)
    print(path)


if __name__ == "__main__":
    make_flat_smooth()
    make_reference_comparison()
