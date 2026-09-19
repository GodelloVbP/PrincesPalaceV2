# Spell book art plan

## Visual language

Color identifies damage type. Each spell has a distinct primary glyph silhouette that identifies its action; spell families may share secondary motifs. Glyphs must remain recognizable at the actual shop/inventory thumbnail size.

Dual-damage covers require a purpose-built jagged diagonal split with two independently recolorable regions. This is a base-art requirement, not a straight stripe added during recoloring. Region A occupies the left/top-left; region B occupies the right/bottom-right. Use the same upper-right-to-lower-left crack direction across all dual-damage books and their split glyphs.

## Damage palette

| Damage type | Color |
| --- | --- |
| Physical | Bone-gray / steel |
| Fire | Red-orange |
| Ice | Icy cyan-blue |
| Nature | Leaf green |
| Poison | Sickly yellow-green |
| Arcane | Violet-magenta |
| Earth | Umber brown |
| Water | Deep sea blue |
| Wind | Pale teal-white |
| Lightning | Electric yellow |
| Void | Near-black with a violet edge-glow |

Keep hardware neutral regardless of damage palette. Distinguish Nature/Poison and Ice/Water/Wind by hue and value, then verify at thumbnail size. Void's edge-glow belongs to the recolored cover treatment, not the generic hardware.

## Existing book spells

| Spell | Cover | Glyph | Readability |
| --- | --- | --- | --- |
| Mud Burst | Solid umber brown | A mound with three jagged mud/rock shards bursting upward; a few small debris flecks | A radiating burst rising from a base |
| Frost Flare | Jagged split: A red-orange, B icy cyan | One fused flame-and-icicle glyph; flame occupies A, icicle occupies B, meeting along the same diagonal zigzag as the cover | Repeats the cover's damage duality; distinct paired silhouette |
| Cinderfault | Jagged split: A red-orange, B leaf green | A broad horizontal root-shaped ground fissure with a flame erupting through its center; one bold green root branch integrated into the fissure | Horizontal fault contrasts with Mud Burst's upward mound; root motif supports Nature |
| Lightning Bolt | Solid electric yellow | One thick angular zigzag bolt; no secondary details | Plain, familiar silhouette |

Cinderfault is Fire + Nature in the current authored skill data. Preserve that identity; the root-shaped fissure clarifies Nature without changing gameplay. The cover carries the palette even when a glyph uses a high-contrast neutral treatment.

## Generic base commission prompt

A single closed spellbook in painterly fantasy game-item icon style, matching the project's detailed relic icons: tactile worn leather, controlled brushwork, dimensional materials, and crisp readable outer silhouette. Thick leather-bound tome in a cover-dominant three-quarter view; spine on the left, narrow cream page fore-edge visible on the right and bottom. The cover remains nearly frontal and large enough to read a glyph at thumbnail size.

The leather cover is one broad uninterrupted recolorable field, with subtle texture and material shading kept separate from its base color. No painted illustration, text, runes, decorative border patterns, or spell glyph. Use plain neutral gunmetal corner guards and one modest clasp; hardware must never inherit the cover tint. A small round, flat, low-relief neutral gunmetal emblem plate sits at the cover center, roughly one-third of the cover width, providing a clear surface for a later glyph. No faceted gem. Keep the plate's rim slim and its interior unornamented.

Second variant: the exact same book silhouette, angle, proportions, lighting, wear, hardware, and plate, with the leather cover divided into approximately equal areas by a single jagged diagonal seam from upper-right to lower-left through the center. The plate straddles and occludes the central seam. The seam remains visible above and below it. It is a closed surface division with a narrow dark edge, not a gaping hole, glowing lava crack, or straight flag stripe. Both regions must be independently recolorable. No physical separation of the book halves.

Deliver on genuine transparency with no baked ground shadow, background, or surrounding glow. Internal material shadows are allowed.

## Production delivery

Reusable asset review package now lives in `output/spell-books/`: one shared master, solid and jagged-split templates with transparent center opening, exact leather/region/slot masks, four separate glyphs, assembled 512px and 48px icons, eleven solid palette previews, generation prompts and reproducible build script. See its README for verified outputs and remaining visual limitations. These are review assets; no runtime replacements have been made. The raster mask workflow replaces the earlier flattened-only concept limitation for this package.

- Matching solid and split bases as transparent PNGs, at identical canvas size and alignment, with safe padding around the silhouette.
- Layered editable source: solid-cover base mask; split-region A and B masks; leather shading/highlights; seam; hardware; center plate; pages. Masks must account for hardware and plate occlusion and support recoloring without recoloring metal or pages.
- Cover texture and lighting must survive recoloring. Avoid clipping bright Lightning and dark Void treatments.
- Separate spell glyph overlays; glyphs are not baked into generic bases.
- Inspect at intended in-game size, including silhouette, plate legibility, and both split regions. Compare all four spell thumbnails together.
- Generated iterations are flattened concept previews. They do not constitute the required layered source or independently editable production masks.

## Initial iterations

1. Compact practical tome: restrained wear, simple metal guards, broad cover field.
2. Heavier old tome: thicker leather/spine and slightly more worn edges, while retaining the same plain hardware and unobstructed glyph plate.

Each iteration preview shows a solid neutral cover and its matching split cover. Two neutral leather values demonstrate the split without assigning a spell's damage colors. Final selection precedes production layer/mask preparation.

Saved previews:
- `output/spell-book-concepts/generic-book-iteration-01.png`: cleaner leather, restrained wear.
- `output/spell-book-concepts/generic-book-iteration-02.png`: heavier spine, stronger age and wear.
- `output/spell-book-concepts/prompts.md`: exact generation prompts.

Visual review: both previews provide blank center plates and distinct jagged split regions. Both rendered with a dark backdrop despite the transparency request; background removal remains required for production. The second preview's seam reads more deeply cut than intended and should be flattened to a narrow closed boundary in the selected final base. Matching pairs are conceptual matches, not guaranteed pixel-aligned production layers.
