# Art brief: containers on the Character / Inventory screen

Eight painted containers to replace the flat colour blocks the dossier draws
today. Every one of these is currently a rectangle of tinted violet, which is
why the screen reads as a wireframe with good art dropped into it: **the
contents are painted and the things holding them are not.**

Written as generation prompts. Sizes are exact, and they matter — see
*Non-negotiables* before generating anything.

## Style anchor

The reference is the game's own portrait work (`Art/Portraits/Sheep/`) and its
item sheets. Painterly, visible brush economy, no line-art outlines, no cel
shading, no vector or flat-UI look. Warm key light from **above**, cool teal
rim light where a form turns away. Everything sits on deep violet-black.

Palette, and these are the actual tokens in `Domain/UiKit/FightHudPalette.cs` —
use them rather than inventing neighbours:

| Role | Hex |
|---|---|
| Ground the panel stands on | `#120A18` |
| Panel violet | `#1A1024` |
| Gold rim, the only bright accent | `#E7B25C` |
| Gold light, highlights on that rim | `#FFE0A8` |
| Hairline violet | `#C8AAE6` at ~22% |
| Text it must never fight | `#F4EBFF` |

Material vocabulary: **blackened iron, worn brass, oiled dark wood, waxed
leather, candle-smoked stone.** No marble, no chrome, no glass, no gemstones
except where an item's own art supplies them.

## Non-negotiables

These are the constraints that decide whether a piece is usable at all, and
the first three have already cost a regeneration on this project.

1. **The centre stays empty.** Every container here holds something — an icon,
   a numeral, a portrait. Ornament belongs on the *border*. A previous
   armour-stand brief said "no internal detail competing with the slot cells"
   and came back with pauldrons and tassets sitting exactly where the slot
   cells land. If the piece looks good on its own, it is probably wrong.
2. **Transparent PNG, alpha to the edge.** No baked background colour — these
   composite over a gradient that changes down the panel.
3. **Exact pixel dimensions, generated at 4x and downsampled.** These are fixed
   sizes, not nine-slice. Do not author a stretchable frame; the layout never
   stretches them.
4. **Two states per interactive container**, delivered as separate files at
   identical dimensions and identical geometry — the rest state and the hover
   state must line up pixel-for-pixel, because the hover swap is a sprite swap
   with no transform. Only light and colour change between them.
5. **Legible at 100%.** Detail finer than ~2px at final size turns to mush.
   Read the size before deciding how ornate to be: a 60px pack cell can carry a
   rim and a corner, and nothing else.

---

## 1. Equipment slot, rest — `slot_rest.png` — 74 x 74

> A small square niche set into blackened iron, viewed straight on. A shallow
> recess with a soft inner shadow at the top edge, as though light falls from
> above and the hollow catches it. The border is a thin band of worn dark metal,
> barely a millimetre proud of the surface, with faint hammer marks. The centre
> of the recess is empty, flat, and unlit — a plain dark surface with no
> ornament, no engraving and no texture pattern. Painterly, deep violet-black
> `#120A18`, cool and unlit. Transparent background. 74x74.

The niche the eight equipment icons drop into. It is the most repeated shape on
the screen, so it must be the quietest.

## 2. Equipment slot, hover — `slot_hover.png` — 74 x 74

> The same square iron niche, exactly the same geometry, now lit. A warm brass
> rim `#E7B25C` catches along the border with a brighter glint `#FFE0A8` on the
> top-left corner. A faint warm bloom sits just inside the border and fades to
> nothing before the centre. The centre of the recess stays empty, flat and
> dark. Painterly. Transparent background. 74x74.

The light arrives at the **rim**, not in the middle. A filled glow behind an
item icon greys the icon out, which is what the current violet block does.

## 3. Pack cell, rest — `pack_cell_rest.png` — 60 x 60

> A small dark leather square with a stitched edge, like a compartment in a
> travelling roll. Thread visible only as a suggestion along the border. Very
> slightly domed so the top edge is a touch lighter than the bottom. The centre
> is plain, unlit and completely free of detail. Painterly, muted, near-black
> with a faint warm brown in the leather. Transparent background. 60x60.

