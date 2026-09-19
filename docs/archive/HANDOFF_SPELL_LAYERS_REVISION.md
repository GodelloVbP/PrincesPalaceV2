# Revised brief for Claude: durable layered spell presentation

Date: 2026-09-08
Status: owner-requested revision of the proposed layered spell presentation plan. This document is a design and implementation handoff, not a report that the system has been built or verified.

## Owner intent

Build a SOLID, maintainable spell presentation system that does not need replacement when the next richer spell arrives. Water is the first consumer, not the architecture. New combinations of supported visual behaviours must be authored through content. Genuinely new rendering behaviours may require a small, local extension; they must not require rewriting combat timing, the scheduler, existing content, or unrelated renderers.

Do not interpret durability as implementing every conceivable effect now. Prove the design with the real variations already known: Water and Cinderfault. Avoid both four hardcoded spell slots and a speculative general-purpose effects engine.

Use this brief to revise PLAN_SPELL_LAYERS.md against the actual repository. Verify existing classes, terminology and tests before adopting names from the earlier proposal. Preserve unrelated work. No gameplay mechanic or damage formula changes are requested.

## Changes required to the original proposal

1. Replace the fixed flight/trail/contact/particles blocks with an ordered collection of authored layers. Permit multiple instances of each supported rendering behaviour.
2. Support particle emission during flight as well as bursts at impact. Arrival-only particles do not deliver the requested water shedding.
3. Remove the contradiction between arrival and contact impactFrame. Define one authoritative impact cue per gameplay hit.
4. Remove beat-end cleanup of living visual tails. Combat completion and visual completion are distinct.
5. Replace ownership by stage slot with ownership by cast instance. Concurrent effects on the same target must coexist.
6. Decouple animation FPS from travel duration.
7. Normalize legacy definitions through a compatibility adapter into the common playback model. Avoid two permanent orchestration implementations.
8. Validate with real-time in-game playback as well as screenshots and deterministic tests.

## Composition model

A layer describes three independent concerns:

| Concern | Required initial choices |
| --- | --- |
| What renders | Animated sprite, still/ribbon sprite, ballistic particle emitter |
| Where it belongs | Caster, projectile, target, shared formation, fixed battle position |
| When it runs | Release, arrival, authoritative impact cue, or an explicitly named visual event plus offset |

Allow content to express loop/once playback, FPS, start frame, local offset, scale, facing, sorting category, and end/fade policy where applicable. Give layers stable identifiers when other layers reference them. A projectile-following emitter can reference that projectile layer without assuming a unique global flight slot.

Use typed, validated options rather than an unrestricted bag of fields. Define supported combinations and errors. Do not create a graph editor, scripting language, reflection-driven plugin framework, or an interface for every class. If event references form dependencies, validate missing references and cycles before playback.

Keep event scopes explicit: cast-level release/formation effects versus target-level arrival/impact. Per-target visual fan-out must not accidentally repeat a shared ground effect or duplicate gameplay hits.

## Modules and SOLID expectations

The presentation module exposes a small interface for starting a resolved presentation, advancing it through the existing clock, and cancellation/cleanup. It hides scheduling, layer lifetimes and resource ownership from combat callers.

- Content resolution validates and normalizes definitions; it does not operate pooled Images.
- Scheduling handles visual events and lifetimes; it does not implement droplet physics.
- Rendering implementations draw sprites, ribbons and particles; they do not apply damage.
- Cast instances own their active resources until those resources finish or are cancelled.
- Put seams where behaviour actually varies. Reuse the existing clock and battle presentation contracts where possible.

Document how one new rendering behaviour would be added. A future beam may need its own renderer and content validation, but should not require changing damage dispatch or unrelated scheduling. This is a design walkthrough, not a request to implement beams now.

Pooled uGUI Images are acceptable if they fit the current battle renderer. Unity Particle System is not a requirement. Explain the choice against actual coordinate spaces, sorting and performance needs.

## Timing and gameplay contract

Specify release time, arrival time, contact playback start and hit-cue time separately. For Water, arrival starts compression, then one designated contact frame triggers the synchronized hit response. A zero-delay impact remains valid for other spells. Do not trigger once at arrival and again at impactFrame.

The existing combat mechanism remains responsible for applying the resolved hit. Presentation supplies at most the agreed timing signal, not damage logic. State whether current gameplay resolution and displayed impact are already separate, and preserve that contract deliberately.

Visual tails never delay the next actor. Cosmetic pool exhaustion or missing cosmetic artwork must not suppress damage, duplicate it, or leave combat waiting. Define bounded fallback timing and explicit cancellation semantics consistent with combat ownership. Distinguish a visual-only stop from cancellation of a combat action.

Loop FPS is independent from flightSeconds. Travel duration changes motion, not internal animation speed. Playback must correctly cross event times on a large clock step, delivering each cue once. Pause/time-scale/ClockOverride behaviour applies consistently to every layer.

## Lifetime, anchors and pooling

