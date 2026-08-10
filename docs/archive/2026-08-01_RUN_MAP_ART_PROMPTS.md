# Image-generation prompts — Run Map, forest floor nodes

> **ARCHIVED 2026-08-01: delivered.** `forest_map_background.png` and the
> per-room-type node icons this describes are in `Art/Backgrounds/` and
> wired into `DescentMapView`/`SceneBuilder.BuildMapPanel`. Kept as the
> copy-paste record of what was actually asked for.

Nine prompts: one shared node backdrop, an escalating three-tier creature face, three
encounter props, two additions this project's room list needs beyond the design
handoff's own seven types, and an optional dirt-trail texture. Paste them one at a
time. Reasoning and full spec live in `Handovers/design_handoff_run_map/README.md`;
this file is copy-paste prompts plus the two gaps it left.

The engine generates eight room types on a floor (`RoomType.Fight`, `EliteFight`,
`Boss`, `Rest`, `Shop`, `Event`, `Treasure`, `Unknown`), not the seven the design
prototype demonstrates — its `TYPES` table has no equivalent for `Treasure` (a free
stash) or `Unknown` (unscouted, resolves on entry). Prompts 8 and 9 below fill that
gap in the same visual language as the rest of the set.

---

## Before you start — two things that decide whether this works

**Every asset except the backdrop is generated on a pure green field, not on
transparency**, same reasoning as `HUB_ART_PROMPTS.md`: image generators are
unreliable at real alpha, and brightness-keying would punch holes through the dark
shadow that is half of what sells these as creatures rather than mascots. Pure green
`#00FF00` appears nowhere in this palette, so keying on hue removes the background
and touches nothing else.

**The three-tier face (mob/elite/boss) has to read as ONE creature escalating**, not
three unrelated designs — bigger, spikier, angrier, same silhouette family. Generate
the three in the same sitting and explicitly reference the previous one in each
follow-up prompt ("the same creature as the last image, but—") rather than pasting
all three prompts independently.

Ask for **square, 1024×1024** on the props/faces; the backdrop wants the **widest
landscape** the tool offers, cropped after. Check before moving on: no border or
frame, no text anywhere, and (where specified) the green reaching all four edges.

---

## 0 — `clearing_forest.png` — the shared backdrop

The ONE tile every room sits on regardless of type. Self-contained, not green-keyed —
this is the backdrop the props composite onto, the same relationship the nebula has
to the hub buildings.

> A small clearing on the floor of an ancient forest, seen as a single self-contained
> vignette rather than a wide scene — five or six tree trunks with heavy canopies
> arranged around the frame's edges, opening onto a patch of bare earth and leaf
> litter in the centre. Painted in a rich hand-painted style with visible brush
> texture — the painterly look of Hades, not photorealism and not vector-clean
> digital art.
>
> Portrait framing, taller than it is wide. The trees are thick and mossy, their
> canopies overlapping at the top of the frame so the clearing reads as enclosed
> rather than open sky. A little diffuse daylight comes through the gaps, landing on
> the ground, not on anything at standing height.
>
> The centre of the frame — roughly the middle third, at standing height — stays
> completely bare: no rocks, roots, plants or debris tall enough to break a small
> figure's outline. This is where a creature or a prop will be composited on top, and
> it needs a clean, uncluttered spot to sit in.
>
> Colour: deep desaturated greens, mossy browns, cool shadow. Vignette the whole
> frame darker at the very edges so it does not read as a hard-edged rectangle when
> tiled across a floor map next to identical copies of itself.
>
> No creatures, no props, no text, no border or frame. Widest useful vertical
> framing the tool allows.

---

## 1 — `face_mob.png` — the standard-fight tier

> A small, snarling forest creature's face, painted in a rich hand-painted style with
> visible brush texture and strong rim light — the painterly look of Hades.
>
> Rounded, low-slung, more animal than humanoid — think a cross between a boar and a
> goblin, angry rather than menacing. Dark reddish hide, two small tusks or teeth
> showing, narrow angry eyes. No horns, no crown, no ornamentation — this is the
> baseline tier the next two prompts escalate from.
>
> Head and upper shoulders only, facing forward, centred with generous margin on all
> sides — roughly 15% of the width. Straight-on view, no perspective tilt, no ground,
> no cast shadow.
>
> No text, no border or frame, no vignette. The entire background around the creature
> must be flat pure green, hex #00FF00, perfectly uniform, reaching all four edges.

---

## 2 — `face_elite.png` — the elite tier

> The SAME creature as the previous image — same hide colour, same face shape, same
> family — but bigger, angrier, and armoured: add a crown of three short curved
> spikes rising from its brow, and brighten the trim around its eyes and tusks to a
> warm gold, as if it has been marked as something worth fearing. The expression
> reads more confident, less feral.
>
> Painted in the same rich hand-painted style with visible brush texture and strong
> rim light — the painterly look of Hades.
>
> Head and upper shoulders only, facing forward, centred with generous margin on all
> sides — roughly 12% of the width (slightly larger in frame than the standard tier,
> to read as a bigger creature at the same zoom level). Straight-on view, no
> perspective tilt, no ground, no cast shadow.
>
> No text, no border or frame, no vignette. The entire background around the creature
> must be flat pure green, hex #00FF00, perfectly uniform, reaching all four edges.

---

## 3 — `face_boss.png` — the boss tier

