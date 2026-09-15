# Progression v2: the reward tracks redone

Status: proposal for owner sign-off, 2026-09-15. Replaces the 100-level tracks.
Numbers come from `scratchpad/xp_model.md` (computed from the live content by
`xp_model.py`); every figure there is reproducible.

## 0. Answers to the three gate questions

**Q1. What ends at level 30?** Combat power. Levels 2 to 30 change the next
fight. Levels 31 to 40 change how the character is seen (titles, plate trim, a
victory pose) and carry one utility (Second Life). No stat, bump or ability
lives above 30. The promise is therefore two promises, stated in section 1.

**Q2. What should one deep run deliver?** Not three abilities. The spacing is
fixed first (ability at fight 6, second ability by the third or fourth run,
the level-15 capability at the end of the first deep run, third ability two
deep runs later, fourth around the twelfth run) and the run count to cap is
DERIVED from it: about 24 runs, of which 21 deep. The earlier "25 deep runs"
target is dropped as an input.

**Q3. What counts as ready to build?** A complete 39-node table (section 4)
and a behaviour spec for every new skill and capability (section 5). Four
abilities have specs written here as proposals awaiting your sign-off, and
Shawn's fourth is yours to design. Until those are signed off, phases 4 and 5
of the build cannot start; phases 1 to 3 can.

## 1. The contracts

Two promises, not one:

- **Combat levels (2 to 30):** every node gives an effect that is useful to
  the build the character is meant to play, at the depth the player will be
  when they collect it. Section 4 lists the intended effect per node; section
  7 says how each is checked.
- **Identity levels (31 to 40):** every node is visible to the player without
  changing combat strength, except Second Life at 35.

Pacing contracts, all checked against the model in section 3:

1. First new ability earned by the sixth fight of the first run.
2. No level costs more than one deep run's experience; none costs less than
   three fights at the depth where it is normally earned.
3. A run that dies on leg 2 still earns at least three levels.
4. A downed character earns half the fight's experience, never zero.
5. Within the combat stretch, no two neighbouring levels give the same kind
   of node, and the level after an ability is always a stat choice (the
   fourth ability at 30 is the last combat node; 31 is identity).
6. One way to learn a skill: the reward track. The level ladder in
   `skills.json` (`unlockLevel`) is removed.

What does not change: manual collection, talents bought with Embers, spell
books as per-run shop finds, persistent gear, the shared milestone geometry
idea on the track screen.

## 2. Definitions the model uses

- **Fight:** one victory in a Fight, Elite or Boss room. A leg is 8 rooms:
  room 4 is an Elite (2 enemies, 1.56x pay), room 8 a Boss, the other six roll
  Fight 54.6% of the time. So a leg has about 5.3 fights.
- **Deep run:** a run that clears leg 10 (80 rooms, about 53 fights). Its
  total pay is D.
- **Experience scaling:** today `ScaleReward` reuses the health curve, 7.5%
  per room, so a room-80 fight pays 325x a room-0 fight and D = 195,369 while
  a leg-2 death pays 1,189. This plan gives experience its own rate,
  **2.5% per room** (`ExpPermillePerStep = 25`), leaving health and gold as
  they are. Then D = 10,153, a leg-2 death pays 710, a leg-5 death 2,642.
  Levels then come from playing, not only from depth.
- **Level cost:** an authored table, one row per level, in content
  (`level_curve.json`), validated monotonic and inside contract 2. The
  geometric formula in `LevelCurve.cs` goes; a formula cannot meet contracts
  1 and 2 at once (the model fitted it three ways and every fit prices level
  40 at four to five deep runs).
- **Time, assumed until measured:** two minutes per fight, one per other
  room at the default battle speed. A leg is about 13 minutes, a leg-2 death
  about 25, a deep run about 2 h 10. Measure this in phase 6 and repin.

## 3. The cost table and the trajectories it produces

Cost to enter each level, in experience, with D = 10,153:

| Levels | Cost each | Cumulative at the last | As a share of D |
|---|---|---|---|
| 2, 3 | 90, 95 | 185 | 3 to 6 fights of run 1 |
| 4, 5 | 150, 250 | 585 | inside a leg-2 death (710) |
| 6 to 10 | 300, 350, 400, 450, 500 | 2,585 | a leg-5 death (2,642) |
| 11 to 15 | 1,200, 1,600, 2,000, 2,400, 3,000 | 12,785 | end of the first deep run |
| 16 to 20 | 3,500, 4,000, 4,000, 4,500, 5,000 | 33,785 | 0.35 to 0.5 D each |
| 21 to 30 | 6,000 rising by 500 to 10,000, then 10,000 | 115,785 | 0.6 to 1.0 D each |
| 31 to 40 | 10,000 each | 215,785 | 1.0 D each |

