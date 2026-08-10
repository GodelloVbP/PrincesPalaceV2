# Image-generation prompts — Floor 1 battlefields

> **ARCHIVED 2026-08-01: delivered.** The battle backdrops these prompts
> describe are in `Art/Backgrounds/` (`Fight.png` and friends) and wired
> into `FightController`/`SceneBuilder`. Kept as the copy-paste record of
> what was actually asked for.

Four prompts: three battle backdrops and one run-map backdrop. Paste them one at a
time. Same house style as `HUB_ART_PROMPTS.md`; the reasoning for the composition
rules is in the section below, and it is the part that decides whether these work.

---

## Why the current battlefield fights the sprites

The dungeon-hall backdrop in use today is a good painting and a bad backdrop, for
three specific reasons. Every prompt here is built to avoid them.

**1. It occupies the same value range as the characters.** Shawn is a light,
mid-saturation sprite with a clean silhouette. The hall behind him is full of
mid-value warm stone at roughly the same brightness, so his outline has to fight
for separation. A backdrop needs to sit in the **bottom 40% of the value range**
almost everywhere, with the light sources small and far from where anyone stands.

**2. Its detail frequency matches the sprites' detail frequency.** Carved masonry,
chains, brickwork and rubble are all rendered at about the size of a character's
head. The eye cannot tell foreground from background by texture alone. The fix is
to push the midground into **atmospheric haze** and keep rendered detail either
much larger (a tree trunk, an arch) or much smaller (leaf litter) than a sprite.

**3. Its composition points at the middle, where nothing happens.** A one-point
perspective corridor drags the eye to a vanishing point dead centre — which is
empty floor between two combatants. These backdrops want the opposite: quiet in
the centre, interest pushed to the upper outer thirds.

### The four zones every battle prompt protects

Combat UI and sprites sit in fixed places, and the backdrop has to stay out of
all four:

| Zone | What lands there | Requirement |
|---|---|---|
| Upper left, to ~35% width | Turn order strip, HP/MP readout, combat log | Dark and quiet, no busy texture |
| ~20–35% width, lower half | The player's party | Mid-dark, no light source, no hard edges |
| ~65–80% width, lower half | The enemies | Same |
| Lower centre, bottom ~20% | The five action buttons | Darkest part of the frame |

That leaves the **upper centre and the two upper outer corners** to carry the
painting, which is exactly where a forest canopy wants to be anyway.

**Ask for the widest landscape the tool offers** and I will crop to 16:9. These are
full-bleed backdrops, so unlike the hub buildings there is no green field and no
keying — generate them as finished paintings.

Before moving on from each: check there is no text, no border or frame, no figures
or creatures of any kind, and no bright hotspot sitting where a character stands.

---

## 1 — `Forest_Clearing.png` — the ordinary fight

> A small clearing on the floor of an ancient forest, painted in a rich hand-painted
> style with visible brush texture — the painterly look of Hades, not photorealism and
> not vector-clean digital art.
>
> Enormous moss-furred tree trunks rise out of frame on the far left and far right,
> so wide that only two or three of them fit in the whole image. Between them the
> forest recedes into a soft blue-grey haze — no individually rendered trees back
> there, just layered silhouettes losing contrast with distance. A high canopy closes
> most of the sky; a few narrow shafts of pale gold daylight come down through gaps in
> it, well above head height, landing on the mist rather than on the ground.
>
> The floor of the clearing is flat, open and almost featureless: packed earth and
> old leaf litter, painted loosely, with no rocks, roots or plants tall enough to
> break a standing figure's outline. Ferns and undergrowth are pushed to the extreme
> left and right edges only.
>
> Value plan — this is the important part. The whole image sits DARK, in the bottom
> forty percent of the value range. The brightest things in it are the light shafts in
> the UPPER centre. Keep the lower third of the frame the darkest part of the whole
> painting, and keep the two areas about a quarter and three quarters of the way
> across — at standing height — free of any light source, bright highlight or hard-
> edged detail. Figures will be composited there and must read cleanly against it.
>
> Colour: deep desaturated forest greens and cool blue-greys, with the gold light as
> the only warm note. Muted overall — this is a backdrop and nothing in it should
> compete for attention. Soft vignette darkening all four corners.
>
> Wide landscape composition, eye-level camera, horizon low. No characters, no
> creatures, no figures, no text, no border or frame. Widest landscape aspect ratio
> available.

---

## 2 — `Forest_Ruin.png` — the elite fight

