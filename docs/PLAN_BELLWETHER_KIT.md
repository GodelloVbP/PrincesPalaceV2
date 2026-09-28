# Plan: the Bellwether's kit (rev 2)

Rev 2, 2026-09-28, planning only; owner answers in. **(assumed)** = gap-fill. Builds on PLAN_EVENTS_BELL_AND_CARAVAN.

## 0. Finding first: a solo fight has no ranks today

- **Rank is list order among the living, never stored** (`CombatEncounter.LivingRankOf`): solo is always rank 0.
- **Move needs a living partner** (`FightSession.TryFindMovePartner`): solo, `CanMove` is false both ways;
  `PalacePassageTests.ASoloPartyIsRefusedTheCast` pins the same for the Passage.
- **The stage spreads rank r of n** (`FightController.DrawSide`, `SlotOffset(r, n)`): solo stands front; a duo's
  second member is drawn at the far end. `EncounterRoll.FieldableParty` drops the party screen's saved holes.
- **What works:** reach reads only the *target's* living rank (`ReachAllows`), so both sides' melee connect anywhere.

So **M1 is field seats** (3.1), an architecture change: it redefines what a party position is.

## 1. Behavioural contracts

### 1.1 Field seats (every fight; owner, 2026-09-28)
- The party side has **three seats** (front, middle, rear). A living member occupies exactly one; a seat may be empty.
  A fight starts with the fielded members in seats 1..n in list order, the rest empty **(assumed: party-screen holes
  are not carried in; section 7)**.
- **Trigger** a party member falls. **Outcome** every living member behind them moves forward one seat (the line
  closes up, as today; the view still waits for the corpse to fade).
- **Close-range reach is unchanged**: melee, Reach, Provoke and "front rank" read rank among the living. So a lone
  character (solo, or the last one standing) is always reachable by close-range physical attacks wherever he stands;
  there is no hiding in an empty rear. Seats decide only placement on the stage, Move, Palace Passage, Reposition
  (1.4) and rank-read effects (1.5). With no empty seat in front of anyone, seat == living rank.
- **Move** (turn-ending, unchanged cost): the actor goes one seat forward or back, trading with the occupant or
  stepping into an empty seat, in every fight. Refused if the actor, or an occupant it would trade with, is Rooted.
  Accepted by the owner: in fights with no rank-read threat such a move changes nothing but position.

### 1.2 Palace Passage: any ally to any seat (owner-agreed)
- Two picks, as today: **a traveller** (any living ally, caster included) and **a destination seat** (occupied or
  empty, not the traveller's own). **Outcome** the traveller goes there; an occupant goes to the traveller's seat.
  Free action, 7 mana, cooldown 3, unchanged. Rooted traveller or occupant refuses before payment, as today.
- Picking an occupied seat is exactly today's two-ally swap: same picks in the same order, same result, same messages.
  Solo: Shawn + an empty seat is a legal cast (front -> rear in one free action).

### 1.3 Scratch and Bleed (the attack the player sees most)
- **Trigger** the Bellwether resolves Scratch (every turn that is not Chains or Knell; its plain swing is weighted
  out). **Outcome** a physical hit; a layered claw rake plays on him (5, the most-polished effect in the kit); he gains
  **Bleed**, a debuff. Intent badge: the Bleed kind with the hit's expected damage.
- **Bleed** is Burn's shape (snapshot magnitude, ticks at the holder's turn start, stacks like the other DoTs) with one
  difference: each tick meets the holder's **physical defence and Physical affinity** on a hit's curve; no ward,
  variance or Protect/Vulnerable, floor 1. Any skill, monster rider or weapon modifier may apply it by name.

### 1.4 Dark Chains
- **Trigger** the Bellwether's 1st acting turn, and its 5th **(assumed)**. **Outcome** a Void spell, no damage
  **(assumed)**: Shawn is placed in the **front seat** (trading with its occupant in a party fight). Chains pull; they
  do not root. Badge: the Pull kind, "Dark Chains", and the plate line and tooltip say **"Death Knell next"**.
- Rooted target: the chains hold nothing ("Shawn does not move"); the knell still follows. An enemy moving a party
  member is not the party's deliberate move: Sparring Buckler/Saber do not pay.