Career trajectory, three failures then deep runs (the model's sequence):

| After | Cumulative XP | Level | Rough clock |
|---|---|---|---|
| run 1, dies leg 2 | 710 | 5 | 25 min |
| run 2, dies leg 3 | 1,931 | 8 | 1 h |
| run 3, dies leg 5 | 4,573 | 11 | 2 h |
| run 4, first deep run | 14,726 | 15 | 4 h 15 |
| run 6 | 35,032 | 20 | 8 h 30 |
| run 10 | 75,644 | 25 | 17 h |
| run 14 | 116,256 | 30 | 26 h |
| run 24 | 217,786 | 40 | 48 h |

Ability 1 lands at fight 6 of run 1 (cumulative 185 against 184 earned by
then). Ability 2 lands on run 3. The level-15 capability is what the first
deep run delivers. Ability 3 on run 6, ability 4 on run 14. Identity levels
then arrive about one deep run apart.

Early-failure trajectory (a player who never gets past leg 3): 1,221 per run,
so level 10 on run 3, level 15 on run 11, level 20 on run 28. Slow but moving;
today such a player never leaves the first twenty levels.

## 4. The complete track

Node kinds. **V** ability (new skill). **C** choice: 4 stat points, banked,
player-spent (4 in one score is +80 health, or +8 mana, or +2 speed, or +1
signature gain per turn; 4 is the smallest bundle where every score moves).
**B** bump: one felt numeric step. **K** capability: a passive that changes
how the character plays. **U** utility. **I** identity.

Rule check on this table: 39 nodes; kinds V 4, C 12, B 11, K 1, U 1 in the
combat stretch (29); I 9 and U 1 in the identity stretch (10); no two
neighbouring combat nodes share a kind; each V is followed by a C.

| Lv | Kind | Shawn | Bjorn | Odette |
|---|---|---|---|---|
| 1 | start | Shear | Slam, Brace | Prismatic Orb |
| 2 | B | +40 max health | +50 max health | +30 max health |
| 3 | **V1** | Woolgathering | Bellow | Frost Flare |
| 4 | C | 4 stat points | 4 stat points | 4 stat points |
| 5 | B big | +1 Wool per turn | Fury per attack 15 to 20 | +6 max mana |
| 6 | C | 4 stat points | 4 stat points | 4 stat points |
| 7 | B | +5% Nature damage | +5% Physical damage | +5% Fire damage |
| 8 | U | Respec | Respec | Respec |
| 9 | C | 4 stat points | 4 stat points | 4 stat points |
| 10 | **V2** | Battering Ram | Rampage | Lightning Bolt |
| 11 | C | 4 stat points | 4 stat points | 4 stat points |
| 12 | B | +3 Wool capacity | +5 Fury when hit | +1 mana regen |
| 13 | C | 4 stat points | 4 stat points | 4 stat points |
| 14 | B | +40 max health | +50 max health | +5% Ice damage |
| 15 | **K** | Wool absorbs damage | Fury opens at 25 | Spells cost 1 less |
| 16 | C | 4 stat points | 4 stat points | 4 stat points |
| 17 | B | +5% Nature damage | +5% Physical damage | +6 max mana |
| 18 | C | 4 stat points | 4 stat points | 4 stat points |
| 19 | B | +2 Wool when hurt | Fury per attack 20 to 25 | +5% Arcane damage |
| 20 | **V3** | Cinderfault | Bulwark | Mend |
| 21 | C | 4 stat points | 4 stat points | 4 stat points |
| 22 | B | +40 max health | +50 max health | +30 max health |
| 23 | C | 4 stat points | 4 stat points | 4 stat points |
| 24 | B | +3 Wool capacity | +5 Fury when hit | +1 mana regen |
| 25 | C | 4 stat points | 4 stat points | 4 stat points |
| 26 | B big | +1 Wool per turn | Fury per attack 25 to 30 | +6 max mana |
| 27 | C | 4 stat points | 4 stat points | 4 stat points |
| 28 | B | +5% Nature damage | +5% Physical damage | +5% Lightning damage |
| 29 | C | 4 stat points | 4 stat points | 4 stat points |
| 30 | **V4** | owner-designed | Second Wind | Prism Ward |
| 31 | I | title "Contractor" | title "Bruiser" | title "Scholar" |
| 32 | I | plate rim, silver | plate rim, silver | plate rim, silver |
| 33 | I | title "Reaver" | title "Warden" | title "Adept" |
| 34 | I | portrait frame | portrait frame | portrait frame |
| 35 | U | Second Life | Second Life | Second Life |
| 36 | I | title "Veteran" | title "Veteran" | title "Veteran" |
| 37 | I | victory pose (art) | victory pose (art) | victory pose (art) |
| 38 | I | plate emboss, gold | plate emboss, gold | plate emboss, gold |
| 39 | I | title "Legend" | title "Legend" | title "Legend" |
| 40 | I | Mastery: gold plate, "Master" | same | same |

Totals against today: 48 stat points (today 52). Shawn's Wool per turn +2 and
capacity +6 (today +2 and +12 plus 12 filler). Bjorn's physical +15% (today
+30% plus 20 filler). Odette's max mana +18, regen +2 (today +20 and +3).
Power per track is lower than today on paper and felt more often; the
difficulty curve is not retuned in this plan, and section 7 measures the
effect.

