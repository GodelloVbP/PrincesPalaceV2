# Handover — 2026-08-12

Working tree is clean at `7d43f79`. Full gate green: **1337 EditMode, 249 PlayMode**
(9 skipped, all pre-existing `Assert.Ignore` content guards).

---

## Start here: the Reckoning feels cheap, and I know why

Diagnosed from a screen recording (`C:\Games\Recordings video\2026-08-12 15-22-07.mp4`)
by extracting frames with ffmpeg and reading them one at a time. Four findings, two
sharing a root cause. **None of these are fixed.**

### 1 & 2. The frame has no mask — this is the big one

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

### 3. The item offers still wear button chrome

I told the user twice I had stripped it. I had not. `BuildOffer` removes the
declared children but `UiEmitter` applies its default `SceneBuilder.ButtonSprite()`
to every `Ui.Button` unless `SpriteKey` is set — and I only set it on the tabs and
Continue. So the three offers are still big gold plates.

One-line fix, but decide what "no chrome" means: an empty `SpriteKey` may make the
button non-raycastable. Check `UiEmitter.EmitButton` before assuming.

### 4. The exp track reads as a flat black gap

`BarTrackKey` (`proc:bar_track`) is applied and tinted `#2A1C46`, but at 26px tall
on a 1344-wide frame against a violet panel it looks like a hole rather than a
recessed channel. Wants to be taller and lighter, and the fill wants more bloom.

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
  before believing a file is missing.

---

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
