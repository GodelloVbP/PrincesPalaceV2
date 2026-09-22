#!/usr/bin/env python3
"""Assemble typed, glyph-less spell-book covers for the expansion books.

Owner item: "Fix spell-book art in menus" (2026-09-22). Thirteen bookOnly
expansion skills (docs/PLAN_SPELL_EXPANSION.md milestone A) have no commissioned
glyph and no iconPath, so their books draw nothing in the dossier/shop/fight
submenu. This produces a typed base cover only -- leather tinted to the
spell's damage type(s), glyph opening left genuinely transparent -- as the
same graceful-degradation posture ItemIcons.Apply already takes for a gear
card with no art. It is a placeholder awaiting a commissioned glyph, not a
final asset.

output/spell-books/build.cjs is the authored compositor for this kit and is
NOT modified or re-run here: it needs the `sharp` npm package, which is not
installed anywhere under this tree (`node -e "require.resolve('sharp')"`
throws MODULE_NOT_FOUND from both the repo root and output/spell-books). This
script reproduces build.cjs's own tint() step in Python/Pillow/numpy, reading
the SAME full-resolution (1254x1254) masks and templates that build.cjs wrote
and that are already delivered under Shared/ -- the neutral leather shading,
the leather/region-A/region-B masks -- rather than re-deriving them from the
raw book-master scan. No glyph compositing happens here (there is no glyph to
composite): this is exactly build.cjs's tint(colors[, split]) with nothing
drawn into the slot afterward, which is what build.cjs itself emits for a
palette swatch (its own line 56, `palette/<type>.png`) before any per-spell
glyph is ever touched.

Usage: python3 tools/build_spellbook_bases.py
Reads:  Assets/_Project/Art/Items/SpellBooks/Shared/{Templates,Masks}/*.png
Writes: Assets/_Project/Art/Items/SpellBooks/<Display Name>/
            <Display Name> - Spell Book.png       (512x512)
            <Display Name> - Spell Book - 48x48.png
"""
from __future__ import annotations

import numpy as np
from PIL import Image
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SHARED = ROOT / "Assets/_Project/Art/Items/SpellBooks/Shared"
OUT_ROOT = ROOT / "Assets/_Project/Art/Items/SpellBooks"

# Same eleven damage-type colors build.cjs uses (output/spell-books/build.cjs
# line 5) and the same lowercase DamageType names
# (Assets/_Project/Scripts/Domain/Stats/DamageType.cs), so a spell's damage
# type maps onto this table with no renaming step.
PALETTE = {
    "physical": "#bac0bd",
    "fire": "#ed4b1a",
    "ice": "#65d4ef",
    "nature": "#61943c",
    "poison": "#a9bb38",
    "arcane": "#b34bd2",
    "earth": "#936039",
    "water": "#24558f",
    "wind": "#c9e9e4",
    "lightning": "#f6d92c",
    "void": "#241d32",
}

# (display name, folder-safe name, [one or two lowercase DamageType keys])
# A single-key entry uses the SOLID leather tint (one color, whole cover).
# A two-key entry uses the JAGGED-SPLIT tint (build.cjs's tint(colors, split=True)):
# region A gets the first color, region B the second, with the same
# seam-darkening pass build.cjs applies at the region boundary.
SPELLS = [
    ("Gilded Aegis", ["arcane"]),
    ("Winter's Rebuke", ["ice"]),
    ("Viper's Bite", ["poison"]),
    ("Crownfall", ["arcane"]),
    ("Ashen Reckoning", ["poison", "fire"]),
    ("Blackglass Spear", ["void"]),
    ("Borrowed Moment", ["arcane"]),
    ("Gale Scythe", ["wind"]),
    ("Palace Passage", ["arcane"]),
    ("Velvet Shackles", ["arcane"]),
    ("Censer of Embers", ["arcane"]),
    ("Thorn Tithe", ["arcane"]),
    ("Court of Whispers", ["arcane"]),
]


