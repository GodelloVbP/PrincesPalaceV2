# Spell feel: making a cast land

Principles, measurements and a workflow for strikes and projectiles, with a
short section on the spells that have no single contact frame (buffs, heals,
wards, multi-hit, damage over time, staggered area spells). Blackglass Spear is
the worked example. How to author the layers: `docs/ART_PIPELINE.md` §5b.

## The reference: Blackglass Spear

Measured by `tools/spell_feel.py` on raw 60 fps runtime frames, against a tall
target (treant) and a short one (rat). Source:
`docs/captures/spell_feel/blackglass_spear_vs_treant/report.json` and
`..._vs_rat/report.json`.

| Measure | vs treant | vs rat |
|---|---|---|
| Gather | 0.60 s | 0.62 s |
| Travel | 0.10 s | 0.10 s |
| Contact frame vs authored `hitCueSeconds` (0.7 s) | frame 42 = authored | frame 43, 1 later |
| Residue after contact (activity above 25% of its peak) | 0.22 s | 0.37 s |
| Clean (back to idle level) | 1.12 s into the capture | 1.10 s |
| Peak activity | 15.3 | 8.0 |

Peak depends on the target's size and the crop, so it is a way to find the
accent frame, not a target. Gather, travel, contact sync and the short residue
are the comparable numbers.

An earlier version with a slower flight and a plateau of leftover effect
lasting ~1.5 s read as slow and heavy. Travel time, wake length and streaks
changed together between the two, so their separate effects are not known.

Authored values (`skills.json` `blackglass_spear`):

| Beat | Layer | Timing |
|---|---|---|
| Gather | `amass`, sky, 6 fps, punch 0.3, glow 1.7 | 0 - 0.6 s |
| Release | `fire` starburst, punch 0.4, glow 1.9 | 0.6 s, 0.14 s + 0.08 fade |
| Travel | `spear`, `travelSeconds` 0.1, `orient: path`, `through: true` | 0.6 - 0.7 s, then off-screen at arrival speed |
| Speed | `streaks` emitter on the spear, `alignFlight`, `lateral` 130, life 0.1-0.2 s | while it flies |
| Wake | `rift`, `align: flight`, painted tear | 0.2 s + 0.15 fade |
| Contact | `impact`, punch 0.25; hit cue and sound on arrival | 0.7 s |
| Residue | `tear`, `settle` shards, `tear-air` haze | gone ~0.4 s after contact |

The spear eases in (u²) into contact, an explicit choice: it accelerates into
the target instead of arriving at a constant speed.

## Principles, not a template

Copy Blackglass's numbers or its layer stack onto another spell and the roster
becomes one effect in nine colours. What carries over is the principle under
each choice; how a spell expresses it is its own.

| Principle (every spell) | Blackglass's expression (this spell only) | Other expressions |
|---|---|---|
| Contrast in time: a quiet part makes the loud part loud | 0.6 s gather, 0.1 s shot | a long silent hold then an eruption; a fast flurry then one slow heavy blow; a gentle swell with no spike for a heal |
| One moment the eye locks onto | white starburst at release | the ground cracking open; a chain snapping tight; a bell's strike; a heal's single bloom |
| Motion shown by what it leaves behind | speed lines and a wake on the flight line | dust kicked up, a ripple in the ground, afterimages, falling petals |
| The payoff lands where the eye already is | hit on the body centre | on the ground under the target; on the caster for a self-buff |
| Leftovers clear so the payoff stands alone | residue gone ~0.4 s after contact | a heavy spell can hold its aftermath on purpose (smoke settling, scorched ground) if it stops competing with the payoff |
| One action line | a single diagonal | a vertical drop, a ring outward, a spiral in, a wave across the floor |
| The art means the right thing | a tear reads as "rip", not as a crystal stuck in the target | each spell names what it does to reality and what it must not read as |
| Value contrast | black void, white-hot rim | bright on dark, dark on bright, one saturated hue on a muted scene |
| Follow-through | continues off-screen at arrival speed: force to spare | stops dead for a crushing blow; sinks into the floor |

