# Run map P0: EV sheet

Plan: `PLAN_RUN_MAP.md` rev 5 §8 P0. Gold set against `PLAN_ITEMS.md` §1.6 and
§5.7. **Redo this sheet when item phase I5 sets `GoldIncomePermille`.**
(calc) = my arithmetic. (proposed) = my literal, with the reason beside it.
Numbers come from `enemies.json`, `characters.json`, `items.json` and the
code cited. I did not run Unity: the party figure below is authored stats, not
a measured party (see "Assumptions").

## 1. Literals P2 needs

| Literal | Value | Basis |
|---|---|---|
| `T1` | **0.34** | 40th percentile of `r`, floors 1-10 pooled (calc, section 2) |
| `T2` | **0.61** | 80th percentile (calc). `T2/T1` = 1.79 is party-independent |
| Reference party `P` for those `r` | **4560** = sum of maxHP x attack (calc, section 2) | if P2's real figure is `P'`, set `T = 0.34 x 4560 / P'` (and 0.61 likewise) |
| Gold chip, step-0 gold, strength 1 / 2 / 3 | **5 / 15 / 30** (base `G` = 5, x1 / x3 / x6) | (proposed) section 3 |
| Consumable chip, count, strength 1 / 2 / 3 | **1 / 2 / 4** | (proposed) section 3 |
| Gear / Book / Relic chip, strength effect | **none: chip size only, one pick per chip** | not derivable here; `PLAN_ITEMS.md` 1.1 owns what they pay |
| `GoldIncomePermille` target (for I5) | **about 260** | (calc) puts the median route at 104-119 against 120 +/- 20; I5 re-measures |
| Chip gold multiplied by the permille? | **No**: chips are authored net, scaled by `ScaleReward(step)` only | (proposed) so I5's permille moves fights, boss and treasure, not prizes |
| Prize-kind weights, default profile (floors 1-10) | gold **0.5**, consumable **0.5**, Gear/Book/Relic **0** | (proposed) sizing assumption for the gold target; P1 owns the table |
| Skip heal | **8%** of max HP, every living member, capped | owner, plan section 3 |
| Room fight damage, average | **5%** of party max HP per fight | `DifficultyCurve.cs:104-107` (a measured claim in its comment, not mine) |
| Damage by strength, % party max HP | **2.5 / 5.3 / 8.7** | (calc) 5% x median `r` of the band / mean `r` 0.437 |
| Pocket cost per room | **2.75%** of party max HP (55% fight share x 5%) | (calc) `DescentMap.cs:~171-184`, measured fight share 54.6% |

## 2. Strength thresholds

**Method (calc).** `r = threat / party` per plan section 2, with `threat` =
sum over rolled enemies of `ScaleHealth(hp) x ScaleAttack(atk)` at the node's
step (`DifficultyCurve.cs:46-47,83,96`), elite x1.40 HP and x1.15 attack
(`FightEncounterAdapter.cs:61-62`), and `party` = sum of maxHP x attack at
realm open. Rolls are enumerated exactly from `EncounterRoll.Roll`
(`EncounterRoll.cs:162-200`): 1 or 2 enemies at 50/50, drawn with
replacement, a draw dropped if it overflows 3 slots (treant takes 2). Nodes
sit at offsets 1-7, uniform. The pool is every rollable, `active` enemy whose
`minFloor` is reached (`RunEncounter.cs:149-152`).

| Floors | Pool (non-boss) |
|---|---|
| 1 | rat, beetle |
| 2-4 | + bog witch, treant |
| 5+ | + golem |

