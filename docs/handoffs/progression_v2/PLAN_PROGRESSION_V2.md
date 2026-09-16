# Progression v2, revision 2

Status: Revision 2.4, 2026-09-16. Supersedes revision 1 (kept beside this file).
Revision 2.1, 2026-09-15: corrections from phase 1 and phase 2 reports (see CHANGELOG_r2.md addendum).
Revision 2.2, 2026-09-15: the ward numbers retuned to the percent model phase 4 found in the code (section 5; CHANGELOG_r2.md addendum r2.2).
Revision 2.3, 2026-09-16: the owner's call on AUDIT #152 -- a ward IS a shield. The percent model is gone, section 5's ward bullets are back to a pool of points, and the numbers are shield points (CHANGELOG_r2.md addendum r2.3).
Revision 2.4, 2026-09-16: the owner's four calls on the shield model as built -- wards STACK, the default duration is ONE turn, Brace is a percentage of Bjorn's health with a cooldown, and The Golden Fleece belongs to the caster (CHANGELOG_r2.md addendum r2.4).
Model: xp_model.md in this folder, computed by xp_model.py from the live
content. Every number below traces to it or to the cost table in section 3.

Scope of this revision's request: approve the separate experience rate
experiment, the corrected trajectory work and the combat prototypes (phases
1 to 3). Migration, curve acceptance and content rollout (phases 4 to 6) stay
held until the three decisions in section 0 are settled by the owner.

## 0. The three decisions, with this revision's answers

**D1. Is "first ability by fight 6" a guarantee or a typical outcome?**
Typical, for a standing character, under the encounter model in section 2.
The guarantee is weaker and stated as such: a character who stands at every
victory of leg 1 has the ability by the leg-1 boss at the latest, because the
two forced rooms of leg 1 alone (elite at room 4, boss at room 8) pay 189,
above the 160 the level costs, even if all six other rooms roll no fight. A
character downed at both forced rooms earns 95 and gets there by the leg-2
elite. Knockout trajectories are reported separately in section 3.

**D2. Does Second Life affect combat success?** Yes. It is combat power and
moves into the combat stretch, at level 25. Spec in section 5. Levels 31 to
40 then contain nothing but identity.

**D3. Is level 30 the completion point?** Yes. The track screen names level
30 as completion. Levels 31 to 40 are an optional prestige stretch, built
cheaply (titles, rim states, one pose) and evaluated on its own in phase 6;
if it does not hold interest, the cap is 30 and nothing else changes.

## 1. The contracts

Combat levels (2 to 30): every node offers an immediately useful option when
collected and applied. For a bump or capability the effect is on the next
fight. For a choice, 4 stat points bank and the screen shows what 4 points in
each score buys, so the useful option exists at once; whether the player
spends is theirs.

Identity levels (31 to 40): every node is visible on the plate, roster or
victory screen and changes no combat number.

Pacing:
1. Ability 1 by fight 6 of run 1, typical; guaranteed by the leg-1 boss for a
   standing character (D1).
2. No level costs more than one deep run's pay (D); none costs less than two
   typical room-0 fights (58).
3. A run that dies on leg 2 earns at least three levels.
4. A character out for the whole fight earns half, rounded up; never zero.
5. Combat stretch: no two neighbouring nodes of one kind; the node after an
   ability at 3, 10, 20 is a choice; 30 is the last combat node and is
   followed by identity. Utility nodes (respec at 8, Second Life at 25) occur
   only in the combat stretch.
6. One way to learn a skill: the track. The unlockLevel ladder is removed.

Unchanged: manual collection; talents via Embers; spell books as per-run
finds; persistent gear; the milestone geometry idea on the track screen.

## 2. Definitions and the encounter model