def load_rgba(path: Path) -> np.ndarray:
    return np.array(Image.open(path).convert("RGBA"), dtype=np.float64)


def hex_to_rgb(h: str) -> np.ndarray:
    h = h.lstrip("#")
    return np.array([int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16)], dtype=np.float64)


def clamp(v: np.ndarray) -> np.ndarray:
    return np.clip(np.round(v), 0, 255)


def main() -> None:
    # neutral: the solid template, RGB carries the leather's own shading
    # (grayscale), alpha is already zero outside the book AND at the glyph
    # opening -- build.cjs zeroes the slot's alpha on the raw scan (line 17)
    # before ever saving this file, so "Alpha Opening" in its name is literal.
    neutral = load_rgba(SHARED / "Templates/Spell Book - Solid - Alpha Opening.png")
    leather = load_rgba(SHARED / "Masks/Spell Book - Leather Tint Mask.png")
    region_a = load_rgba(SHARED / "Masks/Spell Book - Region A Mask.png")
    region_b = load_rgba(SHARED / "Masks/Spell Book - Region B Mask.png")

    h, w = neutral.shape[0], neutral.shape[1]
    leather_alpha = leather[:, :, 3] > 0
    a_alpha = region_a[:, :, 3] > 0
    b_alpha = region_b[:, :, 3] > 0
    shade = neutral[:, :, 0] / 255.0  # neutral's R==G==B (grayscale), per build.cjs

    written = []
    for display_name, keys in SPELLS:
        colors = [hex_to_rgb(PALETTE[k]) for k in keys]
        split = len(colors) == 2

        out = neutral.copy()
        # tint(): every leather pixel gets its region's color, scaled by the
        # template's own shading -- build.cjs lines 30-32.
        pick_b = split & b_alpha
        for k in range(3):
            color_per_px = np.where(pick_b, colors[1][k] if split else colors[0][k],
                                     colors[0][k])
            tinted = clamp(color_per_px * shade)
            out[:, :, k] = np.where(leather_alpha, tinted, out[:, :, k])

        if split:
            # Seam-darkening pass: build.cjs walks every horizontal pixel
            # pair and darkens a 3px-wide band wherever region membership
            # flips across two adjacent leather pixels (lines 31, the
            # `Boolean(a[i+3])!==Boolean(a[i-1])` test). Vectorized as a
            # shifted-column comparison over the same leather mask.
            leather_prev = np.zeros_like(leather_alpha)
            leather_prev[:, 1:] = leather_alpha[:, :-1]
            a_prev = np.zeros_like(a_alpha)
            a_prev[:, 1:] = a_alpha[:, :-1]
            seam = leather_alpha & leather_prev & (a_alpha != a_prev)
            band = np.zeros_like(seam)
            for dx in (-1, 0, 1):
                shifted = np.zeros_like(seam)
                if dx < 0:
                    shifted[:, :dx] = seam[:, -dx:]
                elif dx > 0:
                    shifted[:, dx:] = seam[:, :-dx]
                else:
                    shifted = seam
                band |= shifted & leather_alpha
            for k in range(3):
                out[:, :, k] = np.where(band, clamp(out[:, :, k] * 0.38), out[:, :, k])

        img = Image.fromarray(out.astype(np.uint8), mode="RGBA")
        folder = OUT_ROOT / display_name
        folder.mkdir(parents=True, exist_ok=True)

        big = img.resize((512, 512), Image.LANCZOS)
        big_path = folder / f"{display_name} - Spell Book.png"
        big.save(big_path)

        small = img.resize((48, 48), Image.LANCZOS)
        small_path = folder / f"{display_name} - Spell Book - 48x48.png"
        small.save(small_path)

        written.append((display_name, keys, big_path, small_path))

    for display_name, keys, big_path, small_path in written:
        print(f"{display_name}: {'+'.join(keys)} -> {big_path.name}, {small_path.name}")
    print(f"Wrote {len(written)} typed base covers, no glyph.")


if __name__ == "__main__":
    main()
