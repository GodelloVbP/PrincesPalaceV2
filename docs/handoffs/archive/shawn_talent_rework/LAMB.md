# The Fragile Lamb — full spec

Design session 2026-08-08. Companion to `README.md` in this folder, which
covers the wool resource, the allegiance rule, the ember economy, and the
Black Ram. **Read that first** — everything here assumes it.

This document supersedes README §4's "Path 1" stub, which recorded only the
Lamb's engine and Wail.

Status markers: **[LOCKED]** decided in session · **[PROPOSED]** suggested,
not confirmed · **[ASSUMED]** built on an unanswered question, stated so it
can be corrected cheaply · **[OPEN]** needs a call.

---

## 0. What has changed since the README was written

The Black Ram has since been **built**. That changes this handoff from a
greenfield design into an extension of a system that exists, and the Lamb
should be authored the same way rather than inventing a parallel one.

| What exists now | Where |
|---|---|
| `TalentEffectType` — a closed enum of talent rules | `Domain/Combat/TalentEffect.cs` |
| `TalentEffect { Type, Magnitude, Threshold }` — **two numbers, never three** | same file |
| `TalentEffectSet.Best / BestBelowHealth / Threshold / IsBelowThreshold` | `Domain/Combat/TalentEffectSet.cs` |
| `ContentDatabase.AllegianceRootOf` — root exclusivity | `Core/Content/ContentDatabase.cs` |
| `StatusEffectType.Shielded` — next hit reduced by Magnitude %, then spent | `Domain/Combat/StatusEffect.cs:49` |
| `ActiveStatus.Source` — **added explicitly for this engine** | `Domain/Combat/StatusEffect.cs:99` |
| `WoolWhenWardedAllyHit` — the Lamb's engine, already in the enum | `Domain/Combat/TalentEffect.cs:67` |

Effects are authored in `talents.json` by **enum member name**, not by
integer. Magnitude is a percent for every `*Percent` member and a flat count
otherwise. Threshold is "percent of max health" everywhere except one
documented exception.

**Two conventions to respect, both stated in `TalentEffect.cs`'s own header:**

1. **A rule needing a third number wants splitting into two rules.** Several
   Lamb effects below are deliberately split for exactly this reason.
2. **A new rule costs a line in the enum *and* a hook in the pipeline.** That
   cost is meant to stay visible. §7 lists every new member this path needs
   rather than burying them.

### Numbers are percentages on purpose

`CombatMath.DamageScale = 10`, Shawn has 200 max health and 7 attack,
enemies run 80–950 health and 3–12 attack. Rather than guess absolute
figures against a formula that is still moving, **every number below is a
percentage or a multiple of Attack.** Those stay correct if the damage
curve is retuned.

---

## 1. Identity

Frail. Debuffs enemies, buffs allies, lets others tank. She has **no
defensive stats** — only wards she paid for. Wool is her armour and her
ammunition, and every point of it is a live question: **protection, or
damage?**

She must be **playable solo**, which is what the Weight of Wool and Shatter
strands exist for. [LOCKED]

---

## 2. Naming — read this before writing any code

**The word "shield" is already taken twice in this codebase.**

- `BreakShield` (`Domain/Combat/BreakShield.cs`) is a **stagger meter**.
  Nothing to do with absorbing damage.
- `StatusEffectType.Shielded` is the existing per-hit damage reduction from
  the Magical Shield relic.

**Call the Lamb's construct a Ward, everywhere — content, code, and UI.**
It is implemented *using* the `Shielded` status, but a third meaning of
"shield" in this pipeline will produce a real bug.

---

## 3. The engine (root, free, exclusive)

| Rule | Value | Status |
|---|---|---|
| Effect | `WoolWhenWardedAllyHit` | Already in the enum |
| Magnitude | **+2 wool** | [LOCKED] |
| Trigger | A combatant carrying a Ward **you** applied takes a hit | [LOCKED] |
| Cap | **Once per ally, per turn** | [LOCKED] |
| Includes yourself? | **Yes** — a self-Ward pays like any other | [PROPOSED] |

Max income with a full flock warded and all three hit: **+6 wool/turn**, on
top of the +1 baseline. That ceiling is what the ability prices in §5 are
set against.

### [OPEN] — narrow the engine to Wards specifically

`TalentEffect.cs:67` currently reads "an ally carrying a status **you**
applied." That is broader than the design intends, and it interacts badly
with Wail: implementing Wail as party-wide `Protect` (§6) would make every
Wail also an income engine, paying up to +6/turn for two turns on top of
whatever the Wards are already producing.

**Narrow the trigger to `StatusEffectType.Shielded`.** The ward *is* the
setup step this engine exists to reward — a Regen tick or a Protect should
not count.

---

## 4. Ward mechanics

The one shared construct the whole path is built on.

