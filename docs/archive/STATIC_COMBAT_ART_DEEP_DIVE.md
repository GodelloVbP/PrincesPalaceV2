# Static Art That Feels Alive

**A production recommendation for Prince's Palace**  
**Audience:** game design, art, and Unity implementation  
**Date:** 4 September 2026

## Executive answer

Yes: one coherent drawing plus procedural performance is a viable default. It is already how much of the roster works. The decision is primarily about production cost, consistency, and how much bespoke motion an actor deserves—not about single-sprite feasibility.

The governing principle is **one drawing, posed rather than redrawn**, implemented in two tiers:

- **Tier A — rig:** for important actors whose anatomy, secondary motion, or signature attacks justify bones. `Core/Rig/` already implements this for the rat.
- **Tier B — static performance:** the default. One hero illustration receives anticipation, translation, restrained rotation, squash/stretch, hit-stop, afterimages, weapon arcs, impact shapes, particles, stage impulse, sound, and target reaction.

Allow one or two silhouette-breaking overlays or alternate poses only when topology genuinely changes. The practical question is whether Tier B is good enough often enough that Tier A remains rare.

The references do not prove Tier B. Darkest Dungeon uses rigged characters, and Limbus Company is also widely understood to be rig-driven, although a first-party technical breakdown was not found. What they demonstrate is the broader principle shared by both tiers: preserve one coherent drawing, pose it, and surround decisive movement with tightly synchronized audiovisual punctuation. Limbus Company's official description also emphasizes simultaneous action and “realtime brawl”; that concurrency contributes to the sensation of motion.

### What the existing 12-frame troll experiment actually establishes

The two untracked forest-troll sheets are dated 26 August 2026. They predate the rat measurement and the final per-frame direction added to `docs/STANCE_SHEET_SPEC.md` on 30 August. They also use a 4×3 grid, while the final specification requests a 3×2 grid of six frames. They therefore **do not test the final repaired specification**.

The reproducible measurements below use `tools/actor_stance_qa.py`'s own `churn_stats`, split evenly across the visible 4×3 grids. The idle uses its real alpha channel unchanged. Only the attack is keyed off its baked checkerboard.

| Sheet | Redraw ratio | Churn/step | Travel | Feet-band churn | Protocol B result |
|---|---:|---:|---:|---:|---|
| `sheet_idle_12.png` | **2.16** | 17.1% | 7.9% | **27%** | fails feet ≤12%; travel is at the 8% rejection boundary; ratio is only amber |
| `sheet_attack_12.png` | **1.75** | 41.6% | 23.8% | **72%** | ratio passes, but the sheet fails sequence, scale, and geometry checks |

Protocol A exposes failures that the aggregate redraw ratio cannot:

- **No gutters:** the first two idle rows fill their roughly 341-pixel-high cells, with as much as 41 pixels of ink crossing cell edges. This fails Protocol A #9 and makes the 4×3 format itself unsuitable.
- **Attack scale drift:** figure height ranges from about 270 to 341 pixels, a 25% spread, failing Protocol A #8's identical-scale requirement.
- **No coherent attack sequence:** cells 1, 11, and 12 have no club in hand; cells 4 and 8 are overhead poses; cells 5, 7, 9, and 10 are low swings. No chronological ordering produces one continuous swing.

The supplied playback GIFs confirm what those checks predict. The idle boils rather than breathes: planted feet and hands change shape, arm and torso poses jump, mushrooms migrate, and body scale shifts. The attack alternates between incompatible phases while the club appears and disappears. Its acceptable redraw ratio is therefore a false pass when read alone.

The defensible conclusion is narrower: **no sheet commissioned in the final specification's format and under its final directions exists yet, so that workflow's viability remains untested.** This neither disproves frame animation nor weakens the single-sprite option. The static recommendation stands on its lower production cost, absence of redraw risk, existing in-game baseline, and the proposed visual pilot.

## What the eye is really reading

“Fluid” combat does not require continuous anatomical motion. It requires the viewer to receive a clean sequence of causes and consequences:

1. **Intent** — a brief lean, compression, aim line, or held anticipation tells us what is about to happen.
2. **Velocity** — the attacker crosses space quickly, often too quickly for anatomical detail to matter.
3. **Contact** — several signals coincide on one exact instant: hit-stop, flash, slash shape, sound transient, stage impulse, target compression, and damage number.
4. **Weight** — the target moves away, debris continues, and the attacker overshoots or holds the contact pose.
5. **Recovery** — everything takes longer to settle than it took to strike.

The brain reconstructs the missing movement between those beats. The technique works because animation is fundamentally about change over time, not about the number of drawings. A single coherent drawing that moves with excellent timing will usually look more alive than twelve mutually inconsistent drawings.

## How the reference styles create motion

### Darkest Dungeon: rig economy and theatrical punctuation

The original Darkest Dungeon uses Spine-rigged combatants rather than flat pose swaps. Its presentation still offers transferable lessons:

- **Large, readable silhouettes.** Thick contours, large black masses, and restricted interior detail survive a fast cut and make a held pose feel intentional.
- **Economical continuous posing.** The drawing remains coherent while its parts move through a rig, allowing idle sway and attack motion without frame-to-frame redraw.
- **Shallow stage movement.** Figures advance or lean into the exchange, then return to their marks. The stage remains legible and the eye always knows who acted.
- **Impact replaces in-betweening.** Bright attack graphics, target flashes, blood/debris, recoil, screen motion, and audio occupy the exact interval in which a fully animated body would otherwise be scrutinized.
- **Asymmetric timing.** The strike is fast; the hold and recovery are slower. That creates force. Equal timing out and back reads as a UI tween.
- **Unified direction.** Chris Bourassa's GDC talk frames strong creative direction as both a unifying beacon and a “razor” for production choices: the lesson is to design limitations into the aesthetic rather than apologize for them.

Tier A adopts this principle directly through the project's rig. Tier B borrows the timing and staging while moving a monolithic image; it stands on its own production tradeoff and should not be presented as Darkest Dungeon's implementation.

### Limbus Company: concurrency, aggressive compositing, and event density

Limbus Company feels more continuously animated, but much of that sensation comes from **many inexpensive motion layers firing together**:

- Units act concurrently after commands are committed. Project Moon's official description calls this a combination of turn-based RPG and realtime brawl and says both sides act simultaneously. Several moving subjects make the whole shot feel busy even if each subject uses economical animation.
- Attacks are staged as short authored sequences: close distance, show a key pose, cover the motion with a graphic arc or burst, land the result, then separate.
- The screen frequently spends its visual budget on weapon trails, speed lines, particles, masks, flashes, contrast changes, camera motion, and large special-attack illustrations.
- Overlap matters: the attacker's motion, the target's reaction, the effect layer, damage UI, and sound begin at slightly different times. Motion persists after contact, avoiding the “everything starts and stops together” feel of a tweened cut-out.
- Exceptional skills get exceptional art. Ordinary actions can reuse a base language, while signature attacks buy a pose, cut-in, or bespoke effect where it has the greatest value.

The public evidence supports the simultaneous-brawl design and the visible result. It does **not** establish that every combatant literally uses one sprite, nor does Project Moon publish a technical breakdown proving a Spine/Live2D workflow. The useful lesson is therefore the composition and timing—not imitation of an unverified toolchain.

## The recommended Prince's Palace system

### The asset contract

For each actor, commission:

- **1 canonical combat sprite:** transparent, high-resolution, strong three-quarter silhouette, feet/pivot defined, weapon and hands readable.
- **1 hit/death treatment:** preferably the existing deformation/flash/fade recipe; an alternate collapsed sprite only when the base silhouette cannot communicate defeat.
- **0–2 action overlays:** weapon arm, jaw/head, cloak, spell focus, or foreground limb. These are not full sprites; they are small transparent pieces placed over the base.
- **A small VFX kit by attack family, not by character:** slash, stab, blunt, projectile, fire, poison, arcane, heal, guard, death.

Do not commission idle sheets. Do not ask an image generator to redraw the same creature six or twelve times. If a bespoke pose is worth buying, commission it as a single clean key pose.

### Transform hierarchy

Keep the current stage slot as the authoritative formation anchor and add child layers:

```text
StageSlot (formation position and depth scale)
└── PerformanceRoot (lunge, recoil, hop, rotation)
    ├── Shadow (independent scale/alpha)
    ├── BodyRoot (breath, squash/stretch, hit tint)
    │   ├── BaseSprite
    │   ├── RearOverlay
    │   └── FrontOverlay
    ├── WeaponTrail / AttackGraphic
    ├── ContactBurst
    └── StatusAnchor / DamageAnchor
```

This hierarchy is a conceptual ownership map, not a proposed refactor. Before adding any new rotation, verify the live actor `RectTransform` pivot actually sits at the ground contact. The current slot/actor separation already prevents the important ownership conflicts: re-anchoring should not cancel a lunge, and breathing should not overwrite impact squash.

### A default melee beat (about 0.50 seconds)

| Time | Attacker | Target | Effects / stage / audio |
|---:|---|---|---|
| 0–70 ms | Compress 4–7%, lean/rotate 2–4° away from target | Still | Quiet cloth/weapon cue |
| 70–125 ms | Snap 30–40% of the gap; stretch 8–12% along travel | Still | One faint afterimage; trail begins |
| impact | Hold or overshoot slightly | Instant 15–25% compression and short recoil | Existing weight-derived hit-stop; silhouette flash; impact burst; sharp sound; stage impulse |
| 170–300 ms | Settle at contact, then begin return | Spring toward shape; debris continues | Trail and particles decay; damage number arrives just after the hit |
| 300–500 ms | Ease home, slower than outbound | Ease home | Low-frequency tail/reverb |

The table illustrates relative rhythm only. It must not introduce a second clock: `FightBeatPlayer` already derives hit-stop and stage shake from damage weight, scales beats through `BeatSpeedMultiplier`, and flushes contact cues at one impact instant. New visuals should subscribe to that existing instant.

### Idle without an idle sheet

The existing `BreathCurve` and `StageActorAnimator.SetBreath` already provide the body's restrained loop. Do not duplicate them. Potential additions are limited to:

- rotation: ±0.3–0.8° around the feet;
- shadow: inverse scale/alpha, very subtle;
- optional overlay (tail, cloth, flame): delayed by 80–180 ms or run at a nonmatching period;
- occasional blink or spark as an event, not part of every loop.

Avoid continuous vertical bobbing at equal speed. It reads as floating. Motion should dwell near the top and bottom, and the feet must remain visually planted.

### Attack families from one sprite

| Family | Body motion | Foreground drawing | Contact language |
|---|---|---|---|
| Slash | short recoil, diagonal lunge, small rotation | broad tapered arc crossing the target | white cut line → colored burst → thin particles |
| Stab | narrow horizontal stretch, fast close, hard hold | spear/line trail with a bright tip | tiny high-energy burst; minimal stage impulse |
| Blunt | deeper anticipation, slower outbound, large target squash | thick crescent or displaced air ring | longer hit-stop, dust chunks, stronger low-frequency shake |
| Projectile | body leans back then releases; actor need not travel | projectile with trail and launch puff | target-local burst and recoil delayed by travel time |
| Cast | small rise/opening; overlay or mask exposes focus point | glyph, ribbons, particles, light pulse | stage need not shake; use contrast and sound swell |
| Guard | compress backward and widen slightly | shield/parry shape in front | sparks, ring, short reverse impulse; no damage-style recoil |
| Death | balance breaks, root rotates/translates down, fade/desaturate | dust/embers/ink breakup | remove shadow late; let particles outlive the body |

## What the current code already gives us

Prince's Palace is unusually close to this architecture already:

- `StageActorAnimator` has fast-out/slow-return lunges, directional travel, squash/stretch, a separate breath channel, recoil/punch deformation, afterimages, and a protected home position.
- `FightBeatPlayer` already owns wind-up, impact, follow-through, hit-stop-sensitive pacing, target recoil, concurrent flinch playback, and world/UI stage mirroring.
- `StageHitFlash` and `RigHitFlash` provide silhouette-aware hit flashes rather than flashing a rectangular image bound.
- `IStancePlayback` is already an abstraction between the beat and the visual representation. That is the seam for a one-sprite performance implementation if the pilot justifies one.
- `StageShake` deliberately moves the stage racks instead of the camera, keeping the screen-space painted HUD fixed.
- `BreathCurve` already drives procedural idle deformation.
- `SoundController`, per-skill `sfxPath`, stance sound timing, and character voice playback already provide contact and voice audio. The pilot may require new anticipation or aftermath clips; those assets have not been verified as present.
- The project includes Unity 6, URP 2D, Unity's 2D Animation package, particles, UI Particle, and UI Effect. No new commercial dependency is required.

