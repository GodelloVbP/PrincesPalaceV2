#!/usr/bin/env python3
"""Generates the fight detail card's ability icons.

FightHudModel.DetailForSkill (2026-09-23 icon rework, AUDIT #196-#204's
follow-up playtest) replaces the detail card's text stat rows with icon
cells: damage element, defense interaction, reach, area-of-effect and
scaling stat. Ten of the eleven DamageType members and every structural
icon (defense/reach/AoE/scaling) had no art at all -- this script authors
flat, 64px placeholder glyphs in the Giant Rat sheet's own style (bold dark
outline, flat colour fill, no painterly shading) so the card has something
legible to paint rather than a blank cell, same spirit as the TalentTree
kit's ten medallion icons but at this card's plainer, smaller scale.

REPLACE, DO NOT HAND-EDIT. Re-run this script after touching it rather than
opening a PNG in an editor -- the point of keeping it in tools/ is that the
icon set is reproducible from source, the same rule ART_PIPELINE.md states
for every other generated sheet in this project.

Colours for the eleven elements are FightHudPalette.ForDamageType's own hex
values, restated here as literals (Domain has no PIL to import) -- if that
palette moves, re-paste the eleven DamageType* consts below from
FightHudPalette.cs and re-run.

Output: Assets/_Project/Resources/Icons/Ability/<key>.png, 64x64 RGBA.
RESOURCES ONLY (coordinator pass 2, 2026-09-23) -- the icon shown per row
varies at runtime with whatever is hovered, so it has to be reachable
through Resources.Load the same way every other runtime-swapped HUD icon
(Status/*, the enemy intent set) already is; FightController.Hud.cs's
DetailIconResourcePath reads "Icons/Ability/<key>" off this exact folder.
An Assets/_Project/Art/UI/Icons/Ability "source" copy used to sit alongside
it, mirroring the Status icon pipeline's authored-vs-processed split --
dropped because nothing here has a separate authoring step to keep a source
copy FOR; this script IS the source, and one output location is one less
place for the two to drift apart.
Usage: python tools/make_detail_card_icons.py
"""
import math
import os

from PIL import Image, ImageDraw

SIZE = 64
OUT_DIR = os.path.join(
    os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
    "Assets", "_Project", "Resources", "Icons", "Ability",
)

# ---- palette -----------------------------------------------------------
# Pasted from FightHudPalette.cs's own ForDamageType literals (2026-09-23).
ELEMENT_HEX = {
    "physical": "#ED423D",
    "fire": "#FF6A2A",
    "ice": "#9FD8F5",
    "nature": "#5FA24A",
    "poison": "#A8E63C",
    "arcane": "#C69AF1",
    "earth": "#C98A3A",
    "water": "#3F9BE8",
    "wind": "#A8F0E0",
    "lightning": "#F5F06A",
    "void": "#C22FB0",
}

OUTLINE = "#1A1410"
BADGE_DARK = "#2B2018"
BADGE_RIM = "#5A4636"
NEUTRAL_GOLD = "#E8C77E"
NEUTRAL_SILVER = "#C7CDD6"


def hex_to_rgba(hex_str, alpha=255):
    hex_str = hex_str.lstrip("#")
    r, g, b = (int(hex_str[i:i + 2], 16) for i in (0, 2, 4))
    return (r, g, b, alpha)


def new_canvas():
    return Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))


def draw_badge(draw, fill_hex):
    """The shared base every icon sits on: a flat dark disc, a bold
    outline, and a thin inner rim -- the Giant Rat sheet's own "flat cel
    fill, bold dark outline" language at icon scale, not the painterly
    medallions the TalentTree kit uses (memory: enemy sprite style is the
    Giant Rat sheet; the treant/beetle's painterly look is not precedent,
    and that note applies here just as much as to a character)."""
    pad = 3
    draw.ellipse([pad, pad, SIZE - pad, SIZE - pad], fill=hex_to_rgba(BADGE_DARK))
    draw.ellipse([pad, pad, SIZE - pad, SIZE - pad], outline=hex_to_rgba(OUTLINE), width=3)
    draw.ellipse([pad + 4, pad + 4, SIZE - pad - 4, SIZE - pad - 4],
                 outline=hex_to_rgba(BADGE_RIM), width=1)


