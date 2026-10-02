# Spell feel: making a cast hit hard

What made Blackglass Spear land, measured, and the workflow that reproduces it.
How to author the layers themselves: `docs/ART_PIPELINE.md` §5b.

## The reference: Blackglass Spear (2026-10-02)

Four captured versions of the same spell, measured on the GIFs in
`docs/captures/playtest-2026-10-02/blackglass/`. "Activity" is the mean pixel
difference from the clean end frame over the stage (HUD excluded); "hot" is the
share of near-white pixels.

| Version | Gather : travel | Peak activity | Residue after contact | Owner |
|---|---|---|---|---|
| v1 lodged (`blackglass_vs_treant.gif`) | 0.6 : 0.18 | 17 | 1.5 s plateau at ~6 | "doesn't strongly accumulate... flash... tear" |
| v2 fly-through (`_through.gif`) | 0.6 : 0.18 | 16 | 1.5 s plateau | "slash should dissipate way faster" |
| v3 speed (`_speed.gif`) | 0.6 : 0.10 | 21 | 0.33 s | "looks good" |
| v4 final art (`_rift_art.gif`) | 0.6 : 0.10 | 21 | 0.33 s, clean by 1.1 s | "looks very good" |

The winning curve: a low, steady build (~5) for 0.6 s, one spike (~21) lasting
three frames, a clean drop to zero within a third of a second. The losing curve
had a lower spike and then a long tail. **The tail is what killed it**, not the
peak: a plateau of leftover effect reads as slow and heavy, and it buries the
hit under its own aftermath.

Authored values (`skills.json` `blackglass_spear`):

| Beat | Layer | Timing |
|---|---|---|
| Gather | `amass`, sky, 6 fps, punch 0.3, glow 1.7 | 0 - 0.6 s |
| Release | `fire` starburst, punch 0.4, glow 1.9 | 0.6 s, 0.14 s + 0.08 fade |
| Travel | `spear`, `travelSeconds` 0.1, `orient: path`, `through: true` | 0.6 - 0.7 s, then off-screen at arrival speed |
| Speed | `streaks` emitter on the spear, `alignFlight`, `lateral` 130, life 0.1-0.2 s | while it flies |
| Wake | `rift`, `align: flight`, painted tear | 0.2 s + 0.15 fade |
| Contact | `impact`, punch 0.25; hit cue and sound on arrival | 0.7 s |
| Residue | `tear`, `settle` shards, `tear-air` haze | all gone by ~1.05 s |

## Why it works

1. **Anticipation against release.** Gather 0.6 s, travel 0.1 s: a 6:1 ratio.
   Power reads from the contrast, not from either half. Other sky spells sit
   near 1.5:1 to 2:1 (`winters_rebuke` 0.28 : 0.2, `crownfall` 0.36 : 0.16)
   and read soft.
2. **One accent frame at release.** The `fire` starburst is the brightest thing
   on screen for ~3 frames. The eye needs a single "now".
3. **Speed shown by what is left behind.** A 6-frame flight is barely seen;
   the streaks and the wake behind the tip carry the motion. Short particle
   lives (0.1-0.2 s) read as motion blur, long ones as clutter.
4. **Follow-through, not a stop.** Stopping at the target soaks up energy;
   continuing off-screen at arrival speed says there was force to spare. The
   hit still lands on the body centre, so the payoff is where the eye is.
5. **Residue leaves fast.** Everything after contact is gone in ~0.35 s. Twice
   the owner asked to cut lingering effects; nothing in v1-v2's tail added power.
6. **One line through everything.** Spear, wake and streaks all lie on the
   flight line: one strong diagonal composition, nothing competing with it.
7. **Art that means the right thing.** The stand-in wake (a crystal splinter)
   read as a second spear stuck in the target. The painted tear (opaque void,
   hot violet-white rim) means "rip". Semantics beat rendering quality.
8. **Value contrast.** Near-black void with a white-hot rim over a mid-dark
   backdrop survives a 0.1 s glance.
9. **Sound on contact.** The cast cue is cut so the hit sound lands on arrival;
   every travel change re-cut it (`tools/make_spell_cast_cues.py`).

## Targets for a projectile or strike spell

Starting points, not laws; break one only on purpose and say why in the layer's
`_comment`.

| Measure | Target |
|---|---|
| Gather | 0.4 - 0.7 s, low and steady, building in size or density |
| Gather : travel | at least 4 : 1 |
| Travel | at most 0.12 s (7 frames at 60 fps) |
| Release accent | one layer, the brightest of the cast, 0.08 - 0.15 s |
| Contact | hit cue, sound, impact layer and damage number on the same frame |
| Residue after contact | at most 0.35 s; screen back to baseline by 0.5 s |
| Activity curve | one dominant spike; no plateau above ~1/4 of the peak after contact |
| Composition | every moving layer on the one action line |

## Workflow

1. **Write the beat sheet first.** Five lines, one per beat: gather, release,
   travel, contact, residue. For each: what the player should feel, its timing
   band from the table above, and which layer carries it. Ask the owner only
   about meaning ("what does this spell do to reality?"), never about numbers.
2. **Check the model before authoring.** The layer fields already cover
   fly-through (`through`), flight-aligned wakes (`align: flight`) and speed
   streaks (emitter `alignFlight` + `lateral`), §5b. Extend a field only when a
   beat has no way to be authored, and make it general.
3. **Block it with stand-ins.** Author all layers from existing frames, timings
   from the table. Get the curve right before any new art.
4. **Capture and measure.** Runtime frame series against a tall target (treant)
   and a short one (rat): `tools/screenshot.ps1 -Runtime -Spell <id>` with
   `PP_RUNTIME_VERSUS=<enemy>`. Build the GIF (960 px, ~30 fps, 96 colours, no
   dither, loop, 1 s hold on the last frame) and read the activity curve.
   Look at the frames yourself.
5. **Fix the curve before the art.** Tail too long → cut hold and fade. Spike
   too low → raise the release accent or shorten travel. Mushy → widen the
   gather : travel ratio.
6. **Commission only the frame that carries meaning.** One painted still on
   `#00FF00` (dark interiors must stay opaque: green key, never luminance).
   The prompt names the spell, the file name, the save path, the beat it
   serves, and what it must NOT read as. Precedent:
   `docs/prompts/2026-10-02_petting_zoo_and_frost_flare.md`.
7. **Slice, recapture, compare.** Re-slice via the recipe, recapture both
   targets, put the before and after GIFs side by side for the owner.
8. **Pin it.** A timing test with literals (travel, hit cue, wake hold/fade),
   precedent `SpellBaselineTimingTests.TheBlackglassSpearFliesFastLeavesABriefWakeAndStreaksAlongIt`.
