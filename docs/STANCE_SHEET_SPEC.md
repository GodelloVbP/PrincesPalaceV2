# Commissioning stance stills — the work order

**Who this is for.** A session generating enemy combat art for this project.
It is self-contained: everything needed to produce, check, reject and
deliver a kit is here. You do not need the conversation that produced it.

**What it replaces.** This document used to commission six-frame animation
sheets per stance (idle, attack, ...) and this project no longer ships those
at all — see §0. `docs/ART_PIPELINE.md` still covers keying conventions,
the kit registry, and the sizing/delivery-scale reasoning that survives the
change; where the two disagree, this one wins for anything stance-related.

---

## 0. The fault, in one paragraph

Three animated enemies shipped with six-frame stance sheets and all three
played badly. The frames were **redrawn rather than moved**: mushrooms
changed count between frames, toes appeared and vanished, branch silhouettes
wobbled. Every runtime rule was already satisfied — feet planted to within a
pixel, centre held, ground line correct — and it still looked wrong, because
an image model's default behaviour is to draw a subject afresh each time it
is asked for a frame, which produces six competent illustrations of one
creature and zero frames of actual animation. Directing every frame
individually and pasting a fixed "Detail Inventory" into every prompt
narrowed the problem but never closed it to zero, across three separate
kits and repeated regeneration passes.

**The fix is not a better frame-sheet prompt. It is not shipping frame
sheets at all.** One drawing per pose, posed procedurally by the engine at
runtime, cannot drift between frames because there are no frames to drift
between. This is the policy now, for every combat actor — no skeletal rigs
either; that pipeline (§9 of a previous revision of `docs/ART_PIPELINE.md`)
solved the same redraw problem a different way and is retired for the same
reason a still is simpler than both: nothing to keep consistent across, by
construction.

**Said once more, flatly, because it decides what to deliver: a stance is
ONE DRAWING, and frame animation on an actor is not supported.** This is the
owner's decision, and it is settled — the question is not open and does not
need re-asking per kit. There is no code path in the engine that plays actor
frames, so a frame sheet delivered here is not merely unwelcome, it is
unusable: `Core/StanceAnimationLibrary.cs` resolves a stance to exactly one
sprite and there is nowhere for a second frame to go. (Spell and contact
VFX are frame sequences and always have been — see `docs/ART_PIPELINE.md`
§5b and §5c. This rule is about actors.)

---

## 1. The rule that matters

> **A stance is one drawing. It is posed by the engine, never redrawn.**

Everything else in this document is machinery around that sentence. Six
poses of one creature are commissioned together, on one sheet, so the
character stays recognisably itself across idle/attack/hurt/defeated — the
same reason the old Stage 1 design sheet existed — but each of those six
cells is now a finished deliverable in its own right, not a reference for
something drawn six more times afterward.

One supporting rule, learned the hard way and still true here:

- **Reference the rat, not the most recent sheet.** `Giant_rat_sheet.png` is
  the house style. The treant and beetle kits came back painterly and were
  kept, so "use the most recent example" picks up the wrong style. Never use
  `bog_witch_sheet.png` as a reference — it has baked-in captions, a
  background and floor shadows, all now prohibited.

---

## 2. Work order — where the job currently stands