def glyph(draw, points, fill_hex, outline_w=3):
    draw.polygon(points, fill=hex_to_rgba(fill_hex), outline=hex_to_rgba(OUTLINE))
    # PIL's polygon outline has no width param pre-9.2 in all builds; redraw
    # the edge with a thick line loop for a consistently bold border.
    loop = points + [points[0]]
    draw.line(loop, fill=hex_to_rgba(OUTLINE), width=outline_w, joint="curve")


CX, CY = SIZE / 2, SIZE / 2


def flame(draw, fill_hex):
    pts = [(CX, CY - 20), (CX + 11, CY - 2), (CX + 7, CY + 6), (CX + 13, CY + 18),
           (CX, CY + 22), (CX - 13, CY + 18), (CX - 7, CY + 6), (CX - 11, CY - 2)]
    glyph(draw, pts, fill_hex)


def snowflake(draw, fill_hex):
    for angle in range(0, 360, 60):
        rad = math.radians(angle)
        x2, y2 = CX + 18 * math.cos(rad), CY + 18 * math.sin(rad)
        draw.line([(CX, CY), (x2, y2)], fill=hex_to_rgba(fill_hex), width=5)
        draw.line([(CX, CY), (x2, y2)], fill=hex_to_rgba(OUTLINE), width=7 if angle == 0 else 1)
    draw.line([(CX, CY), (CX, CY)], fill=hex_to_rgba(fill_hex))
    for angle in range(0, 360, 60):
        rad = math.radians(angle)
        x2, y2 = CX + 18 * math.cos(rad), CY + 18 * math.sin(rad)
        draw.line([(CX, CY), (x2, y2)], fill=hex_to_rgba(fill_hex), width=4)
    draw.ellipse([CX - 4, CY - 4, CX + 4, CY + 4], fill=hex_to_rgba(fill_hex),
                 outline=hex_to_rgba(OUTLINE), width=2)


def leaf(draw, fill_hex):
    pts = [(CX - 16, CY + 16), (CX - 4, CY - 18), (CX + 18, CY - 16), (CX + 4, CY + 18)]
    glyph(draw, pts, fill_hex)
    draw.line([(CX - 12, CY + 12), (CX + 12, CY - 12)], fill=hex_to_rgba(OUTLINE), width=2)


def droplet(draw, fill_hex, poisoned=False):
    pts = [(CX, CY - 18), (CX + 13, CY + 6), (CX, CY + 20), (CX - 13, CY + 6)]
    glyph(draw, pts, fill_hex)
    if poisoned:
        draw.ellipse([CX - 3, CY, CX + 3, CY + 6], fill=hex_to_rgba(OUTLINE))


def rune_star(draw, fill_hex):
    pts = []
    for i in range(6):
        angle = math.radians(60 * i - 90)
        r = 17 if i % 2 == 0 else 8
        pts.append((CX + r * math.cos(angle), CY + r * math.sin(angle)))
    glyph(draw, pts, fill_hex)


def mountain(draw, fill_hex):
    pts = [(CX - 18, CY + 16), (CX - 4, CY - 14), (CX + 4, CY - 2), (CX + 10, CY - 12),
           (CX + 18, CY + 16)]
    glyph(draw, pts, fill_hex)


def swirl(draw, fill_hex):
    bbox_outer = [CX - 16, CY - 16, CX + 16, CY + 16]
    draw.arc(bbox_outer, start=20, end=320, fill=hex_to_rgba(fill_hex), width=5)
    bbox_inner = [CX - 8, CY - 8, CX + 8, CY + 8]
    draw.arc(bbox_inner, start=200, end=500, fill=hex_to_rgba(fill_hex), width=4)
    draw.ellipse([CX + 12, CY - 4, CX + 20, CY + 4], fill=hex_to_rgba(fill_hex))


def bolt(draw, fill_hex):
    pts = [(CX - 3, CY - 20), (CX + 9, CY - 3), (CX + 1, CY - 3), (CX + 6, CY + 20),
           (CX - 9, CY + 1), (CX - 1, CY + 1)]
    glyph(draw, pts, fill_hex)


