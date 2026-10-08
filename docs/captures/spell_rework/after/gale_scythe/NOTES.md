# Gale Scythe: state record

Structure: `docs/captures/spell_rework/NOTES_TEMPLATE.md`. Plan S3, spell 3.4. Pre-workflow pass; owner gates were skipped, the calls below are the worker's.

## Status

**Closed** (step 7, 2026-10-09). The owner approved r5 as it is. Snapshot below is `ed6d9b26`. Open items stay listed as accepted as is.

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
| 2026-10-08 | step 2 | leaf-spiral emitters (anticipation), `leaves_away` (resolution), `vfx.kick`; E8 echo marked pending | orchestrator brief | `ff649960` |
| 2026-10-08 | step 6 | spiral and gust sheets replace the leaf emitters `spiral_a/b/c` and `leaves_away`; spiral `dy` -10, gust `dy` 35 and 0.38 s after the first capture (spiral sat 40 px above the whirl; gust lay on the cut's line) | orchestrator step 6 | `b6927318` |
| 2026-10-08 | r5 | gust 250 to 170 so each stays one per body | owner: gusts merge across the rack f24-f30 | `ed6d9b26` |

## Measures

`tools/spell_feel.ps1 -Spell gale_scythe -Versus treant,rat` at 1x and `-Speed 1.5`; reports in `spell_feel_close_1x/` and `spell_feel_close_1.5x/` (report.json, GIF, strip, stills; run 2026-10-09). 60 fps; times in seconds.

| Run | Contact (authored / visual) | Peak (floor), at | Residue after contact | Payoff to clean | Hot peak (flash proxy) | Warnings |
|---|---|---|---|---|---|---|
| 1x rat | f18 / f18 | 3.8 (0.0), 0.13 s | 0.38 | 0.40 | 0.16% at f12 | no accent |
| 1x treant | f18 / f18 | 5.6 (0.0), 0.30 s | 0.37 | 0.38 | 0.28% at f8 | no accent |
| 1.5x rat | f12 / f12 | 4.4 (0.0), 0.10 s | 0.25 | 0.48 | 0.16% at f7 | no accent |
| 1.5x treant | f12 / f12 | 6.3 (0.6), 0.20 s | 0.23 | 0.47 | 0.28% at f7 | none |

"No accent" is by design: the loudest frame is the sweep (rat, 0.13 s) or the cut and hurt flash that follow (treant, 0.30 s), not a single cue spike; the cue carries a thin slash on every body at once. Contact is on the authored frame in all four runs. Residue and clean are inside the 0.5 s limit.

Other measures (owner reviews, retries, attempts per piece, handling time): not recorded.

## Snapshot

- **Commit:** `ed6d9b26` (the content at the owner's approval; the closing commit only edits NOTES and adds the reports).
- **Capture run id:** `spell_feel_close_1x` and `spell_feel_close_1.5x` (2026-10-09); GIF runs `gale_scythe_r5_{rat,treant}_full` and `gale_scythe_r5_rat_1.5x_full` (timing json committed; the runs' frames are pruned).
- **Gate:** `tools/run_tests_parallel.ps1 -Changed -BuildContent`: "All tests passed (full suite, 538s wall)" for the step 2 and step 6 content, "271 EditMode + 144 PlayMode classes, 217s wall" for r5.
- **Binding checks:** no transform jump: pass (no authored arc; the sweep and gust drift are eased slides, pinned by `GaleScythePresentationTests`). Sound, number, badge and accent on one frame: pass (contact authored = visual: f18 at 1x, f12 at 1.5x, all four runs). Beat layers inside the stage crop: pass on the stage; the sweep tip runs to the crop edge on a 3-wide rack (open item 4, accepted).
- **Flash guard:** `spell_feel` hot peak (share of near-white stage pixels) 0.16-0.28% on any frame; no frame near a flash. A single-step brightening area was not re-measured since the gust and kick (the older figure, 7.9% of the crop on three rats at 1.5x, predates them).
- **Rubric (worker scored):** Motion: 1 ✔, 2 ✔, 3 ✔, 4 ✔ (bodies readable under the slash; the white hurt flash is the shared typed flash), 5 ✔ (gust and leaves fade, clean 0.38-0.48 s after contact), 6 ~ (not played beside the Censer or Ashen Reckoning). Art: 1 ✔, 2 ✔, 3 ✔, 4 ✔, 5 ✔ (no whirl, no tornados, no flat silhouettes, the gust is not a second slash).
- **Still weak (accepted as is):** open items 1-5: the per-body slash reads through the flash and numbers; treant gusts touch where the bodies overlap; the sweep tip on a 3-wide rack; the faint grey arc under the third treant at f18; the E8 body echo is not built.

