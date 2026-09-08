# Spell Feel Deep Dive — Adversarial Revision

**Project:** Prince's Palace  
**Date:** 4 September 2026  
**Revision goal:** Critique the first proposal as an outside reviewer, remove unjustified scope and architecture, and produce a plan that is more likely to improve the game rather than merely improve its effects system.

**Canonical spell definition:** See `docs/SPELL_DESIGN_STANDARD.md`. That document owns what qualifies as a global spell and the required design contract; this document owns the staged presentation-improvement plan.

## Outsider verdict

The first plan had the right central insight—spell feel is the relationship between choice, anticipation, contact, consequence, and recovery—but it was too eager to turn that insight into a platform.

It proposed a coordinator, a broad presentation schema, multiple pooled effect types, four-part audio, AOE spread modes, persistent status anchors, progression variants, accessibility controls, and full-roster coverage before proving that the existing spells become more fun when any of those pieces are added. That is a credible engine roadmap, but not yet a disciplined product plan.

The more serious issue is that it treated **feedback** as if it were the whole of **fun**. Better timing, animation, and sound can make an existing decision satisfying. They cannot make a weak decision interesting, distinguish spells whose tactical outcomes feel alike, or create anticipation when the player already knows the result is obvious. Before scaling presentation, we need to test both halves:

- Is the spell a compelling choice?
- Does the game present that choice and result exceptionally well?

The revised recommendation is therefore:

> Prove the complete experience with two contrasting existing spells, repair only the data that blocks honest feedback, and earn every reusable system through demonstrated repetition.

Lightning Bolt and Mud Burst are the best first pair. Both already have art, placement metadata, and real timing, but their desired feel is opposite: lightning should be immediate, sharp, and electrically precise; mud should gather, travel with weight, and land with mass. If the same small set of presentation seams can make both feel distinct, the approach is worth extending. Woolgathering then tests positive/non-damage feedback without allowing the first implementation to balloon into healing, warding, AOE, summons, and transformation at once.

## What the first plan got right

The following decisions survive the review:

- Keep synchronous combat resolution and recorded `CombatBeat` playback.
- Keep `FightBeatPlayer` as the authority for when an impact is presented.
- Preserve pre/post snapshots so cause appears before the health change.
- Use stage-local shake rather than shaking the ornamental HUD.
- Treat bespoke frame sequences as one layer of a cast, not the entire definition of spell feel.
- Let decorative aftermath overlap recovery and, eventually, the next beat where safe.
- Make damage, healing, protection, and transformation use different response languages.
- Judge timing in real-time video with audio, not only in isolated frames or accelerated tests.
- Reserve exceptional duration and screen coverage for exceptional actions.

These are foundations. They should not be confused with proof that the proposed feature set is necessary.

## Where the first plan fails under pressure

### 1. It quietly changes the scope from spells to every skill

There are 32 authored skills, but many are physical attacks or utility abilities: Headbutt, Battering Ram, Grapple, Trunk Slam, and others should not be forced through a magic grammar merely to achieve “100% spell coverage.” The first plan used the count of all skills to justify a spell platform, then proposed mapping every skill to spell families. That risks flattening melee identity and duplicating the existing contact-cue work.

**Correction:** scope the first initiative to abilities whose authored fantasy is a cast, magical manifestation, supernatural state change, or summon. Physical skills continue through the static/melee performance language unless a shared primitive genuinely fits. Build one common impact event, but do not make all skills look like spells.

### 2. It assumes presentation can create fun independently of mechanics

The plan defines responsiveness and impact well, but not why a player looks forward to pressing one spell instead of another. It barely tests targeting value, resource tension, combo payoff, build expression, or whether the visible outcome matches the promise made in the skill detail card.

**Correction:** every spell entering the presentation pass first receives a five-minute gameplay audit:

1. What choice does this create that another button does not?
2. What is the player risking or spending?
3. What outcome should feel clever, lucky, powerful, or relieving?
4. What combat state visibly changes?
5. What would make this cast memorable after the effect is removed?

If the answers are weak, mark a design issue. Do not bury it under particles.

### 3. It designs a general system before measuring two casts

`SpellPerformancePlayer`, family recipes, four sound phases, persistent anchors, shared fields, chain behaviors, and several effect pools are plausible abstractions. None is yet proven necessary. The current `SpellVfxPlayer` plus `FightBeatPlayer` delegates may be sufficient for the first improvements.