The project therefore does **not** need a new baseline system. Static actors already receive procedural movement. The first decision requires only a small visual pilot; formal playback/profile infrastructure comes later if that pilot succeeds.

## Concrete implementation recommendation

### Phase 1: extend one existing static actor

Add only the missing cues to one existing still:

- anticipation compression;
- an attack graphic;
- an impact burst;
- an afterimage on the return;
- appropriate sound, reusing current assets where they fit and identifying any missing cue explicitly.

Route the graphics through the existing uGUI stage. `com.coffee.ui-particle` is already present for particles; avoid an unresolved SpriteRenderer/Image split. Route shake through `StageShake`, never the camera.

### Phase 2: formalize only after the pilot passes

If the pilot succeeds, add `StaticStancePlayback` beside frame and rig playback. Reuse the existing duration/impact/sound timing record rather than inventing a fourth format. The new data should contain only `travelStyle`, curve/easing choice, overlay cue, `attackFxId`, and `impactFxId`.

Profiles belong in JSON beside the existing content, or in the rig `animations.json` schema where appropriate. Do not create hand-authored ScriptableObjects under `Resources/Content/`; `ContentBuilder` owns generated content assets.

Only then build the smallest uGUI effect pool justified by the pilot. Ten characters sharing one slash texture but using different weight, travel, scale, tint, sound, and recoil can still feel distinct.

Pool uGUI-compatible effects for:

- arc or thrust graphics;
- impact bursts;
- speed-line wedges;
- particles/debris;
- afterimages;
- ground dust;
- optional full-screen flash/vignette.

Unity's Particle System supports trails that remain in a particle's wake. Camera impulse is intentionally excluded: the existing `StageShake` protects the fixed HUD while providing the same impact cue.

### Keep two escape hatches

One sprite cannot convincingly express every topology change. Permit:

- **alternate key pose:** for overhead slam, fully curled beetle, huge roar, or collapsed death;
- **detached overlay:** for a weapon arm, jaw, wing, tail, cloth strip, or spell focus.

These are exceptions selected by silhouette need. They are not an invitation to rebuild six-frame sets.

### Migrate tests only with migrated content

`FightPlayableTests` currently protects the party leader's multi-frame attack content; it is not a blanket rule against static enemies. Keep it until Sheep and Golem are deliberately migrated. For static profile validation, test only deterministic contracts:

- a damaging beat has a positive wind-up or explicit instant class;
- one impact event fires exactly once;
- all transforms return to baseline after the beat;
- pooled effects are released;
- the same profile behaves correctly at accelerated test speed.

Do not unit-test a “visible displacement” by sampling a fast mid-beat frame; it would be timing-sensitive and flaky. Judge perception through the captured pilot.

## Art direction rules that make the illusion hold

- Draw for **silhouette first**. The body must read at combat scale without interior animation.
- Put the pivot at the contact with the ground; leave transparent breathing room around likely arcs and overlays.
- Keep the base pose loaded but not perfectly symmetrical. A slightly coiled pose can plausibly move forward, backward, cast, or defend.
- Separate foreground weapon/limb only when it crosses a large arc. Tiny segmented parts produce paper-doll wobble.
- Let effects obscure the least believable transition for 1–3 frames, but never obscure the target or contact point so thoroughly that causality is lost.
- Reserve large effects for rare attacks. If every hit shakes, flashes, freezes, and fills the screen equally, nothing has weight.
- Audio is part of animation. Give anticipation, contact, and aftermath different sounds; do not place one generic hit sound over the whole action.

## Risks and controls

**Risk: the sprite looks like a cardboard cut-out.** Use short rotation ranges, pivot from the feet, combine translation with deformation, and let secondary effects lag. Do not swing the entire body through a large angle.

**Risk: attacks all feel identical.** Author motion by attack family and weight class; vary anticipation, path, hit-stop, target response, and recovery—not just VFX color.

