# Prince's Palace Spell Design Standard

**Status:** Canonical design reference  
**Applies to:** Designing, reviewing, implementing, balancing, presenting, and testing global spells  
**Does not replace:** Character talent/skill design, general combat balance, or the spell-feel implementation plan  
**Last revised:** 4 September 2026

## Purpose

This document answers one permanent question:

> What must a spell be in Prince's Palace?

Use it whenever the team proposes a spell, writes its content, assigns it to a spellbook tier, implements its mechanics, commissions its art or audio, tunes its balance, or reviews it in play.

This is a product standard, not a catalogue of finished spells. Individual values can change through balancing. The identity rules, authorship questions, and review gates should remain stable unless the game's overall magic system changes.

## Canonical definition

> **A spell in Prince's Palace is a globally discoverable magical action that any character can learn, equip, and cast, converting a deliberate universal cost into a meaningful battle-state change through a recognizable audiovisual performance.**

In shorter form:

> **Spells belong to the world. Skills belong to the character.**

The same Lightning Bolt is Lightning Bolt regardless of who equips it. Its fundamental mechanics, targeting, magical material, visual identity, and sound identity remain recognizable. The caster's attributes, equipment, relics, and talents determine how effectively that character expresses it.

The governing principle is:

> **Everyone can learn the same spell; not everyone can use it equally well.**

## Spell, skill, and power

These terms must not be used interchangeably.

### Spell

A spell is found in the global spell pool, normally acquired as a spellbook during a run, and equipped into one of a character's three spell slots.

Examples:

- Mud Burst
- Frost Flare
- Lightning Bolt

### Skill

A skill is an intrinsic expression of a particular character. It is unlocked through that character's level or talent tree and remains part of their identity.

Examples:

- Shear
- Woolgathering
- Battering Ram
- Headbutt
- Static Fleece (removed 2026-09-15, AUDIT #150 -- see below)
- Golden Fleece (removed 2026-09-15, AUDIT #150 -- see below)

A skill may be supernatural and may reuse spell-presentation technology. That does not make it a global spell.

### Power or convergence ability

A power changes the character's nature, form, or operating rules. It is generally character-specific even when its presentation is highly magical.

Examples:

- Black Ram Mode
- character-specific transformations
- talent-tree convergence abilities

Powers may use the same timing, VFX, audio, and impact primitives as spells. Classification is determined by ownership and fantasy, not by implementation.

## The universality test

Before calling an ability a spell, ask:

> Could every playable character plausibly learn this exact action from the same book without rewriting its fantasy or requiring another character's anatomy, signature resource, personal history, or talent tree?

If yes, it can be a global spell.

If no, it is a skill or power.

### Passes

- Calling a lightning strike
- Raising a general magical ward
- Freezing an enemy in place
- Transferring mana to an ally
- Opening a temporary arcane rift

### Fails

- Spending Shawn's Wool
- Using a species-specific body part
- Invoking a named personal companion
- Entering a character's unique alternate form
- Cashing in a talent-tree resource unavailable to the rest of the roster

A spell may interact with character systems after casting. A Shawn talent could grant Wool when he casts a ward. The global spell itself must not require Wool to function.

## Why global spells exist

Global spells serve three connected purposes.

### Discovery

Finding a spellbook should create a build possibility, not merely award another attack. The player asks:

- Who benefits most from this spell?
- Is it better than one of their three equipped spells?
- Does it complete a synergy or cover a weakness?
- Is it worth building attributes or relic choices around?

### Expression

The same spell should reveal differences between casters. A high-Intelligence damage dealer, a Wisdom-focused support, and a hybrid character may all equip the same book but value and express it differently.

### Adaptation

Because spells are globally available but slots are limited, they let the party adapt to a run's enemies, relics, and rewards without erasing character identity. Skills define the character's base language. Spells let the player modify the sentence.

## The three kinds of spell fun

A shippable spell must offer all three.

### 1. Decision fun

The spell creates a meaningful choice involving some combination of target, timing, cost, matchup, setup, risk, or opportunity cost.

Questions:

- Why cast this instead of attacking?
- Why cast this instead of another equipped spell?
- When is holding it the smarter choice?
- Who should equip it?

### 2. Outcome fun

The result creates a satisfying change in battle state: an enemy falls, an opening appears, pressure is relieved, a combo is completed, or a future turn becomes stronger.

Questions:

- What becomes possible after this resolves?
- What danger did it answer?
- What visible state changed?
- Did the result repay the cost and setup?

### 3. Sensory fun

The act of casting is pleasurable. Input response, anticipation, release, impact, sound, target reaction, and aftermath communicate both the spell's identity and its outcome.

Questions:

- Can the player tell when it lands?
- Does it feel like its material or element?
- Does power have appropriate audiovisual weight?
- Will the twentieth cast still feel crisp rather than exhausting?

Presentation is not decoration. It is how the game teaches the action, confirms the decision, and rewards the result. Conversely, presentation cannot permanently rescue a tactically redundant spell.

## The spell thesis

Every proposal begins with one sentence:

> Spend **[cost]** to **[magical verb]** **[target or space]**, producing **[primary result]** with **[situational rider]**. It is strongest when **[favourable context]** and weakest when **[limitation or counter-context]**.

Then add one presentation sentence:

> The caster **[anticipation]**, the magic **[release/travel/manifestation]**, the target **[contact response]**, and the spell leaves **[aftermath or persistent state]**.

If either sentence is vague, the spell is not ready for implementation.

### Example: Lightning Bolt

> Spend mana to call a precise arcane strike onto one enemy, producing reliable burst damage that rewards a spell-focused caster. It is strongest when immediate single-target pressure matters and weakest when distributed control or defence is required.

> The air contracts into a thin charge, a white line snaps onto the target, the victim is held in a sharp electrical flash, and a brief branching afterimage collapses into darkness.

## Required spell anatomy

Every spell design must answer the following.

| Field | Required answer |
|---|---|
| Name | Short, distinctive, and consistent with the world |
| Fantasy | What impossible act does the caster perform? |
| Magical verb | Gather, project, impose, transform, exchange, reveal, bind, etc. |
| Tactical job | The battle problem it solves |
| Targeting | Valid targets and target shape |
| Universal cost | Normally mana; exact amount and any supplementary constraint |
| Tempo | Immediate, charged, delayed, channelled, reactive, or persistent |
| Primary effect | The reliable state change |
| Rider | Situational bonus, setup, payoff, or secondary effect |
| Scaling | Which caster investments improve it and how |
| Limitation | When it is inefficient, unsafe, or inappropriate |
| Book tier | Its acquisition rarity and run impact |
| Presentation thesis | Anticipation, delivery, impact, aftermath |
| Readability requirement | What the player must understand without the log |
| Repetition budget | How often it is expected to play and how long it may occupy attention |

## Mechanical design rules

### A spell must change battle state

Raw damage is a state change, but damage alone does not automatically create identity. A damage spell needs at least one differentiator:

- unusual target shape;
- elemental matchup;
- timing or delayed payoff;
- status application;
- resource conversion;
- positioning or turn-order effect;
- setup or detonation relationship;
- risk/reward profile;
- exceptional efficiency under a specific condition.

Avoid two spells whose only practical difference is their coefficient.

### Mana is the common spell language

Until the design is deliberately changed, every global spell should cost mana. Mana provides a universal price that makes the same book usable across the roster.

Cooldown, health sacrifice, delayed resolution, setup, or another drawback may supplement mana. They should not replace it casually. A spell with no mana cost needs a specific reason and a review of how every character can exploit it.

Character-specific signature resources belong primarily to skills. Talents may create interactions between those resources and global spells without placing the resource requirement inside the spell definition.

### Slots create opportunity cost

Each character has three spell slots during a run. A new spell competes not only for mana but for a slot.

Therefore:

- narrow spells may be highly efficient in their niche;
- broad spells should pay for flexibility through cost or lower peak output;
- high-tier spells should create build direction, not simply dominate lower tiers;
- no single spell should solve damage, control, defence, and recovery at once without an extreme price.

### Reliable core, conditional excitement

The primary effect should be understandable and dependable. The rider creates mastery.

Good shape:

- reliable damage plus Vulnerable;
- reliable ward plus bonus when the target is already injured;
- reliable chill plus a stronger effect on a delayed turn;
- reliable healing plus mana refund when it prevents overhealing.

Weak shape:

- several unrelated random effects;
- a spell that usually does nothing unless a rare condition happens;
- a tooltip containing so many branches that the player cannot predict the cast.

### Every spell needs a wrong moment

A spell without a bad use case is likely too broad or too efficient. Its limitation might be:

- high mana cost;
- cooldown;
- poor single-target efficiency;
- poor multi-target efficiency;
- setup requirement;
- delayed result;
- dependence on an elemental matchup;
- vulnerability during a channel;
- consumption of a valuable status;
- low value when allies or enemies are in the wrong state.

The limitation should create judgment, not make the spell feel defective.

### Complexity must earn its tooltip space

Prefer one primary effect and one rider. Add a second rider only when it completes a strong, coherent fantasy.

Every clause should change a real decision. If removing a clause does not change when, where, or by whom the spell is cast, remove it.

## Tactical spell jobs

Classify a spell by its primary job before choosing its element.

| Job | Purpose | Typical limitation |
|---|---|---|
| Burst | Remove or pressure one priority target | expensive or narrow |
| Sweep | Pressure multiple enemies | lower per-target efficiency |
| Execute | Finish a weakened target | inefficient above threshold |
| Control | Deny, delay, redirect, or reposition | lower immediate output |
| Setup | Create a future opening | delayed payoff |
| Detonate | Consume setup for a larger result | needs prior state |
| Protection | Prevent, absorb, redirect, or delay harm | no immediate offence |
| Recovery | Restore health or mana | tempo and opportunity cost |
| Cleanse | Remove harmful states | weak without those states |
| Conversion | Exchange one universal quantity for another | efficiency or risk cap |
| Invocation | Create a temporary entity or field | delayed value and stage limits |
| Wild spell | Offer extraordinary upside with real uncertainty or drawback | unreliable or dangerous |

A spell may have a secondary job, but its primary job must remain obvious.

## Magical verbs

Magical verbs describe what happens in the fiction. They guide animation and help prevent every spell from becoming a projectile.

- **Gather:** draw matter, light, life, or force inward.
- **Project:** send a force from caster to target.
- **Call:** cause a force to arrive from elsewhere.
- **Impose:** place a condition, rule, or boundary.
- **Bind:** restrict, connect, or redirect subjects.
- **Exchange:** turn one state or resource into another.
- **Reveal:** expose weakness, intent, or hidden potential.
- **Fracture:** break protection, stored energy, or a status.
- **Invoke:** create a temporary entity or battlefield presence.
- **Transform:** alter form or operating rules.

Most strong spell concepts combine one or two verbs. More than two often signals an overloaded design.

Examples:

- Mud Burst: Gather → Project
- Lightning Bolt: Call → Strike
- Frost Flare: Impose → Fracture
- General ward: Gather → Impose
- Mana transfer: Bind → Exchange

## Targeting and spatial identity

Targeting is part of the spell's fantasy and balance.

Supported conceptual shapes include:

- self;
- single ally;
- single enemy;
- all allies;
- all enemies;
- a rank or group;
- a chain between subjects;
- a battlefield field;
- a delayed mark on one subject.

Rules:

- The visual origin and mechanical target must agree.
- Single-target spells must make the selected target unmistakable.
- AOE should read as one shared action with per-target consequences, not several unrelated casts.
- Self and party spells should not route through enemy-target language.
- A targetless spell must make its affected space or rule clear.
- Any target restriction must be visible before confirmation, not discovered after spending mana.

## Scaling and caster expression

The spell definition owns base behavior. The caster supplies effectiveness.

### Principles

- Global availability does not imply equal performance.
- Scaling should make an investment feel rewarded without making a low-investment cast meaningless.
- Offensive and supportive magic should have legible attribute relationships.
- Fixed damage packets must still participate in the intended caster-scaling system.
- A spell's identity must survive different casters; scaling changes magnitude or a controlled rider, not its fundamental job.

### Recommended attribute posture

- **Intelligence:** primary offensive spell potency, precision, or destructive rider strength.
- **Wisdom:** healing, protection, duration, efficiency, or supportive rider strength.
- **Hybrid scaling:** reserved for spells whose tactical identity genuinely bridges offence and support.

Do not add hybrid scaling merely to make a spell good for everyone. Slot choice becomes interesting when different characters value the same book differently.

### Talent interactions

Character talents may:

- reduce cost for a spell family;
- add or improve a rider;
- convert a small part of one outcome into another;
- trigger a signature resource response;
- change targeting under a controlled condition;
- reward repeated or alternating spell use.

Talents should modify the character's relationship with the spell. They should not rewrite the spell so completely that its global identity disappears.

## Book tiers

Book tier communicates acquisition rarity, complexity, and run impact—not merely damage.

The current scale is Tier 1–4.

### Tier 1 — Foundation

- immediately understandable;
- broadly usable;
- modest cost and effect;
- establishes one clean magical verb or job;
- useful without specialized setup.

### Tier 2 — Synergy

- stronger contextual payoff;
- introduces a status, multi-target shape, or build interaction;
- asks a more interesting timing or party-composition question.

### Tier 3 — Build-defining

- high impact or distinctive rule interaction;
- meaningfully influences equipment, attributes, or later choices;
- may demand higher mana, setup, or narrower use.

### Tier 4 — Run-shaping

- rare and strategically transformative;
- creates a new line of play rather than being a larger Tier 3 spell;
- carries a serious opportunity cost, condition, cooldown, or risk;
- earns exceptional audiovisual treatment without becoming unrepeatably slow.

A higher tier is not permission to remove counterplay.

## Audiovisual design standard

Every spell must have a presentation thesis before art or audio production begins.

### The four moments

| Moment | Player question | Required communication |
|---|---|---|
| Begin | Who is casting and what are they preparing? | caster intent and magical material |
| Release | Where is the magic going or what rule is being imposed? | direction, target, or affected space |
| Impact | When did the result happen? | synchronized visual, sound, target response, and UI consequence |
| Settle | What material or state remains? | aftermath, recovery, or persistent state |

Not every spell needs four separate assets. It needs four readable ideas.

### One authoritative impact

The perceived impact must align:

- health, mana, or state change;
- damage, heal, miss, or status popup;
- target pose or deformation;
- impact visual;
- impact sound;
- hit-stop or positive settle hold;
- stage impulse when appropriate.

The effect animation may anticipate or decay around this moment. It must not establish a competing impact time.

### Material before colour

The spell should remain identifiable in grayscale through shape, direction, timing, and motion.

| Material | Motion and timing | Contact language | Aftermath |
|---|---|---|---|
| Lightning | contraction then abrupt path | sharp flash, minimal heavy recoil | brief branches and darkness |
| Frost | formation and tightening | brittle flare, short held victim | shards or mist |
| Mud/earth | accumulation and weight | squash, ground burst, low transient | falling debris and dust |
| Fire | accelerating expansion | bloom and outward pressure | embers and heat decay |
| Nature | growth, spiral, or spread | organic burst or binding | leaves, spores, roots |
| Arcane | ordered geometry and convergence | clean tonal snap or seal | collapsing runes |
| Healing | inward gathering and opening | recipient brightens and expands | gentle upward motes |
| Ward | boundary tracing and closure | ring or seal locks | restrained persistent boundary |

Colour reinforces the material. It cannot be the only difference between spells.

### Contrast creates impact

Power comes from contrast:

- stillness before velocity;
- contained shape before expansion;
- restrained brightness before a flash;
- reduced sound before a transient;
- fast contact followed by slower recovery.

If every frame is large, bright, shaking, and loud, no frame feels powerful.

### Impact strength must match meaning

Damage amount is one source of weight, not the only source.

- Heavy damage can drive shake, recoil, squash, hit-stop, and audio weight.
- Lightning may be bright and sharp without feeling physically heavy.
- Healing needs a receive/settle response, not damage recoil.
- Ward needs closure and persistence, not a hit flash.
- Summoning and transformation may use an authored spectacle floor even with no damage.
- Status ticks should not replay the full original cast.

### Repetition budget

Frequency determines spectacle budget.

- Common spells should be short, legible, and pleasant on repeated use.
- Long anticipation belongs to rare or highly consequential spells.
- Decorative aftermath may continue without delaying the next meaningful beat when readability permits.
- Persistent effects should settle into a quiet state rather than continuously competing with combatants.
- Signature effects may briefly command the stage; routine effects should not.

### Audio

Audio must express material, phase, and weight.

At minimum, a damaging spell needs an impact transient synchronized with its result. Add charge or release audio when the wind-up or departure needs definition. Prefer a coherent impact clip with a natural tail over four mandatory clips.

Review audio:

- in the full music and voice mix;
- across repeated casts;
- at different effect-volume settings;
- for material recognition without visuals;
- for exact perceived synchronization;
- for tails that mask the next action.

Loudness is not impact. Shape, frequency, silence, and timing matter.

### Accessibility

Effects must retain meaning when flash, shake, or particles are reduced.

- Never make shake the only impact signal.
- Never make colour the only element or state signal.
- Avoid rapid high-contrast flicker.
- Prefer one intentional flash to repeated strobing.
- Provide stable silhouette, motion, sound, and UI confirmation as redundant channels.
- Design reduced-intensity behavior with the effect, not after the entire catalogue is complete.

## Naming standard

Spell names should be:

- two or three words where possible;
- concrete enough to evoke material or action;
- distinct when spoken aloud;
- free of character names or anatomy unless the fiction establishes a globally teachable tradition;
- stronger than generic construction such as “Greater Fire Spell.”

Useful shapes:

- material + action: Mud Burst, Frost Flare;
- evocative phenomenon: Pale Current, Hollow Sun;
- magical instruction: Bind the Flame;
- consequence: Borrowed Breath.

The name, mechanic, and presentation should make the same promise.

## Tooltip and player-facing description

The tooltip must communicate decision-making information before flavour.

Recommended order:

1. target and primary outcome;
2. cost and cooldown;
3. rider and condition;
4. scaling or relevant keyword;
5. short flavour line if space permits.

Avoid hiding a significant drawback exclusively in flavour text. Avoid presenting implementation arithmetic when a stable gameplay term communicates the same decision.

The audiovisual performance may surprise the player aesthetically. The mechanic should not surprise them unfairly.

## Design workflow

### Step 1 — State the need

Name the missing tactical job, build opportunity, enemy problem, or world fantasy. Do not start with “we need another fire spell.”

### Step 2 — Pass the universality test

Confirm that the same book makes sense on every playable character. Reclassify it as a skill if it relies on personal anatomy, resources, or history.

### Step 3 — Write the two-sentence thesis

Complete the mechanical and presentation sentences. Stop if either is vague.

### Step 4 — Prototype the decision

Test targeting, cost, effect, scaling, and rider with placeholder presentation. Verify that the spell creates a real choice.

### Step 5 — Define the sensory identity

Choose material behavior, anticipation, release, contact, aftermath, and repetition budget. Compare it against nearby spells to prevent overlap.

### Step 6 — Implement the smallest honest presentation

Use existing timing and effect primitives first. Add new infrastructure only when the spell exposes a reusable missing capability.

### Step 7 — Review in context

Cast repeatedly in real combat, with music, voices, multiple enemy shapes, both travel directions, and adjacent actions.

### Step 8 — Validate global use

Equip it on characters with different attributes and talent interactions. Confirm that it remains the same spell while producing meaningful differences in value.

### Step 9 — Assign tier and finalize content

Tier follows proven run impact and decision complexity. It is not assigned from spectacle or raw damage alone.

## Spell proposal template

Copy this section for every new spell.

```markdown
# [Spell name]

## Identity
- Magical verb(s):
- Fantasy:
- Tactical job:
- Emotional target:
- Why this is global rather than character-specific:

## Mechanical thesis
Spend [cost] to [verb] [target/space], producing [primary result] with
[situational rider]. Strongest when [context]; weakest when [limitation].

## Rules
- Valid targets:
- Mana cost:
- Cooldown or supplementary cost:
- Primary effect:
- Rider:
- Duration:
- Scaling:
- Element/damage packets:
- Status interactions:
- Failure/miss behavior:
- AI use considerations:

## Build value
- Best caster profile:
- Weak caster profile:
- Talent/relic synergies:
- Competing spells or skills:
- Why it deserves one of three slots:

## Acquisition
- Proposed book tier:
- Why this tier:
- Earliest desirable appearance:
- Duplicate/replacement value:

## Presentation thesis
The caster [anticipation], the magic [release/travel/manifestation], the target
[contact response], and the spell leaves [aftermath/persistent state].

## Audiovisual beats
- Begin:
- Release:
- Impact:
- Settle:
- Material motion:
- Impact weight:
- Sound identity:
- Reduced-intensity behavior:
- Repetition budget:

## Readability
- What must be understood without the combat log:
- Targeting preview:
- Weakness/resistance/miss treatment:
- Persistent-state treatment:

## Validation
- The closest existing spell and the meaningful difference:
- The wrong time to cast this:
- Primary balance risk:
- Primary readability risk:
- Required test cases:
- Required capture cases:
```

## Review checklist

A spell is ready for implementation only when every design item is answered.

### Classification

- [ ] It passes the universality test.
- [ ] It does not depend on a character-specific resource, anatomy, or talent.
- [ ] It belongs in the global spell pool rather than a talent tree.

### Gameplay

- [ ] It has one obvious primary tactical job.
- [ ] Its primary effect is reliable enough to understand.
- [ ] Its rider creates a meaningful situational decision.
- [ ] It has a genuine wrong moment or limitation.
- [ ] It deserves one of only three spell slots.
- [ ] Its mana cost and supplementary constraints are stated.
- [ ] Its scaling rewards a caster profile without making other casters nonsensical.
- [ ] It does not duplicate an existing spell with different numbers or colour.

### Presentation

- [ ] Begin, release, impact, and settle are described.
- [ ] The audiovisual design communicates material through motion and timing.
- [ ] The impact aligns with the actual mechanical result.
- [ ] The target and affected space are unmistakable.
- [ ] Its weight matches its effect rather than its brightness.
- [ ] It remains readable with reduced flash, shake, or particles.
- [ ] Its duration suits how often it will be cast.

### Production

- [ ] Existing primitives and assets were considered before new infrastructure.
- [ ] Required art, audio, data, code, and tests are listed.
- [ ] Missing optional assets degrade safely.
- [ ] Multi-target and interruption behavior are defined where relevant.
- [ ] The spell is reviewed in the full combat mix, not in isolation.

### Final sign-off

- [ ] A player can explain what changed without reading the log.
- [ ] A player can distinguish it from its nearest competitor.
- [ ] Repeated casts remain satisfying and appropriately paced.
- [ ] Different casters value it differently while it remains recognizably the same spell.

## Anti-patterns

Reject or revise these shapes.

### Attack, but blue

The spell has no meaningful targeting, timing, status, or matchup distinction. Colour is doing all the identity work.

### The overloaded grimoire

One spell damages, heals, buffs, controls, refunds mana, and summons. It eliminates rather than creates decisions.

### The personal spellbook

The spell is sold globally but only functions because one character has Wool, wings, a specific companion, or a personal transformation.

### The VFX alibi

Spectacle disguises a weak, redundant, or automatic decision.

### The tooltip puzzle

The player must evaluate several unrelated conditions and exceptions before knowing the likely outcome.

### The universal best slot

Every character wants the spell in every run. It has no meaningful limitation, build preference, or competing use.

### The cinematic tax

A common spell repeatedly delays the next decision to replay spectacle the player understood long ago.

### The silent state change

The animation looks attractive but does not communicate the actual target, timing, status, resource movement, or persistent result.

## Current implementation note

The intended design and current data layout are not yet identical.

Today:

- globally acquired spellbooks reference entries in `skills.json` by `skillId`;
- every entry still carries a `characterId`;
- `ContentDatabase.AvailableSkillsFor` filters all entries—including learned book spells—against that character ID;
- the current book-eligible spells are authored against `sheep`;
- `spells.json` contains the shared level-based spell power/cost curve rather than the global spell catalogue;
- Static Fleece and Golden Fleece were book-eligible despite depending on Shawn's Wool and therefore failing the universality test -- **removed 2026-09-15 (AUDIT #150)** rather than reclassified, which resolves this specific bullet; the remaining four book spells (Mud Burst, Frost Flare, Cinderfault, Lightning Bolt) cost mana only and pass the test as authored.

This is a transitional implementation constraint, not the product definition.

Before multiple playable characters use spellbooks, the content model must allow a global spell definition to be assigned to any character without duplicating one definition per owner. (Static Fleece and Golden Fleece, which this paragraph used to name as the candidates needing reclassification, are gone as of 2026-09-15 -- see above.)

A likely target model is:

```text
Character combat options
├── level/innate skills
├── talent-granted skills
└── three equipped global spell ids

Global spell catalogue
├── universal mechanics and targeting
├── mana cost and book tier
├── spell scaling
└── audiovisual presentation
```

The exact migration belongs in an implementation plan. New spell designs should follow the global definition now so the eventual migration is clarification rather than content redesign.

## Relationship to spell-feel work

This standard defines **what** a spell is and what information its design must supply.

`docs/PLAN_SPELL_FEEL.md` defines **how to validate and incrementally improve** the presentation technology, beginning with Lightning Bolt and Mud Burst.

When the two documents differ in emphasis:

- this standard owns classification, player value, and the spell-design contract;
- the spell-feel plan owns implementation sequence, evidence gates, and risk control;
- live combat behavior and validated content remain the final source of truth for what is currently shipped.

## Final standard

> A Prince's Palace spell must be globally learnable, strategically worth a scarce slot, shaped by its caster without belonging to them, honest about its cost and result, and delightful to cast through synchronized audiovisual feedback.

If a proposal cannot satisfy all five ideas—global, strategic, expressive, honest, delightful—it is not ready to become a spell.