Fight: a victory in a Fight, Elite or Boss room. Leg: 8 rooms; room 4 is an
Elite (2 enemies, pay x1.56), room 8 a Boss (forest warden, 100 base); the
other six roll Fight 54.6% of the time (measured over 500 seeds). Normal
fights field 1 or 2 enemies, mean 1.5, drawn from the floor's pool. So a leg
has about 5.3 fights; a deep run (leg 10) about 53.

Pay per fight at room 0: normal 29 average (rat-only 15 minimum), elite 61,
boss 100. Experience scaling today reuses the health curve at 7.5% per room
(room 80 pays 325x room 0; deep run D = 195,369; leg-2 death 1,189; ratio
164). This plan gives experience its own rate of 2.5% per room. Then
D = 10,153, leg-2 death 710, leg-3 death 1,221, leg-5 death 2,642. Ratio deep
run to leg-5 death: 3.8. The alternative 4% rate gives D = 23,311 and a ratio
of 6.0, weighting depth more; 2.5% is chosen because levels should come from
playing, not only from depth. Health and gold keep the 7.5% curve. (Revision
1 stated this comparison backwards.)

Level cost: an authored table, one row per level, in content, validated
monotonic and inside contract 2. The geometric formula goes; the model fitted
it three ways and every fit prices level 40 at four to five deep runs.

Time: assumed two minutes per fight and one per other room until phase 6
measures it. A leg about 13 minutes; a leg-2 death about 25; a deep run about
2 h 10.

## 3. Cost table and trajectories

Cost to enter each level, experience:

| Levels | Cost each | Cumulative |
|---|---|---|
| 2, 3 | 75, 85 | 160 |
| 4, 5 | 150, 250 | 560 |
| 6 to 10 | 300, 350, 400, 450, 500 | 2,560 |
| 11 to 15 | 1,200, 1,600, 2,000, 2,400, 3,000 | 12,760 |
| 16 to 20 | 3,500, 4,000, 4,000, 4,500, 5,000 | 33,760 |
| 21 to 30 | 6,000 rising 500 per level to 10,000; level 30 = 10,000 | 115,760 |
| 31 to 40 | 10,000 each | 215,760 |

Deterministic career (fixed room sequence, standing character, expected pay):
exact levels and remainders, the phase-2 pin.

| After | Cum. XP | Level | Remainder to next | Clock |
|---|---|---|---|---|
| run 1, dies leg 2 | 710 | 5 | 150 of 300 | 25 min |
| run 2, dies leg 3 | 1,931 | 8 | 321 of 450 | 1 h 05 |
| run 3, dies leg 5 | 4,573 | 11 | 813 of 1,600 | 2 h 10 |
| run 4, first deep run | 14,726 | 15 | 1,966 of 3,500 | 4 h 20 |
| run 6 | 35,032 | 20 | 1,272 of 6,000 | 8 h 40 |
| run 10 | 75,644 | 25 | 6,884 of 8,500 | 17 h 20 |
| run 14 | 116,256 | 30 | 496 of 10,000 | 26 h |
| run 24 | 217,786 | 40 | capped | 47 h 40 |

Ability 1 at fight 6 of run 1 typical (160 needed; six average room-0 fights
pay 174). Ability 2 on run 3. Level-15 capability at the end of the first
deep run. Ability 3 on run 6. Ability 4 on run 14. Level 30 on run 14 has a
margin of 496, under one normal fight at that depth; phase 2 confirms the row
exactly and the owner may loosen level 30 to 9,500 if the margin reads as
luck.

Knockout trajectory: the same sequence with the character downed at every boss (half pay on the largest room of each leg) reaches level 5 on run 1 still (576 against 560), level 14 not 15 after the first deep run, and level 30 on run 17. Downed at every victory: level 3 by the leg-2 elite, level 30
around run 26. Phase 2 prints all three.

Stuck-player trajectory (never past leg 3, 1,221 per run): level 10 on run
3, 15 on run 11, 20 on run 28. The track does not fix the wall; phase 6
evaluates it with the weakest bot policy and names one lever (section 7)
rather than pretending the accounting is an answer.

## 4. The complete track

