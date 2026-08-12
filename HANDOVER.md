# Handover — 2026-08-12

> **Update, later the same day (`96e115f`).** All four Reckoning findings below
> are **fixed** and struck through; read them for the reasoning, not as work
> outstanding. Gate green at **1341 EditMode, 251 PlayMode** (9 skipped, the same
> pre-existing `Assert.Ignore` content guards).
>
> Two things came out of that pass and are recorded in `AUDIT.md`, not here:
> **#43**, `screenshot.ps1` reporting success for a panel it never captured —
> which is why the fix was verified by compositing instead; and **#44**, the
> Reckoning row's gain label sitting on top of the bar it annotates, which needs
> a design call rather than a patch.
>
> What is still open in this file: everything from "The session that happened in
> v1 by mistake" onward, including the three repainted backdrops and the
> Loose ends.

Working tree is clean at `7d43f79`. Full gate green: **1337 EditMode, 249 PlayMode**
(9 skipped, all pre-existing `Assert.Ignore` content guards).

Two sessions wrote this file. With the Reckoning findings now closed, the live
work is **"The session that happened in v1 by mistake"** below. Short version:
another session spent a day fixing the battle screen in the wrong tree, and v2
turns out to have the identical bug with the identical constants — front-row
figures standing shin-deep in the HUD, which `UiAudit` cannot see because of an
over-broad `AllowOverlap` on the stage frame. That section has the numbers, the
port order, and three repainted backdrops worth taking.

---

## ~~Start here: the Reckoning feels cheap, and I know why~~ — all four FIXED in `96e115f`

Diagnosed from a screen recording (`C:\Games\Recordings video\2026-08-12 15-22-07.mp4`)
by extracting frames with ffmpeg and reading them one at a time. Four findings, two
sharing a root cause. ~~**None of these are fixed.**~~

**How they were fixed**, since the shape differs from what was proposed below:
`UiNode` grew a `Masks` flag and `UiEmitter` emits a `RectMask2D` for it, as
predicted — but the screen needs **two** masks, not one. `ReckoningFrameWipe` is
panel-sized and its *width* is what the controller opens; `ReckoningPhaseClip` is
inset to the painted interior so a sweeping card vanishes behind the gold rather
than at the panel's outside edge. The inset is horizontal and symmetric on
purpose: every child of a phase is placed relative to its centre, and insetting
by the true vertical border (16.7% crest against 14.6% foot) would have moved all
of them 9px. The lift moved onto the mask too — lifting the frame alone would now
slide it out from under its own clip.

Chrome came off with a `Chromeless` flag rather than an empty `SpriteKey`; the
warning below was right, and the reason is better than expected. The `Image` has
to stay, because a `Button` needs a `targetGraphic` — but `Image` raycasts on its
**rect**, not its alpha, so a fully transparent one still takes the click.

### ~~1 & 2. The frame has no mask — this is the big one~~

**The "wipe" is a horizontal squash.** `ReckoningController.PlayIn` animates
`frame.localScale.x` from 0 to 1. `localScale` scales CHILDREN, so during the
open the ornate border's corners, the three item cards and every label compress
horizontally and spring out to full width. That rubbery stretch is the single
biggest reason the screen reads cheap. Frames 18–24 of the recording show it
plainly.

**The phase sweep leaks outside the frame.** When the item phase slides right and
the summary slides in, the outgoing cards travel *past the frame's right border*
and float over the battlefield before disappearing. Nothing clips them.

Both are the same missing thing: a `RectMask2D` on the frame node.

The fix is a UiKit change, not a controller tweak:

- `UiEmitter` has no way to emit a mask. It needs one — probably a `Masks` flag on
  `UiNode` (alongside `Decor`) that makes `EmitNode` add a `RectMask2D`.
- Then the open animation stops touching `localScale` and instead drives the
  frame's **width** (`offsetMin.x`/`offsetMax.x`, or a `sizeDelta`), so a fixed-size
  content child is revealed rather than scaled.
