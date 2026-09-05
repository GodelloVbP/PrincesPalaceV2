# Art Pipeline

How generated art gets from a raw AI output to a game-ready sprite in this
project, and where each kit's pieces actually live.

## 1. Kit registry

| Kit | Source | Keying | Output | Status |
|---|---|---|---|---|
| Hub buildings | `Art/UI/Hub/` | grouped, green `#00FF00` | `Resources/Hub/<building>/f0.png..` | delivered |
| Relic icons | `Art/Items/Relics/` | direct, mostly real alpha delivered (two files, and so far only two, arrived green `#08D111`-ish and needed the keyer) | `Art/Items/Relics/Processed/` | delivered (36/38) — `dancers_anklet` and `loaded_dice` still on placeholder, see the Relics README |
| Talent Tree kit | `Art/UI/TalentTree/` | direct, green `#00FF00` | `Art/UI/TalentTree/Processed/` | delivered, not yet wired (gated on Phase 7 design confirmation — see `docs/handoffs/talent_tree/`) |
| Portraits | `Art/Portraits/<Character>/` | flood-fill from border (`tools/remove_portrait_backgrounds.py`, `PORTRAITS` manifest) | `Art/Portraits/<Character>/Processed/` | ongoing, per-character — **Sheep does not reproduce, see below** |
| Actor stance sheets | one design sheet per actor | grid slice (`slice_actor_sheet.py`) | one still `Resources/<Enemies\|Characters>/<id>/<stance>.png` per pose, shared canvas (see §4) | ongoing — `docs/STANCE_SHEET_SPEC.md` is the commissioning work order |
| Item/spell sheets | raw sheet per type | grid slice (`slice_item_sheet.py` / `slice_spell_sheet.py`) | one PNG per frame/level, or `{id}/f0..fN` for a spell effect (see §5b) | ongoing |
| Backgrounds | `Art/Backgrounds/` | none — full-frame opaque | same folder | ongoing, one outstanding: `Relics.png` |
| Map icons | raw | flood-fill (`process_map_icons.py`) | `Art/Backgrounds/Processed/` | delivered |
| Six-theme UI kit | `Art/UI/Buttons/` sheets (button plates legacy/3x1/5x1, row 6x1, containers 3x4/9x16/3x2/2x1, flag banners 3x4/9x16), one set per theme (Gold/Crimson/Violet/Blue/Green/Silver) | grid slice (`tools/splice_ui_kit.py`), nominal filenames but measured aspect — see `ButtonPlateArt`/`ContainerArt`'s own headers for the per-shape numbers | `Art/UI/Buttons/Processed/` | delivered |

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

## 4. Actor stances (`Resources/<Enemies|Characters>/<id>/<stance>.png`)

Every combat actor ships **one still drawing per stance** — idle, attack,
cast, hurt, defeated, plus any skill-specific pose (the beetle's
`shell_closed`) — posed procedurally at runtime. No skeletal rigs, no
multi-frame animation sheets: `Core/StanceAnimationLibrary.cs` resolves a
stance to exactly one sprite.

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
- **Re-slicing a sheet can invalidate it.** `StanceManifestValidationTests`
  re-measures the art and fails if an authored ground line has drifted more
  than 8px from it, naming the actor and both numbers.
- **There is no per-stance timing left to author.** `secondsPerFrame`,
  `impactFrame` and `soundFrame` belonged to multi-frame stances and have
  no meaning for a still. A monster's `vfxSeconds`/`vfxImpactFrame` (in
  `ContentData/enemies.json`) still time the *effect* layered over a
  stance — that is unrelated and unaffected by this.

The file lives in `Resources/` rather than `ContentData/` because it is
read at runtime rather than baked — and deliberately **not** under
`Resources/Content/`, which `ContentBuilder` deletes wholesale.

One rule the tooling doesn't enforce for you: **slice every stance of one
actor in a single tool invocation.** The slicer sizes its shared canvas
from whatever it's given in one pass; two separate invocations for the
same actor produce two different canvases, which makes the actor visibly
resize/jump the moment that stance shows. `ActorArtAssertions.AssertOneCanvasSize`
checks this for every stance an actor has authored, and both
`EnemyStageTests` and `PartyStageTests` run it over their own half of the
roster.

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

Arguments on the command line, not a committed manifest — each actor's
`README.md` is what records the recipe (see the spec's own provenance
rule). **"Actor", not "enemy": this covers both sides of the fight stage.**
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
caption colour-coded against the same tolerance band
`ActorArtAssertions.AssertOneDrawScale` asserts (shared by
`EnemyStageTests` and `PartyStageTests`). A creature folder still holding
old `<stance>/f0..fN/` frame folders (mid-migration to this pipeline) is
skipped with a one-line notice rather than guessed at. `--report` has no
dependency on the slicer, so it can render today's *committed* art as a
"before" picture, turning a re-slice into a demonstrable before/after.

### `slice_spell_sheet.py`'s `VFX` manifest