Why each bump is felt, at the depth it is collected:

- Health: +40 on a 280 base is one more average enemy hit at leg 2 (a rat
  hits for about 20). Bjorn's +50 on 340 likewise.
- +5% of the character's own damage type: on a 27 slam that is 1 to 2 points
  per hit, at the floor of noticeability; three of them across the track are
  the maximum the research supports. They are the weakest nodes in the table
  and the first to swap if the bot says so.
- Wool per turn: from 1 to 2 doubles Shear's ammo, felt on turn 2.
- Fury per attack 15 to 20 to 25 to 30: reaches the 50 tier after 3, 3, 2, 2
  attacks respectively; each step removes an attack from the wait.
- Odette +6 mana on a 30 pool with an 8-cost orb: 3 casts to 4 at 36. Regen +1
  is one extra cast every 8 turns; it is the weakest of hers.

## 5. Behaviour specs for everything new

**Capabilities (level 15).**
- Shawn, Wool absorbs damage: existing rule, moved from 60. Each Wool point
  absorbs 1 damage, automatic, from the same bank Shear and Battering Ram
  spend. This is a tension by design: Wool is both armour and ammunition.
  Risk: at 1 to 2 Wool per turn it may read as a drain. Phase 1 measures it;
  if it lowers Shawn's win rate or his damage share, the fallback is a
  toggle in the skill menu ("Brace with Wool", ends the turn).
- Bjorn, Fury opens at 25: the pool's start rule becomes 25 instead of 0 for
  fights after collection. With gain 20 per attack and 10 per hit taken, the
  x2 slam is available on turn 2 rather than turn 3 or 4. It stacks with the
  gain bumps; phase 1 checks that x4 slams do not become the majority of his
  turns (target: under a third).
- Odette, Spells cost 1 less: every skill with a mana cost costs 1 less,
  minimum 1. Orb 8 to 7: 4 casts from a 30 pool instead of 3.

**Abilities, proposals for sign-off.** Shawn's V4 is not proposed: the owner
said Shawn's direction is undecided. Until signed off, no scaffold counts.
- Bjorn V1 Bellow (level 3): today's Provoke, unchanged.
- Bjorn V2 Rampage (level 10): hits every enemy for 70% of Slam's base
  damage, physical, weapon-scaled, fury tiers apply (x2 at 50, x4 at 100).
- Bjorn V3 Bulwark (level 20): spends 50 Fury (refused below 50), wards the
  chosen ally, including himself, for 30% of his max health for two turns.
  Uses the ally picker.
- Bjorn V4 Second Wind (level 30): spends all Fury, heals him 1% of max
  health per Fury spent. The decision it creates: x4 slam or a full heal.
- Odette V1 Frost Flare (3), V2 Lightning Bolt (10): existing, halved today.
- Odette V3 Mend (level 20): heals the chosen ally for 20 plus spell scaling,
  6 mana. Uses the ally picker; her first healing verb.
- Odette V4 Prism Ward (level 30): wards the chosen ally for 20 plus spell
  scaling for two turns, 8 mana.

**Identity rewards.** A new reward type carrying one of: title (string shown
under the name on the party plate and in the hub roster), plate rim colour
(silver, gold), portrait frame, plate emboss, victory pose (a stance the actor
wears on the victory screen; needs one still per character, commissioned
through the usual slicer route), Mastery (gold plate plus title). Titles are
free art-wise; poses are the only art cost, and level 37 can ship as a title
until the stills exist.

**New reward types in total:** FuryGainOnAttack (sets the value), FuryOnHit,
FuryStartOfFight, SpellCostReduction, Identity. FuryGainOnAttack is a set,
not an add, so the table reads as the number the player will see.

## 6. Experience outcomes, collection, saves

Experience per fight:
- Victory: full pay to every standing fielded character.
- Downed at victory: half pay, rounded up. Revived before victory counts as
  standing.