| actor | Art folder | delivered id | stances | status |
|---|---|---|---|---|
| beetle | `Art/Enemies/beetle/` | `beetle` | idle, attack, turtle_up, shell_closed, hurt, defeated | design sheet needed (regenerate as stills — the delivered kit predates this policy). Protected legacy until then: registered in `hand_assembled.json`, bytes pinned |
| treant | `Art/Enemies/treant/` | `treant` | idle, attack, trunk_slam, cast, hurt, defeated | **delivered as six stills, 2026-09-04** — one Stage-1 pose sheet sliced straight to `Resources/`, no Stage 2. **Reproducible:** `Art/Enemies/treant/recipe.json` replays it byte-identical |
| forest troll | `Art/Enemies/forest_troll/` | `forest_warden` | idle, attack, attack_roar, attack_charge, hurt, defeated | not started. Protected legacy until then — its current stills were recomposited from hand-picked crops, see `Art/Enemies/forest_troll/README.md` |
| owl | `Art/Characters/owl/` | `owl` | idle, attack, cast, hurt, defeated, victory | **delivered 2026-09-04**, the first kit commissioned under this policy. **Reproducible:** `Art/Characters/owl/recipe.json` |
| sheep_black_ram | `Art/Characters/sheep_black_ram/` | `sheep_black_ram` | idle, attack, cast, hurt, defeated, victory | **delivered 2026-09-09** — Shawn's Black Ram transformation form (`black_ram_mode`), not a roster character. **Reproducible:** `Art/Characters/sheep_black_ram/recipe.json` replays it byte-identical |

> **The troll's delivered id is `forest_warden`, not `forest_troll`.** The
> art folder and the content id disagree and always have. Do not rename
> either; just be careful which one you are typing.

Per actor the sequence is now: **one design sheet** (§3) → **Detail
Inventory** (§4) → **slice + deliver** (§6). Update the status column and the
per-actor `README.md` as you go, so a later session can resume without
re-reading anything.

---

## 3. The design sheet

One image per actor, six poses, and this time **everything in it ships** —
there is no separate reference stage and no animation pass after it.

**Attach:** `Assets/_Project/Art/Enemies/Giant_rat_sheet.png` and nothing
else.

**Save as:** `Assets/_Project/Art/Enemies/<actor>/sheet_poses.png`

### Template

```
You are producing a video-game sprite sheet. One image, 1536x1024, a 3x2
grid of six cells (~512x512 each), each cell one distinct pose of the SAME
creature. Leave clear empty gutters between cells.

STYLE - match the attached reference image's RENDERING STYLE ONLY. Do not copy
its subject, its poses, or its grey/pink palette. The style is: bold,
uniform-weight dark outlines around every shape; flat cel shading with exactly
2-3 tones per colour area; a small palette (6-9 colours total); no gradients,
no rim light, no soft airbrushing, no dense micro-texture. Big readable shapes.

SUBJECT - {SUBJECT}

EVERY CELL SHIPS - each of the six poses below is a finished piece of game
art on its own, not a reference for anything drawn later. Give the character
a small fixed set of distinct features ({FEATURES}), each drawn as a clearly
separate object. No scattered texture, no random clusters, no fur or leaf
noise. Every one of these features must appear in EVERY cell in exactly the
same count and the same construction — the six cells are six views of one
character design, not six different characters that happen to look similar.

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
3. curl-up (turtle_up) - rearing back, legs beginning to tuck, body starting to curl.
4. sealed shell (shell_closed) - fully curled into a closed armoured ball, no legs visible.
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
2. overhead slam (attack) - both fists raised together above the head, at the top of the arc.
3. roar (attack_roar) - chest up, head thrown back, mouth wide open, arms flung out.
4. charge (attack_charge) - lunging forward low, both arms reaching to grab.
5. hurt - recoiling, one arm up defensively, face turned away.
6. defeated - collapsed face-down on the ground, arms sprawled.
```

---

## 4. The Detail Inventory

Written **once per actor, by counting the accepted design sheet**, and kept
in `Art/Enemies/<actor>/README.md`. Its job is narrower than it used to be —
there is no Stage 2 prompt to paste it into — but it is still the fastest
way to check Protocol A #1 without re-eyeballing all six cells from scratch,
and it is the record of what "the character" is for anyone regenerating this
sheet later.

Shape:

```
DETAIL INVENTORY (fixed across all six cells of the accepted design sheet):
- exactly 3 mushrooms: one large on the left shoulder, two small on the right hip
- exactly 4 leaf masses: crown, left shoulder, right shoulder, chest
- exactly 3 twig fingers per hand; 2 root toes per leg
- 2 eyes, 1 mouth crack, 1 knothole on the chest
- palette: {list the 6-9 colours actually used}
```