Same idea as `ACTORS`, for spell/ability effects — and it exists because
every parameter used to come from argv, including the frame names, so how
the three shipped effects were cut survived nowhere. `frost_flare` and
`lightning_bolt` are verified entries: re-running them regenerates the
committed PNGs byte-identical.

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

Several of the art tools carry a committed manifest (`VFX`, `PORTRAITS`)
alongside `key_green_screen.py`'s `KITS` and `slice_item_sheet.py`'s
`SHEETS` — where an entry has been **verified**, re-running it regenerates
the committed PNGs byte-identical, which is the proof the recipe is the
real one rather than a plausible guess. `slice_actor_sheet.py` takes its
arguments on the command line instead (see §4b) — for that tool the same
proof lives in each actor's own `README.md`, which records the exact
invocation that produced the committed art.

Where it has *not*, that is recorded too rather than left blank, because a
silent gap reads as "nothing to see here":

- **`golem_boulder`** is in `slice_spell_sheet.py`'s `HAND_ASSEMBLED`. The
  tool refuses to regenerate it.
- **`Sheep`'s portraits** are marked `reproduces: False`. A fresh run
  produces a 1122×1360 crop where the committed `Shawn_neutral.png` is
  1122×1402 — so the shipped art came from different settings, an older
  version of the tool, or a hand edit, and nobody recorded which. Running
  the tool over that folder now **refuses** rather than silently replacing
  all six; `--check` renders to scratch and reports what *would* change
  without writing anything. That hazard was documented in the tool's own
  docstring for months and enforced nowhere.

## 5. Wiring checklist (new art → visible in-game)

1. Drop raw source file(s) in the kit's source folder, run the keyer.
2. Point the content JSON's `iconPath` (or equivalent field) at the
   `Processed/` file.
3. Rebuild content and/or scenes as needed:
   `run_tests_parallel.ps1 -BuildContent -BuildScenes` — this is also what
   syncs the regenerated `.meta`s back to main (see `docs/WORKFLOW.md` §9).
4. Commit the `.png` **and** its `.meta` together.
5. After a TestRunner build, double-check `Art/` actually diffed back to
   main — `LoadSprite` flips a texture's importer settings and can generate
   a fresh `.meta` for anything newly referenced, and if that diff doesn't
   sync back, the committed scene ends up pointing at a Sprite sub-asset
   that doesn't exist in main's copy of the file.

## 5b. Spell effects (`Resources/Spells/{id}/f0..fN`)

A spell's frames are cut from a sheet by `tools/slice_spell_sheet.py`, and
everything about how one LOOKS is one value in the content — see
`SpellPresentation`. Adding a spell is a manifest entry, a JSON block, and one
command; it should not require touching C#.

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

### The `vfx` block

| field | default | what it does |
| --- | --- | --- |
| `path` | — | `Resources`-relative folder of `f0..fN`. Empty means no visual. |
| `seconds` | `0.6` | How long the whole sequence takes. |
| `impactFrame` | `3` | Which frame (from 1) the blow lands on. The damage number, the flash and the hit-stop are all timed to it. |
| `anchor` | `target` | Where the art happens — see below. |
| `size` | `380` | Square box the art is fitted into, in reference-frame units. |
| `departFrame` | `0` | `travel` only: which frame (from 1) it leaves the caster on. Holds the wind-up in place instead of letting it drift. |
| `impactX` | unset | Where across its own frame the sheet actually strikes, `0`..`1` from the LEFT. |
| `impactY` | unset | Where up its own frame the sheet actually strikes, `0`..`1` from the BOTTOM. |
| `sfxPath` | — | `Resources` path to the clip that plays on impact. |

Anything left out takes its default. There are no sentinels to remember,
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
| `target-centre` | on the target's midpoint | rings, binds, status glints — art drawn around a body |
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

### Composing frames from cells

The manifest's optional `sequence` builds frames out of the cut cells, so a
six-drawing sheet can become a twenty-six-frame animation without new art:

- `{"from": "f0", "spin": 8}` — eight frames rotating a full turn
- `{"from": "f3", "hold": 4, "scale": (1.0, 1.08)}` — four frames, growing
- `{"from": "f5", "scale": (1.0, 1.04)}` — one frame, resized

Left out entirely, the cells **are** the frames one for one.

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

`Assets/_Project/Art/Sheets/hand_assembled.json` names sequences the slicer must
never write over — hand-cut frames with held duplicates or stepped fades that
re-running the tool does not restore. The slicer refuses them, and
`HandAssembledArtTests` pins their bytes so a clobber from any direction fails
the suite. See `docs/INCIDENTS.md`.


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


## 6. Per-kit README index

- `Assets/_Project/Art/UI/Hub/README.md`
- `Assets/_Project/Art/Items/Relics/README.md`
- `Assets/_Project/Art/UI/TalentTree/README.md`

Each documents its kit's exact expected filenames and any kit-specific
sizing/tiling requirements.

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
