# Art Pipeline

How generated art gets from a raw AI output to a game-ready sprite in this
project, and where each kit's pieces actually live.

## 1. Kit registry

| Kit | Source | Keying | Output | Status |
|---|---|---|---|---|
| Hub buildings | `Art/UI/Hub/` | grouped, green `#00FF00` | `Resources/Hub/<building>/f0.png..` | delivered |
| Relic icons | `Art/Items/Relics/` | direct, mostly real alpha delivered (two files, and so far only two, arrived green `#08D111`-ish and needed the keyer) | `Art/Items/Relics/Processed/` | delivered (36/38) — `dancers_anklet` and `loaded_dice` still on placeholder, see the Relics README |
| Talent Tree kit | `Art/UI/TalentTree/` | direct, green `#00FF00` | `Art/UI/TalentTree/Processed/` | delivered, not yet wired (gated on Phase 7 design confirmation — see `docs/handoffs/talent_tree/`) |
| Portraits | `Art/Portraits/<Character>/` | flood-fill from border (`tools/remove_portrait_backgrounds.py`, `PORTRAITS` manifest) | `Art/Portraits/<Character>/Processed/`, then the one the dossier uses is moved to `Resources/Portraits/<characterId>.png` and named by `portraitPath` | ongoing, per-character — **Sheep does not reproduce, see below** |
| Actor stance sheets | one design sheet per actor | grid slice (`slice_actor_sheet.py`) | one still `Resources/<Enemies\|Characters>/<id>/<stance>.png` per pose, shared canvas (see §4) | ongoing — `docs/STANCE_SHEET_SPEC.md` is the commissioning work order |
| Item/spell sheets | raw sheet per type | grid slice (`slice_item_sheet.py` / `slice_spell_sheet.py`) | one PNG per frame/level, or `{id}/f0..fN` for a spell effect (see §5b) | ongoing |
| Backgrounds | `Art/Backgrounds/` | none — full-frame opaque | same folder | ongoing, one outstanding: `Relics.png` |
| Six-theme UI kit | `Art/UI/Buttons/` sheets (button plates legacy/3x1/5x1, row 6x1, containers 3x4/9x16/3x2/2x1/**5x1**, flag banners 3x4/9x16), one set per theme (Gold/Crimson/Violet/Blue/Green/Silver) | grid slice (`tools/splice_ui_kit.py`). **Since the 2026-09-07 regeneration the delivered PNGs are at TRUE NOMINAL ASPECT** — every file of every group is one exact size on its filename's fraction, where earlier deliveries were spliced several percent off it and the seam carried the measured number instead. Re-measure with `py tools/measure_ui_kit.py` and `measure_inset` in `splice_ui_kit.py`; see `ButtonPlateArt`/`ContainerArt`'s own headers for the per-shape numbers and the four edges whose six themes still disagree | `Art/UI/Buttons/Processed/` (and `Art/UI/FightButtons/Processed/`), import settings (CompressedHQ/BC7, mipmapped, Trilinear) forced by `UiKitImportPostprocessor`, one-shot reimport via `Prince's Palace/Reimport UI Kit` | delivered |
| Per-PC fight-HUD plates | `Art/UI/Plates/Source/<name>.png` (one wide leather strip per playable character, their head EMBOSSED at the right end) | none — delivered with real alpha | `Resources/Plates/pc_<characterId>.png`, plus the shared acting-highlight decal `pc_plate_glow.png`. `tools/normalize_pc_plates.py` brings all three to ONE canvas (2048x365) and ONE aspect (5.6111) — heights matched, surplus field width removed with a crossfade seam, nothing scaled non-uniformly and no head touched — and writes `Art/UI/Plates/recipe.json`, which `--recipe` replays byte-identical. Import settings (CompressedHQ/BC7, mipmapped, Trilinear) forced by `UiKitImportPostprocessor`, which owns this folder alongside the six-theme kit. Measured shape in `Domain/UiKit/PcPlateArt.cs`, re-measured against the PNGs by `PcPlateArtTests` | delivered (3/3 — Shawn, Bjorn, Odette). WHICH plate a character wears is CONTENT (`characters.json`'s `plateArt`, Resources-relative, required, refused at build if it loads nothing), so a fourth character costs a PNG and a JSON line |
| Status icons | `Art/UI/Status/Raw` | direct, green `#00FF00`, hue-dominance, 1024px master keyed then resized to 256 (`status` kit, alpha-aware resample — see the tool's module docstring) | `Resources/Status/<slug>.png`, slugs from `docs/STATUS_ICON_PROMPTS.md`'s headings | delivered (16 statuses + speed, plus `silenced`/`disarmed`, which are `Suppression` windows shown by `StatusHud.SilenceRow`/`DisarmRow`) |
| Stage props (planted shield, Shieldwall) | `Art/Sheets/bjorn_planted_shield-v2.png` (3 cells: intact/cracked/broken), `bjorn_shield_wall.png` | `slice_actor_sheet.py --key white_flood` into a scratch `--out-root` (its actor kinds are only Enemies/Characters), copied by hand | `Resources/Props/planted_shield/{intact,cracked,broken,wall}.png`, one shared canvas for the three states, dirt-mound base on the ground line (`PlantedShieldArt`, 523x571, groundLine 8); Sprite import forced by `StanceSpriteImporter` | delivered; drawn by `FightController.ShieldProps.cs`. `wall.png` is not loaded: a Shieldwall draws the single shield per ally until a party-wide prop exists |
| Talent backdrops | `Art/Backgrounds/{Sentinel,Einherjar,Juggernaut}.png` (1920x1080) | none | referenced by `TalentBackdrops` (plot id -> sprite + tint), baked into the Talents scene by `ScreenRegistry` | delivered |
| Spell book kit | `Art/Items/SpellBooks/` | shared tinted cover templates plus one transparent glyph per spell (`tools/build_spellbook_bases.py` for the expansion set) | same folder, 512px master and 48px menu icon | delivered (17/17 book spells) |

## 2. Keying conventions

Three distinct techniques, picked by what the asset actually is:

- **Painted objects (icons, hub buildings, kit pieces): flat `#00FF00`
  field, keyed on hue-dominance** (`tools/key_green_screen.py`). Brightness-
  keying (generate on black, key alpha from luminance) is the obvious
  alternative and is **wrong** here — it punches straight through any
  legitimately dark shadow or void the object is supposed to have opaque.
  Green appears nowhere in this project's palette, so keying on how much the
  green channel *dominates* red/blue (not on raw brightness) removes exactly
  the backdrop and nothing else.
- **Pure light/glow effects with no opaque subject: solid black + screen-
  blend at composite time**, no keying at all. A light source has no hard
  edge to cut an alpha mask around; screen-blending a black-background image
  achieves a cleaner result than alpha compositing would, and sidesteps the
  whole keying question. (`activation_line.png` in the Talent Tree kit is
  the example — see the composited previews referenced in
  `docs/handoffs/talent_tree/`.)
- **Full-frame backgrounds: opaque, no keying, no alpha.** 1672×941 is the
  established delivery size for this project's screen backgrounds — match
  existing files in `Art/Backgrounds/` rather than the engine's own
  1920×1080 reference resolution.

### Drawn borders survive an authored alpha

`slice_spell_sheet.py` runs two passes that delete lines a generator drew
and nobody asked for: `erase_drawn_lines` (luminance, sheet-wide) and
`erase_frame_borders` (coverage, per cell).

The second runs on **every** sheet, including one delivered with its own
alpha. That was not always true, and the reasoning for skipping it sounds
right: a frame whose alpha the artist wrote is a frame whose every opaque
pixel was meant. It holds for the luminance pass — which would cheerfully
blank a bright horizon — and it fails for the border pass, because
`mud_blast.png` arrived with a box drawn around every cell *and* with the
alpha to make that box solid. "The artist authored it" was true of the
artefact as well as of the art, and every Mud Burst played a white rectangle
around its target. Frost Flare had produced the same complaint by the other
route.

Safe to run unconditionally because it does not ask what a pixel looks like,
only what its row does: edge to edge, with nothing beside it
(`FRAME_BORDER_ISOLATION`). Real art that spans a frame has neighbours.

## 3. `key_green_screen.py` usage

```
py tools/key_green_screen.py
```

Processes every kit in `KITS` that has files present in its source folder;
a kit with no source directory is skipped, not an error, so kits can be
delivered incrementally.

- **Grouped mode** (Hub only): sources named `name_1.png`, `name_2.png`,
  `name_3.png` become one output with three animation frames
  (`f0.png`, `f1.png`, `f2.png`), resampled to that kit's configured
  delivery size. This is what lets `HubBuildingAnimator` find extra frames
  to cycle at runtime.
- **Direct mode** (everything else): one source file in, one keyed file of
  the same name out, written to a `Processed/` sibling of the source
  folder, **no resampling** — the source resolution IS the delivery
  resolution, since these are baked into the scene once by `LoadSprite`
  (editor-time), never `Resources.Load`ed at runtime the way animated Hub
  buildings are.

The tool also force-corrects each output PNG's `.meta` — `textureType: 8`
(Sprite), `alphaIsTransparency: 1`. This isn't optional: a PNG Unity has
never seen imports as a plain Texture by default, `Resources.Load<Sprite>`
on a plain Texture silently returns `null`, and the affected slot just
renders nothing with no error anywhere. Only a `.meta` that's actually wrong
gets touched, so re-running the tool doesn't force a project-wide reimport.

### Reward-screen art: `process_reward_art.py`

`py tools/process_reward_art.py [--only reward_chain.png]` replays
`Art/UI/Reckoning/reward_recipe.json`: `key_out` per sheet with its own soft
band (`opaque_below`/`transparent_above`), then optionally `trim_wisps` (opening
by a disc: thread ends thinner than the disc past a silhouette vanish), `grade`
(saturation/value/cool, for the chain's dark iron), cell cutting by `rect` or
grid cell, tight crop, and one shared canvas per sheet. Outputs land in
`Reckoning/Processed/`. Cut rects sit in the empty columns between objects, not
at even thirds. Skipped sheets and why are in the recipe's `_notes`.

## 4. Actor stances (`Resources/<Enemies|Characters>/<id>/<stance>.png`)

Every combat actor ships **one still drawing per stance** — idle, attack,
cast, hurt, defeated, plus any skill-specific pose (the beetle's
`shell_closed`) — posed procedurally at runtime. No skeletal rigs, no
multi-frame animation sheets: `Core/StanceAnimationLibrary.cs` resolves a
stance to exactly one sprite.

**This is an owner decision, not a stopgap, and it is not open.** Frame
animation on an actor is not supported and nothing in the engine plays it —
there is no code path that would, so a delivered frame sheet is not "not
wired up yet", it is unusable. `docs/STANCE_SHEET_SPEC.md` §0 records why:
three animated enemies shipped with six-frame stance sheets and all three
played badly, for a reason no better prompt closes. Spell and contact VFX
are the exception and always were — they are frame sequences under
`Resources/Spells/` and `Resources/Vfx/` (§5b, §5c), and nothing about this
rule touches them.

**Full commissioning work order:** `docs/STANCE_SHEET_SPEC.md` — the
prompt template, per-actor SUBJECT/FEATURES/POSES, the accept/reject
checklist and the delivery steps all live there. This section covers what
underlies it: keying (§2-3 above), the slicer's mechanics, and the sizing
note the delivery step depends on.

### 4a. The stance manifest — where the feet are

`Resources/StanceManifest.json` states, per actor, **where the figure's
feet sit inside its own canvas** (`groundLine`, in pixels up from the
canvas bottom) and, optionally, `breath` (idle sway amplitude as a
fraction of canvas height; a runtime default applies when absent).
`StanceManifestLoader` reads it once; `Domain/Stage/StanceManifest.cs`
holds the parsing and defaults.

It exists because the ground line used to be **inferred at runtime** by
scanning for the lowest opaque pixel, which is wrong whenever that pixel
belongs to something other than the feet — the golem's slam erupts an
earth spike ~50px below its own feet, and Shawn's idle plants a staff
~33px below his, so both were read as the floor and both figures floated.
One authored number per actor, checked against the art, replaced the guess:

- **`groundLine` is per ACTOR, never per stance.** A character's feet do
  not move relative to their own art — only the effects drawn around them
  do. One value per actor is what makes "the figure cannot jump between
  poses" true by construction.
- **`groundLineSource` says who owns the number** — `"slicer"` or
  `"authored"`, **absent meaning authored**. The slicer writes `groundLine`
  only when the entry is absent (a new actor) or the source is `slicer`;
  against an authored entry it prints its own measurement and the delta and
  leaves the file closed. `breath` and `hover` are never written by any tool
  at all — how hard a creature breathes and whether it flies are judgements
  about the art rather than measurements of it.
- **Re-slicing a sheet can invalidate it.** `StanceManifestValidationTests`
  re-measures every actor's committed stills and fails if the number has
  drifted more than 8px, naming the actor and both numbers. A `slicer` value
  beyond the band is stale and fails outright; an `authored` value beyond it
  is an **override**, which passes only if the actor's `Art/**/README.md`
  says why in a line naming `groundLine`. An override nobody wrote a reason
  for is indistinguishable from a number a re-slice left behind.
- **The re-measurement is a MEDIAN across an actor's stances**, and that is
  the load-bearing choice rather than a detail. The minimum is what a naive
  scan takes, and it reads the golem's earth spike (attack and cast measure
  20 against the other four stances' 64) and Shawn's staff (idle 9 against
  the other five's 41) — the exact two cases this file exists to stop the
  runtime guessing at. An actor's feet are where MOST of its drawings put
  them.
- **There is no per-stance timing left to author.** `secondsPerFrame`,
  `impactFrame` and `soundFrame` belonged to multi-frame stances and have
  no meaning for a still. A monster's `vfxSeconds`/`vfxImpactFrame` (in
  `ContentData/enemies.json`) still time the *effect* layered over a
  stance — that is unrelated and unaffected by this.

**`castPoint` — where a cast leaves this actor's body.** A third judgement
beside `breath` and `hover`, same rule: no tool ever writes it, because there
is nothing to measure — "where the light should come from" is a choice about
the drawing, not a fact a pixel scan reports. Absent means what it always
meant before this field existed: the cast leaves from the caster's own slot
origin, which is what every actor without an entry still gets, byte for byte.

The gripe this was written for: a travelling spell's launch point used to be
the caster's slot origin unconditionally, which lands behind an actor whose
weapon or book is held out in FRONT of them. Odette casts with a book held
out; her book, not her spine, is where the light should leave from.

```json
"castPoint": { "dx": 201.5, "dy": 135.5 }
```

Units, and why they are not canvas-absolute: pixels on the actor's own
**cast** still, the same canvas `groundLine` is measured on.

- **`dx`** — pixels from the figure's own horizontal centre (canvas width /
  2), **positive toward the actor's own drawn facing** (`SpriteFacing`), never
  toward whichever side of the stage the actor happens to stand on. The
  mirror that flips a leftward-drawn monster onto the right side of the stage
  flips this the same way, automatically — it rides the same transform the
  sprite itself does.
- **`dy`** — pixels **above this actor's own `groundLine`**, the same
  "up from the floor" direction `groundLine` already counts in, never up from
  the canvas edge.

Anchoring to the figure's own centre and its own ground line rather than to
canvas corners is the same move `groundLine` itself made, for the same
reason: a re-slice can change the canvas (a wider pad, a taller sheet)
without moving the figure's anatomy relative to itself, and a point measured
off a corner that can move would drift off the book the moment the canvas
resized. A point measured off the two things that do not move under a
re-slice moves with the figure instead.

At play time this becomes a stage position by scaling and mirroring `(dx,
dy)` onto the caster's own slot — `FightController.SpellVfx.CasterCastPoint`
is the seam every caster-anchored travelling layer and every non-travelling
`caster`- or `caster-centre`-placed layer goes through (an authored
`castPoint` IS where a cast leaves that actor's body, whichever of the two
words placed it — `caster-centre` for an actor with no entry still means the
figure's own centre, exactly as before), and `Domain/Stage/CastPointPlacement`
is the one piece of that seam that is a formula rather than "ask the transform"
(engine-free, so it is pinned with literal numbers in the fast EditMode
suite). It rides the slot's current depth scale and hover for free, because
both are already baked into the slot's transform by the time a cast reads
it — `AnchorOne` writes depth into `localScale`, `SetHover` writes the bob
straight into `anchoredPosition` every frame.

Two actors are authored today, as the model's two-use check: Odette
(`Characters/owl`) at the hinge of her book, and Shawn (`Characters/sheep`)
at the glowing mote over his raised hand — his `cast.png` holds no staff,
despite what an earlier draft of this brief guessed; the hand-raised cast is
what the art actually shows and reads just as clearly as a launch point.
`StanceManifestValidationTests` refuses a `castPoint` that falls outside its
actor's `cast.png` alpha bbox, the same way it refuses a `groundLine` more
than 8px off the art.

The file lives in `Resources/` rather than `ContentData/` because it is
read at runtime rather than baked — and deliberately **not** under
`Resources/Content/`, which `ContentBuilder` deletes wholesale.

One rule the tooling doesn't enforce for you: **slice every stance of one
actor in a single tool invocation.** The slicer sizes its shared canvas
from whatever it's given in one pass; two separate invocations for the
same actor produce two different canvases, which makes the actor visibly
resize/jump the moment that stance shows. `EnemyArtCompletenessTests` checks
this for every enemy stance; no test covers the party roster.

### 4b. `slice_actor_sheet.py`

Cuts one committed design sheet (a grid of poses — see
`docs/STANCE_SHEET_SPEC.md` §3) into one still per named stance,
composited onto a single shared canvas per actor:

```bash
python tools/slice_actor_sheet.py \
    --sheet Assets/_Project/Art/Enemies/<actor>/sheet_poses.png \
    --actor Enemies/<id> \
    --stances idle,attack,<ability_pose>,hurt,defeated
```

**Every run writes `Art/<Enemies|Characters>/<id>/recipe.json`** — the source
sheet, the full argv with every default made explicit, the tool's sha256, the
Pillow/numpy/Python versions, the output folder, the measured ground line and
a timestamp. `--recipe <path>` replays it:

```bash
python tools/slice_actor_sheet.py --recipe Assets/_Project/Art/Enemies/treant/recipe.json
```

A replay re-parses every cut-affecting argument out of the file, so nothing
typed alongside can quietly change what it claims to reproduce — any of those
flags given alongside `--recipe` is refused by name rather than silently
discarded. `--out-root`/`--quiet` are the exception: they choose *where* the
replay writes, not *what* it cuts, so an explicit `--out-root` is honoured —
`--recipe <path> --out-root scratch/verify` verifies into scratch instead of
overwriting the shipped PNGs, which is the safe way to run a verification a
second time. A replay refuses by name if the source sheet is gone; it never
rewrites the recipe (the argv it would write is the argv it just read) and
never writes a `.meta` (Unity generates those on import — a tool copying one
alongside a regenerated PNG is how a duplicated asset gets a duplicated GUID).
Defaults are written out rather than omitted, so a recipe does not change
meaning the day a default does.

The README is still where the *reasoning* belongs and is no longer where the
recipe lives. The treant's said `delivery_scale 1.05`, `white_flood` and "a
raised `pocket_max_area`", and reconstructing the run from that meant guessing
the raised value and testing candidates against the committed bytes — 2000 and
4000 reproduce, 1600 does not. Prose is right about everything a person needs
and one number short of what a machine needs.

**"Actor", not "enemy": this covers both sides of the fight stage.**
A party member's stance art (`Resources/Characters/<id>/`) and a monster's
(`Resources/Enemies/<id>/`) are resolved by one runtime path and held to
one set of invariants, so one tool produces both — `--actor Characters/sheep`
picks the other output tree.

- **Never scales an individual pose.** Only one literal `--delivery-scale`
  for the whole sheet is applied, after native-size compositing.
  `sqrt(opaque pixel count)`, not bbox height, is the pose-invariant proxy
  the tool measures and prints per stance — bbox height is confounded by
  the pose (a crouch is shorter than a rear-up at identical draw scale).
- **`--anchor ground_band` (default) vs `centroid`.** `ground_band`
  (largest connected component, intersected with a thin band at the
  creature's own ground line) ignores detached debris and raised
  limbs/tails that would otherwise drag a whole-mass centroid sideways.
  `centroid` is the simpler whole-mask behaviour, useful where a pose's
  mass genuinely touches the ground away from its feet (a staff, a
  dragging tail) and `ground_band` would mis-anchor.
- **`--drop-far-components-px N`** keeps the largest silhouette component
  plus anything within N px of it and zeroes the rest — for a
  neighbouring cell's stray limb bleeding across a gutter. Off by default;
  a clean design sheet needs nothing here. Reject on **distance from the
  main silhouette**, not size: contamination lands in dead space (every
  real stray measured >40px clear), while genuinely detached art (a loose
  leaf, the treant's mushrooms) sits close against the body. Erasing
  everything except the single largest component is *also* wrong — it
  eats the mushrooms too.
- **`--nudge STANCE:DX,DY`** repositions one stance's anchor without
  resizing it — the only per-stance correction the tool offers, because a
  per-stance scale correction is what made an earlier version of this
  pipeline make creatures visibly pulse size between poses.
- **One shared canvas, checked against what was written.** Every stance is
  pasted bottom-aligned onto one canvas sized to the largest stance's
  content plus uniform padding — `FightController.StageVisuals` sizes each
  combatant's slot to its sprite's own canvas and stands it on that one
  authored ground line, so a per-stance canvas would make the actor
  visibly resize the moment its stance changes. `_assert_one_ground_line`
  re-reads the PNGs actually on disk afterward and refuses to leave a
  >6px foot-row spread uncaught.
- Refuses any input path under `Assets/_Project/Resources/` — processing
  already-processed output would compound resample loss.
- **`--prune`** deletes stance PNGs already in the output folder that the
  run did not (re)write; without it they're only listed.
- **Slicing: cut at the gutters, reject strays by distance.** The
  generator does not lay poses out on an exact grid — slicing at even
  thirds has cut straight through bodies before. `sheet_slicing.best_cut`
  searches a bounded window for the emptiest row/column near the nominal
  split and ties break toward it. **Use it. Do not hand-roll a slicer.**
- **Sizing: pixel size IS on-screen size.** There is no per-enemy scale in
  content, so a `--delivery-scale` picked against the delivered roster's
  idle content heights is what keeps a beetle from standing as tall as a
  golem. **Measure the target, do not copy a number out of a doc:**

  > **A stale hardcoded target table lived here once and caused a real
  > near-miss (2026-08-30).** It read `rat 226, beetle 241, bog_witch 282,
  > golem 284, treant 423, forest_warden 472`. Re-measured against the art
  > actually on disk, four of the six were wrong — bog_witch 313, golem
  > 337, treant 441, forest_warden 483 — by up to 19%. Acting on the stale
  > `rat 226` produced a `--delivery-scale` that would have shrunk a
  > correctly-sized rat by a fifth; it was caught only because the number
  > was checked against the actual committed art before being applied.
  > Measure the target off the art you are matching, in the same command
  > you set the scale in:
  >
  > ```bash
  > python -c "from PIL import Image; im=Image.open(P).convert('RGBA'); b=im.getchannel('A').getbbox(); print(b[3]-b[1])"
  > ```

### Visual QA before shipping a kit

`ScreenshotTool` runs in Editor Edit Mode and cannot render a live combat
stage, so there is no other way to *see* a sliced actor before it ships.

```bash
python tools/actor_stance_qa.py --report Assets/_Project/Resources/Enemies --only <id>
```

renders `tools/screenshots/actor_qa/<id>.png` (gitignored, never
committed): one row per actor, one cell per stance, each with its canvas
outline, ground line, alpha-centroid tick, and a `sqrt(area)/median`
caption colour-coded against a tolerance band (no test asserts it). A creature folder still holding
old `<stance>/f0..fN/` frame folders (mid-migration to this pipeline) is
skipped with a one-line notice rather than guessed at. `--report` has no
dependency on the slicer, so it can render today's *committed* art as a
"before" picture, turning a re-slice into a demonstrable before/after.

### `slice_spell_sheet.py`'s recipes

One file per effect under `Art/Sheets/recipes/<id>.json`, loaded into the
`VFX` dict the tool has always read. It exists because every parameter used to
come from argv, including the frame names, so how the shipped effects were cut
survived nowhere; it lives **under `Assets/`** for the same reason
`hand_assembled.json` moved there — the headless runner mirrors `Assets` and
nothing else, so a record beside the tool is invisible to the C# suite.
`--new <id> --sheet <p.png>` writes the recipe and prints the `skills.json`
block.

A recipe names one sheet (`sheet`/`grid`/`names`) unless the delivery itself
was more than one — `prismatic_orb_water` cuts a travelling-ball loop from one
PNG and its contact burst from another — in which case `sources` replaces all
three: a list of `{"sheet", "grid", "names"}` entries, cut in order under the
recipe's one `keyed` flag and merged into a single cell dict (a name reused
across sources is refused). Cells that come out different sizes — one sheet's
grid truncated tighter than another's — are centred, transparent-padded onto
a shared canvas sized to the largest cell before anything downstream runs, so
one sequence never changes box size frame to frame.

**A painted still on #00FF00 is a recipe with `keyed`, not a new kit.** `"keyed": "green"` keys on hue and then pulls every pixel's green down to min(red, blue), right for violet and grey art (the Blackglass rift, the Court's courtier). `"keyed": "green-soft"` keeps only the keyer's own spill suppression, for a subject with green or gold in it: the Gale Scythe's sea-green blade and its gold flecks went lilac and red under the clamp. The recipe usually also states `rects` for the whole image, `content_crop`, and one `sequence` step; see `court_of_whispers_courtier.json`.

**A subject that contains green or teal is generated on solid #FF00FF and keyed `"magenta"`.** Same keyer as green with the backdrop swapped: alpha from magenta dominance (min(red, blue) over green), spill pulled toward neutral by lowering red and blue together, greens never touched. There is no clamp variant, because the clamp exists to remove backdrop colour from the subject and a green subject has none of the backdrop's. Use it for venom and anything teal; use green or green-soft otherwise. Pinned by `tools/key_green_screen_test.py`; prompt wording in `docs/prompts/TEMPLATE_spell_art.md`.

**A sprite layer authored `at: hit` is a tail**: the renderer shrinks it to a fraction of the struck body (`TargetBody.TailScale`), whatever its `size`. Art that must be full size on the cue (Thorn Tithe's closed cage) is a frame of a `release` layer on its own clock, not a second layer opening at the hit.

> **The recipe describes how frames are PRODUCED. The skill's `vfx` block
> describes how they PLAY.** A skill may override a recipe's intent
> deliberately, and two skills may share one frame folder with different
> timing — `mud_burst` and `bog_mud_burst` share `Spells/mud_burst` today. So
> `SpellVfxRecipeDriftTests` checks only what is arithmetic rather than
> judgement: every folder a skill plays was produced by a recipe or is named
> in `hand_assembled.json`, and no skill times a beat to a frame its folder
> does not have.

`frost_flare`, `mud_burst`, `cinderfault_ground` and `cinderfault_eruption`
are verified: re-running them regenerates the committed PNGs byte-identical.
**`lightning_bolt` is verified for eight of its nine frames** — the committed
`f5` (cell `f3` at 1.08 scale) differs from what its recipe produces in 906
pixels at full channel range, and the tool is deterministic across runs, so
the drift is in the committed file rather than in a scaling path that moved.
Left alone rather than corrected: overwriting it changes how a shipped spell
looks. Running the tool on that id **will** rewrite it; its `_notes` say so.

**A VFX is NOT held to the actor rules, and must not be.** Two of them
invert:

- **No draw-scale band.** An effect is *supposed* to grow and fade —
  `frost_flare` legitimately spans 0.42×–1.44× of its own median mass.
  Applying the actor rule here would be the same category error as the
  bbox-height pass that broke the enemy art.
- **No baseline re-anchoring.** Where the effect sits inside its cell *is*
  the animation; a frame of sparks gathering at the top has no ground line
  to align to. The slicer cuts a plain fixed grid and preserves each cell
  as drawn.

What does hold: every frame resolves, all frames share one canvas, and no
frame is blank *after* the effect has started. A blank frame is legitimate
only as a lead-in beat (the golem's boulder opens on one); a blank one
mid-sequence means the luminance key ate a cell.

`HAND_ASSEMBLED` records VFX the tool did **not** produce and cannot
reproduce — `golem_boulder` is hand-built (`f2`==`f3` is a held peak,
`f4`/`f5` are a stepped alpha fade-out). Recording "we don't know how this
was made" is the point; a silent gap in the manifest reads as "nothing to
see here." Running the tool against such an id refuses rather than
overwriting.

> **Anything under `Resources/` that is `Resources.Load<Sprite>`ed at
> runtime must be covered by `StanceSpriteImporter`.** A PNG imported with
> Unity's default `textureType` returns `null` from `Resources.Load<Sprite>`
> — no error, no warning. `Resources/Spells/` was missing from that list
> and the golem's Boulder Slam shipped playing *nothing at all* for weeks
> (its frame probe broke on the first null). `Enemies/`, `Characters/` and
> `Spells/` are covered now; `Hub/` is handled separately by
> `key_green_screen.force_sprite_import`. A new runtime-loaded folder needs
> one of the two.

### Reproducibility is recorded, including where it fails

Several of the art tools carry a committed manifest (`PORTRAITS`,
`key_green_screen.py`'s `KITS`, `slice_item_sheet.py`'s `SHEETS`) and the two
slicers carry recipe files. Where an entry has been **verified**, re-running
it regenerates the committed PNGs byte-identical, which is the proof the
recipe is the real one rather than a plausible guess.

**Every combat actor is in exactly one of two categories, and
`HandAssembledArtTests.EveryDeliveredActorIsInExactlyOneCategory` refuses any
other arrangement:**

| category | means | today |
|---|---|---|
| reproducible | a `recipe.json` beside its source art that `--recipe` replays byte-identical | treant, owl, and every actor sliced from now on |
| protected legacy | an entry in `hand_assembled.json`'s `actors` block, hash-pinned, source and chosen frames named, explicitly not reproducible | rat, golem, bog_witch, beetle, forest_warden, sheep |

An actor in **neither** is the state that matters, because it is invisible: it
reads exactly like one somebody wrote down, right up until the sheet is needed
and is not there. An actor in **both** is a contradiction — it claims the art
is reproducible and also that nothing can remake it — and usually means an
actor gained a recipe and was never taken out of the register.

Where reproducibility has *not* been established, that is recorded rather than
left blank, because a silent gap reads as "nothing to see here":

- **`golem_boulder`** is in `hand_assembled.json`'s `sequences`. The tool
  refuses to regenerate it.
- **`lightning_bolt`'s `f5`** does not come back from its recipe. Eight of
  nine frames do; see the recipes section above.
- **The six protected-legacy actors.** Each entry says what is actually known:
  the rat's `idle` is `f0` of the 12-frame idle sheet and its `attack` is
  `f5`, the peak of the leap; the golem's `attack` and `cast` are both `f2` of
  the attack sheet, the same slam deliberately (their hashes agree, which is
  the register's claim confirmed by the pin); the bog witch's source sheet
  carries baked captions, a background and floor shadows, so whatever removed
  them was not this pipeline; the forest warden's stills went through
  `slice_actor_sheet.py`'s anchor but with already-cropped PNGs as input
  rather than a design sheet; the beetle's `sheet_poses.png` survives, so a
  fresh slice is possible but would be a different delivery. **Shawn has no
  design sheet anywhere under `Art/`** — there is nothing to slice.
- **`Sheep`'s portraits** are marked `reproduces: False`. A fresh run
  produces a 1122×1360 crop where the committed `Shawn_neutral.png` is
  1122×1402 — so the shipped art came from different settings, an older
  version of the tool, or a hand edit, and nobody recorded which. Running
  the tool over that folder now **refuses** rather than silently replacing
  all six; `--check` renders to scratch and reports what *would* change
  without writing anything. That hazard was documented in the tool's own
  docstring for months and enforced nowhere.

### 4c. Phase-keyed stances, and transformation forms

Two things a stance kit can be commissioned for beyond the four the fight
drives on its own (`idle`/`attack`/`hurt`/`defeated`). Both are authored in
`skills.json`; neither costs any code.

**A blow can be three drawings, not one.** A skill authors up to three of the
caster's own poses, keyed by the PHASE of the beat rather than sequenced with
their own timings — the phases already exist in the beat and are already
timed, so a phase key picks a drawing for a moment the beat already owns.

| field | worn |
|---|---|
| `approachStance` | while the actor is travelling — a `close`'s walk-in, a `lunge`/`charge`'s crossing |
| `windupStance` | between arrival and impact; on a `hold` or a `close` this BUYS the beat a wind-up wait it would not otherwise have |
| `stance` | the strike, from the impact instant through the settle. This is the field that already existed |

Precedence, which is what an author actually needs to know:

- Author nothing and the beat plays as it always did — the strike from the
  open, idle at the end, no extra waits. Every existing skill is this case.
- `hold` has no travel, so `approachStance` on one is **ignored** (not
  refused: a skill's approach can be edited without its poses being
  rewritten).
- `lunge` and `charge` have exactly ONE pre-impact interval, and that
  interval IS the travel — the crouch-and-cross is the swing rather than
  something in front of it. Authoring both there wears the **approach** pose
  and drops the wind-up, rather than inventing a midpoint the animator knows
  nothing about. Author one or the other on those two.
- `close` is the only approach with a real boundary inside it (it arrives
  before the blow opens), so it is the one that can wear all three.
- A wind-up a `hold`/`close` buys is charged to the beat's own budget: the
  settle afterwards gives back exactly what it took, so a beat with a wind-up
  is the same length as one without.

The worked example is `placeholder_brawler_slam` (Bjorn): `"approach":
"close"`, `"approachStance": "rush"`, `"windupStance": "overhead"`,
`"stance": "slam"` — he crosses, raises it, brings it down. The kit therefore
needs `rush`, `overhead` and `slam` on the same shared canvas as the other
six. The full table with its fallbacks lives on `CombatBeat`, and
`EnemyArtCompletenessTests` audits both new fields against disk exactly as it
already audits `stance` — a phase pose naming art a monster does not have
fails the same way the strike does.

**A transform can change which actor is drawn.** `transform.spritePath` names
a Resources-relative stance FOLDER (`"Characters/sheep_black_ram"`), and
while the transformation runs, every stance name the fight asks for resolves
inside it — so the form is a whole delivered actor with its own
`StanceManifest` entry, its own canvas and its own ground line, commissioned
exactly like any other. It is not a per-stance override list: a second actor
already is one, on disk.

What the runtime does with it, so a delivery knows what it is being judged
against:

- The swap becomes visible at the **impact instant** of the transform's own
  beat, under a white outline flash, with the beat's authored `shake`. The
  beat before that instant is drawn from the caster's OWN folder, so
  authoring the same pose name on `windupStance` and `stance` (as
  `black_ram_mode` does with `victory`) reads as one drawing replaced by the
  other actor's version of it.
- The **revert** is its own beat, and looks like the entry in reverse: the
  base art goes back on at that beat's impact instant, under the same white
  silhouette flash, with a house shake floor of 0.45 (below
  `black_ram_mode`'s authored 0.7 — becoming the thing is the bigger event).
  The pose is the actor's own `idle`. The **permanent** form the Wrath
  capstone grants records no beat and flashes nothing: nothing changed.
- A form folder that is not on disk leaves the figure in its own art and logs
  one line. A Black Ram nobody has drawn yet looks like Shawn with better
  numbers, not like a nameplate.

**And a form authors how its holder's blows land.** `transform.hit` is a
three-field block, all optional, and an unauthored one is byte-for-byte
today's behaviour:

| field | what it does |
|---|---|
| `hit.vfx` | a contact effect, in exactly the `vfx` vocabulary of §5b, played **on every body the blow reached** at the impact instant. Its layers author `at: release` — the presentation's own clock starts on the frame the blow lands, so there is no cue to wait for |
| `hit.shake` | a floor on the stage kick, `0..1`, the same floor a skill's own `shake` is |
| `hit.hitStopSeconds` | a floor on the hit-stop, in seconds, clamped to `HitStop.MaxSeconds` (0.18). This is the one reaction that had no authored floor before |

It lands on any beat the holder **landed damage** with — his plain Attack, a
headbutt, a Trample, a monster ability, whatever comes next — because it is
recorded at `CommitBeat` off the actor's running `Transformation`, and no
skill id or character id appears in the code that does it. A heal, a miss, a
buff and the transform's own entry beat get nothing.

It is an ADDITION, never a replacement: a skill that already draws its own
spell draws both, on a second presentation slot (`CombatBeat.FormVfx`) rather
than merged into `Vfx`, because two layered presentations have two
`hitCueSeconds` and one `sfxPath` each and a merge would have to silently
pick.

`black_ram_mode` authors all three. Its burst, `Spells/black_ram_impact`, was
generated placeholder art (`tools/make_contact_fx.py`'s polar arithmetic)
until a commissioned sheet replaced it; the function that drew the
placeholder is gone (`--form-sheet` had no other caller) and its bytes live
in git history. The recipe's own `_notes` carry the delivered sheet's
provenance the same way every other recipe's do.

## 5. Wiring checklist (new art → visible in-game)

1. Drop raw source file(s) in the kit's source folder, run the keyer.
2. Point the content JSON's `iconPath` (or equivalent field) at the
   `Processed/` file.
3. Rebuild content and/or scenes as needed:
   `run_tests_parallel.ps1 -BuildContent -BuildScenes` — this is also what
   syncs the regenerated `.meta`s back to main (see `docs/WORKFLOW.md` "Commits").
4. Commit the `.png` **and** its `.meta` together.
5. After a TestRunner build, double-check `Art/` actually diffed back to
   main — `LoadSprite` flips a texture's importer settings and can generate
   a fresh `.meta` for anything newly referenced, and if that diff doesn't
   sync back, the committed scene ends up pointing at a Sprite sub-asset
   that doesn't exist in main's copy of the file.

**Event-room page art** (`events.json` → `pages[].artPath`): **16:9,
commissioned at 1920 x 1080**. It is drawn at 1280 x 720 as the dialogue
stage's set piece and at 960 x 540 in the line-less layout's frame
(`EventScreen.StageArtWidth` / `ArtWidth`, pinned in `EventScreenTests`); the
image keeps its aspect, so off-ratio art letterboxes rather than stretches. File it in the event's own folder,
`Assets/_Project/Art/Events/<event_id>/<page_id>.png` (e.g.
`Art/Events/petting_zoo/zoo.png`; the content build refuses art under
`Art/Events/` filed anywhere else), and write that Assets-relative
path, extension included, into `artPath` -- it is baked into the Map scene
like an item `iconPath`, so it needs step 3's `-BuildScenes`. Full authoring
guide: `docs/EVENTS.md`.

### 5a. Delivering a combat actor's stances

The stance kit has its own checklist, because two of its steps used to be a
person retyping a number and the rest of this section does not cover them.
`docs/STANCE_SHEET_SPEC.md` §6 is the full work order; this is what the
delivery actually consists of now:

1. **One `slice_actor_sheet.py` invocation, all stances at once.** Two
   invocations for one actor produce two different shared canvases, which
   makes the figure visibly resize the moment a stance changes.
2. **Sizing:** slice once at `--delivery-scale 1.0`, measure the written
   `idle.png` alpha bbox height, re-slice at `target/measured`. Measure the
   target off the *currently delivered* roster, never a number copied out of
   a doc — see the sizing note in §4b.
3. **`Resources/StanceManifest.json` is written by the tool, not by you.** A
   new actor gets an entry with `groundLineSource: "slicer"`; an existing
   slicer-owned entry is updated in place; an authored one is left alone with
   the delta printed. Nothing to copy, and a no-op run leaves `git diff`
   empty.
4. **`recipe.json` is written by the tool too**, beside the source sheet.
   Verify it: `--recipe <path>` and check `git status` is clean under
   `Resources/`. If the replay is not byte-identical, the actor is protected
   legacy rather than reproducible — register it in `hand_assembled.json`'s
   `actors` block and pin its bytes in `HandAssembledArtTests`.
5. **The head point AND the body point, both required.** Read the head
   centre and the torso centre off `idle.png` as fractions of its opaque box
   (X from the left as drawn, Y from the bottom, `headSize` = head height over
   box height) and write `headX`/`headY`/`headSize`/`bodyX`/`bodyY` into the
   monster's `enemies.json` entry. The body point is the centre of the torso
   region, not the box centre: it is where `target-centre`, `above`, `sky` and
   the melee contact burst aim, and the box centre is wrong for a long-bodied
   or tall-necked animal. That file is their only home: the slicer reads it
   and draws `head_check.png` (the idle still, a crosshair on the head, a box
   of `headSize`, a green ring on the body) beside the recipe in `Art/`
   (git-ignored). The slice of a new monster therefore exits non-zero with
   the stances written until both points are authored; run it again and LOOK
   at `head_check.png`. `--no-head` is for a prop. The content build refuses
   any enemy with a `spritePath` and an unauthored head or body, so it cannot
   ship without. A transform form with its own `spritePath` carries the same
   five fields in its skill's `transform` block, refused the same way.
   `python tools/head_overlay.py --out <png>` draws every monster's and form's
   marks in one grid for batch review.
6. **`README.md` records the reasoning** — the accepted prompt, the Detail
   Inventory, the model and date, the accepted metrics, and a provenance line
   pointing at the recipe or the register. Plus, if the ground line is an
   authored override more than 8px off what the art measures, a line saying
   why, naming `groundLine`; the validator greps for exactly that.
7. **Delete the superseded files with their `.meta`s** — old `<stance>/f0..fN/`
   frame folders, `NN_<stance>.png` singles, `_*sheet_source*.png`,
   `_contact_sheet_preview.png`, `_base_poses/`. Git history is the archive,
   and a stale off-style sheet is what gets referenced next time.
8. `run_tests_parallel.ps1 -BuildContent`, then visual QA:
   `python tools/actor_stance_qa.py --report Assets/_Project/Resources/Enemies --only <id>`.
9. Commit assets **and** their `.meta` files together, staged by explicit
   path.

## 5b. Spell effects (`Resources/Spells/{id}/f0..fN`)

A spell's frames are cut from a sheet by `tools/slice_spell_sheet.py`, and
everything about how one LOOKS is one value in the content — see
`SpellPresentation`. Adding a spell is a manifest entry, a JSON block, and one
command; it should not require touching C#.

Timing targets and the workflow for a cast that hits hard: `docs/SPELL_FEEL.md`.

### The whole loop

```bash
python tools/slice_spell_sheet.py --new <id> --sheet <file.png>
```

Cuts the sheet, writes a preview, and prints the two blocks to paste: the
manifest entry and the `skills.json` `vfx` block, with `impactFrame` guessed
from the frame carrying the most ink. Then tune against the preview:

```bash
python tools/slice_spell_sheet.py <id> --preview
```

The preview renders the sequence at the speed content actually declares,
dissolve and all, to `tools/screenshots/vfx/{id}.gif`.

A re-run that emits fewer frames than the folder currently holds prunes the
stale tail itself (`f<N>.png`/`.meta` at or past the new count) — a leftover
frame is not inert, a pre-layer `fps: 0` layer fits the whole folder into
`seconds` and it silently retimes the spell. `SpellVfxRecipeDriftTests`
(EditMode) pins that no shipped folder holds more frames than its recipe.

### The `vfx` block

| field | default | what it does |
| --- | --- | --- |
| `path` | — | `Resources`-relative folder of `f0..fN`. Empty means no visual. |
| `seconds` | `0.6` | How long the whole sequence takes. |
| `impactFrame` | `3` | Which frame (from 1) the blow lands on. The damage number, the flash and the hit-stop are all timed to it. |
| `anchor` | `target` | Where the art happens — see below. |
| `size` | `380` | Square box the art is fitted into, in reference-frame units. |
| `fit` | `none` | `target` scales `size` by the struck body (see `fit: target` under Layers). |
| `departFrame` | `0` | `travel` only: which frame (from 1) it leaves the caster on. Holds the wind-up in place instead of letting it drift. |
| `impactX` | unset | Where across its own frame the sheet actually strikes, `0`..`1` from the LEFT. |
| `impactY` | unset | Where up its own frame the sheet actually strikes, `0`..`1` from the BOTTOM. |
| `sfxPath` | — | `Resources` path to the clip that plays on impact. |

Anything left out takes its default. **This is the single-block form; a cast
that is more than one drawing authors `layerFormat: 1` and an array of layers
instead -- see "Layers" below.** There are no sentinels to remember,
with one exception: `impactX`/`impactY` are unset at `-1`, because `0` is a
legitimate point (the frame's bottom-left corner) and a sentinel that
collides with a real value is a bug waiting for its first author.

### `impactX` / `impactY` — where the sheet hits

**State them, or the view guesses, and the guess is only right for one kind
of art.** Without a point the box's bottom edge goes on the target's ground
line, corrected by the lowest opaque pixel the sheet reaches once it has
landed. That is exactly right for an eruption drawn standing on a floor
(`golem_boulder`), and it shipped two visible bugs for everything else:

- `frost_flare`'s burst sits a sixth of the way up its frame with embers
  falling *below* it, so the measured "floor" was the embers and the strike
  rendered on the Giant Rat's **chest**;
- `mud_burst` is a lance fired flat — its impact is halfway up the frame and
  two thirds of the way **across** it, because the left third is reserved
  for the incoming bolt. Bottom-anchored and centre-aligned it detonated
  above the rat's head and to one side.

Same conclusion the stance manifest reached about actors' feet, for the same
reason: a scan believes whatever it finds, and what it finds is a spray of
sparks. Measured in the same direction, too — `impactY` counts up from the
bottom, like `groundLine`.

How to read one off a sheet: open its `impactFrame` and find the centre of
the burst — not the centroid of the whole drawing, which a bolt's shaft
drags upwards. `tools/screenshots/vfx/{id}_frames.png` is the contact sheet.

Both halves or neither: a partly-stated point is ignored at play time and
**fails the suite**, because silently reverting to the rule the spell was
authored to escape is the least visible way to be wrong.

The point is aligned to whatever the `anchor` aims at — the ground line, or
the body's middle. It is corrected on **both** axes, and the horizontal
correction flips with the sheet's mirroring.

### `anchor`

Where the art happens, which is **not** the same question as who the skill
hits. Two axes — whose body, and where on it — flattened into one word:

| word | meaning | for |
| --- | --- | --- |
| `target` | on the target's ground line | bolts, impacts, eruptions — almost everything |
| `caster` | on the caster's ground line | wind-ups, stomps, transformations |
| `target-centre` | on the target's body point (`bodyX`/`bodyY` in `enemies.json`, mirrored with the sprite; allies read the box centre 0.5 / 0.5) | rings, binds, status glints — art drawn around a body |
| `target-head` | on the struck body's head point (`headX`/`headY` in `enemies.json` or the worn form's `transform` block, mirrored with the sprite; allies read the fallback 0.5 / 0.8) | eyes, crowns, whispers: art that belongs on a head. Per target; `follow` rides it. |
| `caster-centre` | on the caster's midpoint | self-buff auras, shield bubbles |
| `travel` | flies caster → target, arriving at its ground line | a bolt that buries itself in the floor |
| `travel-centre` | flies caster → target, arriving at its **middle** | anything thrown flat across the stage |

Both `travel` words are mirrored when the caster is on the right.

`travel` versus `travel-centre` is the third axis this word carries, and it
matters more the taller the target: a Giant Rat is 675×306 of canvas, mostly
length, so its ground line and its middle are nowhere near each other.
`mud_burst` arrived at the ground line and detonated above the rat's head
until it was moved to `travel-centre`.

An unrecognised word falls back to `target` at play time and **fails the
suite** — `SpellVfxTests` refuses any anchor that does not parse, so a typo
cannot reach a player as a quietly misplaced explosion.

### Cells that are not a uniform grid: `rects` and `canvas`

Two recipe keys for sheets whose delivered dimensions do not divide, and both
were earned by the water pack.

**`rects`** replaces `grid` on a sheet (or on one entry of `sources`): a list of
`[x, y, w, h]` in the sheet's own top-origin pixels, one per name, in play
order. `water_particles_8.png` is 1774×887 over 4×2 and its own README states
the column edges 0, 444, 887, 1331, 1774 — 444/443 alternating. A floor-divided
grid takes 443 everywhere, and the cost is not one lost pixel per cell: by the
fourth column it starts two pixels LEFT of where the cell does, so a sheet whose
art reaches its seams slices a sliver of the neighbour in. Measured on that
sheet, five of eight cells' content bboxes touch a cell edge.

State `rects` when the sheet does not divide AND the cells become sprites drawn
small (a particle atlas). A uniform grid is still right for a sheet that
divides, and truncation is still acceptable for cells padded onto a much larger
canvas — say so in `_notes` either way, because an unstated trade reads as an
unnoticed one.

**`canvas`: [w, h]** pads every cell onto a stated canvas before composing,
instead of onto the largest cell. It is headroom for a `scale` step: `turned()`
grows a cell about its own centre INSIDE its bounds and then feathers the outer
ring, so 1.06 applied to a crown already filling 425 of a 444 cell is 6% bigger
with 3px shaved off each side and a 12px fade welded to what is left.

### Composing frames from cells

The manifest's optional `sequence` builds frames out of the cut cells, so a
six-drawing sheet can become a twenty-six-frame animation without new art:

- `{"from": "f0", "spin": 8}` — eight frames rotating a full turn
- `{"from": "f3", "hold": 4, "scale": (1.0, 1.08)}` — four frames, growing
- `{"from": "f5", "scale": (1.0, 1.04)}` — one frame, resized

Left out entirely, the cells **are** the frames one for one.

`"feather": {"left": 0.32}` on a recipe fades the alpha to nothing over that fraction of the frame at the named edges (`left`, `right`, `top`, `bottom`; width or height fractions), before the content crop. It is for painted art delivered cut off by its canvas: Viper's Bite's snake neck leaves the left edge, and the feather turns that straight cut into a tail that dissolves.

`"cap_green": 0.75` on a recipe pulls green down to that fraction of red on lime pixels (green near or above red, almost no blue), after the key and before the crop. It is for warm art painted on #00FF00 whose soft glow keeps a lime cast the keyer cannot tell from the glow: the Gilded Aegis's gold halo. Gold and orange never have green above red, so the body is untouched.

`"cap_blue": 0.83` on a recipe pulls blue down to that fraction of green on pink pixels (red well above green, blue near or above green), after the key and before the crop. It is for warm art (dust, clay) painted on #FF00FF whose soft edges keep a salmon cast the keyer leaves because the pixel is opaque with little magenta dominance: Thorn Tithe's crack. Tan and brown never have blue at or above green.

`"alpha_floor": 40` on a recipe zeroes every alpha at or under that value in each cell, before the sequence. It is for authored-alpha sheets (`keyed: false`) whose key left dark outline fringe and stray specks at low alpha: Ashen Reckoning's three step-4 sheets. A sequence step's `"shift": [dx, dy]` moves that cell's drawing by whole pixels (right, down) inside its cell before the step's scale and turn, to register a drawing the artist placed off-centre.

### Layers: a cast that is more than one drawing

`layerFormat: 1` replaces the single `path`/`seconds`/`impactFrame` block above
with an ordered array of layers. A block authors ONE form or the other and the
content build refuses one that authors both.

Reach for it when a cast is more than one drawing: a projectile with a wake and
a shed of droplets, a contact animation that arrives after the flight, a shared
ground sequence under a whole formation. A single sheet on a single anchor is
still a single block, and every spell that shipped before the pilot still is
one -- the adapter turns those into layers at cast time, so there are not two
timing implementations, and re-authoring one buys nothing on its own.

```json
"vfx": {
  "layerFormat": 1,
  "hitCueSeconds": 0.35,
  "sfxPath": "Audio/Sfx/...",
  "layers": [ { "id": "core", "render": "sprite", "place": "caster-centre", ... } ]
}
```

**The cue is authored in seconds and nothing derives it.** `hitCueSeconds` is
the one instant the blow lands: the damage number, the flash, the hit-stop and
any layer scheduled `at: hit`. A pre-layer block derives that instant from
`impactFrame` over the number of PNGs on disk, so re-cutting a sheet retimed the
gameplay; a layered block cannot, which is the point of the format.

| field | default | what it does |
| --- | --- | --- |
| `id` | — | Name, needed only when another layer rides it through `place: layer:<id>`. |
| `render` | refused blank | `sprite` (an animated folder), `still` (one frame of a folder, held), `emitter` (ballistic particles). |
| `place` | refused blank | `caster`, `caster-centre`, `target`, `target-centre`, `target-head`, `formation`, `sky`, `above`, `counterpart`, `counterpart-centre` (the other end of the cast's move, below), or `layer:<id>`. |
| `height` | `0` | `place: above` only, and required there: how far over the struck body's middle the layer sits, in body heights (the visible body's height). A travelling layer leaves from there and falls onto the middle. Refused on any other placement. |
| `at` | `release` | `release` (the beat opens), `arrival` (the cast's projectile lands), `hit` (the authoritative cue). |
| `offset` | `0` | Seconds added to `at`. |
| `path` | — | `Resources`-relative FOLDER of `f0..fN`, for `sprite` and `still` alike. |
| `seconds` | derived | Total length; `0` fits the folder to `fps`. |
| `fps` | `0` | `0` means fit the whole folder into `seconds`, which is what every pre-layer block becomes. |
| `startFrame` | `1` | Which frame of the folder it opens on, counting from 1. Range-checked against the folder. |
| `until` | `once` | `once`, `loop`, `hold`. Describes what the sheet does while it is alive, never how long it lives. |
| `fade` | `0` | Seconds of alpha ramp after the layer's end. Capped at `SpellLayerRules.MaxFadeSeconds`. |
| `opacity` | `1` | How solid the layer is drawn for its whole life, above 0 and at most 1; multiplies the fade (and an emitter's particles). For a wash drawn over a body -- the Bellwether's toll ripple is `0.5` on `sort: ground` so its rings never hide the figure they ring from. Not a brightness: that is `glow`. |
| `travelSeconds` | `0` | Non-zero makes the layer a projectile, crossing from its anchor to the target. Only a caster word or `sky` has somewhere to leave from. |
| `orient` | `none` | `path` turns the drawing so `artDegrees` lies along its flight, with the impact point riding the line from launch to aim. Travelling layers only. |
| `artDegrees` | `0` | Which way the drawing points as painted, degrees anticlockwise from +x. Read only by `orient: path`. |
| `travelDelay` | `0` | The wind-up held at the caster before the motion starts. |
| `through` | `false` | The projectile flies on through its target instead of stopping: the hit still lands at the arrival, then the layer keeps its arrival speed on the same line until it is fully off the stage, and ends. Travelling layers only. |
| `ballistic` | `false` | Constant speed along the line, like a thrown thing, instead of the projectile's accelerating ease. Implied by `drift.arcY`; set it on a layer that must keep pace with one, such as a ground shadow under a lob. Travelling layers only. |
| `follow` | `false` | Ride the anchor body: placed on it when the cast opens, then displaced with it for the layer's whole life (a bind on a figure a pull drags). Body placements only (`caster`, `target`, `counterpart` and their centres), not a projectile; a layer on `layer:<id>` already follows. |
| `whenConsumed` | blank | Plays only if the cast spent that status on its target: a status name (`Poison`) or `any`. Blank always plays. A layer riding a gated one carries the same gate. |
| `dx` / `dy` | `0` | Local offset from the anchor, in reference-frame units. `dx` mirrors with the cast. |
| `size` / `scale` / `aspect` | `380` / `1` / square | The box the art is fitted into. `place: formation` measures its own span and ignores `size`. |
| `fit` | `none` | `none` (the authored `size`, verbatim), `target` (multiply by the struck target's visible body over a front-rank Giant Rat's) or `head` (multiply by the struck head's on-stage height, `headSize` x body height, over the rat's: `TargetBody.ReferenceHeadExtent` = 79, so a layer authored on a rat's head keeps its size there). Legal on `target`, `target-centre`, `target-head`, `sky` and travelling layers -- see below. The single block takes it too. |
| `impactX` / `impactY` | unset | The same correction the single block's take, per layer. `formation` is exempt from `impactX`. |
| `sort` | `effects` | `ground` (behind the racks) or `effects` (over the HUD, under the damage numbers). |
| `facing` | `auto` | `auto` takes the cast's facing, `none` never mirrors, `reverse` flips it. |
| `align` | `level` | `place: formation`: `level` is an axis-aligned box on the rank's mean ground line; `span` runs the box from the leftmost struck body to the rightmost and rotates it to that line. `place: layer:<id>` on a travelling layer: `flight` lays the layer along that layer's flight (see "A wake left along the flight" below). Refused on any other placement. |
| `overhang` | `0` | `place: formation` only: how far the span reaches past the outermost struck bodies, as a fraction of each end body's half-width. A lone body's span is only its own width, so a ground drawing that wide sits behind the figure and is never seen; `0.5` makes it show. Max `SpellLayerRules.MaxOverhang`. |
| `punch` | `0` | Extra scale at the opening instant, eased out over the first fifth of the layer's lifetime. `0.22` opens it 22% oversized. Capped at `SpellLayerRules.MaxPunch`. |
| `drift` | unset | `{fromX, toX, fromY, toY, via*, arcY}`: a slide across the layer's life, in fractions of its own drawn box (so it scales with `fit`), eased out, X mirroring with the cast; added to `dx`/`dy`. A figure that leans in and a mist that parts. On a travelling layer it runs over the flight (held at its start while the layer waits at the caster, at its end after arrival), and `arcY` adds a parabola of that many box heights at mid-flight: a lob. A flight that authors `arcY` is a throw: it keeps a constant speed along its line (`SpellPerformance.Ballistic`) instead of the projectile's accelerating ease, so with the arc on top the path is one parabola in time, rising and then falling faster as the line drops. `SpellDrift`, pinned by `CourtOfWhispersTimingTests` and `MudBurstPresentationTests`. |
| `fadeIn` | `0` | Seconds of alpha ramp-in from the opening instant (contract 4: no pop-in); pairs with `fade`. Capped at `SpellLayerRules.MaxFadeSeconds`. Inert on an emitter (refused). |
| `rotate` | unset | `{degrees, spin, amplitude, period, damping, phase, delay, swings, pivotX, pivotY}`: one angle, `degrees + spin*t + amplitude*sin(2*pi*t/period)*e^(-damping*t)`, counter-clockwise in the cast's frame (mirrors with the cast), turned about a pivot given as a fraction of the frame (x from the left, y from the bottom; default the centre, `0.5/1` is the top centre of a hanging thing). `swings` stops the pendulum after that many periods (fractions count); 0 runs it until the layer ends. The pendulum starts at rest on the vertical, so a negative `amplitude` swings clockwise first; a negative `damping` grows the swing; `phase` 90 starts it released from the extreme (a cosine) and `delay` holds the start angle still for that many seconds (the fade-in). The pivot may lie outside the frame (`pivotY` 2.5 hangs the art on a chain 1.5 frames longer than it draws): the layer then travels a wide arc and tilts little. An emitter placed `layer:<id>` on a turning layer is born at a point fixed on that layer's art (`sourceDx`/`sourceDy` from its centre), carried along its arc. Pivot, `degrees`, `spin` and `amplitude` are range-checked. `SpellRotate`, pinned by `SpellRotateBreatheTests` and `CenserOfEmbersPresentationTests`. Refused on an emitter and on `align: flight`. |
| `breathe` | unset | `{alpha, scale, rate}`: a flutter for a held or looping layer that must not hold still (contract 3). Alpha dips by up to `alpha` of itself and scale swells by up to `scale` either side, both riding `FlickerCurve.Ember` normalised to -1..1; `rate` multiplies the curve's pace (1 is a slow swell, 3-5 reads as flame). Never brightens past `opacity`. Each layer index starts at its own point on the curve. `SpellBreathe`. Refused on an emitter. |
| `glow` | `0` | How far above 1 the layer's brightest pixels (an emitter's particles, each) are pushed, so the Volume's Bloom can see them. Nothing else on this canvas can exceed 1. |
| `glowCurve` | unset | `{to, pulse, period, crest, knee}`: moves a sprite or still layer's `glow` over its life: a straight ramp from `glow` to `to` at the layer's end, plus a pulse of `pulse` that crests at `crest` seconds and every `period` after (none earlier than half a period before the first). `knee` (0-1, default the shader's 0.72) is the brightness above which the boost lifts a pixel: the default reaches only the hottest highlights and does nothing to a gold lantern, so the censer lowers it to 0.4. The censer's lantern ramps 0.2 to 0.8 and pulses 0.7 at the bottom of each swing. `SpellGlowCurve`, pinned by `SpellPolishFieldsTests`. Refused on an emitter. |
| `unkicked` | `false` | Stays put when the stage is kicked (`vfx.kick`): for a layer or emitter hung in the air (the censer, its crack, the embers born on it), so a kick never displaces it. Figures, ground and body-placed layers still move. |
| `stoke` | `0` | 0-1. While the layer lives, the Burn coal glow (`StageBurnGlow`) of the body it is placed on is lifted by `StageBurnGlow.StokeBoost` (0.3, capped at alpha 1) x `stoke` x the layer's own alpha as a share of its opacity, so its fades and `breathe` flutter the glow with it. The censer's fire sets 1. `SpellFrameCursor.StokeOf` to `SpellPerformancePlayer.StokeAt` to `FightController.ApplySpellStage`. Refused on an emitter. |
| `tint` | blank | `#RRGGBB` multiplied into a layer's frames, or an emitter's particles, so one painted sheet can serve a material it was not painted in (Mud Burst's gold sheet drawn as matte brown, the censer's smoke wisp as grey ash). It only darkens or shifts; brighter is `glow`. `SpellTint`, pinned by `SpellTintTests`. |
| `emitter` | — | The particle block; inert unless `render` is `emitter`. |

**The stage's push on the blow.** The vfx block's `kick` (`{strength, rise, fall}`, beside `shake`) shoves the stage toward the target on `hitCueSeconds`: up to `strength` of the stage's width (0.025 is 48 px of 1920; at most 0.1) over `rise` seconds, eased back over `fall`. It moves the figures (both stage racks, through `StageShake.SetNudge`, riding under any shake noise) and the cast's own layers and particles, so the spell and the bodies move as one; the HUD stays put. Authored seconds on the cast's clock, so the battle-speed preset scales it; presentation only, never read by combat or input. `SpellKick`, `SpellPerformancePlayer.StageKick`, pinned by `SpellPolishFieldsTests` and `SpellCenserStageTests`.

**A price paid by the caster lands after the payoff.** The beat paints every plate at the cue, so a status the cast puts on its caster (Court of Whispers' Vulnerable) would share the payoff's frame. The vfx block's `casterCostSeconds` holds the caster's *newly applied* statuses back that long (`FightBeatPlayer.WithoutCasterStatuses`, charged to the beat's settle); author the caster's pulse as `place: caster-centre`, `at: hit` with the same `offset`.

**A move has two ends, and the beat names the second.** A swap (Palace Passage)
or a pull (Dark Chains) records `CombatBeat.PreFormation` (where everybody
stood), `Counterpart` (the figure that traded seats, null for an empty seat) and
`PlacementHoldSeconds` (how long after the cue the stage paints the move). A
pull holds `CombatBeat.PullHoldSeconds` (0.3 s) so the bind is seen closing; a
swap moves ON the cue (0), so the seats change at `hitCueSeconds` behind the
doors the layers draw, not at the beat's first frame. `counterpart` /
`counterpart-centre` are cast-level (one instance, not a fan-out) and draw
nothing when there is no other end. To tether a layer to a figure through its
displacement, `follow: true` on a layer long enough to cover the glide
(`seconds` past the cue + hold + `StageActorAnimator.GlideSeconds`, `until:
hold`); an accent at the moment the pull starts is `at: hit`, `offset:
0.3`.

**A gate on what the cast did.** The session records every status a cast
spent (`CombatBeat.ConsumedStatuses`: a detonated Poison, a consumed
`ConsumesStatus`), and `whenConsumed` plays a layer only then, so Viper's
Bite's "set off what is already there" is drawn only when something was.

**Placement decides instancing, and no spell id ever appears in code.**
`formation` and the `caster` words are cast-level -- one instance however many
enemies were struck; `target` and `target-centre` fan out over the struck list.
That is the whole of how Cinderfault draws one fault under three eruptions.

**A rank is a diagonal, so a line drawn across one has to be.** Both formations
recede: the enemy's runs (300, -218) -> (660, -125) and the party's
(320, -218) -> (810, -64) (`FightStageAnchors`). A `formation` layer left at
`align: level` is an axis-aligned box on the rank's MEAN ground line, which is
right for a footprint -- a pool, a shadow, a wash across the floor -- and wrong
for anything that IS a line: at `level` a three-body span misses both end
bodies' feet by half the rank's rise. `align: span` takes the two outermost
struck bodies, extends the line past each by that slot's OWN half-width (the
ends sit at different depths and are drawn at different sizes), and rotates the
box onto it; the sheet's `impactY` correction then moves along the box's own up
rather than along screen +Y. The geometry is `Domain.Stage.FormationSpan` and is
pinned with literals by `FormationSpanTests`.

**Every target-side placement aims at the visible body, not the slot.** A slot
is the sprite's canvas, cut for the actor's tallest pose: 62 empty rows over
the golem's head, a rat sitting 38px left of its canvas centre. `target`,
`target-centre`, a traveller's arrival, `sky` and a `formation` span all read
`Domain.Stage.TargetBody` instead -- the idle drawing's opaque box (measured
off its alpha, cached per fight), placed through the slot's origin at its
resting scale. `target-centre` is the middle of that box; `target` is its
middle column on the authored ground line. An actor with no readable art falls
back to the slot rect. Pinned with literals by `TargetBodyTests`.

**`fit: target` is a multiplier, not a replacement.** `size` (or the
`DefaultSize` fallback) is still the base number; `fit: target` multiplies it
by the struck body's larger extent (width or height, on stage) over
`TargetBody.ReferenceExtent` = 280, which is a lone front-rank Giant Rat. So an
authored size keeps meaning "the box on a rat", a back-rank rat gets a little
less, and an Elder Treant gets 1.63x. Measured off the body rather than off
the old stageScale multiplier, which ignored how the art is proportioned, and
off the RESTING scale, so a squash mid-hit does not leak into the size. An
emitter fitted this way scales its particle size, launch speed and source
offset by the same factor. Legal wherever one struck body is resolved --
`target`, `target-centre`, `sky`, and any travelling layer -- and refused
everywhere else (`formation` measures its own span; the caster words never
touch a target). The single-block `vfx` takes `fit` too, refused on a caster
anchor. Every per-target layer in `skills.json` authors it.

**`sky`: a called strike amasses in the air, then fires.** `place: sky` is the
point halfway across from where the cast leaves the caster to the target's
middle, `SpellFlight.SkyLift` (110) above the taller of the two bodies, capped
at `SpellFlight.SkyCeiling` (240) so a treant does not push it into the HUD.
Per target. A non-travelling `sky` layer is drawn centred on it (the amass); a
travelling one leaves from it and arrives at the target's middle (the shot).
Pair the shot with `orient: path` and the sheet's painted direction as
`artDegrees`: the box is turned after the mirror, and the stated
`impactX`/`impactY` (the spear's tip, not its middle) rides the line from the
air point to the aim. Winter's Rebuke and Blackglass Spear are
authored this way (Crownfall drops its spike from `above`, below); the geometry is
pinned by `SpellFlightTests`.

**`above`: a layer that falls onto its target.** `place: above` is the point
straight over the struck body's middle, `height` body-heights up
(`SpellFlight.AbovePoint`: the body's own column, held under `SkyCeiling` so a
treant is dropped on from less far, never below the middle). Per target, and the
launch of a travelling layer by the same rule as `sky`: it leaves the point and
arrives on the target's middle. Where `sky` is the diagonal between the two
casters and reads as a strike called in, this is the vertical one. `travelSeconds`
is the fall and is fit-independent, so it takes the same time on a rat and a
treant; the distance scales with the body because the height is in body heights.
Pair it with `orient: path` and the sheet's painted direction (`artDegrees`
270 for art painted tip-down) and the stated impact point (the tip) so the tip
lands on the middle. Non-travelling, it draws centred on that point (an amass
over the target). Crownfall's spike is authored this way; the geometry is pinned
by `SpellFlightTests` and the rules by `SpellLayerRulesTests`.

**A projectile that does not stop at its target: `through: true`.** The arrival
is still the hit (cue, `travelSeconds` and sound unchanged). Past it the layer
continues on the same line at the speed it arrived with (the ease is `u*u`, so
that is its end speed, twice the average; `SpellPerformance.FractionOf` carries
the continuation) until the whole box is off the stage, then ends. The extent is
`SpellFlight.ExitReach`, measured by `FightController.FlyThrough` against the
effect pool's rectangle with the box diagonal as clearance, and handed to
`SpellPerformance.SetFlightReach`, which re-times the layer's end, its
followers and the cast's clearing. A wake laid `align: flight` along it spans
launch to exit and is revealed behind the tip. Per target: each instance flies
through its own target. Refused on a layer that does not travel.

**A wake left along the flight: `place: layer:<id>` + `align: flight`.** A
layer that names a travelling layer and says `align: flight` lies along that
layer's line of flight, launch point to aim point: centred on the line, turned
onto it (into -90..90 degrees, so a sheet is never upside down on a leftward
cast), stretched rather than letterboxed, `size` thick (times the projectile's
own `fit: target` factor, so it is as thick as the body the projectile ends
in). Only the part of the line the projectile has already crossed is drawn, so
it grows out of the launch point behind the tip on the projectile's own ease;
after the arrival it is held and faded on its OWN `seconds`, `until` and
`fade`, which is the point: it is what the projectile leaves behind. A
follower normally ends with the layer it rides and a flight layer does not.
Per target, like the projectile it names. Refused: a source that does not
travel, an emitter, its own `travelSeconds`, `dx`/`dy`. Author it BEFORE the
projectile so the projectile draws over it. The geometry is
`Domain.Stage.FlightSpan`, pinned with literals by `FlightSpanTests`; the
launch and aim are written by `FightController.SpellVfx` as
`SpellLayerInstance.FlightFrom/FlightTo`.

Authoring one (Blackglass Spear's `rift`) is the same loop as any still:

1. Pick a drawing that already lies along one axis and reads at any length: a
   lens, a streak, a vapour line. A frame painted on a diagonal is turned flat
   by a `sequence` step's `turn` (degrees anticlockwise, about the pivot,
   stacks with `spin`; alpha at or under 2 is cleared afterwards so
   `content_crop` trims to the ink): `blackglass_spear_rift` is the atlas's
   mirror-splinter cell turned -50 degrees (its principal axis measures 49.8)
   and cropped to 271x66.
2. `python tools/slice_spell_sheet.py <id>_rift`, one recipe per folder.
3. Add the layer: `render: still`, `place: layer:<projectile>`, `align:
   flight`, `at`/`offset` equal to the projectile's so the reveal starts as it
   leaves, `seconds` long enough to outlive the arrival by what the cast should
   linger, `until: hold`, a `fade`, `size` as thickness, `fit: target`.
4. `tools/preview.ps1 -Spell <id> -Versus rat` and `-Versus treant`: the
   `_tail` picture shows what lingers after the blow, and the thickness must
   hold on both bodies.

**A called strike's pieces, and what is held back from the HUD.** A sky spell
is authored as layers on one clock: the amass (a `sprite` on `sky`, `fit:
target`), the projectile fired at the end of it (`offset` = the amass length;
`hitCueSeconds` = that offset plus `travelSeconds`), a flash on the air point
at the instant of firing, a rent held on the air point and one on the struck
body (`at: hit`, long `fade`), and the wake along the flight above. Two rules
apply to every layer without being authored. A layer opening at the hit on the
struck body (a tail) is drawn at most `TargetBody.TailMaxOfBody` (0.75) of the
body's larger extent, so the body shows around it; release-time layers (a ring
meant to span the body) are exempt. No layer is drawn into the combat log's
band: a flight whose air point meets the ceiling is shrunk about its own
anchors until its top clears the log (`SpellFlight.ScaleUnder`, never below
`MinFitScale`), so against a tall target the drawing is smaller and the air
point and the aim do not move.

