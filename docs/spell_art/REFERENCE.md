# Spell art reference pack

What a new spell piece is judged against (art rubric: `docs/SPELL_FEEL.md`
"Review rubric"). Attach these to an art prompt per
`docs/prompts/TEMPLATE_spell_art.md`. Images are small thumbnails and crops in
`docs/spell_art/reference/`; the sources are linked.

## The style: the Giant Rat sheet

Source (1536x1024, 1.4 MB, not copied):
`Assets/_Project/Art/Enemies/Giant_rat_sheet.png`. Thumbnail:
`reference/giant_rat_sheet_thumb.png`.

Flat cel art. Reference for rendering style only, never for subject or palette.

| Property | Rule |
|---|---|
| Outline | bold, uniform, dark, closed around every shape |
| Tones | two or three discrete tones per colour area, hard edges between them |
| Palette | small; one saturated hue per object on a muted scene |
| Texture | minimal; no micro-texture, no airbrush, no painted gradients |
| Light | none painted: no rim light, no glow, no bloom. The engine adds glow, flicker and darkness at runtime |
| Green | no green or yellow-green in the subject (the keyer removes by hue) |

Materials are told by tone and shape, not by rendering: brass is a flat gold
with one darker tone and a pale hit; coals are dark brown with orange cracks;
flame is deep red outside, orange, a small yellow core.

## Approved: the Censer of Embers pieces

The owner's approved art. Sources:
`Assets/_Project/Art/Sheets/Spells/Censer of Embers/censer_hanging.png` and
`censer_flame_loop.png`; recipes `Assets/_Project/Art/Sheets/recipes/censer_hanging.json`
and `censer_flame_loop.json`.

| Piece | Image | Note |
|---|---|---|
| Censer, hanging | `reference/censer_hanging.png` (source on a dark backdrop, 512 px) | flat gold, pierced lid, coal light as flat orange, thin outlined smoke; hangs dead vertical, the game swings it |
| Flame loop | `reference/censer_flame_loop.png` (768 px, four cells) | low wide band, same coals in every cell, tongues lean and re-form |

At actual gameplay size (1920x1080 reference canvas, 1x, crop of the stage
around a treant; frames from `tools/gif.ps1 spell censer_of_embers -Vs treant`,
run `20261005T112514Z_a437ad07ae05`, the working tree of 2026-10-05; the
lantern is about 65 px of the 960 px GIF, 130 px here):

- `reference/censer_swing_1x.png`: the lantern at the bottom of a swing.
- `reference/censer_fire_1x.png`: the flame loop at the feet after the break,
  embers falling.

Read these two before judging a sheet: a detail that does not survive at this
size is not there.

## Rejected, and why

| Image | Piece | Why |
|---|---|---|
| `reference/rejected_painterly_glow.png` | Ashen Reckoning catch (`Art/Sheets/Spells/Ashen Reckoning/ashen_reckoning_catch.png`, shown keyed on the stage backdrop) | painted glow and gradients; clashes with the fight's cel art and leaves the engine nothing to add |
| `reference/rejected_flying_head.png` | Viper's Bite lunge v2, frame from `docs/captures/spell_rework/after/vipers_bite/vipers_bite_rat_1x.gif` (half scale) | a body-sized snake head thrown across the stage: the verb changed from bite to projectile, and the head covers the target |
| `reference/rejected_sliding_cage.png` | Thorn Tithe growth, cell 4 (`Art/Sheets/Spells/Thorn Tithe/thorn_tithe_growth.png`, keyed) | the cage is one finished drawing that rises as a unit: it never closes round the body, so it reads as a bush or a slide, and the shading is painterly |