### Reaction

The target's response carries much of the perceived force: anticipation, action,
reaction. The reaction is a hurt flash, a flinch or knockback, a brief pause of
the two actors at contact, and stage shake.

- No global `Time.timeScale` hit-stop (`game-feel` override in `CLAUDE.md`).
- Stage shake exists: `StageShake` (Core), driven from the beat's impact by
  `FightBeatPlayer.ShakeStrength`. It moves the two stage containers, not the
  HUD.
- A beat-level hit-stop exists: `FightBeatPlayer.HitStopFor` waits
  0.055-0.18 s (`Domain/Combat/Session/HitStop.cs`) scaled by damage against
  max health, taken out of the beat's own budget. Whether a spell's VFX
  layers pause with the actors during it is not verified. Follow-up: confirm,
  and if layers keep playing through the stop, decide whether they should.
- Judge the reaction in the capture: flinch and flash start on the contact
  frame, and the stop is 2-4 frames' worth, not a hitch.

### Communication

The effect says who acted, on whom, which element, how much, and what status.

- Element colour language is fixed across spells: `FightHudPalette.ForDamageType`
  (`Domain/UiKit/FightHudPalette.cs`, one token per `DamageType`, pinned by
  `FightHudPaletteDamageTypeTests`), the same tokens the damage popup and the
  typed hit flash (the outline light, `BodyLight`) read. Identity choices stay inside that language: a fire spell
  is not blue.
- The effect does not cover the damage number, the status badges or faces.
  Counter-example: in
  `docs/captures/playtest-2026-10-02/blackglass/blackglass_vs_treant_through_exit.png`
  the -19 sits under the spear at its exit frame. Check the frame at the number's
  spawn.
- A cast aimed at the party (enemy casts, below) must show who is targeted
  before the hit.

### Budget

- Total cast duration scales with how often the spell plays. A spell plays dozens
  of times a run; the test is the fortieth cast. A long gather is for the
  expensive, rare cast; cheap, frequent spells stay short.
  Content tiers (`manaCost`, `bookTier`, `poolTiers`) show where a spell sits;
  no duration table is set, judge each against its tier-neighbours.
- Every spell survives the 1.5x battle-speed preset. `FightBeatPlayer.Pace`
  (`BeatSpeedMultiplier * PlayerSpeedMultiplier`) scales the beat's waits through
  `Scaled`; audio clips do not scale, so a cast cue outlasts the blow
  (`Resources/Audio/README.md`). Capture at 1.5x before accepting a timing.
- Particle budget: Blackglass's streaks emit 170/s for 0.28 s (~48 particles).
  Judge by frame cost in the capture and by clutter at the target, not by a cap.

### Guards

- At most 3 large flashes in any second (WCAG 2.3.1 general-flash threshold).
  Blackglass fires two large white flashes about 0.1 s apart: the release
  starburst and the target's hurt flash. Two is inside the limit, but a second
  such cast inside the same second is not. Count flashes in the capture.
- The project has no reduce-flashing or reduce-shake setting (no such option in
  `Assets/_Project/Scripts`). Follow-up: add one before the flash and shake
  budget is relied on.

### Audio

- Layered hit: transient (the click that marks the frame), body, tail.
- A gather riser. The cast cue is cut as the tail of a riser so its peak lands on
  the blow (`Resources/Audio/README.md`, "Cast cues are riser tails").
- Variation against repetition: several takes per sound, as `StatusCues` and
  `CharacterVoice` do.
- Sound marks the payoff, whichever moment that is.

### The hard rule

Each payoff's sound, damage number and accent share a frame. For a strike that
is one frame, the contact. A spell with several payoffs has the rule once per
payoff. Everything else is a principle to express, not a number to hit.