def crescent(draw, fill_hex):
    draw.ellipse([CX - 16, CY - 16, CX + 16, CY + 16], fill=hex_to_rgba(fill_hex),
                 outline=hex_to_rgba(OUTLINE), width=3)
    draw.ellipse([CX - 8, CY - 16, CX + 22, CY + 16], fill=hex_to_rgba(BADGE_DARK))


def fist(draw, fill_hex):
    draw.rounded_rectangle([CX - 13, CY - 10, CX + 13, CY + 14], radius=6,
                            fill=hex_to_rgba(fill_hex), outline=hex_to_rgba(OUTLINE), width=3)
    for dx in (-8, -2.5, 3, 8.5):
        draw.line([(CX + dx, CY - 10), (CX + dx, CY - 2)], fill=hex_to_rgba(OUTLINE), width=2)


ELEMENT_GLYPHS = {
    "physical": fist,
    "fire": flame,
    "ice": snowflake,
    "nature": leaf,
    "poison": lambda d, c: droplet(d, c, poisoned=True),
    "arcane": rune_star,
    "earth": mountain,
    "water": droplet,
    "wind": swirl,
    "lightning": bolt,
    "void": crescent,
}


def shield(draw, fill_hex, rune=False):
    pts = [(CX - 14, CY - 14), (CX, CY - 20), (CX + 14, CY - 14), (CX + 14, CY + 4),
           (CX, CY + 20), (CX - 14, CY + 4)]
    glyph(draw, pts, fill_hex)
    if rune:
        draw.line([(CX, CY - 12), (CX, CY + 10)], fill=hex_to_rgba(OUTLINE), width=2)
        draw.line([(CX - 7, CY - 3), (CX + 7, CY - 3)], fill=hex_to_rgba(OUTLINE), width=2)


def shield_broken(draw, fill_hex):
    """DEFENSE row when ResolvedSkill.IgnoresDefense -- the cast skips both
    broad defense stats outright, so neither shield glyph applies. An
    outline-only shield with a crack through it, rather than a third solid
    fill, is what reads as "the shield itself does not apply" instead of
    "here is a third kind of defense"."""
    pts = [(CX - 14, CY - 14), (CX, CY - 20), (CX + 14, CY - 14), (CX + 14, CY + 4),
           (CX, CY + 20), (CX - 14, CY + 4)]
    draw.polygon(pts, outline=hex_to_rgba(OUTLINE))
    loop = pts + [pts[0]]
    draw.line(loop, fill=hex_to_rgba(fill_hex), width=3, joint="curve")
    draw.line([(CX - 6, CY - 12), (CX + 4, CY - 2), (CX - 3, CY + 4), (CX + 6, CY + 14)],
              fill=hex_to_rgba(OUTLINE), width=3, joint="curve")


def arrow_wall(draw, fill_hex, blocked):
    draw.line([(CX - 16, CY), (CX + (6 if blocked else 16), CY)],
              fill=hex_to_rgba(fill_hex), width=5)
    draw.polygon([(CX + (10 if blocked else 20), CY), (CX + (2 if blocked else 12), CY - 7),
                  (CX + (2 if blocked else 12), CY + 7)], fill=hex_to_rgba(fill_hex))
    if blocked:
        draw.line([(CX + 10, CY - 12), (CX + 10, CY + 12)], fill=hex_to_rgba(OUTLINE), width=4)


def burst(draw, fill_hex, single):
    if single:
        draw.ellipse([CX - 6, CY - 6, CX + 6, CY + 6], fill=hex_to_rgba(fill_hex),
                     outline=hex_to_rgba(OUTLINE), width=2)
        return
    draw.ellipse([CX - 6, CY - 6, CX + 6, CY + 6], fill=hex_to_rgba(fill_hex))
    for angle in range(0, 360, 45):
        rad = math.radians(angle)
        x1, y1 = CX + 9 * math.cos(rad), CY + 9 * math.sin(rad)
        x2, y2 = CX + 18 * math.cos(rad), CY + 18 * math.sin(rad)
        draw.line([(x1, y1), (x2, y2)], fill=hex_to_rgba(fill_hex), width=4)