**Assumptions.** `P` = 200x7 + 260x10 + 140x4 = **4560** (calc): authored
`maxHealth` x `attack` for Shawn, Bjorn, Odette, unarmed. It leaves out the
Constitution bonus (+20 HP a point, `AbilityDerivation.cs:84`) and any weapon
(`FightEncounterAdapter.cs:200` replaces attack with weapon power). I could
not read the real party without running Unity. The absolute `T1`/`T2` move
with `P`; their ratio and the resulting mix do not. The party is assumed to
keep pace with the enemy curve between realms (plan section 2: "roughly
floor-independent").

| Floor | Strength 1 / 2 / 3 at `T1` 0.34, `T2` 0.61 |
|---|---|
| 1 | 64 / 32 / 4 % |
| 2-4 | 40 / 42 / 18 % |
| 5-25 | 34 / 43 / 23 % |
| 26+ | 68 / 32 / 0 % |
| **1-10 pooled** | **38.9 / 41.5 / 19.6 %** |

Median `r` per band: 0.218 / 0.463 / 0.756.

**Two problems the plan should know about:**

1. **Floor 1 is easy-skewed** (64/32/4). Its pool is two enemies, rat and
   beetle, so almost no node reaches 3. Pooling is what the plan asks for
   ("floors 1-10"). A floor-1-only fit would want a lower `T1`, which then
   over-labels floors 2+. I kept the pooled literals.
2. **Past floor 25 strength 3 disappears** (0%). Enemy scaling freezes at
   step 200 (`DifficultyCurve.cs:206`), so within a realm the scale is flat
   and `r` stops ramping with offset. Whether it matters depends on whether
   the real party keeps pace, which I cannot see. P3's floor-30 bot report is
   the check; if strength 3 is wanted deep, `T1`/`T2` need a floor-26+ branch
   or `r` needs a per-realm normaliser.

## 3. Strength multipliers (steep)

| Kind | s1 | s2 | s3 | Reason |
|---|---|---|---|---|
| Gold chip (base 5) | x1 = 5 | x3 = 15 | x6 = 30 | median-`r` ratio is 1 : 2.1 : 3.5 (calc). Reward follows risk^1.5: 1 : 3.1 : 6.5, rounded to 1 : 3 : 6. Each pip adds more (+2, then +3). Convex because HP carries between rooms, so damage hurts more as HP falls |
| Consumable count | 1 | 2 | 4 | steepest integer table; doubles. Potion = 200 HP, cost 15 (`items.json`) |
| Gear / Book / Relic | one | one | one | undefined here; see section 1 |

Expected chip per fight at 40/40/20 and gold weight 0.5 (calc): 7.0 gold
(0.5 x 5 x 2.8) and 1.0 potion (0.5 x 2.0).

**Caveat on consumables.** One potion is 15 gold in `items.json`, so the
consumable chip is worth 15 / 30 / 60 against the gold chip's 5 / 15 / 30.
Consumables out-pay gold at every strength. A potion heals a flat 200 and
does not scale with depth, so deep it is worth less; P2 should decide whether
potion chips scale. If gold chips look dull in play, raise `G`, not the
multipliers, and re-check section 4.

## 4. Gold: today against the target

Target: **120 +/- 20 step-0 gold per realm** (`PLAN_ITEMS.md` section 1.6).
Metric: income divided by `ScaleReward(1, realm start step)`, on a median
route.

**Today (calc), one realm, the 8-room path as `DescentMap` makes it:** 6
rolled rooms (steps 1-3, 5-7; fight share 55%, treasure 14.2%), the forced
elite at step 4 (`DescentMap.cs:~216-252`), the boss at 8. Gold per enemy is
the authored `currencyReward`; elite x1.56 (`VictoryRewards.cs:27`); depth
rides `ScaleReward` at 7.5% a step (`DifficultyCurve.cs:143`); treasure pays
a flat 15-30, mean 22.5, not scaled by depth (`RoomResolution.cs:23-24`,
`RoomResolver.cs:69-73`). Events and shops excluded.

| Floor | Fights | Treasure | Elite | Boss | **Total** | x target (120) |
|---|---|---|---|---|---|---|
| 1 | 66.9 | 19.2 | 41.7 | 107.0 | **235** | 2.0 |
| 2 | 93.7 | 10.8 | 57.3 | 107.0 | **269** | 2.2 |
| 5 | 102.2 | 1.9 | 63.0 | 107.0 | **274** | 2.3 |
| 10 | 102.2 | 0.1 | 63.0 | 107.0 | **272** | 2.3 |
| 26+ and 100 | 75.6 | 0 | 47.2 | 60.0 | **183** | 1.5 |

So today's income is **2.0 to 2.3 times the target** on the shortest route
alone, and the new maps add pocket fights on top. That matches the owner's
"way too much". Three notes:

- Flat treasure is already worthless past floor 5 in this metric (1.9 gold).
  `PLAN_ITEMS.md` 1.6 scales it with the curve, which makes it matter again.
- Floors 26+ read lower only because the 7.5% ramp inside the realm is frozen.
- The new-map median route (10 rooms before the boss, 5.5 fights, a boss,
  treasure as 14.2% of rooms) at permille 1000 pays 251 / 296 / 310 / 310 on
  floors 1 / 2 / 5 / 10 (calc), so the cut has to be about 60% once chips
  are added.

**Proposed, new-map median route (calc),** at `GoldIncomePermille` 260, `G` 5,
gold weight 0.5:

| Floor | Fights | Treasure | Boss | Chips | **Total** | In 100-140? |
|---|---|---|---|---|---|---|
| 1 | 29.1 | 8.3 | 27.8 | 38.5 | **104** | yes |
| 2 | 40.7 | 8.3 | 27.8 | 38.5 | **115** | yes |
| 5 and 10 | 44.5 | 8.3 | 27.8 | 38.5 | **119** | yes |

Chips are 32-37% of income. The median route is 10 rooms before the boss (the
7-room spine interior plus 3 pocket rooms, from the route table in plan
section 4), which is my guess at the bot's median; I5 replaces it with a
measurement.

## 5. Pocket and skip-heal value

Costs are % of party max HP (calc), from 2.75% a room (section 1). The skip
heal is 8% and is capped at max HP, so it is worth **0 at full HP** and the
full **8 points at 40% HP**.

| Pocket length L | Fight cost | Cost at 100% HP | Left at 100% | Cost at 40% HP (incl. forgone 8) | Left at 40% | Cost as share of current HP, 100% / 40% |
|---|---|---|---|---|---|---|
| 1 | 2.8 | 2.8 | 97 | 10.8 | 29 | 3% / 27% |
| 2 | 5.5 | 5.5 | 94 | 13.5 | 26 | 6% / 34% |
| 3 | 8.3 | 8.3 | 92 | 16.3 | 24 | 8% / 41% |
| 4 | 11.0 | 11.0 | 89 | 19.0 | 21 | 11% / 48% |

An anchor in the pocket is one elite enemy. Its damage is not derivable from
the 5% figure; the elite-pair threat is about 2.1 x an average pair (calc),
so roughly 10% a fight. I did not fold that into the table.

**Reading it.** At full HP a pocket is nearly free, so everyone takes it. At
40% HP it costs a third to half of what is left, plus the 8 points of heal
given up. That is the HP-band split the P3 gate wants. One strength-3 fight
costs 8.7%, so a 40% party that walks one is at about 31%.

**Skip-heal value (calc):** 8% = 1.6 average fights (8/5), or 3.2 fights at
strength 1 (8/2.5), or 0.9 at strength 3. It matters only when HP is below
about 90%. Six pockets skipped: up to 48%.

## 6. Detour exp and gold, per fight room

Rejoin offsets 2-7, mean (calc). Gold in step-0 gold at realm start, then the
absolute figure the player sees. Exp uses `ScaleExperience` at 2.5% a step
(`DifficultyCurve.cs:67,154`), so it is absolute. Floor 100 is capped at step
200. Level costs: 75 at level 2, 500 at level 10, 10,000 from level 29
(`level_curve.json`).

| Floor | Exp per fight | Gold per fight, `p` 260 | + chip (expected) | Gold per fight, absolute (both) | Gold scale at realm start |
|---|---|---|---|---|---|
| 1 | 32.7 | 5.4 | 7.0 | 12 | 1.0 |
| 10 | 264.7 | 8.3 | 7.0 | 2,796 | 182.6 |
| 100 | 5,582.6 | 6.0 | 7.0 | 24.8 million | 1.91 million |

Pocket totals scale by L x 0.55 fight rooms. At floor 1 a 2-room pocket is
1.1 fights: 36 exp (48% of the level-2 cost) and 13 gold (calc). At floor 10
it is 291 exp (58% of the level-10 cost) and 3,076 gold. Anchors (one elite
enemy, no pair) pay (calc), before depth curves: floor 1 15.6 gold and 30.4
exp, floor 10 24.3 gold and 42.4 exp.

Floor 100 gold looks absurd because gold rides the 7.5% curve to 1.9 million
(`DifficultyCurve.cs:143,206`), by design. The step-0 column is the one that
matters, and prices follow it (`PLAN_ITEMS.md` 1.6).

## 7. What I could not derive

- **Real party `P`.** Authored stats only (section 2). Needs a measured party
  at realm open: P2 can read it, or the bot can print it.
- **Strength effect on Gear / Book / Relic chips.** Left at "none".
- **Median route length (10 rooms).** A guess from the route table.
- **Prize-kind weights (0.5 / 0.5).** Mine, to size the gold target. P1's
  profile table owns them.
- **Deep (floor 26+) strength mix**, section 2, problem 2.
- **Anchor damage** (section 5), and how much of the 5% per fight is
  measured rather than designed: its source is a code comment.
- **Event and shop gold** are left out of section 4. Events may pay gold.