**Bloom is reachable and nothing reaches it without `glow`.** Every
precondition is already true -- `supportsHDR` on the pipeline asset, `render
PostProcessing` on the camera, the canvas in `ScreenSpaceCamera`, a `Bloom`
override in the profile -- but the threshold is 1.05 and a Canvas `Image`
cannot exceed 1.0: sprite textures top out at white and a `CanvasRenderer`'s
vertex colour is a `Color32`, so `image.color` clamps. `glow` swaps the layer
onto `PrincesPalace/UISpellGlow`, which multiplies AFTER the sample and ramps
the boost in over the top of the sprite's own luminance, so the painted body of
an effect is untouched and only the hot core climbs past the threshold. There
is no tonemapper in the profile, so a FLAT multiply would clip every midtone to
white -- which is why it is a knee and not a gain.

**Authored order is draw order** within a band: a cast takes pool members in the
order its layers are written, so a spray authored after a splash draws over it.
There is no z-order to author and nothing to keep in sync.

**One recipe per layer folder.** A layered spell's folders are cut by the same
`tools/slice_spell_sheet.py` as any other, one recipe each, because a recipe IS
the provenance record `SpellVfxRecipeDriftTests` checks -- it walks
`layers[].path` and `layers[].emitter.path` and refuses a folder no recipe
explains. The pilot is four recipes where a single-block spell is one:
`prismatic_orb_water_core` (6 frames), `_contact` (9), `_wake` (1 still) and
`_drops` (8 particle stills). Cutting them from one recipe would give four
layers one provenance and one set of slice numbers, which is exactly the drift
that lint exists to catch.