### 1.5 Death Knell
- **Trigger** the Bellwether's very next acting turn after Dark Chains resolved (1.6). **Outcome** a Void spell at the
  chained target, scaled by **the seat he stands in when it lands**: front 100%, middle 30%, rear 0% of the authored
  power **(start values)**. Rear = no hit at all: no number, no riders, no hit-taken gains; log "The knell passes over
  Shawn." Middle and front go through the normal pipeline (magic defence, Void affinity, ward, dodge).
- **Ignoring it is a hard loss** (tuning contract, M7): front kills a full-HP, floor-appropriate, untransformed Shawn
  in >= ~90% of cases; middle is heavy but survivable from >= ~60% HP; rear takes nothing.
- **The telegraph is unmistakable.** Badge: the Knell kind with the number for the seat he stands in *now*, re-read
  whenever the formation changes (including after a free action inside his turn). When that number is at or above his
  current HP the badge takes the lethal style (skull tint) and the plate line reads "Death Knell! Step back". Tooltip,
  derived, never authored: "Death Knell on Shawn: about 610 at the front (lethal), 180 in the middle, none at the rear.
  Step back." ("Step back" appears whenever a further-back seat takes less.)
- **Counterplay:** stay (dead); Move back (heavy, turn spent); Passage to the rear (nothing) and still act.

### 1.6 The pair, on the Bellwether's own turns
- The Bellwether counts **its own acting turns** (a turn on which it resolves an action; stunned or broken turns are
  not counted). Turn 1 is Dark Chains, turn 2 the Death Knell; turns 5 and 6 repeat the pair **(assumed)**; every other
  turn is Scratch. Rounds and tolls play no part.
- **The pair is one unit, never split.** Once Chains resolves, the Bellwether's next acting turn is the Knell, whatever
  comes between (a stun delays it; nothing replaces it). A step is used up when it resolves, not when it is shown.