| Property | Value | Status |
|---|---|---|
| Implemented as | `StatusEffectType.Shielded`, `Source` = Shawn | [LOCKED] |
| Effect | Reduces the **next hit** by Magnitude percent, then is spent | Existing behaviour |
| Base cost | **2 wool** | [PROPOSED] |
| Duration | Until spent — `TurnsRemaining` set generously high, per the existing `Shielded` convention | Existing behaviour |
| Stacking | Re-applying refreshes rather than stacks (`StatusEffects.Apply`) | Existing behaviour |

**Popping on the first hit is load-bearing, not incidental.** It is what
keeps the engine from becoming an infinite loop: a ward that absorbed
several hits would pay +2 each time and return more wool than it cost. The
per-ally-per-turn cap in §3 is the second belt on the same problem.

---

## 5. The six strands

### Below the convergence — 1 / 2 / 3 embers

#### Fleece Ward — the construct itself

Engine-critical. Nothing else in the path functions without it, which is
why it is the cheapest strand.

| Tier | Cost | Effect | Type |
|---|---|---|---|
| T1 | 1 | Unlocks **Ward** (2 wool): apply a Ward reducing the target's next hit by **40%**. | `WardReductionPercent` 40 |
| T2 | 2 | Ward's reduction rises to **60%**. | `WardReductionPercent` 60 |
| T3 | 3 | **Rule-change:** Ward no longer costs an action. Warding and attacking happen in the same turn. | `WardIsFreeAction` |

> T3 is the strand's arrival and the single biggest quality-of-life node in
> the path. Before it, every ward is a turn not spent doing anything else,
> which makes a frail character with no damage output feel awful. After it,
> she has a real turn again.

#### Mending Fleece — sustain on the warded

| Tier | Cost | Effect | Type |
|---|---|---|---|
| T1 | 1 | Applying a Ward also applies `Regen` for **2 turns**, healing **3%** of the target's max health per turn. | `WardAlsoAppliesRegen` 3, threshold 2 |
| T2 | 2 | **6%** per turn, **3 turns**. | `WardAlsoAppliesRegen` 6, threshold 3 |
| T3 | 3 | **Rule-change:** when a Ward is **spent**, it heals the wearer for **10%** of their max health. | `WardHealsWhenSpent` 10 |

> T3 inverts how losing a ward feels. Up to that point a popped ward is a
> resource gone; after it, the ward breaking is itself a payout — which
> matters because *everything* in this path wants wards to break.
>
> Note `WardAlsoAppliesRegen` uses Threshold as **turns**, not percent of
> max health. That is a second documented exception alongside
> `TransformExtendOnKill`; if a third appears, the field wants splitting.

#### Weight of Wool — damage from wards held

Her solo-play damage. **[ASSUMED]** party of three (Shawn plus two) and the
multiplier composing as `1× base + per-ally + self`.

| Tier | Cost | Effect | Types |
|---|---|---|---|
| T1 | 1 | +**25%** attack damage per warded ally; +**100%** if you are warded yourself. *(Max 2.5×)* | `WardDamageBonusPerAlly` 25 · `WardDamageBonusSelf` 100 |
| T2 | 2 | +**50%** per warded ally; +**150%** self. *(Max 3.5×)* | same two, 50 / 150 |
| T3 | 3 | +**50%** per warded ally; +**200%** self *(max 4×)*, and **the self bonus persists for 1 turn after your own Ward is spent.** | same two, 50 / 200 · `WardSelfBonusPersistsTurns` 1 |

> **Split into two effect types on purpose.** "0.5× per ally and 2× on
> self" is two independent numbers, and packing them into Magnitude and
> Threshold would overload Threshold with a third meaning. Two members read
> correctly and compose additively without argument.
>
> T3's persistence clause is what stops her damage from collapsing the
> instant she is hit — otherwise the highest-value ward in the game
> evaporates to the first stray attack and the build feels like a trap.

### Above the convergence — 2 / 3 / 4 embers

#### The Flock — everything applies to everyone

| Tier | Cost | Effect | Type |
|---|---|---|---|
| T1 | 2 | Ward also applies to **one** additional ally, at **half** strength. | `WardSpreadsToAllies` 50, threshold 1 |
| T2 | 3 | Ward applies to the **whole party**, at **half** strength. | `WardSpreadsToAllies` 50, threshold 0 *(0 = all)* |
| T3 | 4 | **Rule-change:** Ward applies to the whole party at **full** strength, for the same wool cost. | `WardSpreadsToAllies` 100, threshold 0 |

> This is the multiplier limb: it triples engine income (§3's +6/turn
> ceiling only exists with the Flock), maxes Weight of Wool, and triples
> Shatter's yield. Expensive on purpose.

#### Wool Gift — spend your resource on someone else's turn

One idea — converting wool into an ally's tempo — escalating in what it
buys.