**A `still` names a FOLDER, not a file.** It draws that folder's `startFrame`.
The drift lint matches a played path's last segment against recipe filenames and
requires it to be a directory, so `..._wake/f0` would present `f0` as its
provenance and fail both halves.

**Uneven atlases**: state `rects`, and `canvas` when a `scale` step needs
headroom -- both above, both earned by the water pack's two 1774x887 sheets.

**The emitter block**, one field each and nothing else: `path`, `rate` and
`window` (or `burst` for all at once, or `burstTimes` + `burstEach` for `burstEach` more at each listed second after the layer opens, ascending: sparks at the turns of a swing), `sourceDx`/`sourceDy`,
`spreadDegrees`/`aimDegrees`, `speedMin`/`speedMax`, `inherit` (how much of the
source's velocity a particle is born with), `drag`, `gravity`,
`lifeMin`/`lifeMax`, `sizeMin`/`sizeMax`, `spinMin`/`spinMax`, `fadeFrom`,
`endScale`, `seed`. A particle's position is a closed form over its age, so a
long frame or a 60x test speed-up lands it where a hundred short frames would.
An emitter placed on a travelling layer sheds ALONG the flight: each particle is
born at its own instant and sampled where the source was then. Two more fields
shape a shed laid on a flight, both legal only on `place: layer:<id>` of a
travelling layer: `alignFlight` turns every particle's art (painted pointing
right) onto the line from launch to aim, and `lateral` scatters births across
that line within +-`lateral`/2 (scaled by `fit`). Together with a thin glowing
sprite they make speed lines: Blackglass Spear's `streaks` layer is 170 a second
of `speed_streak` (baked by `ProceduralSpriteBaker.BakeSpeedStreak`, no painted
art), each living 0.1-0.2 s, so the line of them is gone almost as the spear
has passed. The spear's own wake (`rift`) is held 0.2 s and faded 0.15 s for the
same reason; `fit: target` is legal on any layer riding another.

