# Commissioning stance sheets — the work order

**Who this is for.** A session generating enemy animation sheets for this
project. It is self-contained: everything needed to produce, check, reject and
deliver a kit is here. You do not need the conversation that produced it.

**What it replaces.** `docs/ART_PIPELINE.md` §4b described how the first kits
were commissioned. It is still true and still worth reading for the slicing and
sizing reasoning, but it predates the measurement that matters and is missing
the one instruction that fixes the fault this document exists for. Where the two
disagree, this one wins.

---

## 0. The fault, in one paragraph

Three animated enemies shipped and all three play badly. The frames are
**redrawn rather than moved**: mushrooms change count between frames, toes
appear and vanish, branch silhouettes wobble. Every runtime rule was already
satisfied — feet planted to within a pixel, centre held, ground line correct —
and it still looked wrong, because the problem is not where the figure is, it is
that the *drawing* changes more than the *pose* does.

Measured as a **redraw ratio**: silhouette churn per step divided by how far the
figure's centre of mass travels. About 1 means the drawing changes about as much
as the pose does. A real lunge (the rat's attack) scores 0.7. A genuinely still
pose (the beetle's sealed shell) scores 1.1. **Every six-frame idle in the game
scores near 3.**

The cause is a missing instruction. The generator was never told to keep the
drawing identical between frames, and the reviewer had no number to reject on.
Both are fixed below: §1 is the instruction, §5–6 are the checks.

---

## 1. The rule that matters

> **A frame is the previous frame, posed. Not the same subject, drawn again.**

Everything else in this document is machinery around that sentence. An image
model's default behaviour is to draw the subject afresh each time, which
produces six competent illustrations of one creature and zero frames of
animation. The counter is to name every countable feature up front, fix its
count, and tell the model those features are physical objects that move with the
body part they sit on.

Two supporting rules, both learned the hard way and both recorded in §4b:

- **Direct each frame individually.** Describing the *motion* ("it breathes")
  produces six near-identical copies. Describing each *frame* ("FRAME 4: full
  coil, deepest crouch, horn aimed") forces six distinct drawings. Measured on
  the nymph's idle: 12% → 27% inter-frame change from the rewrite alone.
- **Reference the rat, not the most recent sheet.** `Giant_rat_sheet.png` is the
  house style. The treant and beetle kits came back painterly and were kept, so
  "use the most recent example" picks up the wrong style. Never use
  `bog_witch_sheet.png` as a reference — it has baked-in captions, a background
  and floor shadows, all now prohibited.

---

## 2. Work order — where the job currently stands

Three kits, six stances each. Beetle first: it is the cheapest kit to burn a
round on while the prompts are still being proven. Feed anything learned back
into this document before starting the next actor.

| actor | Art folder | delivered id | stances | status |
|---|---|---|---|---|
| beetle | `Art/Enemies/beetle/` | `beetle` | idle, attack, turtle_up, shell_closed, hurt, defeated | **delivered** (idle over its redraw/travel bars after 3 attempts, see its README) |
| treant | `Art/Enemies/treant/` | `treant` | idle, attack, trunk_slam, cast, hurt, defeated | not started |
| forest troll | `Art/Enemies/forest_troll/` | `forest_warden` | idle, attack, attack_roar, attack_charge, hurt, defeated | not started |
| rat | sheets in `Art/Enemies/`, record in `Art/Enemies/rat/` | `rat` | idle, attack (12 frames each) | **idle needs regeneration** — fails Protocol A #9 and Protocol B travel; prompt written, see its README |

> **The rat was not part of this work order and now is.** It is the project's
> style reference, so it was never queued for regeneration — but 12-frame idle
> and attack sheets were commissioned for it (2026-08-29/30) *without* running
> this document, and the idle came back with exactly the fault §0 describes.
> Its README carries the measurements, the Detail Inventory and a 12-frame
> restatement of §5a's idle direction.
>
> Two things learned there are worth applying to the three actors above before
> their sheets are commissioned:
>
> - **A 12-frame sheet does not change any rule here, only the arithmetic.**
>   §5a's "about 4% above frame 1" is a total for the whole sheet, so at 12
>   frames each step is ~0.4%, not ~0.8%. Say the per-frame increment *and*
>   the total in the prompt; a sheet directed only by per-frame increments
>   drifted to 11.4% total.
> - **State the gutter rule in the frame directions, not only in RULES.**
>   Protocol A #9 has been failed by every 12-frame sheet commissioned so far,
>   on a prompt that did carry the standard RULES block. A figure whose tail
>   crosses the cut line gets that tail severed by the slicer, and the piece
>   reappears in the neighbouring cell as a stray.

Per actor the sequence is: **Stage 1** (one design sheet) → **inventory** →
**Stage 2** (six animation sheets, one at a time) → **delivery**. Update the
status column and the per-actor `README.md` as you go, so a later session can
resume without re-reading anything.

> The troll's delivered id is `forest_warden`, not `forest_troll`. The art folder
> and the content id disagree and always have. Do not rename either; just be
> careful which one you are typing.

---

## 3. Stage 1 — the design sheet

One image per actor, six poses, whose only job is to lock the character design
and prove the feature counts are holdable. Nothing from Stage 1 ships.

**Attach:** `Assets/_Project/Art/Enemies/Giant_rat_sheet.png` and nothing else.

**Save as:** `Assets/_Project/Art/Enemies/<actor>/sheet_poses.png`

### Template

```
You are producing a video-game sprite sheet. One image, 1536x1024, a 3x2 grid
of six cells (~512x512 each), each cell one distinct pose of the SAME creature.
Leave clear empty gutters between cells.

STYLE - match the attached reference image's RENDERING STYLE ONLY. Do not copy
its subject, its poses, or its grey/pink palette. The style is: bold,
uniform-weight dark outlines around every shape; flat cel shading with exactly
2-3 tones per colour area; a small palette (6-9 colours total); no gradients,
no rim light, no soft airbrushing, no dense micro-texture. Big readable shapes.

SUBJECT - {SUBJECT}

DESIGN FOR ANIMATION - this character will be animated frame by frame, so its
surface details must be COUNTABLE and FEW. Give it a small fixed set of
distinct features ({FEATURES}), each drawn as a clearly separate object. No
scattered texture, no random clusters, no fur or leaf noise: every one of these
features will have to be redrawn identically in 36 animation frames, so draw
only what you can keep.

THE SIX POSES, row-major from top-left:
{POSES}

RULES:
- Transparent background (real alpha, not white).
- The creature FACES RIGHT in every cell. Never mirrored.
- Identical scale in every cell; feet on a consistent implied floor height.
- No text, no labels, no numbers, no borders or frames around cells.
- No ground plane, no floor shadow, no cast shadow.
- No particles, dust, glow, or magic effects of any kind.
```

### beetle

```
SUBJECT - a giant armoured beetle, low and wide, the "Ironback Beetle": dark
iron-brown carapace in large plates, one prominent forward-curving nose horn,
six segmented legs, two short antennae, small dark eye. Menacing but simple.

FEATURES - 3 carapace plates, 6 legs, 1 horn, 2 antennae

THE SIX POSES:
1. idle - standing neutral on all six legs, weight settled.
2. attack - lunging forward, horn thrust ahead, front legs off the ground.
3. curl-up - rearing back, legs beginning to tuck, body starting to curl.
4. sealed shell - fully curled into a closed armoured ball, no legs visible.
5. hurt - recoiling, legs splayed, head turned away from a hit from the right.
6. defeated - flipped onto its back, legs curled inward, still.
```

### treant

```
SUBJECT - an elder treant, a walking tree: thick bark trunk for a torso, two
long branch arms with clawed twig fingers, root-cluster legs, a face formed in
the bark (two glowing eyes, a mouth crack), a leaf canopy over head and
shoulders drawn as a few solid leaf masses (NOT individual leaves), and a small
fixed number of mushrooms growing on the bark. Ancient, heavy, slow.

FEATURES - 4 solid leaf masses, 3 mushrooms, 2 branch arms, 2 root legs,
3 twig fingers per hand

THE SIX POSES:
1. idle - standing tall and settled, arms low.
2. attack - a raking sideways swipe with the right branch arm, at full extension.
3. trunk slam - both arms raised overhead together at the top of a slam.
4. cast - arms spread wide, chest open, head tilted up mid-invocation.
5. hurt - flinching back, canopy tossed, one arm shielding.
6. defeated - collapsed onto its roots, canopy drooped forward, arms down.
```

### forest troll

```
SUBJECT - a hulking forest troll boss: gorilla-like build, walks on its
knuckles, massive arms, mossy green-grey hide in flat tones, a shaggy moss mane
around the head and shoulders drawn as SOLID SHAPES rather than strands, tusked
underbite, pointed ears, a small fixed number of mushrooms on its back, a simple
loincloth. Brutish, enormous, ancient.

FEATURES - 3 mushrooms on the back, 2 tusks, 2 pointed ears, one solid moss-mane
mass, 4 knuckles per hand

THE SIX POSES:
1. idle - hunched on its knuckles, head low and forward.
2. overhead slam - both fists raised together above the head, at the top of the arc.
3. roar - chest up, head thrown back, mouth wide open, arms flung out.
4. charge - lunging forward low, both arms reaching to grab.
5. hurt - recoiling, one arm up defensively, face turned away.
6. defeated - collapsed face-down on the ground, arms sprawled.
```

### Stage 1 acceptance

Reject and regenerate if any of these fail. **Do not proceed to Stage 2 on a
sheet that fails — an inconsistent design sheet poisons all six animation
sheets downstream.**

1. **Feature counts already match across all six cells.** Count them. If pose 1
   has 3 mushrooms and pose 4 has 5, regenerate now.
2. Style is flat cel against the rat sheet — uniform dark outlines, 2–3 tones
   per area. Painterly, gradient-shaded or rim-lit → regenerate.
3. All six face right. None mirrored.
4. One consistent creature scale across cells.
5. Real alpha transparency, not white.
6. No text, borders, ground plane, shadows, particles.
7. Six clearly distinct poses.

---

## 4. The Detail Inventory

Written **once per actor, by counting the accepted Stage-1 art**, and pasted
verbatim into all six Stage-2 prompts. This is the instruction that was missing
and the single highest-value part of this document.

Record it in `Art/Enemies/<actor>/README.md` so later sessions and later stances
use exactly the same text.

Shape:

```
DETAIL INVENTORY (fixed for every frame of every sheet):
- exactly 3 mushrooms: one large on the left shoulder, two small on the right hip
- exactly 4 leaf masses: crown, left shoulder, right shoulder, chest
- exactly 3 twig fingers per hand; 2 root toes per leg
- 2 eyes, 1 mouth crack, 1 knothole on the chest
- palette: {list the 6-9 colours actually used}
```

Be specific about **position**, not just count — "3 mushrooms" lets the model
move them; "one large on the left shoulder, two small on the right hip" does
not.

Then cut the six single poses out of the design sheet to
`Art/Enemies/<actor>/pose_<stance>.png`. These are Stage-2 reference
attachments, not deliverables. Use `tools/sheet_slicing.py`'s `best_cut` —
**do not hand-roll a slicer and do not cut on even thirds.** The generator does
not lay figures on an exact grid; even-thirds slicing has already amputated
three frames' legs on a real sheet.

---

## 5. Stage 2 — the animation sheets

One sheet per stance, six per actor, **one at a time** — check and slice each
before commissioning the next, so a systematic fault costs one sheet and not six.

**Attach:** the stance's `pose_<stance>.png` (design + pose reference) **and**
`Giant_rat_sheet.png` (style reference).

**Save as:** `Assets/_Project/Art/Enemies/<actor>/sheet_<stance>.png`

### Template

```
Same creature as the attached pose reference. Produce ONE animation sheet,
1536x1024, a 3x2 grid of six frames (~512x512 cells) with clear empty gutters,
animating the attached pose. Row-major order: top row = frames 1-3, bottom row
= frames 4-6.

THIS IS ANIMATION, NOT SIX ILLUSTRATIONS. Imagine frame 1 was drawn once, and
then the drawing was physically POSED for each later frame, like a puppet.
Between any two adjacent frames, every part of the body that is not moving must
have an IDENTICAL outline and identical interior detail - copied, not redrawn.
Only the moving parts change, and they move as rigid connected pieces.

DETAIL INVENTORY - these features exist in EXACTLY these counts and positions in
EVERY frame. They are solid objects attached to the body: they move with the
body part they sit on, and they NEVER appear, disappear, multiply, change count,
change size, or change shape between frames:
{PASTE THE ACTOR'S DETAIL INVENTORY}

STYLE - identical to both attached references: bold uniform dark outlines, flat
cel shading, 2-3 tones per colour area, no gradients, no texture noise. Use the
same palette in every frame.

THE SIX FRAMES:
{FRAMES}

RULES:
- Transparent background (real alpha, not white).
- The creature FACES RIGHT in every frame. Never mirrored.
- Identical scale in every frame; the feet stay planted on one implied floor
  line at the same height in every cell, except where a frame direction below
  explicitly says the body leaves the ground.
- Planted feet and hands are PINNED: same outline, same toe/claw count, same
  position in the cell, traced identical frame to frame.
- No text, labels, numbers or borders; no ground plane or shadows; no
  particles, dust, glow, or magic effects.
```

### 5a. Idle — the same direction for all three actors

Idles are played **ping-pong** by the runtime (`Domain/Stage/LoopCycle.cs`):
frames run 1→6 then 6→1, eased with a cosine so the turns at each end are
smooth. That means the sheet is **half a breath**, not a whole one, and frame 6
must be the peak — a sheet that returns to rest at frame 6 produces a double
bounce.

```
THE SIX FRAMES - this is one slow breath IN, and nothing else:
FRAME 1: the resting pose, exactly as the attached reference. Fully exhaled:
  torso at its lowest.
FRAME 2: the same drawing, inhaling slightly - torso and head raised about 1%
  of body height. Nothing else changes at all.
FRAME 3: inhale continuing - torso and head up about 2% of body height,
  shoulders lifted with them as one piece.
FRAME 4: nearly full inhale - up about 3%, chest slightly expanded.
FRAME 5: full inhale - torso, head and shoulders at their highest, about 4% of
  body height above frame 1. This is frame 1's silhouette, moved upward.
FRAME 6: the peak, held - identical to frame 5 with the head tilted up a
  fraction.

This sheet is played back and forth (1-6 then 6-1), so it must NOT return to
the resting pose at frame 6. The rise is monotonic: every frame is higher than
the one before it.

The motion is SMALL and the drawing is otherwise an EXACT COPY frame to frame.
If you find yourself redrawing the legs, the feet or the head detail, stop -
they do not move in this animation.
```

### 5b. One-shots

Each has an **impact frame** already authored in
`Resources/StanceManifest.json`, where the sound and the hit VFX land. The
directions below put the biggest pose on exactly that frame, so nothing in
content needs re-authoring. **Frame numbers here are 1-based and match the
manifest; the delivered files are `f0..f5`, so FRAME 4 is `f3`.**

| actor | stance | impact frame |
|---|---|---|
| beetle | attack | 3 |
| beetle | turtle_up | 5 |
| treant | attack | 4 |
| treant | trunk_slam | 3 |
| treant | cast | 4 |
| forest_warden | attack | 5 |
| forest_warden | attack_roar | 4 |
| forest_warden | attack_charge | 5 |

`hurt`, `defeated` and `shell_closed` have no impact (frame 1) and are directed
as arcs.

**beetle / attack** — impact FRAME 3
```
FRAME 1: neutral stance, head beginning to dip.
FRAME 2: wind-up - reared back, weight on the hind legs, horn lowered and aimed.
FRAME 3: THE IMPACT - full-extension lunge to the right, horn thrust forward,
  front legs off the ground. The biggest, longest pose on the sheet.
FRAME 4: follow-through, momentum still carrying forward, front legs coming down.
FRAME 5: landed, body settling, head still low.
FRAME 6: recovered to the neutral stance.
```

**beetle / turtle_up** (the Barrel Roll attack) — impact FRAME 5
```
FRAME 1: neutral stance, legs beginning to tuck.
FRAME 2: body curling, head withdrawing, legs folding in.
FRAME 3: fully curled into an armoured ball, still in place.
FRAME 4: the same ball, rotated and beginning to roll to the right.
FRAME 5: THE IMPACT - the ball at its furthest right, rotated further,
  slamming forward. The largest displacement on the sheet.
FRAME 6: the ball beginning to uncurl, legs emerging.
Frames 3 to 6 are the SAME BALL DRAWING rotated and moved - do not redraw it.
```

**beetle / shell_closed** (Shell Up; plays once then holds the last frame)
```
FRAME 1: neutral stance, legs beginning to bend.
FRAME 2: body lowering, legs tucking under.
FRAME 3: half-sealed, head withdrawing into the carapace.
FRAME 4: nearly sealed, only the tips of the legs visible.
FRAME 5: fully sealed armoured ball, settled.
FRAME 6: identical to frame 5, settled about 1% lower. Frames 5 and 6 are
  nearly the same drawing - this stance ENDS still.
```

**beetle / hurt**
```
FRAME 1: the neutral stance, clipped by a hit from the right.
FRAME 2: head snapped away to the left, legs bracing.
FRAME 3: maximum recoil - body pushed left, legs skidding.
FRAME 4: the recoil easing, weight coming back.
FRAME 5: regaining the stance.
FRAME 6: back to neutral.
```

**beetle / defeated**
```
FRAME 1: struck, stance broken.
FRAME 2: legs buckling, body sagging.
FRAME 3: tipping over sideways.
FRAME 4: mid-flip, body rotating onto its back.
FRAME 5: landed on its back, legs in the air.
FRAME 6: legs curled inward, completely still.
```

**treant / attack** — impact FRAME 4
```
FRAME 1: neutral, right branch arm beginning to draw back.
FRAME 2: arm cocked back and up, torso twisting left.
FRAME 3: arm fully cocked behind, deepest twist, weight loaded.
FRAME 4: THE IMPACT - full-extension rake to the right, twig claws spread wide.
  The biggest silhouette on the sheet.
FRAME 5: follow-through, arm past the body, torso untwisting.
FRAME 6: settling back to neutral.
```

**treant / trunk_slam** — impact FRAME 3
```
FRAME 1: neutral, both arms rising.
FRAME 2: both arms high overhead, stretched up on its roots - the tallest frame.
FRAME 3: THE IMPACT - both arms driven down together into the ground, torso
  pitched forward, deepest crouch. The heaviest pose on the sheet.
FRAME 4: arms on the ground, body compressed, canopy still settling.
FRAME 5: beginning to rise, arms lifting.
FRAME 6: back to neutral.
```

**treant / cast** — impact FRAME 4
```
FRAME 1: neutral, arms lifting.
FRAME 2: arms spreading wide, head beginning to tilt up.
FRAME 3: chest open, arms wide, canopy lifted.
FRAME 4: THE IMPACT - a sharp pulse: chest thrust forward, head thrown back,
  arms at their widest. The most open pose on the sheet.
FRAME 5: easing, arms beginning to lower.
FRAME 6: back to neutral.
No spores, no particles, no glow - the spell effect is composited in-engine.
```

**treant / hurt**
```
FRAME 1: neutral, clipped by a hit from the right.
FRAME 2: canopy tossed, torso rocked left.
FRAME 3: maximum flinch, one arm raised shielding, canopy at its most disturbed.
FRAME 4: easing back.
FRAME 5: straightening.
FRAME 6: neutral.
```

**treant / defeated**
```
FRAME 1: struck, torso sagging.
FRAME 2: roots buckling, weight dropping.
FRAME 3: dropping to its roots, canopy pitching forward.
FRAME 4: down, arms trailing on the ground.
FRAME 5: slumped fully forward.
FRAME 6: still, canopy fully drooped, arms limp.
```

**forest troll / attack** (the overhead slam) — impact FRAME 5
```
FRAME 1: hunched neutral on the knuckles.
FRAME 2: rising off the knuckles, fists beginning to lift.
FRAME 3: upright, both fists raised to chest height.
FRAME 4: fully stretched, both fists together above the head, chest open -
  the tallest frame on the sheet.
FRAME 5: THE IMPACT - both fists smashed down into the floor, body at its
  lowest and widest. The heaviest pose on the sheet.
FRAME 6: fists on the ground, body beginning to rise back toward the knuckles.
```

**forest troll / attack_roar** — impact FRAME 4
```
FRAME 1: hunched neutral, head beginning to lift.
FRAME 2: chest rising, mouth opening, arms starting to spread.
FRAME 3: rearing up, arms spreading wider, mouth half open.
FRAME 4: THE IMPACT - the peak of the roar: chest at its most expanded, head
  thrown fully back, mouth wide open, arms flung out to both sides. The
  biggest pose on the sheet.
FRAME 5: the roar sustaining, arms beginning to come down, mouth still open.
FRAME 6: settling back down toward the knuckles, mouth closing.
```

**forest troll / attack_charge** (the grapple) — impact FRAME 5
```
FRAME 1: neutral, weight shifting back.
FRAME 2: coiled on the haunches, arms drawn in.
FRAME 3: deepest coil, weight fully loaded, eyes forward.
FRAME 4: launching - body low and stretching to the right.
FRAME 5: THE IMPACT - full-extension grab at the furthest right, both arms
  reaching, hands open. The longest silhouette on the sheet.
FRAME 6: arms closing, body beginning to recover.
```

**forest troll / hurt**
```
FRAME 1: hunched neutral, clipped by a hit from the right.
FRAME 2: head rocked away to the left, one arm rising.
FRAME 3: maximum recoil, arm shielding the face, weight back.
FRAME 4: easing.
FRAME 5: re-planting the knuckles.
FRAME 6: back to the neutral hunch.
```

**forest troll / defeated**
```
FRAME 1: struck, arms dropping.
FRAME 2: sagging forward, knees giving.
FRAME 3: knees hitting the ground.
FRAME 4: pitching face-forward.
FRAME 5: landed, arms sprawling out.
FRAME 6: flat, still, arms limp.
```

---

## 6. Protocol A — check before saving

Run on every returned sheet, **before** it goes into the repo. Verdict is
ACCEPT or REGENERATE. On reject, send the quoted line back in the same
conversation and ask for that sheet again.

1. **Count the Detail Inventory in all six cells.** Any mismatch →
   *"Frame N has K {feature}; the inventory says exactly M, in the same
   positions, in every frame. Regenerate with the feature counts locked."*
2. **Onion-skin read.** Compare adjacent frames: do the outlines of parts that
   are NOT moving trace identically? Boiling outlines on a planted foot →
   *"The {part} is planted in every frame but its outline is redrawn
   differently each time. Trace it identically; only the moving parts change."*
3. **Six near-identical copies** (the opposite failure) → re-send the per-frame
   directions and say the frames must be visibly different poses.
4. **Style** against the rat sheet. Painterly, gradients, rim light,
   micro-texture → *"Too painterly: flatten to 2-3 tones per area with uniform
   dark outlines, matching the style reference."*
5. **Facing** — all six right, none mirrored. (A sheet has come back with 2 of 6
   mirrored before.)
6. **Transparency** — real alpha. If white-backed, either regenerate or accept
   and set that sheet's `key` to `"white_flood"` in the slicer manifest; the rat
   is keyed that way and it works.
7. **No** text, labels, borders, ground plane, shadows, particles, glow.
8. **Scale and floor** — one creature size, feet on one implied floor height.
9. **Gutters** — each figure fully inside its own cell. Heavy overlap →
   *"Keep each frame fully inside its own cell with clear empty gutters."*
10. **The impact frame** is visibly the extreme of the sheet (one-shots only).

---

## 7. Protocol B — the numbers

After the sheet is saved, slice and measure. This is the check that catches what
the eye misses — the troll's idle passed an eyeball review and still scored 3.3.

```powershell
py tools/slice_actor_sheet.py <actor_id>
py tools/actor_stance_qa.py --report "Assets/_Project/Resources/Enemies" --only <actor_id>
```

Read the ranked table and the contact sheet at
`tools/screenshots/actor_qa/<actor_id>.png` (the rightmost cell of each row is
the onion skin).

| metric | idle (loops) | one-shots |
|---|---|---|
| **redraw ratio** | target < 1.6, **hard reject at 2.5** | target < 2.5, **hard reject at 4.0** |
| **feet-band churn** | target ≤ 8%, reject > 12% | not applicable — feet legitimately move |
| **travel** | 2–6% of height; reject < 1.5% (static) or > 8% (wandering) | reject if churn < 15%/step AND travel < 4% (came back static) |
| **draw scale** | `sqrt(area)/median` caption green (0.90–1.10) | same; amber acceptable only on `shell_closed` and `defeated`, which legitimately change mass |

Calibration, so the numbers mean something: the rat's attack is **0.7**, the
beetle's sealed shell is **1.1**, and every current six-frame idle is near **3**.

**If a sheet is clean by eye and by onion skin but lands just outside the travel
band with a good ratio, trust the ratio.** The bands are target-versus-reject
for exactly that reason.

**On reject:** `git restore` the sliced output (nothing was committed yet), then
regenerate that one stance quoting the measurement in plain language —

> *"The frames change {churn}% of the silhouette per step while the body only
> moves {travel}% — the art is being redrawn rather than animated. Keep the
> drawing identical except for the parts that move. Most of the change is in
> the {band}: keep the {feet/head} traced identical, same count, same
> position."*

If the six sheets disagree about creature size, the slicer's 10% guard will
refuse to write. That is the designed workflow, not a fault: run
`py tools/slice_actor_sheet.py --suggest-scales <actor_id>` and copy the printed
per-sheet `scale` values into the manifest.

---

## 8. Delivery

1. **Sizing.** Slice once at `delivery_scale: 1.0`, measure the new idle `f0`
   content height (alpha > 16 bounding box), then set
   `delivery_scale = target / measured` and slice again. Targets, measured from
   the current delivered art: **beetle 243, treant 441, forest_warden 473**.
   Pixel size *is* on-screen size — there is no per-enemy scale in content — so
   skipping this changes how big the creature stands. Use **height**, not area:
   flat cel shading changes the area-to-height relationship.
2. **`Resources/StanceManifest.json`.** Set `groundLine` from the value the
   slicer prints. For `forest_warden` delete `breath: 0.7`, `_breathNote` and
   `_groundLineNote` — they describe the old broken kit, and a sheet that
   genuinely moves takes the 0.35 default. For `beetle` delete the idle's
   `"loop": "forward"` — these idles are ping-pong half-breaths.
   Leave `secondsPerFrame`, `impactFrame` and `soundFrame` alone: §5b was
   written to the values already there.
3. **`Art/Enemies/<actor>/README.md`** (with its `.meta`) recording the accepted
   Stage-1 prompt, all six Stage-2 prompts, the Detail Inventory verbatim, the
   model and date, the accepted metrics per stance, `delivery_scale` and
   `groundLine`. This is what makes the next kit cheap and the next session
   possible.
4. **Delete the superseded files** with their `.meta`s — the old
   `<stance>_frames/` folders, `NN_<stance>.png` singles, `_*sheet_source*.png`,
   `_contact_sheet_preview.png`, `_base_poses/`. Git history is the archive, and
   leaving off-style sheets around is how the wrong style gets referenced next
   time.
5. `powershell -NoProfile -ExecutionPolicy Bypass -File tools/run_tests_parallel.ps1 -BuildContent`
6. Commit assets **and** their `.meta` files together, staged by explicit path.
   Never `git add -A`.

### Provenance rule

> **No frame reaches `Resources/Enemies/` unless its source sheet is committed
> under `Art/` and its actor has an entry in `slice_actor_sheet.py`.**

The beetle's idle has no committed source sheet and the troll has no per-stance
sheets at all, because both were delivered by a process that survives nowhere.
That is why the troll shipped with 35 different frame canvases and no test
noticed. This rule closes that hole; do not deliver around it.

---

## 9. Done, per actor

- Six sheets committed under `Art/Enemies/<actor>/`, six stances delivered to
  `Resources/Enemies/<id>/<stance>/f0..f5.png`.
- Every stance inside its Protocol B bands; idle under 1.6 if at all possible.
- `README.md` written with prompts, inventory and accepted metrics.
- Manifest updated, old files deleted, full suite green.
- The work-order table in §2 updated.

When all three actors are done, the redraw gate gets switched on permanently —
`--fail-bars` wired into `run_tests_parallel.ps1`, so no future kit can ship
over its bar. Until then it stays off, because every current idle would fail it.