**Correction:** the first vertical slice may add only:

- one explicit spell-impact callback;
- one explicit impact sound call;
- one caster anticipation/release cue;
- one target-local impact accent;
- one non-blocking aftermath accent;
- the minimum beat data required to present the correct result.

Create a new coordinator only after the two pilots cause duplicated orchestration or competing ownership in the existing player. Architecture is a response to demonstrated pressure, not a Phase 1 deliverable.

### 4. The current beat cannot support promises the plan makes

This is the largest technical omission.

- `CombatBeat` does not retain the resolved skill identity.
- `DamageType` is painted onto the beat later from the actor, so a multi-packet skill can be visually labelled with the caster's type rather than the actual packet types.
- AOE stores one `Amount`: the largest landed hit.
- `SplashTargets` stores additional target identities, but not their damage, miss, effectiveness, death, or status result.
- `ShowAmount` can therefore show only one number on the primary target.

The first plan promised per-target AOE impacts, weakness/resistance variants, semantic elemental reactions, and outcome-specific feedback without acknowledging that the playback record has already discarded those facts.

**Correction:** before advanced feedback, introduce a small immutable presentation result per affected target. Conceptually:

```text
PresentedOutcome
  target
  amount
  isHealing
  missed
  effectiveness
  damageTypes[]
  appliedStatusIds[]
  defeated
```

And identify the action without carrying a mutable `ResolvedSkill` through playback:

```text
CombatBeat
  actionPresentationId   // skill/content id or stable presentation id
  outcomes[]             // one entry for single-target; N for AOE
  vfx                     // current copied SpellPresentation
```

The existing primary fields can remain during migration. Do not implement this structure merely to make the model elegant; implement it when the pilot needs correct elemental or per-target feedback. Lightning's fixed Fire/Ice packet behavior is already enough to expose whether the current single `DamageType` is misleading.

### 5. It prescribes timing numbers without establishing the game's pace

“50 ms confirmation,” “0.40–0.80 seconds through impact,” and survey thresholds such as 4/5 sound rigorous, but they were not derived from Prince's Palace play sessions. False precision creates acceptance theater.

**Correction:** use relative timing rules first:

- acknowledgement appears on the first available rendered frame;
- anticipation is long enough to read once and short enough not to delay a common action;
- release is faster than recovery;
- visual, audio, and UI consequences share one perceived impact instant;
- standard spells return control quickly enough that repeated casting does not feel punitive.

Measure the current spells, tune A/B variants, then record the chosen ranges as project-specific budgets. Scores guide discussion; observed confusion, preference, and repeated-play fatigue decide the gate.

### 6. The audio recommendation is prematurely technical

The existing `SoundController` uses `PlayOneShot`, which already permits overlapping clips. A source pool is only required for independent routing, loops, per-voice pitch, spatialization, or precise voice control. The first plan prescribed a pool before demonstrating one of those needs.

There is, however, a concrete timing concern: content describes `sfxPath` as the sound that plays when a spell lands, while `FightBeatPlayer` currently plays it when the VFX begins, before waiting for the impact frame.

**Correction:** first move or reinterpret the existing cue deliberately. For the pilot:

- current `sfxPath` becomes the impact cue and fires at the impact event;
- add one optional onset/release cue only if silence during the wind-up makes the spell unreadable;
- add an audio pool later only if profiling or mixing requirements demand it.

### 7. It adds UI animation without proving a dead-input problem

The command UI already has hover behavior, mana preview, button press animation, and immediate synchronous action resolution into playback. The assertion that there is a “dead frame” was not measured.

**Correction:** capture from the confirming click through the first visible caster/VFX change. If a gap is visible, overlap menu dismissal with existing caster stance/VFX startup. Do not add a target tick, cast nameplate, or resource pulse by default; each competes with the stage for attention.

### 8. It defers creative direction and accessibility too late

Whether spells may wash the whole screen, how fast routine combat should feel, and how much flash/shake is acceptable will shape the pilot. They are not decisions to postpone until after the system is built. Similarly, reduced flash and shake affect how effect primitives should communicate impact from the start.

**Correction:** use the recommended defaults below during the pilot and expose intensity through existing settings seams only when the pilot establishes a real need.