Kinds. V ability. C choice (4 stat points; 4 in one score is +80 health and
+8 physical defence, or +8 mana and +8 magical defence, or +2 speed, or +1
signature gain per turn). B bump. K capability. U utility. I identity.

Rule check: 39 nodes. Combat stretch 29: V 4 (3, 10, 20, 30), C 11, B 10, K 2
(15, 26), U 2 (8, 25). Identity stretch 10: I 10. No neighbouring combat
nodes share a kind. Each of V1 to V3 is followed by a C.

| Lv | Kind | Shawn | Bjorn | Odette |
|---|---|---|---|---|
| 1 | start | Shear | Slam, Brace | Prismatic Orb |
| 2 | B | +40 max health | +50 max health | +30 max health |
| 3 | V1 | Woolgathering | Bellow | Frost Flare |
| 4 | C | 4 points | 4 points | 4 points |
| 5 | B | Wool per turn 1 to 2 | Fury per attack 15 to 20 | Max mana 30 to 36 |
| 6 | C | 4 points | 4 points | 4 points |
| 7 | B | +5% Nature damage | +5% Physical damage | +5% spell damage |
| 8 | U | Respec | Respec | Respec |
| 9 | C | 4 points | 4 points | 4 points |
| 10 | V2 | Battering Ram | Rampage | Lightning Bolt |
| 11 | C | 4 points | 4 points | 4 points |
| 12 | B | Shear costs 3 to 2 Wool | Slam +5 flat (27 to 32) | Mana regen +1 |
| 13 | C | 4 points | 4 points | 4 points |
| 14 | B | +40 max health | +50 max health | +30 max health |
| 15 | K | Tuck In (free action) | Fury opens at 25 | Spells cost 1 less |
| 16 | C | 4 points | 4 points | 4 points |
| 17 | B | +5% Nature damage | +5% Physical damage | Max mana 36 to 42 |
| 18 | C | 4 points | 4 points | 4 points |
| 19 | B | +2 Wool when hurt | Fury per attack 20 to 25 | +5% spell damage |
| 20 | V3 | Cinderfault | Bulwark | Mend |
| 21 | C | 4 points | 4 points | 4 points |
| 22 | B | +40 max health | +50 max health | +30 max health |
| 23 | C | 4 points | 4 points | 4 points |
| 24 | B | Battering Ram 6 to 4 Wool | Slam +5 flat (32 to 37) | Mana regen +1 |
| 25 | U | Second Life | Second Life | Second Life |
| 26 | K | Tuck In wards 3 per Wool | Fury opens at 50 | Spells cost 2 less |
| 27 | C | 4 points | 4 points | 4 points |
| 28 | B | +5% Nature damage | +5% Physical damage | +5% spell damage |
| 29 | C | 4 points | 4 points | 4 points |
| 30 | V4 | owner-designed | Second Wind | Prism Ward |
| 31 | I | title "Contractor" | title "Bruiser" | title "Scholar" |
| 32 | I | plate rim, silver | plate rim, silver | plate rim, silver |
| 33 | I | title "Reaver" | title "Warden" | title "Adept" |
| 34 | I | portrait frame | portrait frame | portrait frame |
| 35 | I | plate emboss, silver | same | same |
| 36 | I | title "Veteran" | title "Veteran" | title "Veteran" |
| 37 | I | victory pose (art) | victory pose (art) | victory pose (art) |
| 38 | I | plate rim, gold | same | same |
| 39 | I | title "Legend" | title "Legend" | title "Legend" |
| 40 | I | Mastery: gold plate, "Master" | same | same |

Stat points: 44 (today 52). Titles share one display slot: the newest is
shown, earlier ones selectable in the hub roster; their value is the choice,
not the count, and phase 6 judges whether that is enough.

What each bump changes, evaluated with the kit held at that level:

- Shawn. Wool per turn 1 to 2 at level 5: Shear (3 Wool) every other turn
  instead of every third. Shear 3 to 2 at 12: with 2 per turn, Shear every
  turn. +2 Wool when hurt at 19: two hits taken fund a Shear. Battering Ram 6
  to 4 at 24: affordable after two turns instead of three. Health +40 on
  280: one extra hit at leg 2 if a hit is about 20; phase 1 measures the
  average hit per leg and this claim is retracted if it fails. Nature +5%
  three times: 1 to 2 points on a 30 hit; the weakest nodes, kept at three
  because the research gave no threshold below which a bonus is unnoticed,
  and they are the first to swap. Wool capacity bumps are gone: income never
  reaches the cap.
- Bjorn. Fury from 0, no hits taken: 15 per attack reaches the 50 tier in 4
  attacks; 20 in 3 (level 5); with Fury opening at 25 (level 15) and 20 per
  attack, 2 attacks; at 25 per attack (level 19), 1 attack; at opening 50
  (level 26), the x2 slam is the opening move of every fight and the x4 tier
  needs 2 attacks. Each of those four nodes changes when a tier is first
  available; revision 1's third gain bump was dead and is replaced by Slam +5
  flat at 12 and 24 (base 27 to 32 to 37; x4 108 to 128 to 148, felt on
  every slam). Fury-on-hit bumps were dead at the breakpoints and are gone.
- Odette. Orb costs 8. Mana 30 to 36 at 5: 3 casts to 4. Cost 8 to 7 at 15 on
  36: 5 casts. Mana 42 at 17: 6 casts. Cost 6 at 26 on 42: 7 casts. Spell
  damage +5% applies to every element she casts, so it is build-agnostic; the
  per-element bumps are gone. Regen +1 twice: one extra cast every 7 turns;
  her weakest nodes, swap candidates for health.

## 5. Behaviour specs

Capabilities.
- Shawn 15, Tuck In: phase 1 rejected the automatic absorb (Shear starvation
  41% at baseline to 55% with it, see PHASE1_BOT_REPORT.md). Tuck In is a
  skill: costs 0 mana, spends up to 4 banked Wool, and puts up a shield of
  **5 points per Wool spent** -- 20 points at a full four, standing for one
  turn. It costs no mana and does not end the turn (one use per turn), which
  since the one-turn clock is also the only reason he can look at it. Level 26
  raises it to 8 points a Wool, so 32 at four. It is an Ability-kind node; the
  ability-then-choice rule holds because 16 is a choice.
- Bjorn 15, Fury opens at 25; 26, opens at 50: the pool's start value for
  fights after collection. Decay, gain and tiers unchanged.
- Odette 15, spells cost 1 less; 26, 2 less: every mana-costed skill, to a
  minimum of 1.

Utility.
- Respec at 8: refunds Embers and stat points, once per collection.
- Second Life at 25: the existing rule, pinned by phase 2 as the code behaves:
  when the whole party is wiped, every downed member returns at half max
  health (floored, minimum 1); one charge per collected node per run, pooled
  across the fielded squad; consumed on use, reset at run start. It is combat
  power, which is why it sits at 25.

Abilities. New skills: Rampage, Bulwark, Second Wind, Mend, Prism Ward, and
Shawn's V4 (owner-designed, not proposed). Existing and unchanged:
Woolgathering, Battering Ram, Cinderfault, Bellow, Frost Flare, Lightning
Bolt.
- Rampage (Bjorn 10): DamageAll, physical, weapon-scaled, 70% of Slam's base
  (19 at base 27), tiers as Slam: tier chosen at cast from current Fury,
  spent at cast, gain on attack fires once after the action.