- Watch out: `NothingSitsOnThePaintedBorder` measures declared node sizes, not live
  rects, so it will keep passing regardless. It does not cover this.

### ~~3. The item offers still wear button chrome~~

I told the user twice I had stripped it. I had not. `BuildOffer` removes the
declared children but `UiEmitter` applies its default `SceneBuilder.ButtonSprite()`
to every `Ui.Button` unless `SpriteKey` is set — and I only set it on the tabs and
Continue. So the three offers are still big gold plates.

One-line fix, but decide what "no chrome" means: an empty `SpriteKey` may make the
button non-raycastable. Check `UiEmitter.EmitButton` before assuming.

### ~~4. The exp track reads as a flat black gap~~

`BarTrackKey` (`proc:bar_track`) is applied and tinted `#2A1C46`, but at 26px tall
on a 1344-wide frame against a violet panel it looks like a hole rather than a
recessed channel. Wants to be taller and lighter, and the fill wants more bloom.

**"Lighter" was half right and doing it to both halves overshot.** What reaches
the screen is the *product* of `ProceduralSpriteBaker`'s baked shading and the
tint over it, and both were dark: a 0.10..0.22 body times a tint at luminance
0.13 landed at `#07040B` on a `#261433` panel — a fifth of the surface it was
supposedly cut into. Raising both put it at **0.85x** the panel, where the
channel disappears *into* the panel instead. That is a different failure, not a
better one.

So the baked shading carries the fix (body 0.30..0.46) and the tint barely moves
(`#2A1C46` → `#33224F`). Height 26 → 32, which is a ceiling rather than a
preference: the row is 64 tall and the name line starts at y 4.

**No test could have caught either miss** — the audit knows a node's box, not its
colour, and asserting the tint alone measures the wrong quantity.
`tools/measure_bar.py` composites the two against the painted frame and fails
outside a band; it is what caught the overshoot. Now 0.59x the panel with a lit
lip at 1.34x, both fill segments legible inside it. This is the same
"compositing beats reasoning" lesson in Traps below, and it held again.

---

## What landed today (12 commits, `ac8843f`..`7d43f79`)

- **Relics are content**: rarity ladder, achievement-gated unlocks validated at
  content-build time, a numeric modifier table alongside the effect enum.
- **Achievements are a real system**: `achievements.json` → condition + threshold,
  `AchievementProgress` evaluating against a fact snapshot. `AchievementTableTests`
  is the important one — it walks the enum and fails if a condition has no case.
- **Embers are per character** (save v2→3, migration hands the old pool to the
  first roster member).
- **Lifetime run totals**, so "clear a hundred rooms" is reachable at all.
- **Relic draft** at the gate, rolled from the run's own seed so it cannot be
  re-rolled by walking out.
- **Glossary** replacing the dead Relics building.
- **Reckoning**: painted frame, plaque, three tabs, two-phase choose-then-read.
- **Defeat screen**, driven.
- **Hub glows** moved under the buildings and given each building's own colour.

### The two bugs worth remembering

**No relic had ever fired in a fight.** `FightEncounterAdapter.KitFor` passed `null`
for the relic list. Every relic test builds its own `PlayerKit` by hand, so the whole
feature was covered by tests structurally incapable of seeing the break. Fixed and
pinned by `RelicsReachCombatTests`, verified non-vacuous by reverting the null.
Recorded as AUDIT #42 because the *shape* of the miss matters more than the bug.

**Abandoning a run paid nothing.** Settlement was the caller's job and only the
defeat path did it. Moved inside `RunManager.EndRun`.

---

## Traps I hit, so you do not

- **My patch scripts printed success unconditionally.** One anchored on
  `BakeArmourStand();`, removed two commits earlier — it matched nothing, added
  nothing, and said it had worked. I only noticed because the PNG was missing. The
  scratchpad scripts now `sys.exit` on a missed anchor. Keep that.