def scale_glyph(draw, fill_hex, key):
    # Six short-name markers rather than six bespoke pictograms -- the
    # detail card already prints the short name (ScalingLabel) beside the
    # icon, so the icon's job is "which stat, at a glance from its badge
    # colour", not "read the letter". A rounded chip keeps that legible at
    # 64px where a six-way pictogram set would not.
    draw.rounded_rectangle([CX - 15, CY - 12, CX + 15, CY + 12], radius=6,
                            fill=hex_to_rgba(fill_hex), outline=hex_to_rgba(OUTLINE), width=3)


SCALE_HEX = {
    "str": "#ED423D", "dex": "#5FA24A", "con": "#C98A3A",
    "wis": "#3F9BE8", "int": "#C69AF1", "cha": "#F5F06A",
}


def resource_mana(draw, fill_hex):
    droplet(draw, fill_hex)
    draw.ellipse([CX - 3, CY - 6, CX + 3, CY], fill=hex_to_rgba("#FFFFFF"))


def resource_cost(draw, fill_hex):
    """A faceted gem -- distinct from mana's droplet, for the resource/
    health COST row (coordinator pass 2: dropping this row was a
    regression)."""
    pts = [(CX, CY - 16), (CX + 14, CY - 4), (CX + 9, CY + 16), (CX - 9, CY + 16),
           (CX - 14, CY - 4)]
    glyph(draw, pts, fill_hex)
    draw.line([(CX, CY - 16), (CX, CY + 16)], fill=hex_to_rgba(OUTLINE), width=1)
    draw.line([(CX - 14, CY - 4), (CX + 14, CY - 4)], fill=hex_to_rgba(OUTLINE), width=1)


def resource_cooldown(draw, fill_hex):
    """A clock face for the COOLDOWN row (coordinator pass 2: same)."""
    draw.ellipse([CX - 15, CY - 15, CX + 15, CY + 15], fill=hex_to_rgba(fill_hex),
                 outline=hex_to_rgba(OUTLINE), width=3)
    draw.line([(CX, CY), (CX, CY - 10)], fill=hex_to_rgba(OUTLINE), width=3)
    draw.line([(CX, CY), (CX + 8, CY + 3)], fill=hex_to_rgba(OUTLINE), width=3)
    draw.ellipse([CX - 2, CY - 2, CX + 2, CY + 2], fill=hex_to_rgba(OUTLINE))


def build(key, painter, fill_hex):
    img = new_canvas()
    draw = ImageDraw.Draw(img)
    draw_badge(draw, fill_hex)
    painter(draw, fill_hex)
    img.save(os.path.join(OUT_DIR, f"{key}.png"))


def main():
    os.makedirs(OUT_DIR, exist_ok=True)

    for name, painter in ELEMENT_GLYPHS.items():
        build(f"element_{name}", painter, ELEMENT_HEX[name])

    build("defense_physical", lambda d, c: shield(d, c, rune=False), NEUTRAL_SILVER)
    build("defense_magical", lambda d, c: shield(d, c, rune=True), NEUTRAL_GOLD)
    build("defense_ignored", shield_broken, NEUTRAL_SILVER)

    build("reach_front", lambda d, c: arrow_wall(d, c, blocked=True), NEUTRAL_SILVER)
    build("reach_any", lambda d, c: arrow_wall(d, c, blocked=False), NEUTRAL_SILVER)

    build("target_aoe", lambda d, c: burst(d, c, single=False), NEUTRAL_GOLD)
    build("target_single", lambda d, c: burst(d, c, single=True), NEUTRAL_GOLD)

    for short, hexv in SCALE_HEX.items():
        build(f"scale_{short}", lambda d, c, k=short: scale_glyph(d, c, k), hexv)

    build("resource_mana", resource_mana, "#6FB8E8")
    build("resource_cost", resource_cost, "#C98A3A")
    build("resource_cooldown", resource_cooldown, NEUTRAL_SILVER)

    count = len(ELEMENT_GLYPHS) + 3 + 2 + 2 + len(SCALE_HEX) + 3
    print(f"Wrote {count} icons to {OUT_DIR}")


if __name__ == "__main__":
    main()