- Bulwark (Bjorn 20): costs 50 Fury from the primary pool (refused under 50),
  and puts a shield worth **30% of Bjorn's own max health** on the chosen ally
  or himself -- 78 points on his authored 260, and still worth about three
  enemy hits once both bars have grown.

  **The ward rules, in full.** A ward IS A SHIELD: a pool of shield points on
  one character. Incoming damage comes off the pool first and the remainder
  off health, so a hit bigger than the pool carries the rest through and a hit
  smaller than it leaves the pool standing with less in it. It sits after
  dodge and every defence and before health, and before the signature pool
  (Wool) where both exist -- `StatusEffects`' own WARDS header states the
  whole order and is the only place that does.

  **Wards STACK.** A character may carry several; the shield total is the sum
  of the live entries, and a new ward never replaces, refreshes or refuses an
  existing one. Damage drains the entry that **expires soonest first**, then
  the next, and an entry emptied by a hit is removed; ties break by age,
  oldest first, and an entry that never expires sorts last. Soonest-first is
  the only order that does not waste shield.

  **Duration is one of the wearer's own turns** by default, authored per row
  as `wardTurns` and counted down at the wearer's turn start. None of the five
  ward skills authors one, so all five take the default; the three relic wards
  (Magical Shield, Sparring Buckler, Runic's mana conversion) author the whole
  fight instead, which is the promise they have always made. **The Golden
  Fleece** stops the clock on wards whose **caster** holds it -- his own, and
  the ones the Flock spreads onto other people -- and does nothing for a ward
  somebody else put on him. The pool still drains either way.

  This is what revision 2 described and what the code had never had. Phase 4
  found the ward to be a percentage off one hit with a 999-turn duration and
  pinned it honestly; revision 2.2 retuned the numbers into that model rather
  than build the pool, and filed the model question as AUDIT #152. The owner
  answered it on 2026-09-16: shields, stacking, one turn.

  **A one-turn ward is not visible on the caster's own turn**, and that is a
  consequence rather than a bug: it is applied during turn N and ticked away
  at the start of turn N+1, so it covers exactly the enemy phase in between.
  Two things fall out of it -- the badge is only on screen while the monsters
  act, and Shatter cannot reach a ward cast on an earlier turn, so the Fragile
  Lamb's Shatter strand needs her own `WardIsFreeAction` node to function at
  all. Filed as AUDIT #153 for the owner rather than tuned here.
- Second Wind (Bjorn 30): requires at least 25 Fury, spends all, heals 1% of
  max health per Fury spent; at 100 a full heal.
- Mend (Odette 20): heals the chosen ally for 20 plus her spell attack (the
  same ScaledAttack on the spell axis that spells use for damage), 6 mana.
  New effect kind HealSingle; today only HealSelf and HealParty exist.
- Prism Ward (Odette 30): a shield of **20 points plus her spell attack** on
  the chosen ally, 8 mana. Same ward rules. The scaling term is back: revision
  2.2 cut it because the number landed in a percentage and a spell attack past
  80 would have warded for more than the whole hit -- one ally immune for eight
  mana, forever. In shield points a spell attack of 90 buys a 110-point pool,
  which is a big shield and nothing worse, so Prism Ward scales for exactly the
  reason Mend one line above always did.

Reward types to add: FuryGainOnAttack (sets the pool's gain; talents that add
gain stack on top; none exist today), FuryStartOfFight (sets the start
value), SpellCostDelta (all mana-costed skills), SkillCostDelta (one named
skill, one resource; used for Shear, Battering Ram), SkillFlatDelta (one
named skill; used for Slam), SignatureAbsorbPerPoint (1 or 2), Identity
(title, rim, frame, emboss, pose, mastery). Seven; each is one row in the
resolver and one validation rule. SignatureAbsorbPerPoint is built but unused
by the shipped tracks after phase 1.

## 6. Experience, collection, saves

Per fight: victory pays full to standing fielded characters; a character who
was out for the whole fight (downed when the encounter was built) earns half
rounded up; a character who fell during a fight the party won earns full,
since they fought it; a wipe pays nothing for that fight and earlier fights
are already saved; abandon keeps everything.

Three moments: earned (level-up, node shows Waiting), collected (system menu,
available in hub, map and fight), active (from the next fight; the kit is
built once per fight). The screen says "from your next fight".

Saves: version 5 to 6 resets track progression. Level 1, exp 0,
claimedTrackLevel 0, stat points refunded and invested scores reset to base.
Talents, Embers and gear are kept. Rationale: the only saves are developers';
a level-preserving migration would have to reconcile old rewards against new
nodes and would be more code than the feature, and revision 1's clamp was not
a migration. Loading twice is idempotent because the version stamp is written
with the reset.

## 7. Build order and gates

Phase 0, sign-off: this document; the five ability specs; Shawn's V4 from the
owner (phase 4 cannot ship without it).

Phase 1, prototypes with the balance bot. Paired seeds, identical starting
states, 30 runs per arm, results reported with a 95% interval; these are
signals for the owner's decision, not acceptance thresholds.
- (a) Bjorn opening with Slam and Brace, Bellow at 3, against the three-skill
  start: first-leg clear rate (denominator: leg-1 attempts) and average
  health at the end of leg 1.
- (b) Shawn with automatic absorb at 15: deaths per 100 fights, average
  health at fight end, share of Shawn's turns where Shear was unaffordable
  because absorb drained the bank, and win rate. Reject the automatic form
  if the starvation share exceeds a fifth of his turns or deaths rise; then
  the Tuck In fallback is specified and run.
- (c) Bjorn with opening 50 and gain 25: share of his turns that are x4
  slams; over a third sends opening 50 back to 40.
- (d) The average enemy hit per leg, to test the health-bump claims.
About a day.

Phase 2, the experience model. Own rate at 25 per mille; authored cost table;
downed half pay; Second Life pinned as specified. Deterministic pins: the
cumulative costs; per-room pay at rooms 0, 8, 40, 80 per room type; the
career table's exact levels and remainders for the fixed room sequence; the
two knockout trajectories; the stuck-player trajectory. Random-encounter
simulation reported as ranges. About a day.

Phase 3, reward grammar. Seven reward types; validation: adjacency rule in 2
to 30, ability-then-choice at 3, 10, 20, identity-only above 30, utility only
below 31, bump floors, cap 40. Schema regenerated. About a day.

Held until D1 to D3 are settled and phases 1 and 2 report:

Phase 4, content: the three tracks, ladder removed, five new skills plus
Shawn's V4, HealSingle effect, content rebuilt. Gate: content pins; one test
per new skill with literal numbers; ward replacement rules pinned; bot
legality over the new verbs. About two days.

Phase 5, screen: 40 nodes, milestones 3, 5, 10, 15, 20, 25, 30, 35, 38, 40,
level 30 marked as completion, "about N fights to go" from the cost table and
current depth's pay, identity rendered on plate and roster, title selection
in the roster. Scene rebuild with UiAudit. Gate: captures at levels 1, 15,
31, 40; a PlayMode test that earns level 3 mid-run, collects in the fight's
system menu, and casts Woolgathering next fight; a version-5 save loads and
is reset once. About two days.

Phase 6, verification: full suite; career simulation against built content;
owner's timed first hour; a played deep run judging the level-15 capability;
the weakest bot policy's trajectory for the stuck-player case with one named
lever to evaluate if it stalls (a catch-up multiplier on experience below
leg 4 for characters under level 10); the prestige stretch judged on its own
with the cap-at-30 fallback.

About nine agent days plus sign-offs, plus a third for iteration.

## 8. Decisions made for the owner, reversible

Experience rate 2.5% per room (4% weights depth more). Cap 40 with 30 as
completion. Choice bundles of 4. Bjorn keeps Brace, loses Bellow to level 3.
Second Life at 25. Wool absorb at 15 automatic, upgrade at 26. Odette's
element bumps replaced by all-spell bumps. Shawn's capacity bumps replaced by
skill-cost bumps. Save version 6 resets track progression. The post-cap axis
(harder contracts from Prince) named, not built.