> A collapsed stone ruin deep in an old forest, half swallowed by the trees, painted
> in a rich hand-painted style with visible brush texture — the painterly look of
> Hades, not photorealism and not vector-clean digital art.
>
> The remains of a grand arch stand in the upper centre, broken at the top, its
> carved palace stonework furred with moss and split by roots. Fallen columns lie
> at the far left and far right edges, cropped by the frame. Behind the arch the
> forest recedes into cold blue-grey haze. The canopy above is dense and dark.
>
> The ground between the ruins is flat, open and swept almost bare — old flagstones
> gone under moss and earth, painted loosely, with nothing standing on them. No
> rubble, no plants and no fallen masonry anywhere in the middle band of the image at
> standing height; the debris all sits at the outer edges.
>
> Value plan — this is the important part. The whole image sits DARK, in the bottom
> forty percent of the value range. The only real light is a cold, dim shaft falling
> through the broken arch in the UPPER centre, catching the mist. Keep the lower third
> of the frame the darkest part of the painting, and keep the areas about a quarter
> and three quarters of the way across, at standing height, entirely free of light,
> highlight or hard-edged detail.
>
> Colour: grey-green weathered stone, deep moss, cold blue shadow, with a single
> muted warm note in the light shaft. Desaturated and heavy. Soft vignette darkening
> all four corners.
>
> Wide landscape composition, eye-level camera, horizon low. No characters, no
> creatures, no figures, no statues of figures, no text, no border or frame. Widest
> landscape aspect ratio available.

---

## 3 — `Forest_Bog.png` — the boss fight

> A stagnant bog hollow at the heart of a dead stretch of forest, painted in a rich
> hand-painted style with visible brush texture — the painterly look of Hades, not
> photorealism and not vector-clean digital art.
>
> Bare dead trees, thin and black, stand in loose clusters at the left and right
> edges, their upper branches reaching across the top of the frame. Behind them a
> thick low fog erases the far distance entirely. Hanging moss and a few bare vines
> come down from above at the outer edges only.
>
> The floor is a wide, flat, still sheet of dark water broken by low mudbanks — an
> open, uncluttered surface with a soft mirror-like sheen, painted loosely. Nothing
> stands in it. Reeds are pushed to the extreme left and right edges.
>
> Value plan — this is the important part. The whole image sits DARK, in the bottom
> forty percent of the value range, darker overall than a normal forest scene. The
> only light is a scatter of small sickly green-cyan witchlights drifting in the
> UPPER middle distance, plus their broken reflections in the water. Keep those
> witchlights small, few, and away from the areas about a quarter and three quarters
> of the way across at standing height — those two areas must stay dark, quiet and
> free of any hard-edged detail. The bottom fifth of the frame is the darkest part of
> the painting.
>
> Colour: near-black browns, drowned greens, cold slate blue, with the witchlights as
> the only saturated note. Heavy, oppressive, low contrast. Soft vignette darkening
> all four corners.
>
> Wide landscape composition, eye-level camera, horizon low and mostly lost in fog.
> No characters, no creatures, no figures, no text, no border or frame. Widest
> landscape aspect ratio available.

---

## 4 — `Descent_Backdrop.png` — behind the run map

Different job from the three above. The run map draws glowing coloured room tiles
and gold path lines directly on top of this, over a near-black panel. It must read
as atmosphere and **nothing else** — if it has any structure the eye can lock onto,
it competes with the map graph, which is the one thing on that screen that matters.

> A vast ancient forest seen from high above at night, painted in a rich hand-painted
> style with visible brush texture — the painterly look of Hades, not photorealism and
> not vector-clean digital art.
>
> An unbroken dark canopy rolling away into distance, dissolving into layered banks of
> mist. No individual trees are rendered anywhere — only soft masses, tonal shapes and
> fog. No horizon line, no sky, no landmarks, no structures, no paths, no clearings.
>
> Value plan — this is the important part, and it is more extreme than a normal
> painting. The ENTIRE image sits in the bottom fifteen percent of the value range:
> near-black almost everywhere, with only the faintest tonal variation to suggest
> depth. Think of it as a texture rather than a picture. There must be no bright
> area anywhere, no focal point, and no region of higher contrast than any other —
> a map graph will be drawn on top of this and must never have to compete with it.
>
> Colour: near-black with the barest violet and deep teal shift in the mist, warmer
> and very slightly lighter toward the lower centre, cooler and darker at every edge.
> Extremely low contrast, extremely desaturated. Strong vignette pulling all four
> corners to pure black.
>
> Wide landscape composition. No characters, no creatures, no figures, no text, no
> border or frame. Widest landscape aspect ratio available.

---

## After generating

Drop the three battle backdrops in `Assets/_Project/Art/Backgrounds/` and the map
backdrop alongside them. They are referenced by editor-time path and baked into the
scene by `SceneBuilder`, so **the scene has to be rebuilt** before they show up —
see CLAUDE.md rule 1, and gotcha 3 about `Art/` needing to sync back with the scene.

Which backdrop a fight uses is per-room-type, not per-floor, once floor theming
lands: ordinary `Fight` → clearing, `EliteFight` → ruin, `Boss` → bog. Floor 2's
theme becomes a second set of three under the same three room types.