Twenty-four of these tile in a 4-wide grid with an 8px gutter. Leather rather
than iron distinguishes "carried" from "worn" at a glance.

## 4. Pack cell, hover — `pack_cell_hover.png` — 60 x 60

> The same stitched leather square, identical geometry, with the stitching now
> catching warm light `#FFE0A8` and the leather lifted a shade. A thin warm line
> traces just inside the border. Centre unchanged, plain and dark. Painterly.
> Transparent background. 60x60.

## 5. Stat row highlight — `stat_row_glow.png` — 340 x 34

> A long thin horizontal smear of warm violet light on black, like a lamp passing
> behind a shelf. Brightest along a soft horizontal core about a third of the
> way down, fading to nothing at the top and bottom edges and fading to nothing
> at both ends so it has no visible left or right boundary. No border, no frame,
> no shape of any kind — only light. Painterly and very soft. Transparent
> background. 340x34.

**This is the purple block.** It marks which derived stats an attribute feeds
when you hover an ability score, so several appear at once — it has to read as
emphasis, not as a row of selected list items. It must have **no edges**: a
rectangle with visible ends is what makes the current one look like a bug.

## 6. Attribute cell, hover — `attribute_cell_hover.png` — 113 x 66

> A soft warm pool of light on black, roughly oval, wider than it is tall, with
> completely diffuse edges. Slightly brighter at the top where the light comes
> from. Warm violet-gold. No border, no frame, no rim. Painterly. Transparent
> background. 113x66.

Sits behind one of the six ability scores on hover. The numeral sits on top of
it, so the brightest part must be **behind and below** the numeral rather than
across it — keep the top third dim.

## 7. Tooltip plate — `tooltip_plate.png` — 290 x 130

> A small plaque of dark oiled wood bound in thin blackened brass, seen straight
> on. The brass edging is worn brighter at the corners where a thumb would rest.
> The wood surface is dark, evenly lit, and almost featureless — a suggestion of
> grain, nothing more. A very faint warm inner glow along the top edge. The whole
> centre is clear and readable. Painterly, deep and unlit overall so pale text
> reads over it. Transparent background, with a soft drop shadow bleeding into
> the alpha at the bottom edge. 290x130.

Carries an item name and two lines of stats, and it floats over the loadout, so
it needs to be **more opaque and more solid** than anything else here — it is
the one container allowed to fully occlude.

## 8. Portrait frame — `portrait_frame.png` — 266 x 270

> An arched frame of dark carved wood with a thin brass inner lip, like a shrine
> niche. Heavier and more ornate at the top of the arch, tapering to almost
> nothing at the bottom two corners so the frame seems to emerge from the dark
> rather than sit as a rectangle. The interior is completely empty and fully
> transparent — the frame is a border only. Warm light catching the top of the
> arch, teal rim on the right side. Painterly, richly detailed in the woodwork.
> Transparent background. 266x270.

Goes **around** Shawn, not behind him — the interior must be fully transparent
or it will cover the portrait. The bottom corners fading out matters: a closed
rectangle here boxes the character in and fights the arch of the wool.

---

## Order to generate in

If only some get made, this is the order that buys the most:

1. **#5 stat row highlight** — the one the author actually complained about.
2. **#1 / #2 equipment slot** — eight of them, the structural core of the screen.
3. **#3 / #4 pack cell** — twenty-four, but small and quiet.
4. **#7 tooltip plate** — high impact, appears constantly.
5. **#8 portrait frame** — the biggest single upgrade in perceived quality.
6. **#6 attribute cell** — smallest gain; the hover link already reads.

## Wiring, once they exist

Drop the PNGs in `Assets/_Project/Art/UI/Dossier/`. They are Assets-relative
`EditorBaked` paths (`Domain/Content/ArtPathConvention.cs`), so they bake into
the scene at build time like the rest of the menu art — no runtime loading.

The rest/hover pairs need a sprite swap on pointer enter and exit. `HoverIndex`
already reports that for the slot and pack cells, and the dossier controller
already listens to it for the tooltip and the equip preview, so this is a
sprite assignment in the existing handler rather than new plumbing.
