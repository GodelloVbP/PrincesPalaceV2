# Spell art prompt template

Fill the `<...>` fields; delete what does not apply. Judged afterwards by the
art rubric in `docs/SPELL_FEEL.md`. The prompt text is the only thing sent: the
owner pastes it into the image tool and uploads the attachments listed.

## 1. Shared style block (paste first, unchanged)

Upload as **rendering style** references only (not subject or palette):
`Assets/_Project/Art/Enemies/Giant_rat_sheet.png`, then
`docs/spell_art/reference/censer_hanging.png` and
`docs/spell_art/reference/censer_flame_loop.png` (approved spell pieces in the
same style). Optionally attach the rejected examples under "Do not make it like
this": `docs/spell_art/reference/rejected_*.png`. Style rules and reference
notes: `docs/spell_art/REFERENCE.md`.

Flat 2D game cel art matching the references: bold, uniform, dark outlines; flat
cel shading with two or three discrete tones per colour area; a small palette;
minimal internal texture. Clean game sprite art, not illustration. No painterly
gradients, no rim light, no painted glow or bloom, no soft airbrush, no
micro-texture. The game adds glow, flicker and darkness at runtime, so the art
stays flat.

Solid #00FF00 background, no gradient, vignette, floor or shadow plate. No green
or yellow-green anywhere in the subject (the keyer removes by hue). **If the
subject contains green or teal (venom), swap the backdrop to solid #FF00FF, drop
the "no green" line, say "no pink or magenta in the subject" instead, and key
the recipe `"keyed": "magenta"`.** No text,
numerals, frame, border, UI or watermark.

**Sheets:** a grid of equal cells. Every cell has the **same pivot at the same
pixel position** (stated per piece). Nothing crosses a cell edge. The drawing
changes between cells; the canvas, scale and pivot never do.

## 2. Per-piece spec

One block per piece.

```
Piece: <spell name>: <what it is>
Beat it serves: <beat from the spell's beat table>
Filename: <snake_case>.png
Delivery path: Assets/_Project/Art/Sheets/Spells/<Spell Name>/<filename>
Canvas: <W>x<H>   Grid: <cols>x<rows> of <cell W>x<cell H>   (or: one still)
Pivot: <where, in every cell, as a % of the cell: e.g. top edge centre; base line 85% down, centred>
Size in play: <share of the target's body or stage width, from the beat table>
Drawing: <the subject, cell by cell if a sheet; materials in flat tones>
Verb: <what it does to reality>
Must read as: <...>
Must NOT read as: <...>
Not in the picture: <hand, caster, target, floor, smoke, glow, ...>
```

## 3. Delivery spec (paste after the piece block)

Deliver one PNG, named exactly `<filename>`, at the stated canvas size, on solid
#00FF00 with no green in the subject. The owner saves it to the delivery path.
Anything the keyer would remove (green, yellow-green) is wrong.

## 4. Targeted revision

When one piece fails the rubric, send only this, with the accepted-so-far image
attached:

```
Same image, same canvas, same pivot, same style. Change only: <the one change,
in drawing terms: "the vines' base on one line in every cell">. Keep everything
else, pixel for pixel where you can. Do not add glow, gradients or new
elements.
```

One change per revision. If the image needs two changes, send two prompts in
sequence rather than one.

## 5. Batch, single paste

For several settled pieces at once (only spells whose motion is approved). Paste
the style block (section 1) once, then:

```
Make the following <N> separate images, one per piece, each delivered as its
own PNG with exactly the filename given. Each follows the style block above.

1) Filename: <name1>.png   Canvas: <WxH>   Grid: <cxr>   Pivot: <...>
   Drawing: <...>
   Must read as: <...>   Must NOT read as: <...>
2) Filename: <name2>.png   Canvas: <WxH>   Grid: <cxr>   Pivot: <...>
   Drawing: <...>
   Must read as: <...>   Must NOT read as: <...>
...

Solid #00FF00 background on every image, no green in any subject, no text.
```

After delivery, record each accepted prompt, source file, recipe and output in
the spell's NOTES (`docs/captures/spell_rework/NOTES_TEMPLATE.md`, art record).