Beat completion stops blocking; it does not flush active tails. Emission can stop while already emitted particles finish. Battle exit and explicit cancellation release owned resources. A second cast must neither restart nor steal the first cast's living objects.

Distinguish following an anchor from sampling its position once. Detached particles simulate in a stable battle coordinate space. They must not follow the projectile after emission or jump when a target's UI anchor moves. Specify facing/mirroring, canvas scaling, target disappearance, and snapshot/follow behaviour for target impacts and formation layers.

Use semantic draw categories: shared ground behind actors, appropriate contact/foreground spray, and damage UI above effects. Verify against actual battle sorting rather than relying on sibling order assumptions.

Pool resources with explicit ownership and restoration of transform, sprite, tint, alpha, parent and playback state. Size pools from measured representative casts. Cosmetic particles can be dropped under pressure; essential hit timing must remain correct. Measure steady playback allocation after warm-up and document overflow policy.

## Water pilot

Source pack: Assets/_Project/Art/Sheets/Spells/prismatic_bolt/water. Confirm current asset location and canonical spell ID: the original proposal calls it Prismatic Orb while art uses Prismatic Bolt. Do not silently rename gameplay content.

Required composition:

- Compact core sprite loop moving towards the target's near contact surface.
- Short rear-attached wake with controlled length and fade.
- Low-rate flight emitter using the reusable water atlas. Drops inherit part of forward velocity, separate visibly, slow through drag and fall through gravity.
- One-shot contact animation: compression, splash crown, breakup and clearing centre.
- A separate impact emitter reusing the same atlas: directional burst, fast small drops and heavier slower drops.

Minimum particle controls: emission rate or burst count, source anchor/offset, directional spread, initial speed range, inherited velocity fraction, drag, gravity, lifetime range, sprite selection, size and rotation variation, and simple fade/scale over lifetime. No collision or sub-emitters needed for this pilot. Treat random seeds explicitly for repeatable previews/tests.

Initial tuning only: travel around 0.2–0.3 seconds; a brief readable compression; rapid main splash; droplets continue for roughly 0.3 seconds. Tune at actual battlefield scale. Preserve the target's head and damage number. The hit should feel like a dense water mass striking a surface, not a distant decorative splash.

The delivered art is source material, not an unquestionable runtime spec. Inspect alpha edges, core halo, loop seam, frame registration and padding. Contact frame 1 includes an approaching ball; select a playback start that avoids replaying approach after arrival. Record pivots and scale consistently. The 1774 × 887 atlases need explicit slice rectangles or a documented resampling step; do not assume equal integer cells. Preserve source files and record preprocessing in recipes.

## Compatibility and content durability

Normalize existing single-block definitions into the new runtime representation through an adapter. Preserve existing anchors, impact-frame semantics and timing through regression tests. Temporary incremental routing is acceptable during implementation, but the completed design must have one timing/lifetime orchestration path.

Validate paths and recipes, frame ranges, positive durations, references, anchors, sorting choices and legal combinations with spell/layer/field-specific errors. Preserve the existing schema versioning mechanism if present; otherwise add an explicit format version. Document defaults and migration for future incompatible semantic changes. Never silently reinterpret old content.

## Proof before acceptance

1. Capture the existing Water baseline and representative existing spells.
2. Demonstrate Water with independent flight shedding and impact spray.
3. Demonstrate Cinderfault through the same model: one shared ground sequence, separate target plumes, aligned impact peaks. Spell-specific artwork/timing data is expected; spell-ID branches in orchestration are not.
4. Use existing art for a synthetic caster-anchored effect with two delayed overlapping bursts. This proves multiple instances and scheduling without a new art pack.
5. Test exact hit cue dispatch, large clock steps, zero-delay hits, legacy timing, concurrent casts on the same target, tail survival after beat end, cancellation, target disappearance and pool exhaustion. Test behaviour through the module interface rather than internal field layouts.
6. Verify release/reacquisition restores pooled state and no effect survives battle teardown. Measure allocation after warm-up.
7. Preview with the runtime sampling/timing logic where practical; avoid a second divergent simulator. Record real-time in-game playback, including two consecutive casts while old droplets remain visible. Screenshots supplement this evidence.
8. Rebuild content/scenes as required by the repository and run relevant tests. Use deterministic image comparisons where available; do not promise byte-identical captures from an uncontrolled renderer.

## Delivery to the owner

Return the revised plan with actual repository seams, implementation milestones and evidence-backed scope estimate. Distinguish observed limitations from assumptions. Then follow the agreed implementation workflow.

The implementation handoff must include an authoring example, Water settings, slice/pivot recipe, in-game preview instructions, compatibility strategy, test results and real-time recording. Explain where a new rendering behaviour is added and which existing modules stay untouched.

Acceptance: new compositions of supported behaviours are content-only; Water and Cinderfault share the same model; existing spells retain behaviour; gameplay timing remains reliable under failure; overlapping tails work; and the owner can see the intended fast, heavy water collision in game.