- **Compositing beats reasoning for anything visual.** The armour-stand anchors were
  eyeballed wrong three times running and fixed in one pass by measuring the keyed
  alpha (`scratchpad/measure_stand.py`). Same for the frame's interior bounds.
- **The audit cannot see paint.** A1 knows a node's box, not that the box has a gold
  border drawn on it. `NothingSitsOnThePaintedBorder` exists for that and caught the
  Continue banner hanging 13px over.
- **A button child named `<button>Label` collides** with the emitter's generated
  caption. A4 catches it; name them `Caption`.
- Art keeps landing in **v1** (`C:\Games\Prince's Palace`) rather than v2. Check there
  before believing a file is missing. **This got much worse the same day** — a whole
  session's work went into v1. Confirm which tree you are in before the first edit:
  v2 has `Assets/_Project/Scripts/Domain/UiKit/` and this file; v1 has neither.

---

---

## The session that happened in v1 by mistake

A parallel session spent a full day on the battle backdrops in
`C:\Games\Prince's Palace` — **v1** — believing it was this project. Four commits,
`cd07515`..`372b87f` on `wip/intent-speed`. It only surfaced when that session was
handed this file and could not find `HANDOVER.md`, `2d5eca6`, `UiKit` or
`UiEmitter` anywhere on disk.

Nothing inside v1 signals that it is stale. Its own `CLAUDE.md` opens with "This
project is standalone. It has no relationship to any other game in `C:\Games\`"
and never mentions v2. The note in "Traps" above about art landing in v1 is the
same hazard, one order of magnitude larger.

### Take this: three repainted battle backdrops

v2's `Fight.png` is still the original 3344x1882 atmospheric painting. v1 now has
replacements at `C:\Games\Prince's Palace\Assets\_Project\Art\Backgrounds\`, all
1672x941 and delivery-ready:

- `Fight.png` — ordinary fight, forest clearing (took three generations)
- `forest_mob_elite_fight.png` — ruined arch (one generation)
- `forest_mob_boss_fight.png` — dead-tree swamp (one generation)

**Do not copy them in blind.** They were composed against v1's stage geometry —
ground lines at 51%, 58% and 66% of frame height, which set a hard requirement
that the painted floor's horizon sit at or above 45%. v2's `FightScreen` is a
different layout and will have different numbers. Measure v2's actor positions
first, then decide; if v2's rows sit lower, the art still works, and if they sit
higher it does not.

### Take this too: the finding, which is architecture-independent

The backdrops did not read wrong because they were badly painted. They read wrong
because **the actors are hard-outlined, flat-shaded cel art and the backdrops were
soft, line-free, atmospheric painting.** Two incompatible drawing languages — the
figures land as stickers on a photograph regardless of composition. Every previous
brief had asked for "the painterly look of Hades" and never applied that
constraint to the actor sheets.

The fix was to move the backdrops toward the actors: flat shape masses, committed
hard edges, a five-or-six colour palette, hand-inked structure. That holds for any
art this project generates, v1 or v2.

Four measurable faults sat on top of it, all worth re-checking against v2's own
backdrops: the composition's brightest region was dead centre, which is the empty
gap between the two armies; detail frequency in the actor band matched sprite
frequency; the back rows stood in the frame's only light source; and the boss map
was so uniformly dark that 100% of its pixels above L 90 fell inside the actor
band. Full write-up with measurements and the prompt set that fixed it:
`docs/handoffs/battlefield_art/` in v1 (`README.md`, `PROMPTS.md`).

One technique from it is worth stealing outright: **once one backdrop in a floor is
right, attach it as a style reference for the rest.** The first took three
generations, the other two landed first try. The prompt shape changes — drop most
of the style clause, and add an explicit "DO NOT copy its subject, this is a
different location", which is the new failure mode a reference introduces.

### v2 has the same bug, and here are the numbers

I first wrote here that none of the code ports. That was a grep for v1's symbol
names, and it was wrong in the way that matters: v2 renamed the concepts, it did
not drop them. `FightStageAnchors` is v1's `StageNearAnchor`/`StageFarAnchor`, and
both trees share the same `Domain/Stage/StageLayout`.

