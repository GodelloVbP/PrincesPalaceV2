# Image-generation prompts — Divine Principality hub

> **ARCHIVED 2026-08-01: delivered.** All six pieces landed and are wired in
> — see `docs/archive/2026-08-01_HUB_DESIGN_BRIEF.md`. Kept as the copy-paste
> record of what was actually asked for.

Six prompts: one background, five buildings. Paste them one at a time. Specs and
reasoning live in `HUB_DESIGN_BRIEF.md`; this file is just the copy-paste.

---

## Before you start — two things that decide whether this works

**Everything foreground is generated on a pure green field, not on transparency.**
Image generators are unreliable at real alpha, and the obvious workaround — generate on
black and key the alpha from brightness, which is how the spell frames were cut — is
actively wrong here. A building has dark stone, deep shadow and (for the gate) a
*deliberately dark void* inside the arch that has to stay opaque. Brightness-keying
would punch holes straight through all of it.

Pure green `#00FF00` appears nowhere in this palette, so keying on hue removes the
background and touches nothing else. Each prompt ends with the green-field instruction —
don't drop it.

**Ask for square, 1024 × 1024.** I resample to the delivery sizes. The background is the
exception: ask for the widest landscape the tool offers, and I will crop it to 16:9 —
harmless on a nebula, which is one reason the art direction is a nebula.

Generate one at a time and check three things before moving on: no border or frame drawn
around the image, no text anywhere, and the green actually reaching all four edges.

---

## 0 — The background

> A vast purple and black nebula field seen from deep space, painted in a rich
> hand-painted style with visible brush texture — think the painterly look of Hades, not
> photorealism and not vector-clean digital art.
>
> Deep violet, magenta and near-black indigo, with occasional warm ember-orange and teal
> pockets so it never reads as a flat gradient. The gas has real internal structure:
> filaments, dust lanes, and one brighter core sitting slightly left of centre. Stars at
> three distinct scales — a fine dust of tiny ones, a scattered middle layer, and a
> handful of large bright ones with subtle diffraction spikes. Cluster them unevenly;
> leave voids. The voids are what make it read as depth.
>
> The whole image sits DARK — bottom third of the value range almost everywhere. It is a
> backdrop, and brighter objects will be composited on top of it, so nothing in it should
> compete for attention.
>
> Composition: keep the upper-left, upper-right, lower-left and lower-right quadrants
> quiet and dark — low star density, low contrast. Put a soft, slightly brighter bloom of
> nebula gas in the lower centre, offset about 100px left of dead centre so it is not a
> symmetrical spotlight. Warm rather than cold overall: this is somewhere that feels like
> home, not an empty void.
>
> No horizon, no ground, no planets, no spacecraft, no figures, no text, no border or
> frame. Widest landscape aspect ratio available.

---

## 1 — `gate.png` — Start Run *(the hero asset)*

> A freestanding stone archway floating alone in space, painted in a rich hand-painted
> style with visible brush texture and strong rim light — the painterly look of Hades.
>
> Weathered pale lavender stone with warm gold inlay catching the light along its edges.
> The arch stands on nothing: no ground beneath it, no platform, no base — it simply
> floats, with the stone ending cleanly at the bottom of each pillar.
>
> The opening is NOT empty and NOT transparent. It is filled with a deep, dark, faintly
> swirling violet-black void, slightly darker than deep space, so the arch reads as a way
> *through* to somewhere rather than as a hole you can see past.
>
> A stone brazier stands at the foot of each pillar, both burning with warm orange flame.
> That flame is the brightest, warmest light in the image and it lights the arch from
> below and inward. This should be the most inviting, most eye-catching object
> imaginable — grand, warm, and unmistakably an entrance.
>
> Centred in a square image with generous empty margin on all four sides — roughly 10% of
> the width. Straight-on view, no perspective tilt, no cast shadow on anything.
>
> No ground, no text, no border or frame, no vignette. The entire background around the
> object must be flat pure green, hex #00FF00, perfectly uniform, reaching all four
> edges. No green anywhere on the object itself and no green rim light.

---

## 2 — `talents.png` — the skill tree

> A small bare tree floating on a fragment of dark rock in space, painted in a rich
> hand-painted style with visible brush texture and strong rim light — the painterly look
> of Hades.
>
> No leaves at all. The branches spread wide and the roots trail off the underside of the
> rock into empty space. The bark is a cool violet-grey. Along the branches sit eight
> round glowing nodes connected by faint luminous lines running along the wood, exactly
> like a skill tree: five of them lit warm gold and glowing brightly, three still dark and
> unlit. The light is the foliage — the glow of those nodes is the only bright thing on
> the tree, and it should read instantly as a branching web of upgrades.
>
> Lit from the lower centre with warm light, cool violet shadow on the upper surfaces.
>
> Centred in a square image with generous empty margin on all four sides — roughly 10% of
> the width. Straight-on view, no cast shadow on anything.
>
> No text, no numbers, no icons, no border or frame, no vignette. The entire background
> around the object must be flat pure green, hex #00FF00, perfectly uniform, reaching all
> four edges. No green anywhere on the object itself and no green rim light.