| Tier | Cost | Effect | Type |
|---|---|---|---|
| T1 | 2 | **2 wool:** restore **25%** of an ally's max mana. | `GiftManaPercent` 25 |
| T2 | 3 | **3 wool:** an ally's next attack deals **+50%** damage. | `GiftAttackBonusPercent` 50 |
| T3 | 4 | **Rule-change — 5 wool:** an ally acts **immediately**, pushed to the front of the turn queue. | `GiftImmediateTurn` |

> T3 is the mirror of the Ram's Charge strand: he pushes enemies back, she
> pulls allies forward. Both land on the `wip/intent-speed` turn queue, and
> the two together are the most legible expression of "this game has a turn
> order you can manipulate."

#### Shatter — spend the wards for burst

The counterweight to Weight of Wool. That strand pays you to **hold**
wards; this one pays you to **spend** them. Neither is complete alone, and
the turn-by-turn choice between them is the path's core loop.

| Tier | Cost | Effect | Types |
|---|---|---|---|
| T1 | 2 | Unlocks **Shatter** (3 wool): detonate every Ward you have active. Each deals **100%** of your Attack to a random enemy. | `ShatterDamagePercentOfAttack` 100 |
| T2 | 3 | Each detonation deals **175%** of your Attack. | `ShatterDamagePercentOfAttack` 175 |
| T3 | 4 | **Rule-change:** detonating **your own** Ward deals **triple** damage and applies `Vulnerable` to every enemy hit. | `ShatterSelfWardMultiplier` 300 · `ShatterAppliesVulnerable` |