- Wipe: that fight pays nothing; earlier fights' experience is already saved
  (today's behaviour, kept).
- Abandon: keeps everything earned (today's behaviour, kept).
- The spiral check: with half pay a character who is downed every other
  fight trails by a quarter, not by half; phase 2's career simulation reports
  the gap.

Three moments for a node:
- Earned: the level-up. Shows on the run's level bar and marks the node
  Waiting.
- Collected: the player opens the track in the system menu, which exists in
  the hub, the map and the fight, and collects. Stat points bank; bumps,
  capabilities and abilities become part of the character.
- Active: a collected ability or capability is in the kit from the next
  fight, because the fight kit is built once per fight. So the first-hour
  promise reads: earn at fight 6, collect in the menu, cast at fight 7. The
  track screen says "from your next fight" on collection.

Saves: `SaveData.CurrentVersion` 5 to 6, additive migration:
- level clamped to 40; exp clamped to the cumulative cost of 40;
  claimedTrackLevel clamped to level.
- Stat points already banked or spent stay.
- Skills: nothing is stored for the ladder today; the kit is rebuilt per
  fight from the track. After migration a character's skills are those on
  collected nodes. A migrated character at level 12 with claimedTrackLevel 4
  sees nodes 5 to 12 pulsing and collects them; one at claimedTrackLevel 0
  starts at Shear and collects Woolgathering at 3. Nothing is lost, some of
  it is uncollected. This is a pre-release game and the migration is for the
  developer saves that exist.

## 7. Build order and gates

Phase 0, sign-off. This plan; the six ability specs in section 5; Shawn's V4
spec from the owner (may come later, but phase 4 cannot ship without it).

Phase 1, prototype the risks with the balance bot, before any content moves.
Three A/B runs of 200 fights each:
(a) Bjorn opening with Slam and Brace, Bellow arriving at 3, against today's
three-skill start: first-leg win rate must not drop more than 3 points.
(b) Shawn with Wool absorb on at level 15: win rate and damage share must not
drop.
(c) Bjorn with Fury start 25 and gain 30: share of x4 slams under one third.
Any failure sends the node back to section 5 before phase 4. About a day
including bot runs.

Phase 2, the experience model. Own experience rate (25 per mille), authored
cost table, downed half pay, save version 6. Pins: literal cumulative costs
at 3, 5, 10, 15, 20, 30, 40; per-fight pay at rooms 0, 8, 40, 80 for each
room type; the migration cases in section 6. Career simulation: the bot plays
the three-failures-then-deep sequence and the levels reached must match
section 3's table within one level at every row. About a day.

Phase 3, reward grammar. The five new reward types; validation refusing two
neighbouring combat nodes of one kind, an ability not followed by a choice,
any combat kind above 30, any bump under the floors in section 4, and a cap
other than 40. Schema regenerated. About a day.

Phase 4, content. The three tracks from section 4, the ladder removed from
`skills.json`, the four new skills authored (Bulwark and Prism Ward through
the ally picker, Rampage and Second Wind through the fury pool), content
rebuilt. Gate: content pins per track, one test per new skill with literal
numbers, the bot's legality tests covering the new verbs. About two days.

Phase 5, screen. Forty nodes, ten milestones (3, 5, 10, 15, 20, 25, 30, 35,
38, 40), the focus card gains "about N fights to go" from the cost table and
the current depth's pay, identity rewards rendered on the plate and roster.
Scene rebuild with UiAudit. Gate: captures at level 1, 15, 31 and 40 states;
a PlayMode test that earns level 3 mid-run, collects in the fight's system
menu and finds Woolgathering in the next fight's kit; a migration test that
loads a version-5 save. About two days.

Phase 6, verification. Full suite; the career simulation again against the
built content; a timed first hour played by the owner with a stopwatch to
replace section 2's minutes assumption; a played deep run to judge whether
the level-15 capability lands as the run's reward.

About eight agent days plus owner sign-offs, with a third again for
iteration from phases 1 and 6. Phases 1 to 3 can start on approval of this
document; 4 and 5 wait on the ability specs.

## 8. Decisions this plan makes for the owner

State them so they can be reversed cheaply:
- Experience gets its own 2.5% per-room rate; the alternative 4% makes deep
  runs pay 2.3x a leg-5 death instead of 3.8x, and is a one-number change.
- Cap 40, derived run count about 24. Not 100, not 50 runs.
- Choice bundles of 4 points.
- Bjorn loses Bellow from level 1 and keeps Brace.
- Identity from 31; four titles, two rim states, a frame, an emboss, a pose.
- Second Life moves from 90 to 35 and is the only utility above 30.
- Wool absorb moves from 60 to 15 with its existing automatic behaviour.
- The post-cap axis (harder contracts from Prince) is named and not built.