Be specific about **position**, not just count — "3 mushrooms" lets the
model move them between cells; "one large on the left shoulder, two small on
the right hip" does not.

---

## 5. Protocol A — check before saving

Run on every returned sheet, **before** it goes into the repo. Verdict is
ACCEPT or REGENERATE. On reject, send the quoted line back in the same
conversation and ask for that sheet again — regenerating one cell in
isolation loses the whole-sheet consistency the model was holding, so ask
for the sheet again, not a patched cell.

1. **Count the Detail Inventory in all six cells.** Any mismatch →
   *"Cell N has K {feature}; the inventory says exactly M, in the same
   positions, in every cell. Regenerate with the feature counts locked."*
2. **Six clearly distinct poses**, not six near-copies of one pose →
   *"Cells N and M read as the same pose. Make them visibly different:
   {redescribe the two poses}."*
3. **Style** against the rat sheet. Painterly, gradients, rim light,
   micro-texture → *"Too painterly: flatten to 2-3 tones per area with uniform
   dark outlines, matching the style reference."*
4. **Facing** — all six right, none mirrored. (A sheet has come back with 2 of
   6 mirrored before.)
5. **Transparency** — real alpha. If white-backed, either regenerate or
   accept and slice with `--key white_flood` (see §6); the rat is keyed that
   way and it works.
6. **No** text, labels, borders, ground plane, shadows, particles, glow.
7. **Scale and floor** — one creature size, feet on one implied floor height.
8. **Gutters** — each pose fully inside its own cell. Heavy overlap →
   *"Keep each pose fully inside its own cell with clear empty gutters."*

---

## 6. Delivery

1. **Slice.** One command per actor, cutting all six cells onto one shared
   canvas:

   ```bash
   python tools/slice_actor_sheet.py \
       --sheet "Assets/_Project/Art/Enemies/<actor>/sheet_poses.png" \
       --actor Enemies/<id> \
       --stances idle,attack,<ability_pose>,hurt,defeated
   ```

   Stance names are row-major, matching the template's pose order; use `-`
   to skip a cell that has no shipped stance. See the tool's own module
   docstring for `--grid`, `--key`, `--anchor`, `--nudge` and
   `--drop-far-components-px` — the same gutter-bleed and cross-cell anchor
   concerns the old multi-sheet tool had still apply to a single six-cell
   sheet.

2. **Sizing.** Slice once at the default `--delivery-scale 1.0`, measure the
   written `idle.png` alpha bbox height, then re-slice with
   `--delivery-scale target/measured`. Pixel size *is* on-screen size —
   there is no per-enemy scale in content — so skipping this changes how
   big the creature stands. Use **height**, not area: flat cel shading
   changes the area-to-height relationship. Measure the target off the
   *currently delivered* roster, not a number copied from a doc — see
   `docs/ART_PIPELINE.md`'s sizing note for the measurement command and the
   incident that made this rule explicit: a stale hardcoded target in that
   note nearly shrank a correctly-sized enemy by a fifth.
3. **`Resources/StanceManifest.json` — the slicer writes it.** Nothing to
   copy off the terminal. A new actor gets an entry with
   `groundLineSource: "slicer"`; an entry already marked `slicer` is updated
   in place; an entry marked `authored` (which is what an absent field means,
   so every entry predating the field) is **left alone** and the run prints
   the measurement and the delta instead. A no-op run leaves `git diff` empty.
   `breath` and `hover` are never written by any tool — they are judgements
   about the art, not measurements of it. The frame-indexed timing fields
   (`secondsPerFrame`, `impactFrame`, `soundFrame`) that used to live here
   belonged to multi-frame stances and have no meaning for a still.