> The SAME creature family as the previous two images, now at its most dangerous:
> larger again, with a full crown of six curved gold spikes, an added faint aura or
> outer glow ringing its silhouette (warm orange, not gold, to read as the single
> fixed landmark of the floor), and eyes lit from within rather than merely rimmed.
> Still recognizably the same creature — same hide, same face shape — pushed to its
> most menacing form rather than redesigned.
>
> Painted in the same rich hand-painted style with visible brush texture and strong
> rim light — the painterly look of Hades.
>
> Head and upper shoulders only, facing forward, centred with generous margin on all
> sides — roughly 10% of the width (largest of the three tiers at the same zoom
> level). Straight-on view, no perspective tilt, no ground, no cast shadow.
>
> No text, no border or frame, no vignette. The entire background around the creature
> must be flat pure green, hex #00FF00, perfectly uniform, reaching all four edges.

---

## 4 — `prop_campfire.png` — the rest room

> A small campfire, painted in a rich hand-painted style with visible brush texture
> and strong warm rim light — the painterly look of Hades.
>
> Two or three crossed logs with a modest, warm orange-gold flame rising from the
> centre, painted loosely rather than as sharp vector fire. Faint embers drifting
> upward. Reads as a place to rest, not a hazard — welcoming, not roaring.
>
> Centred with generous margin on all sides — roughly 18% of the width. Straight-on
> view, no perspective tilt, no ground beyond the logs themselves, no cast shadow.
>
> No text, no border or frame, no vignette. The entire background around the object
> must be flat pure green, hex #00FF00, perfectly uniform, reaching all four edges.

---

## 5 — `prop_chest.png` — the shop room

> A small wooden merchant's chest, painted in a rich hand-painted style with visible
> brush texture and strong rim light — the painterly look of Hades.
>
> Weathered wood banded in warm gold trim, lid propped open at an angle with a soft
> golden glow spilling from inside — enough to read as "something to trade for," not
> a treasure hoard. A simple latch and hinge, no lock, no chain.
>
> Centred with generous margin on all sides — roughly 15% of the width. Three-quarter
> view, no ground beyond the chest's own base, no cast shadow.
>
> No text, no border or frame, no vignette. The entire background around the object
> must be flat pure green, hex #00FF00, perfectly uniform, reaching all four edges.

---

## 6 — `prop_rune.png` — the event room

> A small glowing violet rune hovering above a faint circular sigil, painted in a
> rich hand-painted style with visible brush texture and soft violet glow — the
> painterly look of Hades.
>
> A simple spiral or looping glyph, softly luminous, floating just above a thin
> circular ring etched into the air itself (no ground, no pedestal). Mysterious
> rather than menacing — this room is a choice, not a fight, and the art should read
> as "something uncertain," not "something evil."
>
> Centred with generous margin on all sides — roughly 20% of the width. Straight-on
> view, no cast shadow.
>
> No text, no border or frame, no vignette. The entire background around the object
> must be flat pure green, hex #00FF00, perfectly uniform, reaching all four edges.

---

## 7 — `prop_treasure.png` — the Treasure room *(gap fill)*

The design prototype has no equivalent — its `shop` type already covers "spend gold,"
and this room is "free stash" instead, which the engine has always kept as its own
`RoomType`. Distinct from the chest above on purpose: no latch, no trim, nothing to
buy — this is loot lying in the open.

> A small loose pile of coins and a single glinting gem, painted in a rich
> hand-painted style with visible brush texture and warm gold glow — the painterly
> look of Hades.
>
> Coins scattered rather than stacked, catching warm light unevenly; one larger gem
> or jewel sitting slightly apart from the pile as the focal point. No chest, no
> container, no bag — this is a free find lying on the ground, not something locked
> away.
>
> Centred with generous margin on all sides — roughly 18% of the width. Straight-on
> view, no ground beyond a small patch the coins rest on, no cast shadow.
>
> No text, no border or frame, no vignette. The entire background around the object
> must be flat pure green, hex #00FF00, perfectly uniform, reaching all four edges.

---

## 8 — `overlay_unknown.png` — the Unknown room *(gap fill)*

Also missing from the prototype. `RoomType.Unknown` shows a "?" and resolves only
once entered — the one room type whose ENTIRE point is that you cannot tell what it
is from the map. The overlay has to read as "obscured," not as any specific
encounter.

> A small patch of drifting grey-violet fog or mist, thick enough to obscure whatever
> is behind it, painted in a rich hand-painted style with visible brush texture — the
> painterly look of Hades.
>
> No creature, no object, no glow of any particular colour — just dense, softly
> curling mist with a faint cool violet undertone, dark at its core and thinning
> toward the edges. The point is that nothing can be made out inside it.
>
> Centred with generous margin on all sides — roughly 15% of the width. No ground, no
> cast shadow.
>
> No text, no border or frame, no vignette. The entire background around the mist
> must be flat pure green, hex #00FF00, perfectly uniform, reaching all four edges.

---

## 9 — `path_dirt.png` — the trail *(optional, not blocking)*

The README calls this optional-until-later, and the first engineering pass renders
paths as plain coloured strokes (matching the prototype's own outer-tread/inner-tread
colour table) rather than a textured ribbon — so nothing is blocked on this landing.
When it does:

> A worn dirt path seen from a slight downward angle, painted in a rich hand-painted
> style with visible brush texture — the painterly look of Hades. A wide band of
> packed brown earth with a lighter, narrower worn centreline where footsteps have
> compressed the ground, running the full width of the image left to right in a
> straight line (curvature will be applied in the game). Loose leaf litter and small
> stones scattered at the outer edges of the brown band only, never in the pale
> centreline itself.
>
> Designed to tile seamlessly left-to-right — the left and right edges must match up
> with no visible seam.
>
> No text, no border or frame, no figures. Wide landscape aspect ratio, at least 4:1.

A gold-tinted `path_dirt_walked.png` variant (same texture, warmer/richer colour) is
a same-day follow-up once the base texture exists — not needed to start.