## Revised product target

A good cast should create a short emotional sentence:

```text
I chose this deliberately → power gathered → it travelled or manifested clearly
→ the target responded at the exact result → the aftermath expressed what changed
→ control returned before satisfaction turned into waiting.
```

The target is not “continuous animation.” It is continuous comprehension.

Five pillars define the work:

1. **Agency:** the spell is a meaningful choice with a visible cost and payoff.
2. **Causality:** origin, direction, target, and result form one readable chain.
3. **Contact:** the important audiovisual signals agree on a single perceived instant.
4. **Identity:** timing and material behavior distinguish spells, not colour alone.
5. **Pace:** common casts are satisfying repeatedly; rare casts earn more time.

## Recommended creative defaults

These are decisions, not universal truths. Use them in the pilot unless directed otherwise.

- Routine spells stay local to caster, path, and target. No full-screen wash.
- The painted HUD never shakes.
- Standard casts are brisk; recovery and decorative decay may overlap the next presentation where safe.
- Signature names stay in the existing detail/log language rather than adding a cast banner.
- Weakness, resistance, and miss receive distinct contact treatments only when the playback record carries that result honestly.
- Reduced-flash and reduced-shake behavior is considered in every primitive even before dedicated options are exposed.
- Spells use bespoke art only when procedural or shared primitives cannot communicate their material or spatial behavior.

## Revised cast model

Use four authored moments, not eight mandatory phases:

| Moment | Responsibility | Blocking? |
|---|---|---|
| Begin | show actor intent; start existing frame sequence | yes |
| Release | energy leaves caster or manifestation commits | yes, only when visually necessary |
| Impact | update vitals/state; target response; popup; impact visual and sound; shake/hit-stop | yes |
| Settle | restore actor/target; let decorative residue decay | only the readable recovery portion |

Confirmation is a UI/input property, not a spell-animation phase. Travel is a behavior between release and impact. Aftermath is part of settle, with its decorative tail allowed to become non-blocking.

`FightBeatPlayer` should remain the clock. The initial implementation should add narrowly named delegates or methods around these moments. If three or more primitives need shared lifecycle management, then extract `SpellPerformancePlayer` with a measured reason.

## Vertical slice: what to build first

### Pilot A — Lightning Bolt

**Why:** It reveals synchronization errors immediately. Its current nine frames land on frame five and its fixed damage packets complicate a simple “caster element” reading.

**Desired sentence:** contained charge → abrupt electrical release → exact target snap → short branching decay.

**Changes to test:**

- caster-local pre-charge accent using existing art or a minimal procedural flash;
- explicit impact sound at the actual impact event;
- very short target flash/hold shaped as electricity rather than a generic heavy recoil;
- one-frame-or-short-lived impact accent and a restrained branch/afterglow;
- no stronger stage shake merely because the effect is bright.

**Questions it answers:**

- Is the existing frame sequence sufficiently readable with better event timing?
- Does crossfade improve the bolt, or does it create double-image mush that needs a per-sequence `cut/hold/dissolve` mode?
- Does the current beat carry enough elemental truth for the desired response?

### Pilot B — Mud Burst

**Why:** It exercises the existing depart frame, long 26-frame charge, moving effect box, mirrored travel, and authored impact point.

**Desired sentence:** earth gathers and turns → projectile commits with weight → target is struck by mass → debris settles downward.

**Changes to test:**

- clearer release accent exactly where the effect leaves its caster hold;
- faster initial travel or revised travel easing if the current quadratic path reads as dragging;
- impact sound at contact, with a low/mid-frequency transient;
- material-specific target squash and ground debris;
- aftermath that falls and dissipates without holding the next beat.

**Questions it answers:**

- Can one sequence express charge, travel, and impact, or do release/impact need separate layers?
- Does the current single moving box constrain scale or layering?
- Can decorative aftermath safely outlive blocking playback?

### Pilot C — Woolgathering, only after A and B pass

**Why:** It has no current VFX and tests whether the approach can create a readable, pleasant spell from shared primitives rather than bespoke animation.

**Desired sentence:** wool/resource gathers inward → Shawn visibly receives it → health recovery blooms and settles.

**Changes to test:**

- inward rather than outward motion;
- positive target deformation—gentle expand/settle, never recoil;
- healing popup and health bar aligned to the receive moment;
- no stage shake;
- soft tail that does not delay control.