---

## 3 — `principality.png` — the shop

> A merchant's market stall floating on a fragment of dark rock in space, painted in a
> rich hand-painted style with visible brush texture and strong rim light — the painterly
> look of Hades.
>
> A striped fabric awning in DEEP wine-magenta — rich and shadowed, not hot pink and not
> neon — stretches over a worn wooden counter on carved posts. A warm lantern hangs from
> the awning and lights the whole stall from underneath in amber. On the counter sit a
> few unmistakable goods catching that light: a glass potion bottle, a small stack of gold
> coins, and a cut violet gem.
>
> This should be the most cluttered, warmest, most lived-in object of the set — the one
> place that feels like somebody is standing just out of frame. But it must not out-shout
> a brighter object elsewhere in the scene, so keep the awning's colour deep and let the
> lantern do the work.
>
> Centred in a square image with generous empty margin on all four sides — roughly 10% of
> the width. Straight-on view, no cast shadow on anything, no shopkeeper figure.
>
> No text, no signage, no price tags, no border or frame, no vignette. The entire
> background around the object must be flat pure green, hex #00FF00, perfectly uniform,
> reaching all four edges. No green anywhere on the object itself and no green rim light.

---

## 4 — `character_sheet.png` — the lectern

> An open book resting on a carved lectern, floating on a small fragment of dark rock in
> space, painted in a rich hand-painted style with visible brush texture and strong rim
> light — the painterly look of Hades.
>
> The book's pages glow softly from within with a pale blue-white light. Faint
> indecipherable handwriting runs across both pages, and a small diagram of a human figure
> is visible on the right-hand page. A single feather quill rests in the gutter of the
> book. The lectern is dark carved wood with a slim stem and a wide foot.
>
> Cooler and quieter than a warmly-lit object would be — this is the contemplative corner
> of the scene. Pale blue interior light, cool violet shadow, a touch of warm light
> reaching it from below and to the right.
>
> Centred in a square image with generous empty margin on all four sides — roughly 10% of
> the width. Straight-on view, no cast shadow on anything.
>
> No readable text — the handwriting must be abstract marks, not real letters. No border
> or frame, no vignette. The entire background around the object must be flat pure green,
> hex #00FF00, perfectly uniform, reaching all four edges. No green anywhere on the object
> itself and no green rim light.

---

## 5 — `empty_plot.png` — reserved ground

> A flat fragment of dark rock floating alone in space, empty, with the faint glowing
> outline of a building hovering just above it — painted in a rich hand-painted style with
> visible brush texture, the painterly look of Hades.
>
> The outline is drawn as thin DASHED lines of soft violet light, like an architect's
> blueprint of a small house or tower projected into the air: just the edges, hollow, with
> nothing solid inside it. It is clearly a plan for something not yet built.
>
> Deliberately dim and unfinished — this object should be noticeably fainter than a
> finished, lit building would be, roughly a third as bright. But it must still be plainly
> visible and clearly deliberate: it has to read as *reserved ground waiting for
> something*, never as a mistake or a half-loaded image. A soft violet glow rises from the
> rock surface where the structure will stand.
>
> Centred in a square image with generous empty margin on all four sides — roughly 10% of
> the width. Straight-on view, no cast shadow on anything.
>
> No text, no measurements, no border or frame, no vignette. The entire background around
> the object must be flat pure green, hex #00FF00, perfectly uniform, reaching all four
> edges. No green anywhere on the object itself and no green rim light.

---

## When they land

Drop the raw generations anywhere and tell me. I will write the chroma keyer (the
brightness-keyed spell slicer is the wrong tool for these, for the reason at the top),
cut the green, resample to the delivery sizes, drop them into
`Assets/_Project/Art/UI/Hub/` under the exact filenames above, and rebuild the scene.

Nothing else is needed — `SceneBuilder` picks each file up by name and the placeholder
captions remove themselves the moment a sprite loads.

Two checks worth making on your side first, because they are cheap to fix by
regenerating and expensive to fix afterwards:

- **Silhouette.** Squint at the five buildings. Arch, tree, awning-on-posts,
  book-on-lectern and bare rock should be five obviously different black shapes. If two
  read the same, one of them needs redoing.
- **The gate has to win.** Put it beside the shop. If the shop's awning pulls your eye
  first, the awning is too saturated — regenerate it deeper and more shadowed.
