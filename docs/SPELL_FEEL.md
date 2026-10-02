# Spell feel: making a cast hit hard

What made Blackglass Spear land, measured, the principles under it, and a
workflow that applies them without cloning it.
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
   Power reads from the contrast, not from either half; v1-v2 had the same
   gather, a slower shot and a long tail, so the two causes are not separated.
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

## Principles, not a template

Blackglass is one answer, not the shape every spell takes. Copy its numbers or
its layer stack onto another spell and the roster turns into one effect in nine
colours. What carries over is the principle under each choice; how a spell
expresses it is that spell's own.

| Principle (every spell) | Blackglass's expression (this spell only) | Other honest expressions |
|---|---|---|
| Contrast in time: a quiet part makes the loud part loud | 0.6 s gather vs 0.1 s shot | a long silent hold then a sudden eruption; a fast flurry then one slow, heavy blow; a gentle swell with no spike at all for a heal |
| One moment the eye locks onto | white starburst at release | the ground cracking open; the instant a chain snaps tight; a bell's strike; a heal's single bloom |
| Motion shown by what it leaves behind | speed lines and a wake on the flight line | dust kicked up, a ripple in the ground, afterimages, falling petals |
| The payoff lands where the eye already is | hit on the body centre | on the ground under the target; on the caster for a self-buff |
| Leftovers clear so the payoff stands alone | everything gone 0.35 s after contact | a heavy spell can hold its aftermath longer on purpose (smoke settling, scorched ground), as long as it stops competing with the payoff |
| One action line | a single diagonal | a vertical drop, a ring outward, a spiral in, a wave across the floor |
| The art means the right thing | a tear reads as "rip", not a crystal | each spell names what it does to reality and what it must not read as |
| Value contrast | black void, white-hot rim | bright on dark, dark on bright, a single saturated hue on a muted scene |
| Sound on the payoff | hit sound cut to arrival | the sound marks whichever moment is the payoff |

The only hard rule across spells is the payoff: sound, damage number and the
accent land on the same frame. Everything else is a principle to express, not a
number to hit.

## Identity: make each spell its own

Before blocking a spell, pick its signature on these axes and write them in the
beat sheet. Two spells of the same caster should differ on at least two.

- **Verb**: what it does to reality. Pierce, crush, bind, bloom, erupt, freeze,
  unravel. The verb decides the shape of the motion.
- **Rhythm**: where the weight sits. Long wind-up and instant release
  (Blackglass), instant release and long aftermath, a pulse train, a slow swell.
- **Path**: from sky, from the ground, from the caster, appearing on the target,
  across the whole field.
- **Material**: glass, fire, frost, earth, light, shadow, wool. It decides the
  residue (shards, embers, frost, dust) and the sound.
- **Signature beat**: the one thing only this spell does. Blackglass's is the
  tear it leaves in the air. If a new spell's signature is "flies fast and leaves
  a trail", it is Blackglass again; pick another.
- **Scale**: a personal flick, a heavy blow, a field-wide event. Match the
  spell's cost and power.

Reusing a layer field (`through`, `align: flight`, emitter streaks) is fine; the
fields are tools. Reusing the whole stack, the timings and the signature beat is
the thing to avoid.

## Workflow

1. **Write the beat sheet.** The identity axes above, then the beats this spell
   actually has (not every spell has travel; a buff has no contact). For each
   beat: what the player should feel and which layer carries it. Ask the owner
   only about meaning ("what does this spell do to reality?"), never about numbers.
2. **Check the model before authoring.** §5b lists the layer fields (fly-through
   `through`, flight-aligned wakes `align: flight`, emitter `alignFlight` +
   `lateral`). Extend a field only when a beat cannot be authored, and make it
   general.
3. **Block it with stand-ins.** Author all layers from existing frames. Get the
   rhythm right before any new art.
4. **Capture and measure.** Runtime frame series against a tall target (treant)
   and a short one (rat): `tools/screenshot.ps1 -Runtime -Spell <id>` with
   `PP_RUNTIME_VERSUS=<enemy>`. Build the GIF (960 px, ~30 fps, 96 colours, no
   dither, loop, 1 s hold on the last frame) and read the activity curve. It
   describes the rhythm; it does not grade it. Look at the frames yourself.
5. **Check the principles, then the identity.** Is there one moment the eye
   locks onto? Does the aftermath stop competing with the payoff? Do sound and
   number land on it? Then put the GIF next to the caster's other spells: if it
   reads like one of them, change the signature beat, not the timing.
6. **Commission only the frame that carries meaning.** One painted still on
   `#00FF00` (dark interiors must stay opaque: green key, never luminance).
   The prompt names the spell, the file name, the save path, the beat it
   serves, and what it must NOT read as. Precedent:
   `docs/prompts/2026-10-02_petting_zoo_and_frost_flare.md`.
7. **Slice, recapture, compare.** Re-slice via the recipe, recapture both
   targets, put the before and after GIFs side by side for the owner.
8. **Pin it.** A timing test with literals for the beats that matter (travel,
   hit cue, aftermath hold/fade), precedent
   `SpellBaselineTimingTests.TheBlackglassSpearFliesFastLeavesABriefWakeAndStreaksAlongIt`.