Do not include Roar, summon, transformation, persistent wards, or AOE in the first slice. Each changes stage state or needs additional result data. They are deliberately deferred until the core event language passes.

## The gameplay-fun audit

Before polishing a spell, add one short entry to a spell-feel inventory:

| Question | Example answer for Lightning Bolt |
|---|---|
| Tactical job | mixed fixed-damage single-target spell |
| Cost/tension | mana and opportunity cost versus another skill |
| Anticipated payoff | precise burst with effectiveness interactions |
| State change to show | target HP loss and any elemental effectiveness |
| Signature memory | the instantaneous electrical contact, not effect size |
| Overlap risk | may feel too similar to Frost Flare if only recoloured |

Flag, do not silently solve, any spell with:

- no distinct tactical job;
- no meaningful target or timing decision;
- cost that does not create tension;
- payoff that another spell already provides more clearly;
- description and resolved outcome that disagree;
- presentation fantasy incompatible with its mechanic.

This makes presentation work a diagnostic tool for design rather than a cosmetic blanket.

## Minimum technical changes

### 1. Expose the existing impact moment

Split the current impact block into a named, testable dispatch without changing resolution order. Spell-specific impact visual and impact audio subscribe there. The first implementation should preserve:

1. victim stance/silhouette sync;
2. post-impact snapshot;
3. amount/miss popup;
4. target flash and response;
5. stage response;
6. voice.

Do not over-specify sub-frame ordering until captures show a perceptual mismatch. In Unity UI these calls occur on one rendered frame; maintainability matters more than an arbitrary nine-step ordering.

### 2. Correct sound semantics

Move `vfx.sfxPath` from cast opening to impact, or rename its contract everywhere if the current onset timing is intentional. The recommended choice is impact because the content documentation already promises it and the existing Frost Flare and Lightning Strike clips appear contact-oriented.

Add `releaseSfxPath` only if Mud Burst demonstrates a missing release cue. Avoid onset, release, impact, and tail fields for every spell until each phase has a real asset and a proven purpose.

### 3. Add only proven presentation metadata

Do not add the broad schema from the first plan. Candidate additions are earned in this order:

1. `playbackMode`: `cut`, `hold-dissolve`, or another proved sampling mode;
2. `impactFx`: only if a separate target-local layer is needed by both pilots;
3. `releaseSfxPath`: only if release cannot be read from visuals;
4. `aftermathSeconds`: only if non-blocking cleanup cannot be derived from the asset;
5. `cueProfile`: only when the third spell needs to reuse a set of defaults.

Continue using existing `path`, `seconds`, `impactFrame`, `anchor`, `size`, `departFrame`, `impactX/Y`, `sfxPath`, `approach`, and `shake` wherever they already express the requirement.

### 4. Repair result data before outcome-specific feedback

For single-target pilots, add the smallest stable action identity and resolved outcome data required by the capture. Before AOE work, replace the lossy “largest amount plus target list” presentation with per-target outcomes.

Do not route domain mechanics back through the view. The session records facts at resolution time; playback only interprets those facts visually.

### 5. Extend existing pools only when exhausted

The scene already owns pools for spell players and damage popups. Add a pooled impact/aftermath primitive only when the pilot uses it. It must:

- ignore raycasts;
- support multiple targets without one cast restarting another's effect;
- reclaim on completion and `Flush`;
- restore sprite, colour, scale, position, material values, and audio state;
- allocate nothing during steady playback after warm-up.

## Material grammar

Do not define a full taxonomy in code yet. Use this as art/timing direction for the pilots and later family defaults:

| Material | Anticipation | Contact | Aftermath | Avoid |
|---|---|---|---|---|
| Lightning | contained flicker, tightening pitch | sharp flash and snap | brief branching fade | long mushy crossfade, heavy earth shake |
| Frost | air draw-in, crystalline formation | brittle flare and short hold | shards/mist settle | fire-like expansion and recoil |
| Mud/earth | rotation, accumulation, downward weight | squash, low transient, grounded burst | falling chunks/dust | weightless glow or upward sparkle |
| Healing | inward gathering, warm opening | recipient brightens/expands | slow upward motes | damage flash, recoil, shake |
| Ward | boundary traces then closes | seal/ring lock | persistent restrained state | replaying a full cast every status tick |
| Transformation | silhouette occlusion and controlled reveal | held new silhouette | old material sheds away | routine duration or full-screen noise without state clarity |