- At speed 5 (0.71 turns/round vs Shawn's 1.0) a base Shawn gets a turn between the pair; pairs land ~rounds 1-3, 7-9.

### 1.7 The toll, presented
- **Trigger** a round starts and the Bellwether rallies. **Outcome** its `cast` stance with a void ripple layer and
  the shipped toll sfx; its status row shows a permanent **rally badge with the stack count** ("x3", "+24% attack").

## 2. Precedence and edge cases

1. **Round start order** (shipped rule, unchanged): (a) past the limit -> Survived; (b) enemy round effects (rally);
   (c) the next turn opens. The schedule is not a round effect.
2. **Intent commitment order:** stunned -> Forfeit (step kept, turn not counted) > Showcase (preview tool) > scheduled
   step > weighted draw. The RNG draws (ability roll, target roll) are **always consumed** so seeded streams keep
   their position.
3. **The chains are telegraphed from Shawn's first turn** (intents commit at player turn start); the knell's badge
   appears once the chains resolved.
4. **Knell vs. the round limit / a kill:** a Survived or Defeated ending before the knell resolves cancels it.
5. **Chains when Shawn is already front:** nothing moves, knell follows. Pre-positioning at the rear is pointless.
6. **Knell against a party** (future use): only the chained target is struck; if he has died, the knell re-picks a
   target like any single-target intent. No corpse holds a seat (1.1), so Move never "steps over" one.
7. **Extra turns granted to the Bellwether** (none exist today) count as acting turns like any other.
8. **Monster-level status rider** (`RawEnemyEntry.appliesStatus`) fires on *every* hit the monster lands, skills
   included, so it cannot carry Bleed without also bleeding on the knell. Scratch is a skill; the rider stays unused.

## 3. Engine model changes

Each: what it is, why it fits more than one use, its single seam.

**3.1 Field seats.** Three seats, empty seats real; seat stored, living rank computed. Uses: the Bell, Passage into an
empty seat in any short party, carrying party-screen holes in later (AUDIT #93). Seam: one primitive,
`PlaceAt(member, seat)` on the encounter (occupant trade or empty step, Rooted refusal on both ends), called by Move,
Palace Passage and Reposition: one rule for "change field position". `SeatOf` is read by the stage (through
`BeatFormation`, which snapshots seats per beat) and by damage-by-rank. Enemies never move, so an enemy's seat is its
living rank; nothing new is stored there.

**3.2 Bleed.** One `StatusEffectType` plus one `StatusMitigation` member, **Armoured** (defence + affinity). Burn and
Thorned are `AffinityOnly` and meet no armour, so "armour-mitigated like Burn" is two different rules; Bleed needs
the second. Uses: Scratch; any claw/blade monster or a bleeding weapon modifier as content. Seam:
`StatusEffects.MitigatedTickAmount` (one arm) and the per-type tables (`ElementOf` -> Physical, `MitigationOf`,
stacking, clock, `StatusHud` code/bucket/index); the "every member answers" tests force completeness.

**3.3 A skill's own damage type on the Attack-scaled path.** A power-scaled skill is typed by the caster's
`attackType` (`FightSession.ActorAttackType`); only fixed `damageInstances` carry a type, and fixed packets ride
neither the enemy depth rate nor the rally. Optional skill field `damageType` overrides the cast type at the one
place it is read. Uses: knell and chains (Void from a Physical ram); Mud Burst typed as magic; a Physical caster's
elemental spell. Seam: the `castType` read in the single- and all-target resolvers and in `PreviewSkill`.

**3.4 Damage by rank.** Optional skill field `damageByRankPercent` [front, middle, rear]: multiplies the computed
amount by the target's seat (party) or rank (enemies) at resolution; 0 = no hit. Uses: the knell; a player "cleave
that fades down the line"; a front-crushing enemy slam. Sits beside `reachSlots` (which *gates* by rank) as the one
"scales by rank" rule. Seam: one multiplier step after `SkillResolution.Amount`, shared by resolution and preview.
**Amended at M2 (orchestrator, 2026-09-28): percent of the target's max health, not a multiplier on the Attack
formula.** R2 below is unsolvable with attack scaling (front must kill a full-HP Shawn on knell 1 while middle spares
a >= 60%-HP one on knell 2, with the rally growing ~+24% -> ~+64% between them), so the field is
`damageBySeatMaxHpPercent` [front, middle, rear] (each 0..1000, 0 = no hit): the base is that percent of the
target's max HP, read by `SeatOf` at resolution. It replaces the Attack formula (no attack, rally, pool tier), then
meets the skill's `damageType` affinity, defence (unless `ignoresDefense`), Protect/Vulnerable, ward and dodge.
Seam: `FightSession.SeatSizedDamageBase`, read by the resolution and `PreviewSkill`. Start values are M5 content.

**3.5 `Reposition` effect.** `SkillEffect.Reposition` + `toSeat` (1-based, like `reachSlots`), player-side targets
only (enemies never move). Uses: Dark Chains; a knockback to the rear; a player "call to the front". Seam: its
resolver arm calls `PlaceAt`.

**3.6 Palace Passage picks a seat.** The second pick becomes a seat, not a combatant; occupied seat = the ally on it,
so today's behaviour is a subset. Seams: `FightMenuState` picks become field picks (combatant or seat); the SwapAllies
arm of `CanResolveSkill`; `ResolveSwapAllies` calls `PlaceAt`; `FightAction` gains a destination seat, which lifts the
"bot cannot express two picks" skip.

**3.7 Enemy schedule, by own turn.** Enemy field `schedule: [{onTurns: [1, 5], skills: [dark_chains, death_knell]}]`:
a sequence that starts on the listed acting turns and then takes the enemy's following acting turns, one step each,
until done. Each id must also be in `abilities` (weight 0 = never drawn). Per-fight state in the session: the enemy's
acting-turn count and an active-sequence cursor. Seam: the one line in `PrepareEnemyIntents` that already lets
`EnemyShowcase` override the draw; count and cursor advance where an enemy action resolves. One real user; two fields
at an existing seam, no scripting language. Next use: a boss with a fixed opener, content only.

**3.8 Intent carries what the player needs.** `EnemyIntent` gains `DamageBySeat` (set only for a damage-by-rank skill)
and `Then` (label of the next scheduled step). One accessor, `IntentDamageFor(enemy)`, reads the target's current
seat; badge, lethal style, tooltip and bot all read it. Kinds, derived in `KindFor` like the rest: **Bleed** (skill
applies Bleed), **Pull** (effect Reposition), **Knell** (skill has `damageBySeatMaxHpPercent`: "where you stand decides").
Icons `Intent/bleed`, `Intent/pull`, `Intent/knell`, text fallbacks BLD / PULL / KNL. **Lethal** is a presentation
state of any damage badge (number >= target's current HP), not a kind.

**3.9 Rally presentation.** `rallyPerRound` gains optional `stance` and `vfx` (a `SpellPresentation`), recorded on the
round's beat with the rallying enemy as its actor. `StatusHud.RallyRow(stacks, percent)` joins
`FightHudModel.StatusRowsFor`'s synthetic rows (transform, speed). Uses: any "grows each round" monster.

**3.10 Bot answers telegraphs.** A shared rule every archetype consults first: if a committed intent's `DamageBySeat`
is lower at another seat, take the free Passage to the cheapest seat when legal (then act normally), else Move back
when the saving is at least 25% of max HP **(assumed)**. Seam: `FightRunner` before the archetype's `Choose`, so no
policy is edited and a probe can switch it off.

## 4. Milestones

One owner each; `[D]` loop and `tools/test.ps1 <area>` to iterate; commit on `tools/run_tests_parallel.ps1 -Changed`
(plus `-BuildContent` / `-BuildScenes` where named), announced to live sessions first. Sequential: M1-M5 share files.

**M1: Field seats (senior). `Escalation: architecture`** (party position stops being list order; Move/stage contract).
- Store: `CombatEncounter.cs:25-29` (`_party`), `:139` `SwapPartySlots` -> `PlaceAt`; `LivingRankOf` `:117-131`
  unchanged. Move: `FightSession.cs:591-680` (`CanMove`/`Move`/`TryFindMovePartner`), `RootedPlayerHasNoLegalAction`
  `:407-421`. Death close-up where a party member is marked dead. `BeatFormation.cs:65` snapshots seats.
- Stage: `FightController.StageVisuals.cs:171` (`HoldsRank`), `:226-250` (`DrawSide`: seat of 3, not rank of n);
  `FightStageAnchors.cs:282` unchanged. Preview: `PreviewFight` lone/full formations.
- Bot legality: `FightAction.cs` Move list; `ProtectTheFrontPolicy.cs:69-90` reads "behind" as seats.
- Gate: `tools/test.ps1 combat`, then `run_tests_parallel.ps1 -Changed`; runtime captures (solo/duo/trio), owner away.
- Done when: `MoveCommandTests`, `EnemyReachTests`, `BotPolicyTests`, `StageFormationTests` pass unmodified for full
  parties; new literal tests pin solo Back/Forward through three seats, duo Back into the empty rear, rooted refusals
  both ends, close-up on death (including behind an empty seat), melee reaching a rear-seated lone Shawn and a
  rear-seated last survivor, and a seat-per-beat snapshot. `ASoloPartyIsRefusedTheCast` is left for M3.

**M2: Bleed, skill damageType, damage by rank, Reposition (implementer).**
- `StatusEffect.cs:174-197` (type), `:244-256` (`Armoured`); `StatusEffects.cs:981-1017`, `:1031-1047`;
  `StatusHud.cs:121-145`, `:200-215`, `:224-244`. `RawSkillEntry.cs` (`damageType`, `damageBySeatMaxHpPercent`, `toSeat`)
  + `SkillEntryResolver` refusals (rank list length 3, 0..1000; `toSeat` 1..3; Reposition only at a player-side
  target). `FightSession.Skills.cs:785-790` and `:1676` (cast type), `:425` (Reposition arm),
  `FightSession.Enemies.cs:402-423` (`PreviewSkill`). `docs/CONTENT_SCHEMA.md` regen.
- Gate: `tools/test.ps1 combat` and `content`, then `run_tests_parallel.ps1 -Changed -BuildContent`.
- Done when literal tests pin: a Bleed tick against 0 and high physical defence, weakness/resistance, no ward spend,
  stacking; a Void power skill from a Physical caster meeting magic defence; 100/30/0 by seat (0 = no hit, no
  riders); Reposition to front from rear (solo and trading), rooted fizzle, no Buckler pay.

**M3: Palace Passage onto seats, picker and gamepad (implementer).**
- `SkillEffect.cs:222-223` (`PicksRequired` stays 2), `FightMenuState.cs:171-210` (field picks),
  `FightSession.Talents.cs:~567` (legality), `FightSession.Skills.cs:1014-1060` (resolution via `PlaceAt`),
  `Core/FightController.Input.cs:700-708` (picker), empty-seat pick markers in the fight screen tree
  (`Domain/UiKit/Screens/FightScreen.cs`, `ScreenRegistry.cs`); gamepad Left/Right stops include empty seats during a
  seat pick; `skills.json:942-980` description "Move an ally to any position, trading with whoever stands there. Free
  action. Neither may be rooted." `Domain/Preview/PreviewStage.cs` solo passage case.
- Gate: `tools/test.ps1 combat` and `ui`, then `run_tests_parallel.ps1 -Changed -BuildScenes -BuildContent`.
- Done when: every existing `PalacePassageTests` case passes unmodified except `ASoloPartyIsRefusedTheCast`, rewritten
  as "a solo Shawn passes to an empty seat, keeps his turn"; cancel after the first pick unchanged; UiAudit green at
  four aspects; a gamepad test walks traveller -> empty seat -> commit.

**M4: Schedule and intents (implementer).**
- `RawEnemyEntry.cs:157-165` + `schedule`, resolver refusals (unknown id, id not in `abilities`, turn < 1, overlapping
  sequences); `FightSession.Enemies.cs:57-91` (commit order 2.2), `:300-387` (`BuildIntent`: `DamageBySeat`, `Then`),
  `:732-780` (acting-turn count and step used up on resolve; not on a forfeit); `EnemyIntent.cs:19-43`, `:189-291`
  (Bleed/Pull/Knell kinds, slug, tint, fallback); `FightHudModel.cs:1461` (tooltip), plate suffix
  `FightSession.Enemies.cs:487-496` ("Death Knell next", "Step back"); lethal badge style in the HUD's intent paint.
- Gate: `tools/test.ps1 combat` and `ui`, then `run_tests_parallel.ps1 -Changed`.
- Done when literal tests pin: turns 1/2 and 5/6 are the pair, 3/4/7 the draw; a stun after the chains delays the
  knell to the next acting turn and nothing takes its place; draws are consumed identically with and without a
  schedule (same seed, same later rolls); the badge number goes front -> middle -> 0 after a Move and after a Passage
  inside one turn; lethal style on and off across the HP line; tooltip and plate text.

**M5: The Bellwether's kit, presented (implementer).** Content: `skills.json` `bellwether_scratch` (DamageSingle,
melee, physicalMove, Bleed, stance `attack`, layered vfx), `dark_chains` (Reposition, `toSeat` 1, Void, stance
`extra`), `death_knell` (DamageSingle, Void, `damageBySeatMaxHpPercent` (values M5/M7), stance `cast`); `enemies.json:325-345`
(`attackWeight` 0, abilities, `schedule`, `rallyPerRound.stance`/`vfx`). Code: `FightSession.Rounds.cs:119-151`
(rally presentation on the round beat; a round starting inside an open beat records the toll as its own beat right
after it), `FightHudModel.cs:1084-1170` (`RallyRow`). Missing vfx folders degrade to none until M8.
- Gate: `run_tests_parallel.ps1 -Changed -BuildContent -BuildScenes`; `tools/preview.ps1 -Enemy bellwether` looked at.
- Done when `BellInTheFogRunTests` walks Endure/Break/Fall with the kit, the preview shows all three skills, the rally
  badge counts 1..10, and `docs/EVENTS.md` (Bell section) describes the kit.

**M6: Bot (implementer).** `FightAction.cs:210-233` (Passage offered with seat), the telegraph answer (3.10) in
`Domain/Bot/FightRunner.cs`; `BotRunDriver.cs:189-226` + `tools/bot.ps1:34-55`: `-GrantBook <ids>` (taught and
equipped), `-NoTelegraphAnswer`; trace per knell: Bellwether turn, round landed, seat, damage, Shawn HP before,
answered by Move/Passage/none; Bleed share of the Bellwether's damage. `docs/BOT_SUMMARY_SCHEMA.md`.
- Gate: `tools/test.ps1 combat` and `run`, then `run_tests_parallel.ps1 -Changed`; `BalanceBotSmokeTests` green.
- Done when a fixture shows the bot passing rear with the book, stepping back without; RandomLegal x50 clean.

**M7: Retune (implementer adjusts; verifier runs; orchestrator judges).** Runs as M8a did (Fresh, 5 archetypes,
`-ForceEvent bell_in_the_fog -ForceEventFloor 1..5 -EventChoice "Touch"`), four cells per floor, all `-NoTransform`
but the last: **answering, no book** (the contract cell); `-GrantBook palace_passage`; `-NoTelegraphAnswer`; Black
Ram (`-GrantTalent sheep_ram_converge`). Dials, in order: knell power and the middle %, Scratch power, Bleed
magnitude/duration, Bellwether attack last. The round-limited depth rate (5.3%/step) is not a dial.
- Done when, per floor: **answering, no book**: Endure 60-85%, median Shawn HP at Endure <= 30% (the shipped
  contract). **Knell at the front** (measured on the no-answer cell, per knell): kills a Shawn who entered it at full
  HP in >= 90%. **Knell in the middle** (answering cell): never kills a Shawn at >= 60% HP, for both the first and
  the second knell (rally ~+24% vs ~+64%, so the spread is the hard part; M2 sized the knell off max HP, 3.4).
  **No-answer Endure <= 5%.** Black Ram and
  with-book reported, no contract. Per-floor table in the commit message.
- **Landed (2026-09-28):** knell 130/20/0 with no variance roll (the +-20% roll was the M6 208-480 spread), Bleed
  5 x 3, Bellwether attack 40 and speed 6, step threshold 15%. Schedule unchanged. Tables and misses in the M7 commit.

**M8: Art and sound (owner assets; implementer integrates).** Recipes `keyed: false` for every sheet (5);
`tools/slice_spell_sheet.py --new <id>`, §5b vfx blocks, icons via `STATUS_ICON_PROMPTS.md`; `-Runtime` captures of
Scratch (several hits, both facings), chains, the knell at each seat, the lethal badge and the toll ripple, owner away.

## 5. Assets (owner generates)

**Every Bellwether effect is delivered on a real transparent background (RGBA, `keyed: false`), never on black.**
Luminance keying assumes brightness is coverage; dark void and blood tones would come out washed and half-transparent
(`slice_spell_sheet.py`'s own `keyed` note). Style: the Giant Rat flat-cel sheet: uniform bold dark-plum `#302036`
outlines, 2-3 flat tones per colour, small palette, no gradients, no painterly texture, no glow baked in. Void palette:
near-black violet `#1A1024`, bruise violet `#5B2A86`, lilac edge `#C9B6E4`; blood: `#6E0F1A` body, `#B3202E` light.
Sheets under `Art/Sheets/Spells/<id>/`, frames cut to `Resources/Spells/<id>*/`. Prompt stem for all:
"Transparent background PNG with real alpha, no background colour. Flat cel-shaded game VFX, bold dark-plum outline,
2-3 flat tones, no gradient, no glow, no text."
- **`bellwether_scratch/sheet.png`**: 4x3 grid, 512 cells, three layers from one sheet (three recipes).
  Row 1 **slash** (`target-centre`, at hit, `punch` 0.15, fade 0.35): "Four frames. Three curved parallel claw rakes
  tearing diagonally from upper right to lower left: frame 1 thin leading tips, frame 2 full-length strokes with
  tapered ends, frame 3 strokes at full width with a pale `#F2D7D0` inner edge, frame 4 strokes thinning to split
  hairlines." Row 2 **impact flash** (`target-centre`, at hit, `sort: effects`): "Four frames. A flat four-pointed
  starburst behind where the rakes cross, off-white core, blood-red outer star, shrinking to a dot." Row 3 **droplet
  particles** (`emitter`, at hit, burst 8-12, `aimDegrees` along the swipe, gravity, `lifeMin` 0.35): "Four single
  cells, each one flat blood droplet or small splat, bold outline, one highlight." Tuned with `preview.ps1 -Spell`.
- **`bellwether_toll_ripple/sheet.png`**: 4x2 grid, 512 cells (`caster-centre` on the bell, release): "Eight frames.
  Three concentric hard-edged rings of bruise violet with lilac rims expanding from a small point, thinning and
  breaking into arcs, gone by frame 8."
- **`dark_chains/origin.png`** (4x2, 512; `caster-centre`, release): "Eight frames. Two flat void chains unspooling
  upward from a point, links as bold-outlined violet ovals with lilac rims." **`dark_chains/travel.png`** (2x2, 512;
  travelling, `orient: path`, `travelSeconds` 0.35): "Four frames. One horizontal run of eight chain links pointing
  right, slight wave between frames." **`dark_chains/bind.png`** (4x2, 512; `target-centre`, arrival, `until: hold`):
  "Eight frames. Chains wrapping a torso-sized ring and snapping taut, a small violet spark at the lock; no body."
- **`death_knell/shockwave.png`**: 2x4 cells of 1024x512 (`place: formation`, `align: level`, `sort: ground`, at hit):
  "Eight frames. A ground shockwave rolling left to right as a flat violet band with a lilac crest, tallest at the
  left, shrinking to nothing at the right." **`death_knell/bell.png`** (3x2, 512; `sky` over the target, release ->
  hit): "Six frames. A huge spectral bell silhouette, flat lilac outline over a translucent violet fill, swinging once;
  two hard sound-arc lines at the lip on the ring frame."
- **Icons** (`STATUS_ICON_PROMPTS.md` template verbatim, then the subject; intent icons match `Resources/Intent/`):
  status `Art/UI/Status/Raw/bleed.png` "three red droplets falling from a short diagonal cut", `rally.png` "a small
  brass bell with an upward chevron"; intent `bleed.png` "a claw mark with one drop", `pull.png` "a chain link with an
  arrow pulling left", `knell.png` "a bell over three stepped bars, the front bar tallest".
- **Stances:** none new (Scratch `attack`, chains `extra`, knell/toll `cast`). **SFX:** claw rake (dry, wet tail);
  bleed tick; chains (rattle + void whoosh, ~0.8s); knell (cracked toll, sub-bass, ~2s); lethal-badge sting.

## 6. Risks

- **R1 Seat model ripple (M1).** Duo and post-death trios are drawn at seats of three (middle, not the far end);
  RandomLegal batches shift with the new moves. Full-party tests unmodified is the check.
- **R2 The knell's middle/front spread.** One power must kill at the front on the first knell (rally ~+24%) yet leave
  a 60%-HP Shawn alive in the middle on the second (~+64%), across builds and floors. **Taken at M2** (orchestrator):
  the knell is sized off the target's max HP (`damageBySeatMaxHpPercent`, 3.4), so the rally cannot move it; owner
  call 3 is settled that way unless the owner reverses it.
- **R3 The bot answers perfectly.** It reads exact numbers every time; a human misreads. The contract cell is a plain
  step back, the least a player must do; the lethal badge exists so a human does at least that.
- **R4** Palace Passage is a tier-2 book most floor-1 Shawns lack; the contract is set without it. **R5** A round can
  start inside an open beat; the ripple then needs its own beat after (M5 tests the next turn still opens).
- **R6** A high-magic-defence build may survive the front knell; M7 reports survivors by build. **R7** Stun-locking
  the Bellwether delays the knell indefinitely: accepted counterplay, reported by M7.

## 7. Open owner calls

1. **Party-screen holes** carried into fights (a player-placed empty front stays empty)? Default no.
2. **Scratch marks** on hit only. Persistent marks while bleeding need status-on-body visuals, which do not exist.
3. **Knell sizing fallback** (R2): attack-scaled (default) or a percent of the target's max HP if M7 cannot hold both
   the front and middle bars.
4. Gap-fills: chains deal no damage; the pair repeats on turns 5/6; bot steps back at >= 15% max-HP saving (25 until
   M7, lowered to sit under the 20% middle knell so the bot takes the second step a player would).