**Risk: effects become visual noise.** Establish a value hierarchy: actor silhouette, weapon path, contact point, then particles. Keep damage UI later and above the contact, not exactly on it.

**Risk: one pose cannot communicate the action.** Use the alternate-key-pose escape hatch when the silhouette's topology changes. A beetle curling into a ball is a pose problem, not a particle problem.

**Risk: procedural movement feels smooth but weak.** Prefer stepped or sharply eased anticipation/contact curves over universal smoothstep. Smooth motion is not the same as fluid motion; contrast in velocity creates life.

## Recommended pilot

Use an existing static creature as the first, minimal pilot. The baseline system already exists, so implement only the five missing cues listed above and compare before/after at normal speed.

1. Select a still-driven ordinary melee actor.
2. Add anticipation compression, attack graphic, impact burst, return afterimage, and verified audio.
3. Capture before/after video at actual combat size and normal speed.
4. Judge four questions: Can you read intent? Is exact contact obvious? Does the blow have weight? Does the actor return cleanly to formation?
5. If it passes, compare a static rat variant against the existing rat rig without disabling or discarding the rig. This answers whether Tier B is good enough that Tier A can remain exceptional.

Only after the rat works should the system be generalized. The likely winning production policy is:

> **One coherent drawing, posed: static procedural performance by default; rig when the creature earns bones; a bespoke key pose only when topology changes.**

## Evidence and limitations

- [A Torch in the Dark: Using Creative Direction to Light Darkest Dungeon](https://www.gdcvault.com/play/1023082/A-Torch-in-the-Dark), Chris Bourassa / Red Hook Studios, GDC 2016. Primary developer talk; supports the production and coherent-direction argument.
- [Road to the IGF: Red Hook Studios' Darkest Dungeon](https://www.gamedeveloper.com/audio/road-to-the-igf-red-hook-studios-i-darkest-dungeon-i-), Game Developer interview with Chris Bourassa and Tyler Sigman, 2016. Bourassa explicitly identifies Spine as the game's 2D animation package.
- [Darkest Dungeon animation portfolio](https://brx.artstation.com/projects/VAVxX), Brooks Gordon, animator/technical artist. First-person production record describing Photoshop cutouts, rigging, weighting, and animation in Spine.
- [Darkest Dungeon release trailer](https://www.youtube.com/watch?v=h-mXN3akTPU), Red Hook Studios, 18 January 2016. Primary audiovisual source; supports direct observation of the combat presentation.
- [Limbus Company official game-system page](https://limbuscompany.com/), Project Moon, accessed 4 September 2026. Primary source; supports simultaneous action and “turn-based RPG and realtime brawl.”
- [Limbus Company Steam page](https://store.steampowered.com/app/1973530/Limbus_Company/), Project Moon, accessed 4 September 2026. Primary source; corroborates simultaneous combat and provides official trailers/screenshots.
- [Spine runtime skeletons](https://eu.esotericsoftware.com/spine-runtime-skeletons), Esoteric Software, accessed 4 September 2026. Primary technical documentation; supports the general explanation of bones, hierarchical transforms, mesh attachments, and procedural adjustment. It does not prove that Limbus Company uses Spine.
- [Unity Particle System Trails API](https://docs.unity3d.com/ScriptReference/ParticleSystem.TrailModule.html), Unity Technologies, Unity 6 documentation, accessed 4 September 2026. Primary technical documentation.
- Local source review: `StageActorAnimator.cs`, `StageShake.cs`, `FightBeatPlayer.cs`, `StancePerformance.cs`, `RigStancePlayer.cs`, `RigStanceClip.cs`, `FightPlayableTests.cs`, `Packages/manifest.json`, `tools/actor_stance_qa.py`, and `docs/STANCE_SHEET_SPEC.md`, inspected 4 September 2026.

The strongest limitation is the absence of a first-party Project Moon technical postmortem describing Limbus Company's exact animation toolchain. The report therefore separates observable staging and official combat design from implementation claims. The Darkest Dungeon implementation is treated as rig evidence, not static-sprite evidence. The recommendation ultimately rests on the project's measured sheet experiment, current rig and static systems, and a deliberately small in-game pilot.