4. **`Art/<Enemies|Characters>/<actor>/recipe.json` — the slicer writes this
   too** (with its `.meta`, which Unity generates on import). It carries the
   source sheet, the full argv with every default made explicit, the tool's
   sha256, the library versions, the output folder and the measured ground
   line. **Verify it before calling the kit delivered:**

   ```bash
   python tools/slice_actor_sheet.py --recipe Assets/_Project/Art/Enemies/<actor>/recipe.json
   git status --short Assets/_Project/Resources/
   ```

   Clean means the recipe reproduces the committed art byte for byte, which
   is what puts the actor in the *reproducible* category. Not clean means it
   does not, and the actor belongs in `Art/Sheets/hand_assembled.json`'s
   `actors` block with its bytes pinned in `HandAssembledArtTests` instead —
   claiming a recipe that does not replay is worse than claiming none.
5. **`Art/<Enemies|Characters>/<actor>/README.md`** (with its `.meta`)
   recording the accepted design-sheet prompt, the Detail Inventory verbatim,
   the model and date, `delivery_scale`, the accepted metrics, and a
   **provenance line** pointing at `recipe.json` or at the register. The
   README is where the REASONING lives now; the argv lives in the recipe. If
   the ground line is an authored override more than 8px from what the art
   measures, this is also where the reason goes, in a line naming
   `groundLine` — `StanceManifestValidationTests` greps for exactly that and
   fails naming the actor if it is not there.
6. **Delete the superseded files** with their `.meta`s — old
   `<stance>/f0..fN/` frame folders, `NN_<stance>.png` singles,
   `_*sheet_source*.png`, `_contact_sheet_preview.png`, `_base_poses/`. Git
   history is the archive, and leaving off-style sheets or stale frame
   folders around is how the wrong style (or the wrong pipeline) gets
   referenced next time.
7. `powershell -NoProfile -ExecutionPolicy Bypass -File tools/run_tests_parallel.ps1 -BuildContent`
8. Commit assets **and** their `.meta` files together, staged by explicit
   path. Never `git add -A`.
9. Visual QA before calling it done:
   ```bash
   python tools/actor_stance_qa.py --report Assets/_Project/Resources/Enemies --only <id>
   ```
   Open `tools/screenshots/actor_qa/<id>.png` — the ground line (red),
   per-stance centroid tick (cyan) and `sqrt(area)/median` caption catch
   scale drift and floating feet between poses, which still matters even
   though there is no more churn/redraw ratio to read.

### Provenance rule

> **No still reaches `Resources/Enemies/` or `Resources/Characters/` unless
> its source design sheet is committed under `Art/` and a `recipe.json`
> beside that sheet replays the exact invocation that produced it.**

The recipe is the record now, and the README is where the reasoning around it
goes. `HandAssembledArtTests.EveryDeliveredActorIsInExactlyOneCategory`
enforces the rule from the other end: every folder under
`Resources/Enemies/` and `Resources/Characters/` has either a `recipe.json`
or an entry in `Art/Sheets/hand_assembled.json`'s `actors` block, never both
and never neither. The six actors delivered before recipes existed are in the
register, each saying what is actually known about where its stills came
from; Shawn is the case that shows why the register has to exist at all, since
no design sheet for him is committed anywhere.

The old multi-sheet `ACTORS` manifest served this purpose when the tool
processed several committed sheets per actor; the CLI that replaced it took
its arguments on the command line instead, which made the README the only
record and made losing it lose the recipe. That lasted until somebody had to
replay one: the treant's README said `delivery_scale 1.05`, `white_flood` and
"a raised `pocket_max_area`", and getting the committed bytes back meant
guessing the raised value and testing candidates. Prose is right about
everything a person needs and one number short of what a machine needs, which
is why both exist now.

---

## 7. Done, per actor

- One design sheet committed under `Art/Enemies/<actor>/sheet_poses.png`,
  six stances delivered to `Resources/Enemies/<id>/<stance>.png` on one
  shared canvas.
- `README.md` written with the prompt, the Detail Inventory, the accepted
  metrics and the exact slicer command.
- `StanceManifest.json` has a `groundLine` entry (and `breath` if the idle
  needs a nonstandard sway) and no leftover frame-timing fields for this
  actor.
- Old frame-sequence files deleted, full suite green.
- The work-order table in §2 updated.