> **T3 is the best decision in the path and the reason it holds together.**
> Your own Ward is simultaneously the largest damage multiplier in the tree
> (Weight of Wool's 2×) and the only protection a character with no
> defensive stats has. Shattering it is a burst that leaves her naked. Do
> not dilute this — no compensating defensive rider, no "but she gets a
> smaller ward back."
>
> `Vulnerable` already exists (`StatusEffect.cs:37`), so T3's rider rides an
> existing hook.

---

## 6. Convergence — Wail

**Free.** Gated at **9 embers spent in this path**.

| Property | Value | Status |
|---|---|---|
| Effect | All enemies deal **50% less damage** for **2 turns** | [LOCKED] |
| Cost | **10 wool** (raised from 8) | [PROPOSED] |

**Why the reprice.** At 8 it was already flagged as underpriced against the
Ram's transform (7 wool, personal, 3 turns) because Wail is party-wide *and*
enemy-wide. The Lamb's engine also out-earns the Ram's once the Flock is
online — up to +6/turn against his +3. At **10** on a 10-wool cap, Wail
empties the meter completely, which is the right shape: it is the panic
button, and using it means no Ward and no Shatter this turn.

### Implementation — Wail needs no new status

There is no "enemy attack down" hook, and adding one would be a new engine
rule. But **party-wide `Protect` 50% for 2 turns is functionally
identical** — no enemy in this game ever attacks another enemy, so
"enemies deal 50% less" and "allies take 50% less" describe the same
outcome. `Protect` already exists (`StatusEffect.cs:34`) and already
decays by turn count.

Two consequences:

1. It reads as an ally buff rather than an enemy debuff. Fine for this
   character; worth knowing for the VFX brief.
2. **It makes narrowing the engine to `Shielded` mandatory**, not optional —
   see §3. Otherwise Wail hands out three status effects sourced by Shawn
   and doubles as an income engine.

---

## 7. Ultimate — The Golden Fleece [PROPOSED]

**Free.** Gated at **20 embers spent in this path**. Never designed in
session; this is a proposal.

> **Wards no longer pop. They persist until the end of the fight.**

| Type | `WardsNeverExpire` (flag) |
|---|---|

Why this one:

- It is the payoff to the **entire** path rather than to one strand. Weight
  of Wool sits permanently at maximum, the engine pays every turn, Shatter
  always has full ammunition, and Mending Fleece's regen never lapses.
- It mirrors the Ram's capstone shape — `TransformPermanentBelowHealth`
  takes a temporary state and makes it who he is. Same move, different
  resource.
- It resolves her fragility narratively: the frail one finally stops
  breaking.

**Balance note:** this removes the engine's income trigger, since a ward
that never pops never pays. That is a genuine tension and probably the
right one — the capstone converts her from a *generator* into a *battery*.
Worth watching in play; if income collapses too hard, the fix is to have a
persistent Ward pay +2 the first time it absorbs a hit each turn rather
than never.

**Alternative considered:** *Martyr* — when Shawn falls, every ally gains a
full-strength Ward and +100% attack for 3 turns. Thematically sharper for
"fragile," but it is a death-trigger on a character the player is trying to
keep alive, which makes it feel like a consolation prize rather than a goal.

---

## 8. New `TalentEffectType` members

Fourteen, listed so the cost is visible per `TalentEffect.cs`'s own
convention. Each names the one hook it needs.

| Member | Magnitude | Threshold | Hook |
|---|---|---|---|
| `WardReductionPercent` | % reduction | — | Ward application |
| `WardIsFreeAction` | *(flag)* | — | Action economy |
| `WardAlsoAppliesRegen` | % max health/turn | **turns** ⚠ | Ward application |
| `WardHealsWhenSpent` | % max health | — | `ConsumeShieldedReduction` |
| `WardDamageBonusPerAlly` | % per warded ally | — | `CombatMath` attack side |
| `WardDamageBonusSelf` | % if self-warded | — | `CombatMath` attack side |
| `WardSelfBonusPersistsTurns` | turns | — | Turn-start bookkeeping |
| `WardSpreadsToAllies` | % strength on extras | count, 0 = all | Ward application |
| `GiftManaPercent` | % ally max mana | — | New ability |
| `GiftAttackBonusPercent` | % | — | New ability |
| `GiftImmediateTurn` | *(flag)* | — | `TurnOrder` |
| `ShatterDamagePercentOfAttack` | % of Attack | — | New ability |
| `ShatterSelfWardMultiplier` | % | — | Shatter resolution |
| `ShatterAppliesVulnerable` | *(flag)* | — | Shatter resolution |
| `WardsNeverExpire` | *(flag)* | — | `ConsumeShieldedReduction` |

⚠ `WardAlsoAppliesRegen` reads Threshold as **turns**, the second exception
to "Threshold is percent of max health" after `TransformExtendOnKill`. If a
third appears, split the field rather than adding a third exception.

**Note the bias toward the attack side.** `WardDamageBonusPerAlly` and
`WardDamageBonusSelf` must fold into `ScaledAttack` **before** defense is
subtracted, for the reason `CombatMath.cs:42-48` already gives about weapon
scaling: multiplying the finished figure would make armour worth less
against exactly the swings it most needs to blunt.

---

## 9. New abilities

Five, and they are content, not talents. **[OPEN]** — how abilities are
authored (`skills.json`? a new file?) was not checked during this session
and needs confirming before build.

| Ability | Wool | Unlocked by |
|---|---|---|
| **Ward** | 2 | Fleece Ward T1 |
| **Shatter** | 3 | Shatter T1 |
| **Wail** | 10 | Convergence |
| **Gift: Mana / Fury / Haste** | 2 / 3 / 5 | Wool Gift T1 / T2 / T3 |

Each wants its own animation, per the wool-abilities decision in
README §2.

---

## 10. Tests

Mirror what the Ram's build already asserts, plus these — every one of
which pins a failure mode identified above:

- **A Ward absorbing a hit pays +2 wool exactly once**, even if the same
  ally is struck twice in one turn. *(The infinite-loop guard.)*
- **A `Protect` or `Regen` sourced by Shawn pays no wool.** *(§3's
  narrowing — this is the test that stops Wail from doubling as an income
  engine.)*
- Weight of Wool composes to **4×** at T3 with self and both allies warded,
  and to **1×** with none.
- The self bonus survives exactly **one** turn after the self-Ward is spent,
  then stops.
- Shatter with no active Wards deals **no** damage and **spends no wool**
  rather than throwing or consuming the cast.
- The Flock T3 wards the whole party for the **same** 2 wool as T1's single
  ward.
- **Pin every formula with literal expected values**, per CLAUDE.md gotcha
  5 — never recompute the production formula to build the expectation.

---

## 11. Open decisions

1. **Narrow the engine to `Shielded`** (§3). Blocks Wail. Highest priority.
2. **Wail reprice to 10** (§6) — confirm.
3. **The ultimate** (§7) — proposal only, never discussed.
4. **Does a self-Ward pay the engine?** (§3) — assumed yes.
5. **Party size** (§5) — assumed three; the 4× cap moves if it is four.
6. **Ward base cost of 2 wool** (§4) — proposed, never set.
7. **How abilities are authored** (§9) — not investigated.
8. **`WardAlsoAppliesRegen`'s Threshold-as-turns** (§8) — accept the second
   exception, or split the field now.

---

## 12. Out of scope

- **The mage path's six strands.** Its engine (`WoolPerStatusedEnemy`) and
  its convergence exist; the name is still unchosen. Six strands and an
  ultimate outstanding.
- **The Black Ram's ultimate**, still open from README §6.5.
- **Fusion.** Parked in README §8. Note the Lamb's half of two pairs —
  Piercing Roar and the unnamed Wail+Explosion — remains undefined.
- **The other four characters.** 189 talents untouched.
- **Screen layout and art.** Unchanged from `talent_tree_v2/`.
- **Balance validation.** Every number here is first-pass. Nothing has been
  played.