The values in `Domain/Stage/FightStageAnchors.cs` are **v1's, before the fix**:

```
Near = (470, -300)      Far = (250, -140)      SpriteScale = 0.78
```

And the HUD around them is identical too — `FightSubmenuLayout.CommandBottom` is
`-486`, the verb column is `x -286, w 300, h 52, pitch 62`, the party plate is at
`(-694, -392)` sized `452x216`, the plates are `PlateX 720, PlateW 400,
PlateH 104, PlateFirstY 332`.

Run the same arithmetic v1 needed:

| | |
|---|---|
| Front-row ground line | **-300** |
| Party plate top edge | **-284** |
| Topmost *visible* verb row (RUN, i=3) | **-248** |

So the front row stands 52 units below the party plate's top edge and 16 below the
verb column's, with the foot ring hanging 8 lower still. **Every front-row figure
in v2 is standing shin-deep in the HUD**, exactly as v1 was, and the whole
ground-contact system is drawn underneath an opaque panel. That is the single
biggest reason the battle screen reads pasted-together, and it is a layout bug
that presents as an art bug.

**`UiAudit` cannot catch it**, which is worth understanding before trusting the
build gate here. `FightScreen.cs:284` carries:

> `.AllowOverlap("the party and enemy stages share one centred frame, and the HUD is drawn over both - a stage is a transparent coordinate frame, never a surface")`

That reasoning is right about the frame and too broad about the consequence. A
stage node is indeed a transparent coordinate frame, but the exemption is written
on the frame and so covers every *figure* standing in it. The audit is otherwise
stronger than the PlayMode test I wrote in v1 — it re-solves at four aspects at
build time — and it still cannot see this. The narrower exemption is to allow
overlap of the stage frame while still asserting that no always-visible HUD panel
covers the bottom 40 units of a populated slot.

### ~~The port, in order~~ — all four steps DONE, `a8738e7` and `678cba9`

> **Outcome.** Anchors re-derived to `Near = (470, -228)`, `Far = (250, -68)`;
> `PlateFirstY` 332 → 392; a three-layer scrim; and the backdrops judged and
> accepted. Gate green at 1342 EditMode, 251 PlayMode.
>
> **v1's numbers did not survive contact**, exactly as this section warned. -170
> puts the golem's head at 130, well inside the bottom enemy plate. The floor
> here is the topmost always-visible verb row (-248) plus the ring's 8 plus 12
> of daylight.
>
> **The plate move is 392, not v1's 380**, and the extra 12 is the one thing
> hand-arithmetic got wrong. By hand only the front slot appeared to reach the
> plates in x; slot ONE reaches x 522 against a plate edge at 520, so a 2px
> overlap — invisible to inspection — left its head clearing by 7px instead of
> 12. `tools/measure_stage.py` caught it.
>
> **The audit blindness is now covered** by
> `FightScreenTests.NoAlwaysVisiblePanelStandsInFrontOfAFigureSFeet`, and the
> over-broad exemption is kept with its text amended to say what it does not
> cover. Confirmed non-vacuous by restoring -300: it fails naming `PartyPlate`
> (127x24) and `Verb3` (91x40).
>
> **The backdrops are fine for v2** — and the deciding measurement was made on a
> rendered `FightPanel`, not computed. v2's ground lines sit at 71%, 64% and 56%
> from the top against a painted horizon at roughly 45%, so all three rows stand
> on floor. The rule stated below (lower is fine, higher is not) held: v2's rows
> are lower than v1's 66/58/51. An automated horizon detector put the transition
> at 68% and was wrong — it had found the ground's own midtone shift.
>
> **Still open**: the three repainted PNGs are sitting UNCOMMITTED in this tree,
> placed by another session, and are deliberately not staged here. AUDIT #45
> records the detail column, which still covers the front enemy's feet while a
> submenu is open.
>
> `tools/preview_fight.py` was not ported as such. Its measurement half is
> `measure_stage.py` (art versus HUD) and `measure_scrim.py` (backdrop luminance
> behind the slots); both read their constants out of the C# rather than
> restating them, which was the property worth keeping.

