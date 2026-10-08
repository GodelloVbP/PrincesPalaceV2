# Gale Scythe: state record

Structure: `docs/captures/spell_rework/NOTES_TEMPLATE.md`. Plan S3, spell 3.4. Pre-workflow pass; owner gates were skipped, the calls below are the worker's.

## Status

Step 6 (step-4 art wired) in the commit that carries this file; the owner's pieces `gale_leaf_spiral` and `gale_gust_streaks` were accepted at step 5 (`docs/prompts/2026-10-08_gale_mud_art.md`). Captures `gale_scythe_r5_{rat,treant}_full` (1x) and `gale_scythe_r5_rat_1.5x_full`, sheet `gale_r5_sheet.png` (previews and timing committed, full GIFs and the sheet untracked). Waits on the orchestrator's check, then the owner (step 3).

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
| 0.00-0.20 | anticipation: leaves wind into a tight spiral in the hand | `spiral` | `gale_leaf_spiral`, 6 cells wide to tight, `fps` 30 once, size 190, `dx` 70, `dy` -10, opacity 1, untinted | on the whirl's anchor (`gather` keeps its whirl underneath) |
| 0.13-0.25 | delivery: the sweep across the line | `sweep` | painted blade, `formation`, `align span` | kept unchanged (pinned by `GaleScythePresentationTests`) |
| 0.30 | **Cue.** Thin bright slash across each body, all numbers, push, hurt flash, 16 leaves, small stage kick | `cut`, `settle`, `vfx.kick` | blade still, size 380, aspect 6.3; leaves burst; kick 0.008 rise 0.05 fall 0.18 | one frame for every enemy |
| 0.30 | body echo knocked back | none | none | **pending E8** (`PLAN_SPELL_ANIMATION.md` section 2); not built, not faked |
| 0.38-0.70 | resolution: gust passes and breaks up, leaves tumbling | `gust` | `gale_gust_streaks`, 4 cells, `fps` 14 once, size 170 fit target, `dy` 35, drift -0.05 to +0.1, fade 0.1, opacity 1, untinted; `at: release` offset 0.38 (an `at: hit` layer is shrunk) | opens after the cut, above its line; streaks of the cut fade with `cut` |

## Open items

1. Cue frame f18 (rat and treant): thin bright slash across each body joined into one line, hurt flash and numbers on the same frame. Still reads through the flash and numbers more than the stroke. **Owner call**: a heavier slash mark needs a second piece.
2. Anticipation f4-f10: the spiral winds wide to tight on the whirl (centred on it from `dy` -10); at f4 it is about 190 px, a little bigger than the hand and staff head. It hides the old whirl for f4-f10; the whirl shows again at f12.
3. Resolution f24-f30 (`gale_r5_sheet.png`): one gust per body, about a rat's width. On rats each stands apart. On treants the bodies themselves overlap, so neighbouring gusts still touch at f24; the fit scales the gust with the body.
4. Three-wide rack: the sweep tip still runs to the right edge of the stage crop at f8.
5. Treant f18 (cue): faint grey arc under the third treant is the sweep's last fading frame (pinned timings).
6. Not measured with `spell_feel` this round.

## Decisions

- 2026-10-04 (worker): keyed `"green-soft"` (new in `tools/slice_spell_sheet.py`) for the blade: the `green` key clamps green to min(red, blue), turning the pale sea-green blade lilac and the gold flecks red. `green-soft` keeps the keyer's spill suppression and skips the clamp.
- 2026-10-04 (worker): the sheet's three tornado whorls are gone.

## Art

| Piece | File | Recipe | Status |
|---|---|---|---|
| Blade | `gale_scythe_blade.png` 2172x724 | `gale_scythe_blade.json` | accepted (owner's painted) |
| Leaf spiral | `gale_leaf_spiral.png` 1536x256, 6x1 | `gale_leaf_spiral.json`, `keyed: magenta`, no crop | accepted (step 5); prompt 1 of `docs/prompts/2026-10-08_gale_mud_art.md`, frame `2026-10-08_refs/gale_leaf_spiral_approved.png` |
| Gust streaks | `gale_gust_streaks.png` 2048x256, 4x1 | `gale_gust_streaks.json`, `keyed: magenta`, no crop | accepted (step 5); prompt 2, frame `gale_gust_streaks_approved.png` |
| Whirl f0-f3, leaves | old wind sheet | n/a | stand-in (`gather`, `settle`) |

Art still wanted: a heavy slash mark for the per-body payoff (open item 1). Body echo waits on E8.

## Round log

| Date | Round | What changed | Caught by | Commit |
|---|---|---|---|---|
| 2026-10-04 | pass 1 | 12-frame wind sheet only: a rat-sized whirl rolling like a wheel and three tornados | worker | `5915e34f` |
| 2026-10-04 | pass 2 | painted blade as sweep and per-body cut; `green-soft` key | worker | `92e06cba` |
| 2026-10-08 | step 2 | leaf-spiral emitters (anticipation), `leaves_away` (resolution), `vfx.kick`; E8 echo marked pending | orchestrator brief | this commit |
| 2026-10-08 | step 6 | spiral and gust sheets replace the leaf emitters `spiral_a/b/c` and `leaves_away`; spiral `dy` -10, gust `dy` 35 and 0.38 s after the first capture (spiral sat 40 px above the whirl; gust lay on the cut's line) | orchestrator step 6 | this commit |
| 2026-10-08 | r5 | gust 250 to 170 so each stays one per body | owner: gusts merge across the rack f24-f30 | this commit |

## Measures

Not recorded (pre-workflow).

## Snapshot

- **Commit:** `92e06cba`.
- **Capture run id:** not recorded.
- **Binding checks / flash guard:** five casts at 1.5x on a three-rat formation (161 GIF frames): largest single-step brightening 7.9% of the stage crop (white hurt flash plus sweep), no 25% flash; casts about a second apart. Cut marks thin at the waist of each body; numbers (-7, -9, -9) sit above them on every frame, treants included.
- **Rubric:** not scored; identity test passes.
- **Still weak:** Open items 1-3.
