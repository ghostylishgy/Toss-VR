"""
tools/create_tails_comparison_sheets.py
Assembles composite diagnostic sheets for Toss-VR P1.1 Hero Coin Tails:
1. Tails_Clay_Diagnostics_Sheet.png (0 deg, 15 deg, 30 deg, 45 deg, Macro)
2. Tails_Silver_Views_Sheet.png (0 deg, 45 deg, Macro)
3. Heads_Tails_SideBySide.png (Double-sided coin verification composite)
"""

import os
import cv2
import numpy as np

ROOT = r"G:\Dev\Toss-VR"
TAILS_DIR = os.path.join(ROOT, "Assets", "Textures", "Coin", "TailsDiagnostics")
COIN_DIR = os.path.join(ROOT, "Assets", "Textures", "Coin")

def create_sheets():
    print("=== Creating Tails Comparison Sheets ===")
    
    # 1. Load Tails Renders
    clay_0 = cv2.imread(os.path.join(TAILS_DIR, "Tails_Clay_0deg_Front.png"))
    clay_15 = cv2.imread(os.path.join(TAILS_DIR, "Tails_Clay_15deg.png"))
    clay_30 = cv2.imread(os.path.join(TAILS_DIR, "Tails_Clay_30deg.png"))
    clay_45 = cv2.imread(os.path.join(TAILS_DIR, "Tails_Clay_45deg.png"))
    clay_macro = cv2.imread(os.path.join(TAILS_DIR, "Tails_Clay_Macro.png"))
    
    silver_0 = cv2.imread(os.path.join(TAILS_DIR, "Tails_Silver_0deg_Front.png"))
    silver_45 = cv2.imread(os.path.join(TAILS_DIR, "Tails_Silver_45deg.png"))
    silver_macro = cv2.imread(os.path.join(TAILS_DIR, "Tails_Silver_Macro.png"))
    
    # 2. Build Clay Diagnostics Sheet (2 rows: Row 1 = 0, 15, 30; Row 2 = 45, Macro)
    # Resize to 512x512 per panel for crisp presentation
    s = 512
    c0 = cv2.resize(clay_0, (s, s))
    c15 = cv2.resize(clay_15, (s, s))
    c30 = cv2.resize(clay_30, (s, s))
    c45 = cv2.resize(clay_45, (s, s))
    cm = cv2.resize(clay_macro, (s, s))
    
    # Add title labels
    def add_label(img, text):
        out = img.copy()
        cv2.rectangle(out, (0, s - 42), (s, s), (15, 15, 20), -1)
        cv2.putText(out, text, (20, s - 14), cv2.FONT_HERSHEY_SIMPLEX, 0.65, (220, 220, 230), 2, cv2.LINE_AA)
        return out
        
    c0_lbl = add_label(c0, "Tails Clay - 0 deg Ortho Front")
    c15_lbl = add_label(c15, "Tails Clay - 15 deg Raking")
    c30_lbl = add_label(c30, "Tails Clay - 30 deg Oblique")
    c45_lbl = add_label(c45, "Tails Clay - 45 deg Oblique")
    cm_lbl = add_label(cm, "Tails Clay - Macro Details")
    
    # Layout 3x2 grid (bottom right empty or reference)
    row1 = np.hstack([c0_lbl, c15_lbl, c30_lbl])
    row2 = np.hstack([c45_lbl, cm_lbl, np.zeros_like(c0_lbl)])
    sheet_clay = np.vstack([row1, row2])
    
    clay_sheet_path = os.path.join(TAILS_DIR, "Tails_Clay_Diagnostics_Sheet.png")
    cv2.imwrite(clay_sheet_path, sheet_clay)
    print(f"Saved: {clay_sheet_path}")
    
    # 3. Build Silver Views Sheet (1 row x 3 panels)
    s0 = add_label(cv2.resize(silver_0, (s, s)), "Tails Silver - 0 deg Front")
    s45 = add_label(cv2.resize(silver_45, (s, s)), "Tails Silver - 45 deg Oblique")
    sm = add_label(cv2.resize(silver_macro, (s, s)), "Tails Silver - Macro Close-up")
    sheet_silver = np.hstack([s0, s45, sm])
    
    silver_sheet_path = os.path.join(TAILS_DIR, "Tails_Silver_Views_Sheet.png")
    cv2.imwrite(silver_sheet_path, sheet_silver)
    print(f"Saved: {silver_sheet_path}")
    
    # 4. Build Double-Sided Coin Verification Sheet (Heads vs Tails)
    heads_silver_0 = cv2.imread(os.path.join(COIN_DIR, "View_1_Heads_Front.png"))
    heads_silver_45 = cv2.imread(os.path.join(COIN_DIR, "View_2_Heads_Angle45.png"))
    heads_clay_0 = cv2.imread(os.path.join(COIN_DIR, "Clay_View_1_Heads_Front.png"))
    heads_clay_45 = cv2.imread(os.path.join(COIN_DIR, "Clay_View_2_Heads_Angle45.png"))
    
    if heads_silver_0 is not None:
        h_s0 = add_label(cv2.resize(heads_silver_0, (s, s)), "Heads (+Z) - Silver Front")
        t_s0 = add_label(cv2.resize(silver_0, (s, s)), "Tails (-Z) - Silver Front")
        h_s45 = add_label(cv2.resize(heads_silver_45, (s, s)), "Heads (+Z) - Silver 45 deg")
        t_s45 = add_label(cv2.resize(silver_45, (s, s)), "Tails (-Z) - Silver 45 deg")
        
        row_s0 = np.hstack([h_s0, t_s0])
        row_s45 = np.hstack([h_s45, t_s45])
        sheet_double_silver = np.vstack([row_s0, row_s45])
        
        side_by_side_path = os.path.join(TAILS_DIR, "Heads_Tails_SideBySide.png")
        cv2.imwrite(side_by_side_path, sheet_double_silver)
        print(f"Saved: {side_by_side_path}")
        
    print("=== Comparison Sheets Complete ===")

if __name__ == "__main__":
    create_sheets()