### The port, in order

1. **Fix the anchors.** In v1 this became `Near = (470, -170)`, `Far = (250, -10)`,
   `SpriteScale` unchanged, plus `PlateFirstY 332 → 380` so front-row heads still
   cleared the bottom plate, and the detail column shortened by 20. Do not paste
   those numbers — re-derive them, because the binding constraint is whichever v2
   panel tops out highest, and v2's detail column may not be v1's 300 tall. The
   method: ground line must clear the highest always-visible panel top by the foot
   ring's 8 units plus margin; head must stay under the bottom enemy plate.
2. **Size it off the real art, not the sprite canvas.** The mistake that cost the
   most in v1: the golem's canvas is 461 tall but its opaque art above its own
   ground line is 258. Sizing to the canvas said the frame was over-subscribed by
   165 units and implied shrinking every actor 15%. Measuring the opaque bbox —
   and the per-actor `groundLine` in `Resources/StanceManifest.json`, which v2 also
   has — showed the real budget was fine and the fix was three constants.
3. **Add a scrim.** v2 has none (`Scrim` — zero hits). Three layers between
   backdrop and stage: a vertical soft-edged band through the actor rows, a radial
   knock-down over the centre gap, a bottom-focused gradient under the command
   columns. v1's tuned alphas were `0.32 / 0.38 / 0.50`; the first attempt at
   `0.45 / 0.40 / 0.55` scored better on every readability metric and turned the
   painting into a black rectangle, so tune to a target backdrop luminance
   (L 30–50 behind the slots) rather than to maximum separation.
4. **Then judge the backdrops**, not before — art assessed against a broken stage
   tells you nothing.

`tools/preview_fight.py` needs a new parser for `FightStageAnchors.cs` and the
UiKit tree, but its math and its metrics transfer whole: it mirrors `StageLayout`,
which both trees share.

### What genuinely does not port

The emission layer. v1 builds rects imperatively in `SceneBuilder.FightStage.cs`;
v2 declares a tree and lets `UiEmitter` emit it. The scrim in particular is three
`Ui.Sprite` nodes here, not three hand-built `Image` components — and it needs
procedural sprite keys for a soft-edged stripe, a radial glow and a bottom-focused
gradient, which v1 had in `ProceduralSprites` and v2 will need in whatever the
`proc:` key namespace provides.

## Loose ends

- **Unused painted art**, keyed and committed but referenced by nothing: the painted
  `rarity_burst` (beaten by the baked one in a side-by-side), `exp_track`, `exp_fill`.
  User was asked and has not said whether to delete them.
- **Mana potion** never arrived — only `health_potion` was generated. The satchel
  still has no mana consumable art.
- **`RelicModifierType` has ten values and no content uses any of them.** The table is
  wired end-to-end and proven by test, but every relic in `relics.json` is either an
  effect or a placeholder.
- **Tier 0 items exist.** `RarityTable.FloorTier(0)` returns 0 and the Reckoning showed
  "TIER 0" at depth 0. No item in `items.json` has tier 0, so this is the roll floor,
  not content. Never chased down.
- AUDIT #37/#38 (the retreat-shaped economy, the unenforced wipe forfeit) and #41
  (`CurrencyType.Embers` with no live reader) are recorded and deliberately unfixed.

## Conventions that bit me

Read `CLAUDE.md` before touching anything. The two that matter most: scenes and
content are **generated**, never hand-edited; and **never `git add -A`** — this repo
is worked by two sessions sharing one tree.

Run `tools/run_tests_parallel.ps1 -BuildScenes` before every commit. A full scene
rebuild reassigns every `fileID`, so a five-scene diff in both directions is normal
and not a sign something broke.