**Previewing one.** The Python preview draws sprite layers only and says so; an
emitter is previewed by running the real code:

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File tools/preview.ps1 -Spell <id>
powershell -NoProfile -ExecutionPolicy Bypass -File tools/preview.ps1 -Spell <id> -Element Wind
powershell -NoProfile -ExecutionPolicy Bypass -File tools/screenshot.ps1 -Runtime -RuntimeFilter SpellRuntimeCaptureTests -Spell <id>
```

`preview.ps1` writes four to six sampled stills to
`tools/screenshots/preview/spell_<id>[_<element>][_vs_<ids>]/`, a folder emptied
at the start of each run so it only ever holds the latest run's frames
(`_before`, `_impact`, `_after`, `_tail`, plus `_flight` when the cast's arrival
is before its cue and `_ground` when it draws a ground layer). **`-Element`
picks which element of a choice skill is cast** -- without it the preview takes
the first element in authored order that draws anything, which with four drawn
elements always means the first. The name is a `DamageType`, matched
case-insensitively and checked against that skill's own `elements[]` before
Unity starts; an element it does not offer is refused with the list of the ones
it does. The pictures are prefixed `spell_<id>_<element>_` so the four sets sit
side by side, one folder each, instead of overwriting each other, and the fight log names the
element it pressed and whether the ask or the default chose it. `screenshot.ps1
-Runtime` writes the whole cast as a frame series at 60fps to
`tools/screenshots/runtime/`, which is the only thing that answers "does it read
as heavy" -- and it WIPES the previous run's PNGs, so copy a comparison set out
first. `ffmpeg -framerate 60 -i spell_<id>_f%02d.png out.mp4` makes it a video.

`-Spell` defaults to `prismatic_orb`. The two overlap tests in that class are timed
against the pilot and Ignore themselves for any other spell; a failing test no
longer discards the frames a passing one wrote (they are copied back, listed,
and the run still exits 1). Which effects the preview and the runtime capture can
stage is `PreviewStage.Supported` (Domain/Preview): Afflict casts at one enemy,
SwapAllies fields a second squadmate no faster than the caster so both picks
land. Reclaim, Hasten, Ward, Shatter, the Gifts and Provoke are refused by name.

### A monster casting it

A monster's abilities are real skills, drawn weighted, in `enemies.json`:

```json
"abilities": [ { "skillId": "mud_burst", "weight": 2 } ],
"attackWeight": 3
```

Weights are **relative**, not probabilities — 2 against 3 is 40%, and adding a
third entry does not require rebalancing the first two. The basic attack is
always in the pool (`attackWeight`, default 1; `0` removes it), because a
monster whose every turn is a special reads as scripted rather than dangerous.

A monster reaches the whole `SkillEffect` vocabulary this way, and every skill
written for a character in future is available to monsters for free. Targeting
is side-relative: `DamageAll` cast by a monster hits your party, `HealParty`
mends its own side.

The telegraph is derived, never authored — its icon from the skill's effect and
status, its scope (`one` / `your whole party` / `itself` / `its allies`) from
the effect alone, and its magnitude from the same formula the resolution runs.
An author who could disagree with any of those could make the telegraph lie.

A skill id that names nothing **fails the content build**. At play time the
adapter would drop it with a warning, and the symptom of that is a boss quietly
easier than authored — which does not look like a bug.

The old `skillName`/`skillPower`/`skillChance` trio still works and still means
what it did; internally it becomes the same two-entry weighted pool. Migrating a
monster is a content decision, not a plumbing one: `"attack x 1.8"` has no
equivalent skill id until somebody writes the skill it should have been.


### Multi-target

An effect that lands on more than one thing draws on each of them. The beat
carries `SplashTargets` and the pool holds one member per stage slot; nothing
in content asks for it, because "this skill hits everything" is already said by
`effect: DamageAll`.

### Art that is NOT reproducible

`Assets/_Project/Art/Sheets/hand_assembled.json` carries two registers.
`sequences` names spell frames the slicer must never write over — hand-cut
frames with held duplicates or stepped fades that re-running the tool does not
restore; the slicer refuses those ids outright. `actors` names combat actors
whose stance stills predate recipes and cannot be regenerated from a recorded
invocation. `HandAssembledArtTests` pins both by content hash so a clobber from
any direction fails the suite, and refuses an actor that is in neither category
or in both.


## 5c. Melee contact effects (`Resources/Vfx/{name}/f0..fN`)

A third VFX root, and the reason it is not `Resources/Spells/` is ownership: a
spell effect belongs to a SKILL and is authored in `skills.json` beside its
damage. These belong to no skill at all. They are the house's default contact
language for a plain swing -- the beat that authors nothing -- and
`FightBeatPlayer` decides when they play from the class of beat rather than from
content.

Two sequences today, both **generated rather than delivered**:

    Resources/Vfx/slash_arc/f0..f5.png       the attack graphic
    Resources/Vfx/impact_burst/f0..f5.png    the impact burst

`tools/make_contact_fx.py` draws them from polar arithmetic -- there is no sheet
to cut and nothing to key, so none of §2's keying conventions apply. Re-running
it is the only way to change them; editing the PNGs by hand puts them in the
same position as the hand-assembled art §5b's last section describes, without
any of the protection.

Three conventions the runtime depends on, all stated in the tool's own
docstring and worth repeating because breaking one is silent:

- **The impact sits at the exact centre of the frame**, both axes, every frame.
  `FightController.PlayContactFx` aims the box centre at the target's content
  centre and applies no impact-point correction -- unlike a spell, which can
  author `impactX`/`impactY`. A sequence whose bright part drifts off centre
  lands beside the body.
- **Alpha is the luminance falloff**, the same synthesis `slice_spell_sheet.py`
  applies to delivered glows.
- **The arc is drawn sweeping left to right.** A monster's blow mirrors the
  whole sheet through `SpellVfxPlayer.SetFacing`, which needs a direction to
  mirror.

`StanceSpriteImporter` covers `/Resources/Vfx/` alongside the stance and spell
roots, so a PNG dropped here imports as a readable Sprite. Without that it loads
as `null` and the effect simply never appears -- that importer's header records
the same trap catching `Resources/Spells/` for four months.

The two clips these fire with are **placeholders**; see
`Resources/Audio/README.md`. Paths and durations for all four assets live in
`Core/ContactCues.cs`.

### Blunt contact language: burst only, no arc

Relocated from `docs/STATIC_COMBAT_ART_DEEP_DIVE.md` (archived) — the one rule
of that doc's attack-family table still cited elsewhere. A Blunt-family beat's
contact language is deeper anticipation, a slower outbound, and a large target
squash, paired with a longer hit-stop, dust chunks and a stronger low-frequency
shake — **burst only, no slash arc**. `slash_arc` is a Slash-family cue; a Blunt
swing should play `impact_burst` alone.


### Capture GIFs: `gif.ps1 -Out` and `gif.py sheet`

`tools/gif.ps1 spell <id> -Vs rat|treant [-Speed] [-PreStatus]` records a cast
(runs live under `tools/screenshots/gif/runs/`, pruned at the 5 GB cap).

- **Keep a capture:** add `-Out <folder> [-Name <prefix>]`. After `finish` it
  copies `anim.gif`, `anim_preview.gif`, `strip.png` and `timing.json` to
  `<prefix>_<target>[_<speed>x][_<formation>][_<status>]_{full.gif,preview.gif,strip.png,timing.json}`
  (prefix defaults to the id). Point it at the spell's NOTES folder.
- **Compare frames:** `python tools/gif.py sheet --frames 6,12,20,22,26,40 --in rat=<gif|run> --in treant=<gif|run> --out <png> [--crop 0.55,1.0]`
  writes one PNG, a row per `--in`, tiles labelled `<label> f<N>`. Frames are
  60 fps capture numbers (the GIF holds every second one); the crop is the
  x-range as fractions of width, default the enemies' side. A run folder with
  several scenarios must be narrowed to one scenario folder.

## 6. Per-kit README index

- `Assets/_Project/Art/UI/Hub/README.md`
- `Assets/_Project/Art/Items/Relics/README.md`
- `Assets/_Project/Art/UI/TalentTree/README.md`

Each documents its kit's exact expected filenames and any kit-specific
sizing/tiling requirements.

### Description icons (`Resources/Icons/DescriptionIcons/`)

The inline icons in skill, talent and relic descriptions (`{fire}`, `{mana}`,
...) are one TMP sprite asset packed from `Resources/Icons/Ability/*.png`
(64px), listed in `DescriptionMarkup.Icons`. After changing one of those PNGs or
the list, run `Prince's Palace/Generate Description Icons` (or
`-executeMethod TmpBootstrap.GenerateDescriptionIcons`, Editor closed) and
commit `DescriptionIcons.asset`, `DescriptionIcons_Atlas.png` and their `.meta`
files. The generator reads the PNGs as bytes, so the source icons' importer
settings are untouched; diff `Art/` and `Resources/Icons/Ability` after the run
anyway. A sprite asset missing an icon the list names makes every icon render
as its plain word (`TmpAssetResolver.IconSprites`). Wool, HP, gold and Fury
have no icon art and stay words.

## 7. Outstanding tracker

- **`Relics.png`** — the Relic screen's background, 1672×941, opaque, no
  keying needed. `SceneBuilder` already looks for this exact filename in
  `Art/Backgrounds/`; dropping the file in is the entire remaining step.
- ~~**Talent Tree kit wiring**~~ — done since Phase 7 (2026-08-01): the art
  (`orb_lit`/`orb_unlit`, `branch_tile_set`, `activation_line`) is wired
  into `SceneBuilder.Talents.cs`/`TalentController`. A later pass (the
  full-bleed/tree-shape rework) added a root crest and angled limbs built
  from the same kit; a dedicated `trunk_base.png`/`branch_fork.png` would
  replace that placeholder without touching anything else, but nothing is
  blocked on it.

- **Victory-pose stills**, one per playable character (Shawn, Odette, Bjorn).
  Progression v2's level 37 grants a victory pose and §5 authors it as
  "(art)" — a still nobody has drawn. **The pose has no plate rendering at
  all until the stills exist** — a 2026-09-16 code review found the word
  phase 5 meant to ship on the fight party plate ("VICTOR", beside the
  chosen title) had no renderer reaching it (only `IdentityLook.PlateWord`
  ever reached a TMP label, never the field the pose word lived on), so the
  dead field and its `VictorWord` constant were deleted rather than kept as
  unreachable content. The node still shows as collected on the reward-track
  screen and the hub roster (`RewardTrackDefinition.CollectedIdentity`) —
  only the plate itself draws nothing for it. When the stills land, give the
  pose its own rendering on `Core/CharacterIdentity.IdentityLook`. Format is
  the usual key-pose still, one canvas per actor via `slice_actor_sheet.py`
  (§4b).
- **Twelve reward-track card medallions**, in the talent tree's own painted
  hand (gold on a dark ground, ~350px, circular), 86px delivery. Phase 5
  gave the reward track a medallion only where an existing talent icon
  honestly means the reward — nine of twenty-one kinds — and the other
  twelve draw a short word in the kit's type on the card's own art plate.
  The tokens, which are also the brief, are in
  `RewardTrackLayout.CardGlyphFor`: RESET, CAP, /TURN, HURT, SOAK, ELEM,
  SPELL, -MANA, -COST, +HIT, +POWER, CREST. **Nothing is blocked on these**
  — a word is a perfectly good label — but the four that name a number a
  player tunes a build around (ELEM, SPELL, -MANA, -COST) are where a
  picture would earn its commission first.

## 8. Model and tool licences — read before adding an AI step

This is a **commercial** project, so a non-commercial model anywhere in the art
pipeline is a real problem, not a technicality. The traps below are all cases
where the obvious choice is the wrong one, usually because a permissive badge
on the code repo hides restrictive terms on the *weights*.

### Do NOT use

| Item | Why |
|---|---|
| Depth Anything **V2 Base / Large / Giant** | Weights are **CC-BY-NC**. The GitHub repo is Apache-2.0, which misleads — and most tutorials and ComfyUI workflows default to Large. |
| **BRIA RMBG-1.4 / 2.0**, including `rembg -m bria-rmbg` | **CC-BY-NC**; commercial use needs a paid agreement. `rembg` itself is MIT, but its licence does **not** propagate to the model weights it downloads. |
| **FLUX.1 Fill [dev]** | Non-commercial licence. (`FLUX.1 schnell` is Apache-2.0; the *Fill* variant is not.) |
| **3D Photo Inpainting** (vt-vl-lab) | MIT main code, but bundles EdgeConnect under **CC-BY-NC**. |
| **Apple Depth Pro** | `apple-amlr`; commercial terms unresolved upstream. |
| **Stable Video Diffusion**, **HunyuanVideo** | Revenue-threshold and geographic restrictions respectively. |
| Any GitHub repo with **no LICENSE file** | All rights reserved by default. Several popular 2D-parallax Unity repos are in this state. |

### Safe, and what to reach for

- **Depth maps:** Depth Anything **3** (Apache-2.0), or Depth Anything V2
  **Small** (Apache-2.0), or **Marigold** (Apache-2.0).
- **Inpainting:** **LaMa** via **IOPaint** (both Apache-2.0). Better than SD
  inpainting here because it extends existing texture rather than inventing a
  new subject, which is what keeps a fill stylistically identical to painted art.
- **Cut-out:** `rembg` (MIT) **pinned explicitly** to `birefnet-general` (MIT)
  or `u2net` (Apache-2.0). Never leave the model unpinned.
- **Upscale:** Real-ESRGAN (BSD-3). Prefer the `anime_6B` model for painterly
  work — the default photo model de-noises brush texture away.
- **Video:** Wan 2.2 or LTX (both Apache-2.0), plus FFmpeg to make a loop seamless.
- **Audio:** Sonniss GDC bundle, Kenney (CC0), ChipTone (CC0 output).

**GPL/AGPL authoring tools used offline** — Krita, GIMP, Blender, chaiNNer,
Upscayl, ComfyUI, Audacity, FFmpeg — do not touch this game's licence. The
copyleft attaches to the software, not to the images or audio it produces. Just
never bundle or link their code into the shipped build.