The runtime holds it two ways: the beat's impact block (sound, number, badge) follows the
cast's own cue clock (`FightBeatPlayer.HitCueCrossed`), and a sprite frame that opens on the
cue cuts in instead of dissolving in early (`SpellFrameCursor`). A payoff drawn by a
release-clock sheet is therefore safe on a frame boundary at `hitCueSeconds`.

## Spells without a single contact

- **Buff, heal, ward.** No contact frame. The payoff is the moment the status
  lands on the target, and the cue for it exists: `Core/StatusCues.cs` plays a
  sound per status type at the beat's impact. Put the accent and the number on
  that frame; the build-up before it is the anticipation.
- **Multi-hit, damage over time, staggered area.** The payoff is per hit. Each
  hit gets its own sound, number and accent on one frame, The flash budget applies to the
  sum of all hits in a second.

## Identity: make each spell its own

A new spell needs its own **verb** and its own **signature beat**; the other
axes follow from those.

- **Verb**: what it does to reality. Pierce, crush, bind, bloom, erupt, freeze,
  unravel. The verb decides the shape of the motion.
- **Signature beat**: the one thing only this spell does. Blackglass's is the
  tear it leaves in the air. If a new spell's signature is "flies fast and
  leaves a trail", it is Blackglass again.

Axes that follow: rhythm (where the weight sits), path (sky, ground, caster,
appearing on the target, whole field), material (decides residue and sound),
scale (matches cost and power). Changing material and scale alone is a recolour
and a resize, the clone to avoid.

Reusing a layer field (`through`, `align: flight`, emitter streaks) is fine; the
fields are tools. Reusing the whole stack, the timings and the signature is the
thing to avoid.

## Workflow

1. **Write the beat sheet.** Verb and signature beat first, then the beats this
   spell has (not every spell has travel; a buff has no contact). For each beat:
   what the player should feel and which layer carries it. Ask the owner what the
   spell does to reality.
2. **Check the model before authoring.** §5b lists the layer fields. Extend a
   field only when a beat cannot be authored, and make it general.
3. **Block it with stand-ins.** Author all layers from existing frames. Get the
   rhythm right before any new art. Choose the easing on purpose (ease in
   into contact, linear, ease out) and write it in the beat sheet.
4. **Capture and measure.**
   `tools/spell_feel.ps1 -Spell <id> [-Versus treant,rat]` captures runtime
   frames at 60 fps, builds the GIF, strip and stills, and writes `report.json`.
   `-Compare <before>,<after>` diffs two reports; `-ReportOnly` re-measures an
   existing capture; `-Speed 1.5` records at the 1.5x preset and compares authored
   seconds divided by it. The report is descriptive. It WARNs on three principles
   only: a residue tail, no accent frame, contact out of sync with the authored
   `hitCueSeconds`. Semantics and thresholds: header of `tools/spell_feel.py`.
   Look at the frames yourself.
5. **Check the principles, then the identity.** One moment the eye locks onto?
   Aftermath stops competing with the payoff? Sound and number on it? Reaction and
   communication clear? Flash count within the guard? Then recapture the caster's
   other spells and put the GIFs next to each other: if the new one reads like
   one of them, change the verb or the signature beat, and keep the timing that
   works. Rhythm is an identity axis, so a spell that shares another's rhythm
   needs a different signature to stay distinct.
6. **Commission only the frame that carries meaning.** One painted still on
   `#00FF00` (dark interiors stay opaque: green key, never luminance). The
   prompt names the spell, the file name, the save path, the beat it serves and
   what it must NOT read as. Precedent:
   `docs/prompts/2026-10-02_petting_zoo_and_frost_flare.md`.
7. **Slice, recapture, compare.** Re-slice via the recipe, recapture both targets
   with `-Compare`, and show the owner before and after as side-by-side GIFs.
   Ask perceptual questions ("which hits harder?", "which is faster?"); the
   owner's short notes on speed and timing ("way faster", "0.3 sounds good") are
   usable input.
8. **Enemy-cast pass.** Monsters cast real skills (§5b "A monster casting it"),
   aimed at the party. Capture it from the enemy side: the threat has to read
   before it lands, and the party's numbers and faces stay uncovered.