Spell identity should be recognizable from timing and material motion in grayscale. Colour is reinforcement.

## Audio strategy

For the pilot, use at most three functional moments:

- **charge/release**, only when the visual needs help communicating action;
- **impact**, mandatory for damaging pilots and aligned to the impact dispatch;
- **tail**, baked into the impact clip unless overlap or duration becomes a demonstrated mixing problem.

Keep `AudioLevels` normalization. Review spells in the full battle mix with music and voice. Judge:

- whether the transient survives the music;
- whether repeated casting is fatiguing;
- whether the material reads without watching;
- whether the sound starts at the event the eye perceives;
- whether tails mask the next action.

Only add source pooling, pitch variation, category ducking, or loops in response to a specific failed criterion.

## Revised delivery plan

### Gate 0 — Baseline and decisions

**Deliverables**

- normal-speed video with audio for Lightning Bolt and Mud Burst in both directions;
- time markers for confirm, first stage response, release/departure, impact, health update, popup, and return to next actionable state;
- grayscale and reduced-shake review of both casts;
- gameplay-fun audit entries for the two spells;
- explicit confirmation of the creative defaults in this document.

**Gate**

The team can name the top two perceptual failures for each spell. If it cannot, do not implement a system; gather better footage or playtest observation.

### Gate 1 — Timing repair

**Deliverables**

- named impact dispatch in `FightBeatPlayer`;
- spell SFX synchronized to impact;
- verified click-to-first-response behavior;
- per-sequence playback mode only if Lightning proves the current crossfade harmful;
- before/after captures with no new bespoke art.

**Gate**

Observers consistently prefer the revised timing and correctly identify the impact instant. If timing alone creates most of the gain, keep the solution small.

### Gate 2 — Two-spell vertical slice

**Deliverables**

- one caster/release accent where needed;
- one reusable target-local impact primitive;
- one reusable non-blocking aftermath primitive;
- differentiated Lightning and Mud target responses;
- safe cleanup on normal completion, interruption, scene exit, and accelerated playback;
- documented presentation data added because both pilots required it.

**Gate**

The two spells feel materially different, remain readable against small and wide enemies, work in both directions, and do not make repeated rounds slower or noisier.

### Gate 3 — Positive-effect proof

**Deliverables**

- Woolgathering implemented entirely or mostly from reusable primitives;
- heal/state result synchronized to its receive moment;
- positive target response distinct from damage;
- cost comparison: bespoke effort versus reuse from the first two pilots.

**Gate**

The third spell reuses the system without forcing a broad schema rewrite. If it does force one, revise the abstraction now—before roster work.

### Gate 4 — Result model and AOE

**Deliverables**

- stable action presentation identity on the beat;
- per-target presented outcomes for AOE;
- one shared-field plus target-local impact composition;
- correct number/miss/effectiveness feedback per target;
- tests proving mixed hit/miss and weakness/resistance outcomes survive recording.

**Gate**

A three-target cast is clearer than three cloned full effects, and every visible outcome matches the resolved result.

### Gate 5 — State-changing magic

**Deliverables**

- one Ward or buff;
- one Roar/summon;
- Black Ram Mode or another transformation;
- persistent-state visuals only for information the player needs between beats;
- explicit ownership for spawning/removing actors during playback.

**Gate**

The player can identify the new state after the cast effect has disappeared. If understanding depends on replaying the cast animation mentally, the persistent feedback is inadequate.

### Gate 6 — Coverage and productionization

**Deliverables**

- inventory of in-scope magical/supernatural skills, separate from physical skills;
- each mapped to an existing grammar or marked for bespoke work;
- art/audio briefs for genuine gaps;
- validation for paths, frame indices, incomplete impact points, and unsupported combinations;
- performance caps and accessibility variants based on actual worst-case captures;
- short content-authoring guide with three worked examples.

**Gate**

No in-scope ability resolves silently; no physical skill was made visually magical solely to satisfy coverage; adding a routine spell is a content task rather than a controller change.

## Verification matrix

### Automated contracts

