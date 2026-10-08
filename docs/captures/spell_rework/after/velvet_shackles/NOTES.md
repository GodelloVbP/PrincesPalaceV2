# Velvet Shackles: state record

Structure: `docs/captures/spell_rework/NOTES_TEMPLATE.md`. Workflow: `docs/PROPOSAL_SPELL_WORKFLOW.md`. Target design: `docs/PLAN_SPELL_ANIMATION.md` section 3 "Velvet Shackles".

## Status

Step 6 (step-4 art wired, round 2) in the commit that carries this file; owner decisions Q1-Q4 implemented. Round 1 was `83882f88`. Waits on the orchestrator's check, then the owner (step 3 on the art).

Captures: `velvet_shackles_r2_{rat,treant}_full` (1x) and `velvet_shackles_r2_treant_1.5x_full`, 60 fps sheets `shackles_r2_flight_{rat,treant}.png` (f0-f39 = release to cue + 15), `spell_feel_r2_1x/`, `spell_feel_r2_1.5x/` (previews and timing committed, full GIFs and sheets untracked).

## Identity

- **Verb:** bind (a ribbon is lashed from Shawn's hand, wraps the legs, and a cuff cinches shut).
- **Signature beat:** the small cuff snaps tight at the feet on the cue, gold clasp showing, with the Rooted badge.
- **Must read as:** "a velvet ribbon whipped out of Shawn's hand and shackled its legs", then went slack and let go.
- **Must NOT read as:** a velvet blob or sash over the body; a flower; a cuff sized to the body that hides it; a glowing ring.
- **Motion references:**
  - `lash`: a ribbon cracked from the hand like a whip, rippling along its own length, flown to the legs;
  - `back`, `front`: a ribbon loop pulled tight round the ankles, the ends crossing; after the cinch it goes slack, loosens and droops;
  - `cuff_close`, `cuff`: a padded cuff snapped shut and squeezed on the cue;
  - `thread`, `glint`, `settle`: gold thread, a gold glint and velvet scraps, falling.
- **Exceptions:** the cuff's landing cell pops in on the cue frame (the closing cells lead into it); the lash hands over to the wrap where it lands, at the legs; the cuff is a constant size, smaller than the body on a treant.
- **Mechanics, unchanged:** Rooted for 2 turns, no damage, cannot be dodged. No stage kick (a root is not a blow).

## Beat table

Release clock; cue **0.40 s** = f24 at 1x, f16 at 1.5x (cast sound re-cut, `MeleeSoundOverrideTests.TheSpellCastCuesFitTheirBlows`). No stage kick (a root is not a blow). Single target, so `back` takes the one ground renderer. All art at opacity 1, untinted, no glow. Wrap, lash and cuff are constant pixel sizes (`fit: none`): the loop is about half a rat's width at the end, smaller than the body on a treant (owner Q4). Positions use the ground line under the body's middle (`place: caster` on the flight, `place: target` elsewhere) and `dy` 25 to leg height.

| t | Beat | Layer | Art piece | Box / feel |
|---|---|---|---|---|
| 0.00-0.15 | gloom 0.20; gold thread tufts at Shawn's hand | `thread`, gloom | particles cell 1 | 5 tufts, size 0.3-0.5 |
| 0.02-0.22 | the lash flies from the hand to the target's LEGS (owner Q2) | `lash` (travel 0.20 s, `place: caster`, `orient: path`, `dy` 25) | `velvet_shackles_lash` 4 cells ripple, wave, curl, flick, once | 475 box = 170 px long; `fadeIn` 0.04, fade 0.06 |
| 0.20-0.60 | the drawn loop closes round the legs (owner Q1): loose, tighter, closed with the ends crossed on the cue, slack | `back` (ground band), `front` (effects band) | `velvet_shackles_wrap_back/_front` 4 cells, 10 fps | 540 box, loop about 135 px closed; `fadeIn` 0.06; last cell held to 0.70 s, fade 0.12 |
| 0.30-0.40 | cuff snaps shut: open, nearly closed, closed with the clasp | `cuff_close` | `velvet_shackles_cuff` cells 1-3, 30 fps | 150 box, cuff about 108 px wide |
| 0.40 | **Cue.** The squeezed cuff lands, Rooted badge, glint. Sound | `cuff` (still, cell 4), `glint` | cuff cell 4, particles cell 0 | `punch` 0.2; held to 0.70 s, fade 0.15 (gone 0.45 s after the cue, limit 0.5) |
| 0.45-0.90 | thread, velvet scraps and ribbon curls settle | `settle` | particles cells 1-3 | 8 pieces, 0.3-0.45 s |

## Open items

1. **Cuff reads as a collar or bowl** at gameplay size: rat f18-f34 (and treant f26-f39) a red padded ring with a dark inside sits on the legs like a pot or a scrunchie, about as wide as the wrap loop. The art is chunky and the open top is dark. Constant size 150 box (cuff 108 px); smaller or a flatter art revision is the owner's call.
2. **WARN contact desync remains** (`spell_feel_r2_1x`): biggest rise after release f9 (the lash, a long saturated red ribbon entering the stage); authored cue f24, contact on the authored frame. A 0.04 s `fadeIn` on the lash and a `punch` 0.2 on the landing cuff did not move it (rat 1x f9, treant 1x f9, 1.5x f10); the lash is the biggest red area in the cast. By design unless the owner wants the lash smaller or fainter than the 170 px asked for.
3. Wrap on the treant: the loop is round the roots (about 135 px against a trunk base of about 300), small but on the legs; the lash lands at the ground line, f12-f13.
4. Slack column f30-f40: the loop widens past the closed one and droops, then the layer fades at 0.70 s; reads as letting go. The closed cell is held only 0.1 s (f24-f30).
5. `spell_feel` WARN no accent (peak 5.4-5.5 at 0.30-0.37 s, under 1.5x the typical 4.0-4.2): the cue is not a spike by design (a root, no kick, no flash).
6. Glint (cell 0) is small, 5 specks, f24-f26. Flash guard: hot peak 0.00-0.01%.
7. Treant 1.5x frames not stepped through frame by frame beyond the sheet at 1x (same layers on a faster clock).
8. Superseded sources stay on disk, unreferenced: `shackle_cuff.png`, `shackle_ribbon.png`, `velvet_shackles_atlas_v1.png`, `velvet_shackles_cinch.png`. Their recipes (`velvet_shackles`, `velvet_shackles_cinch`, `velvet_shackles_ribbon`) and sliced folders are deleted.

## Decisions

- 2026-10-07 (orchestrator): cue 0.40 s, no kick, travel pattern from Dark Chains.
- 2026-10-08 (owner): Q1 the wrap is the drawn loop (not a straight band); Q2 the lash flies to the legs and hands over to the wrap there; Q3 the cue stays at 0.40 s; Q4 the cuff is sized to the legs, smaller on big bodies (constant pixel size, `fit: none`).

## Art

All four sheets are `#00FF00`, keyed `green-soft` (the gold keeps its hue), accepted at step 5; prompts in `docs/prompts/2026-10-08_velvet_shackles_art.md`, approved frames in `docs/prompts/2026-10-08_shackles_refs/`. Recipes cut each with a fixed pivot, no crop.

| Piece | File | Recipe | Status |
|---|---|---|---|
| Lash | `Art/Sheets/Spells/Velvet Shackles/velvet_shackles_lash.png` 4096x512, 4x1 of 1024x512, tip at 90% across, 50% down | `velvet_shackles_lash.json` | accepted, wired |
| Wrap | `velvet_shackles_wrap.png` 4096x1280, 4x2 of 1024x640, back row on top, loop middle at 50% across, 55% down | `velvet_shackles_wrap_back.json`, `velvet_shackles_wrap_front.json` (rows cut by `rects`) | accepted, wired |
| Cuff | `velvet_shackles_cuff_flat.png` 3072x768, 4x1 of 768, cuff centre at 50% across, 60% down | `velvet_shackles_cuff.json` | accepted, wired |
| Particles | `velvet_shackles_particles_flat.png` 1024x256, 4x1: glint, thread, scrap, curl | `velvet_shackles_particles.json` | accepted, wired |
| Stand-ins `shackle_ribbon.png`, `shackle_cuff.png`, `velvet_shackles_atlas_v1.png`, `velvet_shackles_cinch.png` | same folder | recipes deleted | superseded, unreferenced, kept on disk |

The step-4 prompts are in `docs/prompts/2026-10-08_velvet_shackles_art.md`, the canonical source.

## Round log

| Date | Round | What changed | Caught by | Commit |
|---|---|---|---|---|
| 2026-10-04 | pass 1 | nine painted velvet frames, procedural ring | worker | `42d43291` |
| 2026-10-04 | pass 2 | painted four-frame cinch as one layer round the body; ring and snap removed. Cuff hid a rat and a treant trunk, read as a sash | worker | `386f640d` |
| 2026-10-07 | 1 | new design on flat-cel strips: layers `lash`, `back`, `front`, `cuff`, `thread`, `glint`, `settle`; cue 0.30 to 0.40; wrap 330 to 240, dy 45 to 18, tilt 16 to 10 (bands sat on the torso); cuff 170 to 110 to 130; lash was the loudest frame (diff 4.9 vs cue 1.5) so 250 to 170 with a fade, wraps fade in 0.12, cuff `punch` 0.3 (4.3 vs 2.6) | worker frames, frame-difference script, `spell_feel` | `83882f88` |
| 2026-10-07 | step 3 | lash a flat bar, no wrap, cuff a ring on the ground (Open items 1-3) | orchestrator | `9a67d602` |
| 2026-10-08 | 2 (step 6) | the four step-4 sheets wired: drawn lash to the legs, drawn back/front loop, flat cuff in two layers (closing, landing with `punch` 0.2), flat particles; constant sizes, no glow, no tint; recipes and folders of the superseded pieces deleted; owner Q1-Q4 | owner decisions, orchestrator step 6 | this commit |

## Measures

| Measure | Value |
|---|---|
| Owner reviews (target 3) | 0 |
| Internal retries | 2 (wrap/cuff size, height, tilt; cue energy vs lash) |
| Attempts per art piece | 0 (stand-ins) |
| Active / waiting time | about 1.5 h (15 min builds, captures, tests, `spell_feel`) |
| Owner-found defects the checks should have caught | 0 |
| Quality vs reference pack | not yet |

## Snapshot

Not frozen: step 7 waits for the owner's step 3 on the art.

- **Round 2 commit:** the commit carrying this file.
- **Capture run ids** (`tools/screenshots/gif/runs/`): rat 1x `20261008T183540Z_484e62a50dd8`, treant 1x `20261008T183657Z_2fe995150c5f`.
- **Gate:** `tools/run_tests_parallel.ps1 -Changed -BuildContent`: "All tests passed -- a SLICE: 271 EditMode + 144 PlayMode classes, 187s wall" (the run before the cuff split was the full suite, 309s, green).
- **Measures** (`spell_feel`, 60 fps, rat / treant): 1x contact f24 authored = f24; payoff to clean 0.38 s / 0.38 s; residue 0.37 s; hot peak 0.00%; WARN no accent, WARN contact desync (open items 2 and 5). 1.5x: contact f16; clean 0.30 s (rat) / 0.47 s (treant); residue 0.23 / 0.45 s; treant has the desync WARN only.