9. **Play it in context.** A real fight at 1.5x battle speed, 4:3 as well as
   16:9, after many casts of the same spell. The fortieth cast is the test.
10. **Pin it.** A timing test with literals for the beats that matter (travel,
    hit cue, aftermath hold and fade), precedent
    `SpellBaselineTimingTests.TheBlackglassSpearFliesFastLeavesABriefWakeAndStreaksAlongIt`.

## Review rubric

Scored by the orchestrator before the owner sees a spell. Each item is ✔, ✘
(blocks the owner message) or ~ (named to the owner as weak). The score goes in
the Snapshot section of the spell's NOTES (`docs/captures/spell_rework/NOTES_TEMPLATE.md`).

**Motion**, played at 1x and 1.5x, then stills at the frame edges, the hit and
the last frame:

1. Each beat reads, in order.
2. Weight and easing feel physical (against the motion reference in the identity sheet).
3. One focal point at a time.
4. The body stays readable at the hit.
5. The ending decays, not cuts.
6. Distinct from the approved spells and consistent with them: played beside them.

**Art**, on a contact sheet at gameplay size beside the reference pack
(`docs/spell_art/REFERENCE.md`):

1. Silhouette reads at size.
2. Style matches the reference pack: outline, tone count, palette.
3. Proportions stay consistent across cells.
4. Materials read as named.
5. Nothing in the "must not read as" list.

## Checks: binding and advisory

Instruments support the eye; they never replace it.

- **Binding** (a failure blocks the owner message unless the identity sheet
  lists it as an intentional exception):
  - no transform jump in the spell's own layers;
  - sound, number, badge and accent on one frame (`timing.json`, the cue tests);
  - every layer the beat table marks "must be visible" stays inside the stage crop.
- **Advisory** (reported, never blocking until validated on trial spells):
  pop-in, frozen holds, body coverage at the hit, flash guard. The flash guard
  is measured on rendered frames (largest single-step brightening area), not on
  transforms.

## Polish menu

Engine-made effects, no new art. Each is a layer or field in `skills.json`
(fields: `docs/ART_PIPELINE.md` §5b). Cost is what the Censer pilot spent; it
was not measured per effect, only in total (102 particles thrown, 62 alive at
most of a pool of 64, effects renderers 5 of 12). Each close updates the menu.
Before offering an effect, read its code path: a verdict holds only under the
condition its row names, and a look that seems wrong may be a system working as
specified (the hit rim on a thin body part reads as a pale fill).

| Effect | Verdict | Layer fields | Cost (pilot) |
|---|---|---|---|
| Lantern glow over its life | worked | `glowCurve` (`to`, `pulse`, `period`, `crest`, `knee` 0.4 for a gold body) on the still | 0 |
| Turn sparks | worked | emitter with `burstTimes` + `burstEach`, `glow`, `tint`, flung from a rim; one layer per side | 16 particles in 2 layers |
| Stage kick, cast layers exempt | worked | `vfx.kick` `{strength, rise, fall}` on the cue; `unkicked` on hanging layers, their emitters and the crack | 0 |
| Glowing embers | worked | `glow` on the emitter's particles | 0 extra (38 embers + 5 coals) |
| Smoke from the fire | worked | emitter born below the body's middle, slow rise | not recorded; faint |
| Ash | worked | emitter with the `glow_disc` sprite, warm grey | 6 particles |
| Body glow following the fire | worked | `stoke` on the fire layer, with its own `breathe` | 0; needs the target to carry Burn (stoke lifts StageBurnGlow) |
| Light gloom | worked | stage `gloom` 0.2 over 0.4 s | 0 |
| Light pool under the swing | failed: reads as an orange stain on the floor | a `pool` layer | 1 layer |
| Afterimages of the swing | failed: murky over a green body; additive was still weak | `echo`, `echo.additive` | 3 ghosts |

Whole-body hit flash on a spell is not on the menu: the owner removed it.