- impact dispatch fires exactly once and after any authored departure;
- vitals/state do not paint the post-result snapshot before impact;
- impact SFX fires at impact, not beat opening;
- a missing optional asset degrades without extending or breaking the beat;
- every borrowed effect is reclaimed by completion and `Flush`;
- transforms and material state return to baseline;
- accelerated playback preserves event order and shows a perceptible impact state;
- mirrored travel preserves caster-to-target direction and authored impact point;
- when per-target outcomes are added, mixed AOE results remain distinct;
- reduced shake/flash never removes the only indication that an outcome occurred.

### Capture cases

- caster → Giant Rat and caster → tall humanoid;
- enemy → player mirroring;
- front and rear target placement;
- normal, weakness, resistance, miss, and killing blow where those mechanics apply;
- one, two, and three targets after the AOE gate;
- normal and accelerated combat speed;
- standard and reduced-intensity variants;
- full mix with music, voice, and adjacent combat beats.

### Review questions

Avoid invented numerical thresholds in the first review. Ask observers to point to evidence:

1. When did you feel the spell landed?
2. What material or element did it seem to use?
3. Which target was affected, and what changed?
4. Did any part feel late, early, mushy, or unnecessarily long?
5. Which version would you prefer to cast twenty times, and why?
6. Did the improved effect expose that the underlying choice or payoff is uninteresting?

Record confusion and preference before asking for a 1–5 score. A score without a reason is weak design evidence.

## Production ownership

Each pilot needs one accountable owner per concern, even if one person fills several roles:

| Concern | Owner's decision |
|---|---|
| Combat design | tactical job, cost, payoff, outcome truth |
| Combat presentation | phase timing, screen hierarchy, response strength |
| VFX/art | silhouettes, material motion, effect assets, cropping/anchors |
| Audio | transient timing, material identity, mix fatigue |
| Engineering | event ownership, data recording, pooling, cleanup |
| QA | capture matrix, regression contracts, interruption cases |

The presentation owner signs off the whole cast in motion. Individual asset approval is insufficient; good frames and good sounds can still combine into a bad spell.

## Stop conditions

Pause or cut scope if any of these occurs:

- timing fixes deliver the desired improvement without a new effect system;
- two pilots require unrelated bespoke implementations and expose no reusable seam;
- the spell's mechanical role is too weak to justify polish before redesign;
- new presentation data starts duplicating full combat state rather than recording only observable outcomes;
- decorative aftermath delays control or makes the next beat unreadable;
- the solution depends on full-screen effects to make routine casts noticeable;
- content authoring requires controller code for every new spell.

These are not failures. They prevent sunk-cost architecture.

## Decisions that genuinely require product input

No question blocks baseline capture or the timing audit. Before Gate 2 is accepted, decide:

1. Should routine combat feel **brisk and tactical** or **deliberate and theatrical**? Recommendation: brisk and tactical, with rare theatrical exceptions.
2. May signature abilities briefly affect the whole stage, or must all effects remain local? Recommendation: local by default; full-stage treatment only for transformation, summon, convergence, and bosses.
3. Should weakness/resistance be explicit at contact or remain primarily in text? Recommendation: a small visual modifier plus current log text, once correct outcome data exists.
4. Is reduced flash/shake a launch requirement or a later accessibility pass? Recommendation: design primitives for it now; expose options before content-wide rollout.

## First sprint recommendation

The first sprint is smaller than the original proposal:

1. Capture Lightning Bolt and Mud Burst from click through return to control.
2. Complete the gameplay-fun audit for both.
3. Measure whether confirmation actually has a visible gap.
4. Extract and test the existing impact dispatch.
5. Move the current spell sound to its documented impact moment.
6. A/B the existing frame crossfade against a cut/hold alternative for Lightning.
7. Add one target-local impact accent and one cleanup-safe aftermath accent.
8. Tune the two spells in the full battle mix and repeat them through several rounds.
9. Review evidence and choose whether a coordinator, broader schema, or additional pools have earned their complexity.

Do not begin with full-roster mapping, AOE, persistent status visuals, an audio-source pool, screen washes, or four-phase sound authoring.

## Final production principle

> First make two spells unmistakably better. Then build only the system their success proves the game needs.

The desired end product is not the most flexible spell-effects architecture. It is a combat game in which the player enjoys the decision to cast, understands exactly what happened, feels the consequence, and is ready to make the next decision before the spectacle becomes friction.
