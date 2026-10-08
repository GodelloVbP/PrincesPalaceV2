# Gale Scythe: state record

Structure: `docs/captures/spell_rework/NOTES_TEMPLATE.md`. Plan S3, spell 3.4. Pre-workflow pass; owner gates were skipped, the calls below are the worker's.

## Status

Step 2 (block with existing art) in the commit that carries this file; no new art. Pass 2 `92e06cba` wired the owner's painted blade. Captures `gale_scythe_r3_{rat,treant}_full` (1x) and `gale_scythe_r3_rat_1.5x_full` (`-Out` folder, previews and timing committed, full GIFs untracked), contact sheets `gale_r3_anticipation.png`, `gale_r3_payoff.png` . Waits on the orchestrator's check, then the owner (step 3).

## Identity

- **Verb:** cut across.
- **Signature beat:** one long blade of wind crosses the whole line left to right in one stroke; on the cue every enemy carries a thin bright slash on the same frame.
- **Must read as:** "one cut across all of them".
- **Must NOT read as:** a whirl or tornados (pass 1); flat pale silhouettes (E10).
- **Motion references:** `spiral_*`: leaves pulled into a fist of wind; sweep: a scythe's follow-through; `leaves_away`: leaves lifted off by the gust that has just passed.

## Beat table

Release clock; cue **0.30 s** = f18 at 1x, f12 at 1.5x. One payoff frame for all enemies on the full formation, two, one and the mixed rack (decision 8); nothing staggered.

| t | Beat | Layer | Stand-in | Notes |
|---|---|---|---|---|
| 0.00-0.20 | anticipation: whirl closes in Shawn's hand | `gather` | wind sheet f1-f4 | kept |
| 0.00-0.30 | anticipation: leaves pulled in to the sickle | `spiral_a/b/c` | `gale_scythe_particles` leaves, three emitters born round the hand (`sourceDx` 55/165/110, `sourceDy` 75/75/-30), aimed inward (321/219/90 deg), spin +-420, 32/s for 0.2 s | no orbit field: reads as leaves converging, not as a circle |
| 0.13-0.25 | delivery: the sweep across the line | `sweep` | painted blade, `formation`, `align span` | kept unchanged (pinned by `GaleScythePresentationTests`) |
| 0.30 | **Cue.** Thin bright slash across each body, all numbers, push, hurt flash, 16 leaves, small stage kick | `cut`, `settle`, `vfx.kick` | blade still, size 380, aspect 6.3; leaves burst; kick 0.008 rise 0.05 fall 0.18 | one frame for every enemy |
| 0.30 | body echo knocked back | none | none | **pending E8** (`PLAN_SPELL_ANIMATION.md` section 2); not built, not faked |
| 0.36-0.66 | resolution: leaves spiral away off each body; blades fade | `leaves_away` | leaves, 14/s for 0.3 s, aim 30 deg, spin +-320, life 0.5-0.8 | streaks fade with `cut` (fade 0.2) |

## Open items

1. Cue frame f18 (rat and treant): thin bright slash across each body joined into one line, the hurt flash and numbers open on the same frame. Still reads more through the flash and numbers than the stroke (old item 1). **Owner call**: a heavier slash mark needs a second painted piece.
2. Anticipation f4-f14: leaves are small specks (about 12-25 px, 32/s) converging on the whirl, scattered above and right of it; not a spiral. Needs the leaf-spiral art (Art needed 1) and an orbit.
3. Three-wide rack: the sweep tip still runs to the right edge of the stage crop at f8 (not changed; `drift.toX` 0.22, `overhang` 0.5).
4. Treant f18 (cue): a faint grey arc under the third treant, lower right, is the sweep's last fading frame (sweep ends 0.26 s, fade to 0.31 s). Pinned by `GaleScythePresentationTests`; not changed.
5. `leaves_away` is faint at 1x f24-f34 (small, low-contrast brown leaves on a brown treant).
6. Not measured with `spell_feel` this round; flash guard unchanged apart from the 0.8% stage kick.

## Decisions

- 2026-10-04 (worker): keyed `"green-soft"` (new in `tools/slice_spell_sheet.py`) for the blade: the `green` key clamps green to min(red, blue), turning the pale sea-green blade lilac and the gold flecks red. `green-soft` keeps the keyer's spill suppression and skips the clamp.
- 2026-10-04 (worker): the sheet's three tornado whorls are gone.

## Art

| Piece | File | Status |
|---|---|---|
| Blade | `gale_scythe_blade.png` 2172x724 | accepted (owner's painted) |
| Whirl f0-f3, leaves | old wind sheet, `gale_scythe_particles` | stand-in |

Art needed for step 4 (not prompted yet): 1. a leaf-spiral sheet, 4-6 cells of leaves wound round a point, reading at 150-200 px, for the anticipation; 2. a short heavy slash mark (curved, 2:1) for the per-body payoff; 3. a gust-streak strip for the resolution. Prompts follow `docs/prompts/TEMPLATE_spell_art.md`.

## Round log

| Date | Round | What changed | Caught by | Commit |
|---|---|---|---|---|
| 2026-10-04 | pass 1 | 12-frame wind sheet only: a rat-sized whirl rolling like a wheel and three tornados | worker | `5915e34f` |
| 2026-10-04 | pass 2 | painted blade as sweep and per-body cut; `green-soft` key | worker | `92e06cba` |
| 2026-10-08 | step 2 | leaf-spiral emitters (anticipation), `leaves_away` (resolution), `vfx.kick`; E8 echo marked pending | orchestrator brief | this commit |

## Measures

Not recorded (pre-workflow).

## Snapshot

- **Commit:** `92e06cba`.
- **Capture run id:** not recorded.
- **Binding checks / flash guard:** five casts at 1.5x on a three-rat formation (161 GIF frames): largest single-step brightening 7.9% of the stage crop (white hurt flash plus sweep), no 25% flash; casts about a second apart. Cut marks thin at the waist of each body; numbers (-7, -9, -9) sit above them on every frame, treants included.
- **Rubric:** not scored; identity test passes.
- **Still weak:** Open items 1-3.
