# Spell expansion plan — thirteen global spells

Stage 1b of the owner's thirteen-spell brief (Appendix A, verbatim). Written
against `docs/SPELL_EXPANSION_BASELINE.md` (stage 1a) and against the code,
re-verified this pass; every `path:line` below was opened by the author of this
document, and section 7 lists them with what each one was checked for.

Behaviour is the contract. Every number carried over from the owner's brief is
marked `prototype` and is tuning, not specification.

---

## 0. Decisions taken

Each decision states the alternative that was rejected and what taking it would
have cost.

### Owner decisions, 2026-09-20

Two answers arrived after this document was written. Both are binding and both
are folded into the sections below rather than bolted on here; this block is the
record of what was asked and what was said, so a later reader can tell an
owner's call from the author's.

**(1) "Do the entire plan."** This answers the one open question at the end of
this section — **D1 applies globally**, with the nine re-authored rows in D1's
own table and Lucky Deck's stated exception. The question is closed; the section
that held it now records the answer.

**(2) "The DoTs and everything can stack of course."** This replaces D3 outright
and the refresh half of D4. It is a plain-language answer, so what follows is
**Fable's reading of it, revisable** — implemented as written, and the place to
correct it if the reading is wrong:

- A damage-over-time status (Poison today; Burn and Thorned in milestone E)
  **does not merge on re-application**. Every application creates an independent
  instance carrying its own snapshot magnitude, its own remaining ticks and its
  own source. Instances of the same type coexist; each ticks and expires on its
  own; the holder's per-turn tick total is the **sum** of its live instances.
- **Detonation** (1.6) consumes the remaining stored value of **every** Poison
  instance on the target in one consumption, and is recorded **once**.
- **Censer of Embers' 4→6→8 intensity ladder is dropped** (2.11). A recast adds
  an instance. With it goes the `ActiveStatus.IntensityLevel` field D4 proposed.
- **Existing Poison changes**: `StatusEffects.Apply`'s `max`/`max` merge becomes
  instance stacking for it. Its tick arithmetic, its detonation arithmetic and
  its mitigation (none — flat magnitude, no affinity, no defense) are otherwise
  preserved exactly.
- **Wards already stack** (baseline §3) and are untouched by this.
- **Magnitude debuffs the new spells apply** — Vulnerable, Chilled — stack
  **additively** across instances with independent expiry, subject to the
  existing speed floor for Chilled. The effective percentage is the sum.
- **Binary restrictions** (Stun, Feared, Rooted, Provoked) and **Marked** keep
  their existing refresh behaviour: "stack" has no meaning for a restriction
  beyond duration, and the owner's brief says Marked's meaning is preserved.

`Domain/Combat/FallingOffStacks.cs` was checked first as the existing model for
"a pile where every stack carries its own expiry", per the standing rule against
inventing a third shape. **It is the wrong seam here and is left alone**: it is
keyed by an arbitrary string on `CombatantState.StackTimers`, and a stack in it
is a bare remaining-turn count — it carries no magnitude and no `Source`, which
is exactly what an instance of a DoT has to carry. What it models correctly is
*independent expiry*, and that is the property `ActiveStatus` lists gain here:
the status list becomes the stack pile, several entries of one `Type`, which is
the shape wards have used since 2026-09-16 and which `WardsInDrainOrder`,
`WardPoints` and `SummariseWards` already read. Reusing the ward shape rather
than `FallingOffStacks` is the "swap the seam, not every caller" answer.

### D1. Duration means effective affected turns, and the clock moves to turn end

**Taken.** Replace the two-family duration model with three families, chosen by
one question — *when does this status do its work?*

| Family | Members | Counter moves | N means |
|---|---|---|---|
| **AtTick** | Poison, Regen, and every new DoT | at the tick that deals/heals, turn start | N ticks |
| **AtUse** | Stun, Feared, Provoked, Empowered | when the effect is spent | N uses |
| **AtTurnEnd** | Protect, Vulnerable, Chilled, Rooted, Marked, Shielded | at the END of the bearer's turn, exempting a turn the status was applied during | N affected turns |

Today the `AtTurnEnd` row is true only of `Shielded`
(`StatusEffects.cs:622`, `TickWardsAtTurnEnd`). The other five decrement at
turn START inside `Tick` (`StatusEffects.cs:806-874`) and are therefore removed
*before* the action of their last counted turn — a `turns: 2` Vulnerable exposes
one turn, not two (baseline §9, re-verified). `IsSpentByTheTurn`
(`StatusEffects.cs:795-799`) becomes `DurationClock(StatusEffectType)` returning
one of the three families: one table, no flags, no per-status special case.

The exemption is `TickWardsAtTurnEnd`'s existing `_wardsRaisedThisTurn` rule,
generalised: a status applied during the bearer's own turn does not age at that
turn's end. It has two concrete users — a ward raised on the caster's turn, and
a caster's self-inflicted Vulnerable (Court of Whispers, Ashen Reckoning's
wording "through their next completed turn"). That is the two-use validation
`docs/CODE_STANDARDS.md` §10 asks for.

**Alternative rejected:** author every new spell at N+1 and leave the clock
alone. Cost: "duration" would mean one thing on the thirteen new rows and
another on the eleven existing ones, with nothing in the type system or the
content schema able to say which — the flag-in-place-of-a-model §10 bans, and
the exact shape the baseline's own §9 note warns about.

**What changes if D1 is applied globally**, with the re-authoring that preserves
today's effective behaviour. These are every live application of an
`AtTurnEnd`-family status; all were located by grepping
`StatusEffectType.{Vulnerable,Protect,Chilled,Rooted,Marked}` across
`Scripts/Domain` and `Scripts/Core` and opening each site.

| Source | Status | Authored today | Effective today | Re-author to | path:line |
|---|---|---|---|---|---|
| `mud_burst` | Vulnerable 25 | 2 | 1 turn | **1** | `ContentData/skills.json:43-70` |
| `bog_mud_burst` | Vulnerable 25 | 2 | 1 turn | **1** | `ContentData/skills.json:466-490` |
| `wail` | Protect 50 | 2 | 1 turn | **1** | `ContentData/skills.json:282-295` |
| `shell_up` | Protect 50 | 2 | 1 turn | **1** | `ContentData/skills.json:410-424` |
| Shatter T3 talent | Vulnerable 30 | 2 | 1 turn | **1** | `FightSession.Talents.cs:509-510`, consts `:521-522` |
| Rampaging Bull's Horn | Protect 50 | 2 | 1 turn | **1** | `FightSession.RelicMechanics.cs:256-257`; `FightTuning.cs:202-203` |
| Frosty (item modifier) | Chilled 20 | 2 | 1 turn | **1** | `FightSession.cs:851`; `FightTuning.cs:164-165` |
| Sylvan's root (item modifier) | Rooted | 2 | 1 turn | **1** | `FightSession.cs:872`; `FightTuning.cs:175` |
| Lucky Deck red card | Chilled 30 | 1 | **0 turns** | **1** (cannot be preserved) | `FightSession.Relics.cs:359`; `FightTuning.cs:61-62` |
| Magic Marker / Jar of Bear Urine | Marked | 99 | unchanged | leave | `Marks.cs:26,31` |
| every ward | Shielded | 1 / 99 | unchanged | leave | `FightTuning.cs:98,109` |

Lucky Deck is the one row where exact preservation is impossible:
`ActiveStatus`'s constructor floors turns at 1 (`StatusEffect.cs:209-215`), and
today's one-turn Chilled covers only the interval before the bearer's next turn
starts. Under D1 it becomes one full affected turn. **Recommendation: accept the
change** rather than delete the proc — it is a one-turn slow on a relic's red
card, and the alternative is a zero-duration status the model cannot express.

Fear is untouched: it is `AtUse` (`StatusEffects.cs:221-232`, `ConsumeStun`
decrements it once per actual skip), and baseline §9 is right that the
consumed-on-use family already satisfies the owner's rule.

### D2. Poison detonation takes a premium parameter, not a second path

**Taken.** `StatusCombos.SpendPoisonIfMatched` (`StatusCombos.cs:43-59`) keeps
its job — *spend the entry, report what its remaining ticks are worth* — and
grows one argument, a percent applied to the reported figure, defaulting to 100.
`FightSession.ResolveDetonation` (`FightSession.cs:909-918`) keeps its job —
deal that figure through `DealDamage`, typed Poison, credited by side. Ashen
Reckoning passes 150 (`prototype`); every other Nature/Poison hit passes 100 and
behaves exactly as today.

**Alternative rejected:** a second consume-and-deal path for Ashen Reckoning.
Cost: two places that both know "detonation is `Magnitude * TurnsRemaining`",
free to disagree; and two places a recursion guard would have to be written.

### D3. Statuses stack as independent instances; one table says which ones do

**Superseded by the owner's decision of 2026-09-20 and rewritten.** The
original D3 was a refresh policy ("stronger snapshot wins, duration refreshes").
The owner's answer was *"the DoTs and everything can stack of course."* Fable's
reading, recorded above, is the contract; this is what it means operationally.

A second application of a status either **merges into** the entry already there
or **adds a second entry beside it**. That is one question with one answer per
status type, so it is one total table — `StatusEffects.StackPolicy(type)` —
beside the two tables `StatusEffects` already keeps (`ElementOf`, and D1's
`DurationClock`). No flag on `ActiveStatus`, no per-caller argument.

| Policy | Members | A second application |
|---|---|---|
| **Stack** | Poison, Regen, Protect, Vulnerable, Chilled, Shielded (+ Burn, Thorned in E) | adds an entry: its own magnitude, its own clock, its own `Source` |
| **Refresh** | Stun, Feared, Provoked, Empowered, Rooted, Marked | today's `max`/`max` merge, `Source` re-pointed and never cleared |

The split is not arbitrary and it is not "new spells versus old". It follows
from what the magnitude of each status **means**:

- Where the magnitude is a **quantity or an additive percentage**, two of them
  is a bigger number and the player can add it up — the rule
  `StatusEffects.DamageTakenMultiplier` already applies across entries and
  `StatusEffects.WardPoints` already applies across wards. Those stack.
- Where the status is a **binary restriction** (Stun, Feared, Rooted, Provoked)
  or a **single-spend token** (Empowered, Marked), "stack" has no meaning
  beyond duration: a turn cannot be skipped twice, and `Marks.ConsumeMark`
  spends one mark whatever is underneath it. The owner's brief says Marked's
  meaning is preserved. Those refresh.

Feared sits with the restrictions although it carries a Vulnerable percent,
because that percent is **one authored constant** (`Fear.VulnerablePercent`,
D8) whose own header exists to stop two appliers disagreeing about it; summing
it across instances would be a balance change nobody asked for. Empowered sits
with them for the same shape as Provoked — it is spent on the next occurrence,
and `ConsumeEmpowerment` removes one entry.

Protect and Regen are **not named in the owner's decision**; they are here by
symmetry with Vulnerable and Poison, which are. A model where Vulnerable stacks
and Protect does not has an asymmetry with no stated reason behind it, and
`DamageTakenMultiplier` already sums Protect across entries today. Recorded as
Fable's reading, revisable.

**Consequences, stated rather than discovered:**

- A DoT's per-turn total is the **sum of its live instances' magnitudes**, and
  it is bounded — an applier adding one instance per turn plateaus at
  `instancesPerTurn x magnitude x tickCount`, because the oldest instance
  expires as the newest lands. It does not run away.
- An additive percentage debuff on a **turn-end** clock is bounded by how many
  of them can be alive at once, which is `ceil(duration / castInterval) + 1`.
  Where that product crosses `1.0`, `StatusEffects.MinimumDamageTakenMultiplier`
  (0.1) is the floor and it holds. §5 records the measured cases.
- No cap is invented anywhere. A cap is a balance decision and it is the
  owner's.

**Alternative rejected:** keep the merge and express "stacking" as a bigger
magnitude on one entry. Cost: the instances would have to share one clock, so
a Poison landed on turn 5 would expire with one landed on turn 1 — which is the
opposite of what an instance's "own remaining ticks" means, and it cannot carry
two `Source`s, which two wool engines are paid from.

### D4. Instance stacking needs no new field on `ActiveStatus`; mitigation stays a table

**Rewritten with D3.** The original D4 added an `IntensityLevel` field to carry
Censer of Embers' 4→6→8 ladder. **The ladder is dropped** (owner, 2026-09-20: a
recast adds an instance). With it goes the field: `ActiveStatus` already carries
everything an instance needs — `Type`, `Magnitude` (the snapshotted per-tick
strength), `TurnsRemaining` (its own ticks), `Source` (its own applier) — and
the only thing that changes is that there may now be several of them.

Mitigation remains a table rather than a field: `StatusEffects.MitigationOf(type)`
returns `None` for Poison (today's behaviour, unchanged) and `AffinityOnly` for
Burn and Thorned. The element is already a table, `StatusEffects.ElementOf`
(`:745-759`), whose own comment says a new damaging member must be added there
or `StatusEffectsTests.EveryStatusTypeAnswersElementOf` fails.

So a tick is: `damage = ApplyMitigation(instance.Magnitude, MitigationOf(type),
ElementOf(type), AffinityOf(holder))`, once per instance, summed. One path, two
table values, no branch on status identity and no branch on how many instances
there are.

**Alternative rejected:** a `DotSnapshot` payload object hung off `ActiveStatus`.
Cost: a DoT's tick strength would have two homes (`Magnitude` for Poison,
`payload.TickStrength` for the rest), and every existing reader of `Magnitude`
would need to know which.

### D5. `TickReport` becomes a list of typed rows (closes AUDIT #188)

**Taken.** `TickReport.PoisonDamage`/`PoisonAbsorbed`
(`StatusEffects.cs:761-792`) are replaced by
`IReadOnlyList<TickRow>` where a row is
`(StatusEffectType Status, DamageType Element, int ToHealth, int Absorbed)`.
`RegenHealed` and `Expired` stay. `TickStatuses`
(`FightSession.Riders.cs:457-605`) loops the rows instead of reading two fields
by name; `BeginStatusTickBeat` already takes a `StatusEffectType` and an amount
and already colours by `ElementOf`, so the beat layer needs no change — AUDIT
#188 says so and it is true (`FightSession.Riders.cs:513-554` re-read).

### D6. One status-application seam on the session

**Taken, and it is a correction rather than an addition.** `ApplySkillStatus`
(`FightSession.Skills.cs:1136-1147`) calls `StatusEffects.Apply` directly. For
Chilled that is wrong today: `ApplyChilled`
(`FightSession.SpeedBuffs.cs:374-379`) is the only path that registers the speed
malus, and its own header says so — *"every caller that wants Chilled to
actually slow anyone goes through here rather than calling
`StatusEffects.Apply` directly and risking a forgotten follow-up."* Nothing
authors `appliesStatus: Chilled` today, so the gap is latent. **Winter's Rebuke
is the first content that would hit it and would land an inert badge.**

The fix is the seam, not the caller: one `ApplyStatusTo(recipient, type,
magnitude, turns, source)` on `FightSession` that dispatches to whoever owns
that status's bookkeeping — Chilled to `ApplyChilled`, a DoT to `ApplyDot`,
Shielded already refused outright by `StatusEffects.Apply`'s throw
(`StatusEffects.cs:69-73`), everything else straight through. `ApplySkillStatus`,
the relic sites, the talent sites and the item-modifier sites all call it; no
production code calls `StatusEffects.Apply` for a side-effect-carrying status
any more. Same shape `ApplyWard`'s throw already enforces for `Shielded`, and
the same "swap the seam, not every caller" rule.

**Alternative rejected:** `if (type == Chilled) ApplyChilled(...)` inside
`ApplySkillStatus`. Cost: the next status with bookkeeping repeats the bug.

### D7. `PhysicalMove` is authored, defaults false, and a content lint makes it explicit for damage skills

**Taken.** One `bool physicalMove` on `RawSkillEntry` / `ResolvedSkill`,
independent of damage element (owner's rule). A plain attack is physical in code
and authors nothing — there is no skill row for it. A content-build lint refuses
any `DamageSingle`/`DamageAll` row that does not state `physicalMove`
explicitly, with a vacuity guard asserting the lint saw at least the count of
damage rows the catalogue holds (`docs/CODE_STANDARDS.md` §8). Non-damaging
rows default false and need no authoring.

**Alternative rejected:** derive it from `approach` (`close`/`lunge`/`charge`).
Cost: `boulder_slam`, `shear` and `battering_ram` author no approach and are
plainly physical; the derivation would be wrong on three of nine enemy abilities
on day one.

### D8. Court of Whispers uses Fear's one authored percent

**Taken.** `Fear.VulnerablePercent = 25` is a single authored constant whose own
header argues against per-caller values (`Fear.cs:11-19`): *"so two things that
both apply Fear cannot quietly disagree about how vulnerable it makes its
target."* The owner's brief says Court's Fear carries 20%. Numbers are tuning;
the constant wins. Court applies Fear at 25 (`prototype`).

**Alternative:** move the constant to 20. Consequence: World Ender's Crown's
Fear weakens by the same five points (`FightTuning.cs:185-186` is its duration;
the percent is Fear's). If the owner wants 20 specifically, that is the change —
one constant, one commit, both users move together. What is **not** on offer is
a per-caster Fear percent.

Fear's duration gains a sibling constant `Fear.DefaultTurns = 1` beside the
percent, which Court passes. That is the owner's "one skipped turn".

### D9. Numbers, scale and packets

`CombatMath.DamageScale` is **5** (`CombatMath.cs:61`), and since 2026-08-26
neither `ComputeAttackDamage` nor `ComputeSkillDamage` calls `Scale` at all
(`:309-312`, verified — the body is `Math.Max(1, ScaledAttack(...))` with no
scale term). `skills.json:2`'s `_readme` still says `x10` and still says defense
is subtracted inside the raw figure; both are stale, and that stale line is
where the brief's own "x10" came from (baseline §12 item 1, confirmed).

Every fixed packet in this plan is written on the final scale exactly as
`damageInstances` already are (`DamageInstance.cs:18-29`), and is multiplied at
resolution by the caster's tier multiplier, spell scaling and
`ElementalDamagePercent`, then floored at 1 per packet
(`FightSession.Skills.cs:971-1002`).

`Frost` is a skills.json-only alias for `DamageType.Ice`
(`SkillEntryResolver.cs:1059-1071`); there is no `Frost` enum member
(`DamageType.cs:8-28`). Winter's Rebuke authors `"Frost"` and resolves to `Ice`.

### D10. Five appended `SkillEffect` members, two appended `StatusEffectType` members

Both enums are stored as raw ints by the generated ScriptableObjects
(`SkillEffect.cs:88-96` and `:136-141` say so for `SkillEffect`/`SkillTargeting`;
`DamageType.cs:17-22` says so for `DamageType`). New members **append**, in this
order, and nothing is inserted or reordered:

- `SkillEffect`: `Afflict`, `Reclaim`, `Hasten`, `SwapAllies`, `Enthrall` —
  after `HealSingle`.
- `StatusEffectType`: `Burn`, `Thorned` — after `Feared`.

A member is a *resolution shape*, which is the existing convention — `Shatter`
has exactly one user and is a member because nothing else resolves the way it
does.

- `Afflict` — one enemy, this skill's authored status, no damage. **Three users
  on day one**: Velvet Shackles, Censer of Embers, Thorn Tithe.
- `Reclaim` — one enemy, consume the named status, deal its worth split across
  authored types. One user (Ashen Reckoning), and it is a member rather than a
  field on `DamageSingle` because its packets are *computed at resolution* from
  a consumed total; expressing it as a field would mean `ResolveDamageSingle`
  branching on that field ahead of `HasFixedDamage`, which is a flag deciding a
  resolution shape.
- `Hasten` — advance one other ally in the order. One user (Borrowed Moment).
- `SwapAllies` — two allies trade places. One user (Palace Passage).
- `Enthrall` — every enemy, branching on boss-ness, plus a cost to the caster.
  One user (Court of Whispers).

`ResolvedSkill` itself is serialized by NAME (`ResolvedSkill.cs:1-36`: a
`[Serializable]` class of public fields), so adding fields to it is safe; only
the enums are ordinal.

---

### The one owner question — ANSWERED 2026-09-20

**Asked:** apply the duration model change (D1) globally, or only to the thirteen
new spells? **Recommendation given:** globally.

**Answered:** *"Do the entire plan."* — **globally**, with the nine re-authored
rows in D1's table landing in the same commit as the model change, and Lucky
Deck's one row accepted as changed rather than deleted.

The reason the recommendation was made, kept here because it is also the reason
the answer is safe to build on: there is no smaller *model-consistent* option.
"New spells only" cannot be expressed as anything but a per-row flag saying which
meaning of "duration" this row uses, which is precisely what
`docs/CODE_STANDARDS.md` §10 forbids and what the baseline's §9 note flags as
needing a decision rather than an assumption. The scope of the global change is
nine authored numbers, one predicate rename (`IsSpentByTheTurn` →
`DurationClock`), and moving five status types from the turn-start tick to the
existing turn-end tick that wards already use.

Eight of the nine rows preserve today's effective behaviour exactly, pinned by
`StatusDurationMigrationTests`. Lucky Deck's one-turn chill gains one affected
turn and is pinned as **changed** in the same test, so the deviation cannot go
quiet.

---

## 1. Behavioural contracts

Each contract states its evaluation order, its empty case, and its RNG draw
policy where a roll exists.

### 1.1 Casting and payment order

**Order (unchanged, verified `FightSession.Skills.cs:45-218`).** Nothing is
spent until all six refusals pass:

1. Reach, `SingleEnemy` only — `:59`
2. Ally-target validity, `SingleAlly` only; a null target with live candidates
   is a refusal, never an auto-pick (AUDIT #147) — `:85`. Under 1.12 this step
   validates **every** pick the cast carries, not one; the existing single-pick
   case is a list of one and behaves identically.
3. Element choice — `:99`
4. `SkillResolution.CanAfford` (primary pool + signature pool) — `:110`
5. `CanResolveSkill` — `:121`; this is also where the **cooldown** is refused
   (`FightSession.Talents.cs:397-404`), ahead of every board-state question
6. The once-per-turn free-action lock — `:130`

Then payment, in this order: `ChargeSkillMana` `:149`, `BeginCooldown` `:154`,
signature spend `:155`, pool tier `:164-168`, `RefreshAttackBonus` `:173`, then
`ResolveCharacterSkill` `:175`.

**This plan adds two refusals to step 4 and two to step 5**, and adds nothing
elsewhere:

- step 4 gains the **health cost** (1.2), validated together with mana and
  signature in one call, so a cast that can pay one and not the other spends
  neither;
- step 5 gains `requiresStatus` (the target does not carry the named status —
  Ashen Reckoning) and `advanceSlots` with nowhere to go (the target is already
  at forecast position 1 — Borrowed Moment), both board-state refusals of
  exactly the shape "Shatter with no wards" already is.

**Empty case:** a refusal at any step spends no mana, no health, no signature,
no cooldown and no turn, and returns `false`. **RNG:** no refusal consumes a
draw. Cooldown starts at cast commitment, not at resolution
(`FightSession.Cooldowns.cs:51-66`), and a cast that resolves onto nothing still
spends it.

**Dodge is rolled after payment** and always has been
(`DamagePipeline.cs:234`, reached from deep inside resolution). Nothing here
changes that; every new spell that can miss has already paid in full.

### 1.2 Health cost

A health cost is **a payment, not damage**. It is validated with the other costs
and paid in the payment block, between `ChargeSkillMana` and `BeginCooldown`.

**Amount:** `ceil(maxHealth * healthCostPercent / 100)` — rounded **up**, the
owner's rule. **Validation:** the cast is refused unless
`CurrentHealth - amount >= 1`. **Payment:** `CurrentHealth -= amount`,
written directly, and reported to the ledger as a cost row, not a damage row.

It must not go through `CombatMath.ApplyDamage` (`CombatMath.cs:514-517`) and
must not go through `DealDamage` (`FightSession.Ledger.cs:51-69`). Verified
consequences of routing it through either:

- `ApplyDamageDetailed` (`CombatMath.cs:528+`) applies Last Stand's
  `CapSpikeDamage` and drains `SignaturePool.Absorb` first — a cost would be
  soaked by Wool;
- `ApplyAndCountDamage` (`FightSession.Ledger.cs:77+`) runs `NoteDamageForPools`,
  Phoenix Egg absorption and hatching, and Cursed Idol's bonus;
- `AfterDefences` (`DamagePipeline.cs:221-288`) runs wards, Vulnerable/Protect
  and variance;
- `DealDamage` settles a death with `KillCredit`.

None of those may fire. **Empty case:** `healthCostPercent` of 0 is no cost and
no ledger row. **RNG:** none.

### 1.3 Direct damage

Unchanged and reused whole. `DamagePipeline.AfterDefences`'s typed overload is
the one funnel (`DamagePipeline.cs:221-288`), in this order, verified this pass:

1. dodge, short-circuiting everything below — `:234`
2. effectiveness (weakness 1.5 / resistance 0.5, plus Jo-Sun's bonus on the
   weakness branch) — `:242`, `CombatMath.cs:328-329`
3. Protect/Vulnerable as one additive multiplier — inside `:245` via
   `CombatMath.ApplyStatusEffects`
4. `TotalDefense` = broad (Last Stand bonus → BreakShield zero → Sharp Horns
   percent → flat penetration, physical only) + typed resistance, then
   `AfterResistance` — `:245`, `CombatMath.cs:114-176`
5. poison-combo detonation, mutating, `null` on every preview — `:264`
6. variance (±20% default) — `:270`
7. Stalwart's flat physical reduction — `:275`
8. the ward pool, last, may legitimately return 0 — `:285`

Then `ApplyFinalDamage` → `DealDamage` → ledger and `SettleDeath`, and
`ApplySkillStatus` only if the target is still alive
(`FightSession.Skills.cs:717-721`).

**A dodged cast applies no status and no mark.** **A multi-packet spell rolls
dodge once for the whole cast** (`FightSession.Skills.cs:974`), passing
`dodgeAlreadyResolved: true` per packet. **RNG:** exactly two draws per landed
cast per target — one dodge, one variance per packet.

**One repair this plan must make.** `ResolveDamageInstances`
(`FightSession.Skills.cs:971-1002`) does **not** pass `ignoresDefense` into
`AfterDefences`; the scaled path does (`:685`). A fixed-packet spell's
`ignoresDefense` is therefore inert today. No live content is affected
(`battering_ram` and `overhead_slam` both set it and both take the scaled path),
but Blackglass Spear depends on it. The fix is one argument, and the owner's
brief already names the work ("Connect defense bypass to fixed-packet
resolution").

### 1.4 Duration model

Per D1. In operational terms:

- **`AtTick`**: the tick and the decrement happen in the same pass, at the
  holder's turn start, and an entry reaching 0 is removed in that same pass. `N`
  authored = `N` ticks dealt. Poison, Regen, Burn, Thorned all sit here.

**Every clock is per INSTANCE, not per type** (D3). Three Poison instances tick
and decrement independently in one pass, and the one that runs out is removed
while the other two stand. An expiry is reported once per instance removed, and
the log deduplicates so a player is not told the same status wore off twice in
one breath.
- **`AtUse`**: no clock. The counter moves at the moment the effect is spent —
  `ConsumeStun` for Stun and Feared (`StatusEffects.cs:221-232`),
  `ConsumeProvoke` for Provoked, `ConsumeEmpowerment` for Empowered.
- **`AtTurnEnd`**: the counter moves at the END of the bearer's turn, and a
  status applied *during* the bearer's own turn is exempt from that turn's end
  tick. `N` authored = `N` of the bearer's turns fully covered, restriction
  intact through the final affected action.

**Evaluation order at a turn boundary:** turn start runs `TickStatuses` then
`TickCooldowns` (`FightSession.Riders.cs:302-303`); turn end runs the
`AtTurnEnd` sweep, skipped when the same actor is about to act again on an extra
action (`FightSession.Riders.cs:110-114`, AUDIT #113 — an extra action is the
same turn and re-pays nothing).

**Expiries are reported by whichever clock removed the entry, and the
side-effect teardown follows the clock.** `TickWardsAtTurnEnd`
(`StatusEffects.cs:622`) today returns a count; the generalised turn-end sweep
returns the same `Expired` list `Tick` already returns
(`StatusEffects.cs:868-871`), and the caller does with it what
`TickStatuses` already does at turn start. That matters for exactly one
teardown today: Chilled's speed malus is revoked when the status is reported
expired (`FightSession.Riders.cs:501-504`), and under D1 that report now arrives
from the turn-END sweep instead of from `Tick`. The revoke moves with it.

**And under D3 the teardown is a RECOMPUTE, not a revoke.** Chilled stacks, so
one instance expiring out of three must leave the other two slowing the bearer.
`RefreshChilledSpeed` already revokes and re-grants from scratch against the true
base — the shape its own header argues for — so the turn-end caller calls *that*
rather than `RevokeSpeedBuff`, on any Chilled expiry. When the last instance
goes, the re-grant finds nothing and grants nothing, which is the revoke. One
call covers both cases and there is no "was that the last one?" question for
anybody to get wrong. Any future status with a teardown hangs off the same list,
at whichever end of the turn its clock sits.

**Empty case:** a bearer with no statuses ticks nothing and reports an empty
`TickReport` and an empty expiry list at both ends. **RNG:** none.

### 1.5 New-DoT model

Applies to Burn and Thorned. Existing Poison is **preserved exactly** and is not
routed through anything new except the shared seam (D6), which for Poison
dispatches to today's `StatusEffects.Apply`.

- **Snapshot.** At application, the caster's potency is folded in *once* —
  `intensityStep * SkillPowerMultiplierFor(caster) * SpellScalingMultiplierFor(caster)`,
  rounded away from zero, floored at 1, the same arithmetic
  `ResolveDamageInstances` uses for a packet
  (`FightSession.Skills.cs:980-984`). That figure is stored as `Magnitude` and
  is the tick strength for the life of the status. The caster is never consulted
  again.
- **Tick.** At the holder's turn start, the tick deals its stored `Magnitude`
  through `CombatMath.ApplyDamage`, **after** applying elemental affinity for
  `ElementOf(type)` against the holder's own affinity, and **without** any
  defense term. Flat defense is not subtracted per tick; resistance is not
  subtracted per tick; no ward, no Protect/Vulnerable, no variance. This is
  `MitigationOf(type) == AffinityOnly`.
- **Attribution.** `ActiveStatus.Source` holds the applier and outlives the
  applier's death — the reference is to a `CombatantState`, which persists.
  Nothing on the tick path may gate on `Source.IsAlive`. Kill credit for a tick
  death remains `KillCredit.Nobody` (`FightSession.Riders.cs:554-565`), the
  same answer Poison already gives and for the same recorded reason.
- **Re-application.** Per D3: a second cast adds a second instance. Nothing
  merges, nothing is compared, no `max` is taken. The two instances tick
  together and expire apart, and each keeps the `Source` that paid for it.
- **Expiry.** `AtTick` family: the final tick and the removal are simultaneous.
  The log says the status wore off after it said what the tick did
  (`FightSession.Riders.cs:602-605`).
- **Reporting.** Per D5, one `TickRow` per damaging status per tick, carrying
  the status, the element, what reached health and what a signature pool ate.
  `RecordUnattributedDamage` is called once per row.

**Empty case:** a DoT whose snapshot floors to 1 still ticks for 1; a DoT on a
dead holder never ticks, because `TickStatuses` runs at a turn start the dead do
not get. **RNG:** none — a DoT tick draws nothing, which is why it is safe on a
preview path.

### 1.6 Detonation

One operation. Inputs: the target, the incoming damage type, and a premium
percent (default 100).

1. Refuse unless the incoming type is `Nature` or `Poison`
   (`StatusCombos.cs:45-48`).
2. Find **every** Poison entry (D3 — they stack). **None → return 0**, the
   empty case.
3. `worth = sum over instances of (Magnitude * TurnsRemaining)`, then `premium`
   applied **once to the sum**: `Rounding.AwayFromZero(worth * percent / 100)`.
   Applying the premium per instance and adding up would round `k` times and
   quietly pay a different figure for the same board.
4. **Remove every entry before dealing anything** (`StatusCombos.cs:57`). This is
   the recursion guard and it already exists, widened from one entry to all of
   them: the list is clear before `ResolveDetonation` calls `DealDamage`, so a
   Poison-typed detonation packet that re-enters `AfterDefences` finds nothing to
   detonate — and finds nothing whether the target carried one instance or five,
   which is the case a one-entry removal would have got wrong. `StatusCombosTests
   .DetonatingTwiceInARow_TheSecondCallDoesNothing` pins the old half;
   `.DetonatingThreeStacks_ConsumesAllOfThem_AndLeavesNoneTicking` pins the new.
5. Deal it through `DealDamage`, typed `Poison` regardless of the trigger's own
   type, credited `Attacker` if the attacker is player-side and `Nobody`
   otherwise (`FightSession.cs:909-918`, AUDIT #63).

**No caster scaling is applied at detonation** — the figure is the summed
`Magnitude * TurnsRemaining` and a percent, nothing else. The snapshot was taken
when the Poison was applied; applying spell scaling again here would scale it
twice.

**Recorded once.** Ashen Reckoning splits the consumed figure across several
types (1.7); the split is a division of one already-consumed total, resolved
inside the one operation, and the detonation is recorded once no matter how many
packets carry it.

**Viper's Bite's ordering is preserved and is already correct**: the detonation
runs inside `AfterDefences`, which `ResolveDamageSingle` reaches before
`ApplyFinalDamage`; `ApplySkillStatus` — which applies the spell's own fresh
Poison — runs afterwards and only on a live target
(`FightSession.Skills.cs:682-721`). Every old entry is removed before the fresh
one lands, so the cast cannot re-detonate what it just applied. Under D3 the
fresh Poison is a new instance rather than a merge, which is the same observable
outcome and one less rule: **one detonation, one record, then exactly one Poison
instance on a target that carried nothing else.**

**Preview purity falls out of construction**: a preview passes
`resolveDetonation: null`, and `null` means "no combo", never "combo with
nothing to report" (`DamagePipeline.cs:264` and its header).

### 1.7 Split packets from a consumed total

Ashen Reckoning is the only user today. Given a consumed total `T` and an
ordered list of `k` damage types:

- each type takes `floor(T / k)`;
- the remainder `T mod k` goes to the **first** type in the list, one point at a
  time, which is how "odd point to Poison" is expressed with Poison listed
  first;
- each resulting packet resolves through `AfterDefences` independently, so each
  checks the target's affinity on its own, exactly as `damageInstances` already
  do (`FightSession.Skills.cs:983-1000`);
- **but the consumption happened once, before the split**, so no packet can
  detonate and none re-enters `SpendPoisonIfMatched`. Each split packet is
  resolved with `resolveDetonation: null`.
- the whole split is **one** death settlement per target and **one** detonation
  record.

**Empty case:** `T` of 0 produces no packets and no record.

### 1.8 Status inspect and consume

One facility, replacing two near-duplicates.
`StatusEffects.TrySpend(statuses, type, out ActiveStatus spent)` removes the
first entry of that type and reports whether there was one — the shape
`Marks.ConsumeMark` already has (`Marks.cs:43-52`), generalised so the caller
can also read the spent entry's `Magnitude`/`TurnsRemaining`.

- `Marks.ConsumeMark` becomes a one-line call to it and keeps its name and its
  contract (callers key off the return value, never off `IsMarked` then
  `ConsumeMark` — `Marks.cs:39-42`).
- `StatusCombos.SpendPoisonIfMatched` becomes the type-match guard plus one call
  to it plus the arithmetic.
- Crownfall and Ashen Reckoning are the two new users.

**Marked is preserved exactly.** Crownfall reads and consumes the general
`Marked` status through `Marks` (`Marks.cs:34-52`), never `FightSession`'s
private `_marked` set (`FightSession.Relics.cs:229`), which is the Drowned
Lantern's own independent mechanic. The two are different queries and a target
may carry both, neither or either (baseline §6, verified against `Marks.cs` and
the Lantern's site).

**Dodge preserves the mark.** Consumption happens after the hit has landed, on
the same side of the miss check that `RelicsAfterSwing` already sits on — a
dodged Crownfall consumes nothing and deals nothing
(`FightSession.Skills.cs:655-660`, `:693-698`).

**Empty case:** no entry → `false`, no bonus, ordinary resolution. **RNG:** none;
consumption is deterministic and never rolls.

### 1.9 Initiative displacement

**Definitions.**

- *Forecast* = `TurnOrder.Project(k)` (`TurnOrder.cs:444-459`), a simulation on
  a throwaway `Snapshot` (`:495-508`), **not** list order.
- *Forecast position* = the index in that list. **Index 0 is the current actor**,
  mid-action (`SimulateForward` adds `simCurrent` first, `:520-524`). Index 1 is
  the next action that will happen. An actor may legitimately appear more than
  once in one window (`:511-518`, and
  `UpcomingTurnsTests.WrapsIntoFollowingRoundsWhenAskedForMoreTurnsThanCombatants`
  already pins `["Hero","Rat","Hero","Rat","Hero"]`); an actor's position is its
  **first** appearance.
- *Charge level* = a distinct `Charge` value among the entries. One displacement
  slot moves past one charge LEVEL, so entries tied at the same charge move as a
  block. This is already true of `ApplyPushBack` (`:303-323`) and is what makes
  "equally displaced targets keep their relative order" hold without a
  tie-break of its own.

**Operations.**

- **Delay** reuses `PushBack` / `ApplyPushBack` unchanged (`:286-323`). Each slot
  drops the entry to `(greatest charge strictly below) - 1`, or takes a full
  `TurnThreshold` off when nothing is below.
- **Advance** is the exact mirror, added as `PullForward(actor, slots)` /
  `ApplyPullForward(entries, entry, slots)`. Each slot raises the entry to
  `(least charge strictly above) + 1`, or leaves it where it is when nothing is
  above — that is the clip. `PullToFront` (`:345-375`, Gift: Haste) is left
  alone: it is the "act next" special case and has its own talent and its own
  tests.
- **Forecast preview** gains `ProjectPulled`, the mirror of `ProjectPushed`
  (`:470-491`), running `ApplyPullForward` on the same throwaway snapshot so the
  initiative tracker can show where a Borrowed Moment would land before it is
  committed. `ApplyPullForward` therefore has two callers on day one, the same
  as `ApplyPushBack`.

**Rules.**

1. **One forecast.** Destinations for every target of one cast are fixed from a
   single forecast, taken after damage and deaths have resolved and before any
   displacement is applied. No target's destination is computed against a board
   another target's displacement has already changed.
2. **The current action is never moved.** Index 0 is not a valid destination and
   the current actor is never a legal target of either operation.
3. **No extra action, no `Rate` change.** Neither operation touches
   `_extraTurns` (`:325`) or calls `SetSpeed` (`:58-65`). An advance buys a
   position, never a turn; `PullToFront`'s own header records why crossing the
   threshold outright would buy "a turn and a half".
4. **Clipping.** An advance clips at position 1. A delay clips at the end of the
   window, where `ApplyPushBack`'s "nobody below" branch takes a full turn's
   charge off.
5. **Refusal, not a wasted cast.** A single-target advance whose target is
   already at position 1 is refused in `CanResolveSkill` and spends nothing.
6. **Batch after deaths.** A multi-target delay resolves all damage and all
   deaths first, then takes the forecast, then applies the displacements in one
   pass over survivors only, in the forecast's own order. A dead entry is never
   referenced; `PushBack` returns `false` for an actor not in the order
   (`:288-292`) and that `false` is not an error.
7. **No duplication or loss.** Neither operation adds or removes an entry; both
   only write `Charge` on an entry already in `_entries`.
8. **Stated deviation.** On a board where no entry is removed, N slots moves the
   target exactly N forecast positions later/earlier, and that is the pinnable
   contract. After deaths the guarantee is rules 1 and 6 — the displacement is
   applied — and the target may end at the *same* index, because the entry it
   would have fallen behind is gone. This is stated rather than hidden; it is
   not a bug and no test asserts an index that a death has invalidated.

**Empty case:** an empty survivor list after a multi-kill applies nothing.
**RNG:** none. Neither operation draws, and neither does `Project`.

**Worked examples.** `Rate = clamp(sqrt(Speed / 10), 0.35, 2.5)`
(`SpeedScale.cs:34-61`), `TurnThreshold = 100` (`TurnOrder.cs:29`), tie broken by
the higher `Initiative` and then by insertion order (`:392-397`).

*(a) Equal-speed actors.* A (current, charge 12), B (80, initiative 14),
C (80, initiative 9), D (55); all Speed 10, rate 1.0.
Forecast: A is index 0. After 20 ticks B = 100, C = 100, D = 75; the tie goes to
B on initiative, then C with no further ticks, then D at tick 45. Forecast
`[A, B, C, D]`. Borrowed Moment advances D by 2: D's position is 3, target is 1.
Slot 1 raises D above the 80 level → 81, which jumps B and C together because
they share a level. Slot 2 finds nothing above 81 and clips. New forecast:
D needs 19 ticks, B and C need 20 → `[A, D, B, C]`. D moved two positions with
one slot, because a slot moves past a level.

*(b) A fast actor appearing twice.* A Speed 40 (rate 2.0, charge 60), B Speed 10
(current, charge 5), C Speed 10 (charge 40).
Forecast: B index 0; A at tick 20; C at tick 60; A again at tick 70 →
`[B, A, C, A]`. Borrowed Moment on A: A's first appearance is index 1, already
the earliest non-current position. **Refused before payment** (rule 5).

*(c) An actor already next.* A current, B at 95, C at 40, all rate 1.0.
Forecast `[A, B, C]`. Borrowed Moment on B → position 1 → refused. Borrowed
Moment on C by 2 → target index `max(1, 2 - 2) = 1`; slot 1 raises C above 95 →
96; slot 2 finds nothing above → clip. Forecast `[A, C, B]`.

*(d) Multi-target delay with a death.* Caster A (current, 10, rate 1.0); E1
Speed 14 (rate 1.183, charge 88); E2 Speed 9 (0.949, charge 88); E3 Speed 5
(0.707, charge 30). Gale Scythe lands on all three; E2 dies.
Damage and deaths resolve first; E2 is removed from the order. The forecast is
then taken once: `[A, E1, E3]`. Destinations: E1 1 → 2, E3 2 → 3 (past the
window, clipped). Applied in forecast order: `ApplyPushBack(E1, 1)` → greatest
charge strictly below 88 is E3's 30 → E1 = 29; `ApplyPushBack(E3, 1)` → greatest
below 30 is A's 10 → E3 = 9. Both were displaced by one level, in order, with no
reference to the dead entry. E1's resulting index is 1 again, because A's own
next turn is what now sits between — rule 8, stated.

### 1.10 `PhysicalMove` and the shared legality check

**One classification** (`bool PhysicalMove` on `ResolvedSkill`), **one
predicate** (`CombatActions.IsLegalFor(actor, action)`), read by four callers
that must agree: the player's verb/skill menu, the enemy's
`EffectivePoolFor` draw, `BuildIntent`'s committed telegraph, and
`ResolveSkippedTurn`'s resolution-time recheck.

The predicate's rule today is one line and it is already in the right place:
`EffectivePoolFor` (`FightSession.Enemies.cs:122-171`) zero-weights an ability
when `rooted && ability.IsPlainSwing` (`:170`, `EnemyAbility.cs:75`). This plan
changes that condition from "is the plain swing" to "is a physical move", and
the plain swing is a physical move by definition, so the existing behaviour is a
strict subset of the new one.

**Precedence.** A Rooted actor may not take a physical move. That is checked
after "is this action authored at all" and before "does it have a legal target",
so an actor whose only remaining option is a physical move it cannot take reads
as *no legal action*, not as *no target*.

**The no-legal-action fallback is preserved.** `RootedEnemyHasNoLegalAction`
(`:266-271`) recomputes the effective pool and asks `EnemyAbilityDraw.Pick(pool,
0f) < 0` — a query that spends no draw from the seeded stream, by passing a
literal `0f` rather than `_rng.NextFloat()`. Both `BuildIntent` (`:321`) and
`ResolveSkippedTurn` (`:614-631`) call it, so telegraph and resolution cannot
disagree. Turn forfeiture stays the answer; no Guard mechanic is introduced.

**Committed intents re-validate.** An intent is already re-checked at resolution
against "still alive" and "still reachable by the committed ability's own reach
mask" (`:710-732`), with a re-pick that draws no RNG and a forfeit when nothing
is in reach. Rooted arriving after intent selection is covered by
`ResolveSkippedTurn` asking `RootedEnemyHasNoLegalAction` fresh rather than
trusting the telegraph (`:254-262` says so in writing). This plan adds nothing to
that path; it only widens what the recomputed pool excludes.

**Player side.** Today Rooted forbids only `Move` for a player
(`FightSession.cs:466-471`, `:475-516`), and nothing in `CastSkill` or
`ResolveAttackSwing` checks it. Under Velvet Shackles a rooted *player* must
also be refused a physical move — so `CastSkill` gains the check at step 5
(board state), the basic attack verb gains it, and the menu greys the rows the
same predicate refuses.

**The audit.** Every selectable action in the catalogue, classified. Player-side
rows are Shawn's and Odette's and Bjorn's kits; enemy rows are the nine authored
abilities plus the plain swing every enemy has.

| id | owner | effect | `physicalMove` | why |
|---|---|---|---|---|
| *(plain attack)* | every combatant | — | **true** | a swing; classified in code, no row |
| `shear` | sheep | DamageSingle | **true** | melee, `meleeReach: true` |
| `battering_ram` | sheep | DamageSingle | **true** | a charge; `scalingAxis: Weapon` |
| `headbutt` | sheep (talent) | DamageSingle | **true** | a body blow; `scalingAxis: Weapon` |
| `woolgathering` | sheep | HealSelf | false | no move |
| `mud_burst` | book | DamageSingle | false | cast |
| `frost_flare` | book | DamageSingle | false | cast |
| `cinderfault` | book | DamageAll | false | cast |
| `lightning_bolt` | book | DamageSingle | false | cast |
| `provoke` | sheep | Provoke | false | a shout |
| `black_ram_mode` | sheep | Transform | false | a transformation, not a move |
| `fleece_ward` | sheep | Ward | false | — |
| `tuck_in` | sheep | Ward | false | — |
| `shatter` | sheep | Shatter | false | detonates wards |
| `wail` | sheep | BuffParty | false | — |
| `gift_mana` / `gift_fury` / `gift_haste` | sheep | Gift* | false | — |
| `placeholder_shawn_capstone` | sheep | — | false | `playerSelectable: false`, `placeholder: true` |
| `prismatic_orb` | owl | DamageSingle | false | cast |
| `mend` | owl | HealSingle | false | — |
| `prism_ward` | owl | Ward | false | — |
| `placeholder_brawler_slam` | bear | DamageSingle | **true** | `approach: close`, `meleeReach: true` |
| `rampage` | bear | DamageAll | **true** | `approach: close` |
| `placeholder_brawler_provoke` | bear | Provoke | false | — |
| `placeholder_brawler_ward` | bear | Ward | false | — |
| `bear_bulwark` | bear | Ward | false | — |
| `second_wind` | bear | HealSelf | false | — |
| `boulder_slam` | golem | DamageSingle | **true** | a thrown boulder is a physical action |
| `overhead_slam` | forest_warden | DamageSingle | **true** | `approach: close` |
| `grapple` | forest_warden | DamageSingle | **true** | `approach: lunge`; applies Stun |
| `roar` | forest_warden | Summon | false | a call |
| `barrel_roll` | beetle | DamageSingle | **true** | `approach: charge` |
| `shell_up` | beetle | HealSelf | false | — |
| `trunk_slam` | treant | DamageSingle | **true** | `approach: lunge` |
| `spore_cloud` | treant | DamageAll | false | a cloud, not a move |
| `bog_mud_burst` | bog_witch | DamageSingle | false | cast; `reachSlots: [2,3]` |

Plus the thirteen new rows: all thirteen are casts and all author
`physicalMove: false`.

Two calls worth naming because they are judgement, not derivation:
`boulder_slam` is physical although the golem authors no `approach`, and
`spore_cloud` is not physical although the treant's other ability is. Both match
the owner's rule that classification is independent of damage element and is
about whether the actor *moves to act*.

**Empty case:** an actor with no legal action forfeits its turn. **RNG:** the
legality query spends no draw, by construction.

### 1.11 Post-action hook

**CORRECTED during milestone E.** This section originally put the hook at the
top of `AdvanceAfterAction`, on the claim that it is "the one place every
action funnels through". That claim is false and was already known to be
false by the time this section was written: milestone D's own fix
(`FightSession.EndTurnStatusesForCurrent`, landed in `2c64a252`, recorded as
AUDIT #193) exists precisely because `AdvanceAfterAction` is reached ONLY by
the four PLAYER commands — `CastSkill` `:186`/`:190`, `Move`
`FightSession.cs:515`, `UseItem`, and the attack path. A monster's turn and
every skipped turn on either side advance through
`FightSession.Enemies.StepToNextTurn` instead, and a sealed egg's through
`AutoResolveEggTurns`. Thorn Tithe is cast on ENEMIES, so a hook that only
ever fired from `AdvanceAfterAction` would never retaliate at all — the
identical hole #193 closed for the turn-end clock, one milestone later, for a
mid-turn hook instead of a turn-boundary one.

**Where it actually lives: two call sites, one per side, not one shared
seam.** "Completed action" cannot be recognised at a single point the way
`EndTurnStatusesForCurrent` recognises "turn is ending" (that fix could sit
immediately before all three `_encounter.AdvanceTurn()` calls because ending a
turn is the same event on both sides; completing a PHYSICAL action is not,
because the two sides resolve an action through entirely different methods
with different early-return shapes). The hook is `FightSession.Riders
.TriggerPhysicalMoveRetaliation(CombatantState actor)`, a private method with
no re-entrant path back into itself, called from:

- **The player side**: `AdvanceAfterAction(bool physicalMove)`, at its very
  top — still the correct seam for the four player commands, since a refusal
  returns before reaching it (1.1) and a free action never reaches it either
  (`FightSession.Skills.cs:195-201`). Each of the four callers passes its own
  known classification: `ExecuteAttack` always passes `true`
  (`CombatActions.PlainAttackIsPhysicalMove`), `Move` always passes `false`
  (it is a formation swap, not the `physicalMove` skill classification),
  `CastSkillOnPicks` passes `CombatActions.IsPhysicalMove(skill)`, and
  `UseItem` always passes `false`.
- **The monster side**: `FightSession.Enemies.AutoResolveEnemyTurns`, called
  right after `ResolveEnemyAction(current)` returns and before
  `StepToNextTurn()` advances the clock. `ResolveEnemyAction` was widened from
  `void` to `bool`, reporting exactly 1.11's own condition — every early
  return (a forfeit, a re-pick that found nobody, a commitment
  `CombatActions.IsLegalFor` now refuses) reports `false`; the "real skill"
  branch reports the skill's own `IsPhysicalMove`; every path that reaches the
  legacy scaled-attack section (a plain swing, or a pre-Afflict legacy
  `skillPower` ability) reports `true` unconditionally, hit or miss alike,
  because `EnemyAbility.IsPhysicalMove` answers `PlainAttackIsPhysicalMove`
  for anything that is not a real, authored skill.

**What counts as a completed physical move**: the actor took an action, that
action was classified `PhysicalMove`, and it resolved. **A miss counts** — the
owner's rule, and the natural reading: the actor moved. **An interrupted or
rejected action does not**: a refusal returns before either calling seam is
reached (1.1), and a forfeited turn calls `ForfeitTurn`, not the action paths,
on both sides.

**No recursion.** The retaliation deals damage; it is not an action and never
sets the "an action completed" condition. `TriggerPhysicalMoveRetaliation`
opens no beat of its own action, spends no resource, starts no cooldown and
calls nothing that reaches `AdvanceAfterAction`, `CastSkill`, `ExecuteAttack`
or `Move` — the guard is structural (nothing wires it back in), not a
re-entrancy flag. A retaliation that kills settles the death through
`SettleDeath` like any other (`FightSession.Ledger.cs:319`), guarded so one
tick's several damaging rows cannot double-settle the same death (D5's own
consequence: a tick can now carry more than one row, and only the first row
that observes the target newly dead may call `SettleDeath`).

**A free action does not reach `AdvanceAfterAction`** (`FightSession.Skills.cs:
195-201`), so a free-action cast triggers no retaliation. Palace Passage is a
free action and is not a physical move, so this is consistent both ways.

**Thorned's own second wrinkle**, not present in the plan's first draft:
Thorned sits on the `AtTick` clock like every other DoT (1.4), so its FINAL
instance is removed inside `StatusEffects.Tick` at the very turn it fires its
last tick — at that turn's own START, before the actor has even acted. 2.12
still promises a retaliation on that turn's action. `FightSession.Riders
.TickStatuses` snapshots the live Thorned instances (the `ActiveStatus`
objects themselves, which outlive their removal from the list) before calling
`StatusEffects.Tick`, and `TriggerPhysicalMoveRetaliation` reads that snapshot
back later in the same turn — the one piece of state that lets "the tick
removed it" and "this turn still owes a retaliation" both be true without
giving Thorned a clock of its own.

**Empty case:** an actor carrying no retaliation curse runs nothing. **RNG:**
none.

### 1.12 Two-ally swap

Palace Passage extends the existing `Move` (`FightSession.cs:475-516`) from
"the current actor trades with the nearest living ally in one direction" to
"any two living allies trade places", as the caster's one free action.

**Reused whole:** `_encounter.SwapPartySlots(a, b)`, the `NoteDeliberateMove`
pair fired for both figures with the acting character named
(`FightSession.RelicMechanics.cs:359-367`), the enemy-intent re-validation that
already follows an actor after a move (`FightSession.Enemies.cs:710-732`), and
the free-action lock (`FightSession.Skills.cs:130-135`, `:195-201`).

**Rooted refusal.** Rooted means "cannot change field position", and the swap
changes two, so **either** ally being Rooted refuses the cast — the identical
rule `CanMove`/`Move` already read off both sides. It is a `CanResolveSkill`
refusal, so it spends nothing, and the second ally's Rooted state is checked at
commit as well as at pick time (a pick and a commit are different moments).

**Selection.** `MenuDepth.Target` (`FightMenuState.cs:6-22`) gains a required
pick COUNT rather than a second depth. The existing single-pick case is count 1;
Palace Passage is count 2. `Back()` pops one pick at a time before leaving
Target depth, which is the gamepad rule already decided (Cancel steps back one
level first) and is what "atomic cancellation" means here.

**How two picks reach the session.** `CastSkill` gains one overload taking
`IReadOnlyList<CombatantState> targets`; the existing
`CastSkill(ResolvedSkill, CombatantState, DamageType?)`
(`FightSession.Skills.cs:45`) forwards to it as a one-element list and is
otherwise untouched, so no existing caller changes and there is still exactly
one dispatcher. `FightMenuState` owns the picks until the last one is submitted
and hands the whole list over at commit — the session holds no pending-pick
state between calls, which is what makes cancellation atomic by construction
rather than by cleanup. Two arities, one path: every existing skill at count 1,
Palace Passage at count 2.

**State table.** Rows are states; `Submit` is A / click / Enter; `Cancel` is
B / right-click / Escape. Pointer, keyboard and gamepad differ only in what
produces Submit and Cancel — none of them has a path the others lack.

| State | Submit on a legal ally | Submit on an illegal ally | Cancel |
|---|---|---|---|
| `Sub`, Palace Passage row selected | → `Target`, side `Allies`, picks 0/2 | — | → `Root`, nothing spent |
| `Target`, picks 0/2 | record pick 1, stay in `Target`, picks 1/2 | refusal line, stay, picks 0/2 | → `Sub`, picks cleared, nothing spent |
| `Target`, picks 1/2 | record pick 2 → commit the cast | refusal line, stay, picks 1/2 | drop pick 1 → picks 0/2, stay in `Target` |
| committed | — | — | — |

"Illegal ally" is: dead, not on the caster's side, or already picked. A Rooted
ally is **shown** (static layout) and **refused on Submit** with the reason
(runtime eligibility) — the same split the ally rack already uses, and the same
split `AllyTargeting.Accepts` (`AllyTargeting.cs:32-53`) draws between "which
effect accepts whom" and "who is alive and on my side".

**Atomic cancellation:** nothing is written until pick 2 is submitted. No mana,
no cooldown, no free-action lock, no swap. A cancel at picks 1/2 leaves the
board byte-for-byte as it was.

**After the swap:** formation legality is recomputed by the existing rules; an
actor-targeted enemy intent follows the actor, not the slot, and is re-validated
against the committed ability's reach mask at resolution
(`FightSession.Enemies.cs:710-732`) — a re-pick there draws no RNG, so a swap
cannot change the seeded stream.

**Empty case:** a party of one has no legal second pick; the cast is refused in
`CanResolveSkill`. **RNG:** none.

### 1.13 Preview purity

A preview mutates nothing: no status, no resource, no initiative entry, no RNG
draw, no cast counter, no mark.

This is enforced by construction today and stays so. `PreviewSkillPower`
(`FightSession.Skills.cs:1036-1075`) takes no `SeededRandom`, never calls
`AfterDefences`, and restores `BonusAttackPercent` in a `finally`. Every
telegraph path passes `rng: null`, `resolveWard: null` and
`resolveDetonation: null`, and each of those nulls is the documented "cannot
spend" convention (`DamagePipeline.cs:155-157`, `:256-263`).

**Every new preview obeys the same three rules**: pass `null` for anything that
mutates; take no `SeededRandom`; and, for the two new consume-based spells,
answer "would this consume a mark / a Poison?" with `Marks.IsMarked` and a
`FirstOrDefault` read — never with `TrySpend`. `ProjectPulled` runs on
`Snapshot()` (`TurnOrder.cs:495-508`), which is a fresh copy, so an initiative
preview cannot touch the real schedule.

**One existing divergence, found this pass and not in the baseline.**
`PreviewSkillPower`'s fixed-packet branch (`:1043-1051`) multiplies by tier and
spell scaling but **omits** `ElementalDamagePercent`, which
`ResolveDamageInstances` applies (`:981`). Preview and resolution therefore
already disagree for a caster carrying an elemental rider. That is a live bug,
not a new one, and it is the reason milestone A carries the preview-parity pin
rather than leaving it to the release gate.

**Empty case:** a preview of a skill with no damage returns 0 and says so.
**RNG:** none, ever.

### 1.14 Damage recording

Every new damage source reports absorption, health loss, death and attribution
through the established flow, and none invents its own:

- an attributed hit → `ApplyFinalDamage` → `DealDamage(actor, target, amount,
  type, credit)` → `ApplyAndCountDamage` (pools, Phoenix Egg, Cursed Idol,
  `Dealt`/`Took` rows) → `SettleDeath` (`FightSession.Ledger.cs:51-69`, `:319`);
- an unattributed tick → `RecordUnattributedDamage(target, toHealth, absorbed)`
  with the two figures kept apart, then `SettleDeath(actor: null, target,
  KillCredit.Nobody)` written out rather than omitted
  (`FightSession.Riders.cs:554-565`);
- a health **cost** → no damage row at all (1.2);
- a detonation → one `DealDamage` call, typed Poison, credited by side.

`KillCredit` is named at every call and never defaulted (`KillCredit.cs:17-25`).

### 1.15 Book eligibility and persistence

Unchanged, and all thirteen rows ride it as content.

- A book is available to a character through `run.learnedSpells`, checked by
  `ContentDatabase.AvailableSkillsFor`'s `LearnedThisRun` predicate
  (`ContentDatabase.cs:379-381`), which requires `s.Data.BookOnly &&
  canHoldBooks && run.learnedSpells` names this character and this skill.
- **Fury characters cannot learn books**, enforced through the POOL:
  `SpellBooks.CanHold(primary)` (`SpellBooks.cs:34`, null reads as yes), fed
  into five gates that must agree (`SpellBooks.cs:14-33`).
- **Three slots** per character per run (`SpellBooks.cs:12`).
- **Save/reload** reconciles through `ReconcileLearnedSpells`
  (`SaveData.cs:1047-1069`): an entry is dropped when the skill no longer exists
  or has `BookTier <= 0` (`:1071-1074`), when the owner no longer exists, or
  when the owner's pool stopped reading books — and in that last case the book
  goes back to `unassignedSpellBooks` rather than into the bin.
- `bookTier` gates the shop and `bookOnly` gates the level ladder; they agree
  for all four live books by coincidence and nothing enforces it
  (`SaveData.cs:1019-1029` says so). **All thirteen new rows author both**, with
  `bookTier` equal to the owner's tier and `bookOnly: true`, and no
  `unlockLevel` — which is what `skills.json:2`'s own readme requires of a book.
- `characterId` on a book records who it was drafted for and nothing else. The
  thirteen author `"sheep"`, as all four existing books do.

**Empty case:** a character with no learned spells has three empty slots.
**RNG:** none.

---

## 2. The thirteen spells

Every entry carries: targeting, costs, cooldown, tier, scaling, effect order,
expiry, failure behaviour, presentation, content row. Numbers marked
`prototype` are the owner's and are tuning.

### 2.1 Gilded Aegis — `gilded_aegis`

- **Targeting** one ally, the caster included (`SingleAlly`;
  `AllyTargeting.Accepts` takes the `default: return true` arm for `Ward`,
  `AllyTargeting.cs:45-52`).
- **Costs** 7 mana `prototype`. No health cost.
- **Cooldown** 2 `prototype`. **Tier** 1 `prototype`.
- **Scaling** `scalingAxis: Spell`. Ward points are
  `AuthoredAttackTerm(Spell) + flatAmount`, floored at 1, then multiplied by any
  `WardReductionPercent` talent (`SkillResolution.cs:108-141`). Wisdom enters
  through `SkillScaling.MultiplierFor`, not as a separate term. Opening
  `flatAmount: 18` `prototype`, measured against the existing family:
  `fleece_ward` is a flat 50, `prism_ward` is 24 at spell-Attack 4 and 110 at
  spell-Attack 90, `placeholder_brawler_ward` is 20% of max health (baseline §3,
  pinned by `PhaseFourSkillTests`). 18 + spell attack puts a fresh caster near
  22 and a late one near 108, i.e. deliberately just under `prism_ward`, which
  costs the same 8 mana and has no cooldown.
- **Effect order** (1) pay; (2) `ApplyWard` on the chosen ally for
  `wardTurns: 2` `prototype`; (3) log.
- **Expiry** two of the **wearer's** turns, aged at the wearer's turn END and
  exempting the turn it went up (`StatusEffects.cs:622`,
  `FightSession.Talents.cs:129-134`). Wards stack as separate entries and drain
  soonest-expiring first (`StatusEffects.cs:366-377`).
- **Failure** no legal ally → refused in `CanResolveSkill`, spends nothing. Not
  dodgeable, deals no damage, cannot be lethal.
- **Presentation** begin: gilding gathers at the caster's hands. release: a
  plate of light crosses to the ally. impact: the plate seats over them with a
  ring. settle: the ward badge appears and the gild dims to a held outline.
  `vfx.layers` per `docs/ART_PIPELINE.md` §5b.
- **Tooltip** "Shields one ally. The gilding holds for two of their turns."
- **Content row** existing fields only: `effect: Ward`, `targeting: SingleAlly`,
  `manaCost`, `cooldownTurns`, `wardTurns`, `flatAmount`, `scalingAxis`,
  `bookOnly`, `bookTier`, `physicalMove: false` (new field, D7).

### 2.2 Winter's Rebuke — `winters_rebuke`

- **Targeting** one enemy (`SingleEnemy`, front-rank `Reach` as authored).
- **Costs** 8 mana `prototype`. **Cooldown** 2 `prototype`. **Tier** 2
  `prototype`.
- **Scaling** one fixed packet, `{"type": "Frost", "amount": 6}` `prototype`
  (`Frost` resolves to `DamageType.Ice`, `SkillEntryResolver.cs:1059-1071`),
  scaled by tier × spell scaling × `ElementalDamagePercent`, floored at 1.
- **Effect order** (1) pay; (2) roll dodge once; (3) resolve the packet through
  `AfterDefences` — a Frost packet detonates nothing, so no detonation is
  reached; (4) `ApplyFinalDamage`; (5) marks; (6) **if the target lives**, apply
  Chilled 25% `prototype` for 2 effective turns **through the `ApplyStatusTo`
  seam** (D6), which routes to `ApplyChilled` and registers the speed malus.
- **Expiry** `AtTurnEnd`: two of the target's turns fully affected. The speed
  malus is revoked when the status is reported expired — the same
  `Expired.Contains(Chilled)` teardown that lives at
  `FightSession.Riders.cs:501-504` today, moved to the turn-end sweep with the
  clock (1.4) and widened to a recompute so a second Chill outliving the first
  keeps slowing (D3).
- **Stacking** two Rebukes on one target are two Chilled instances at 25% each,
  a 50% malus, each expiring on its own turn-end. The speed floor
  (`GrantSpeedMalusPercent`) is what stops a pile reaching zero Speed; no cap is
  authored on the status.
- **Failure** dodge → no damage, **no Chill**; lethal damage → no Chill
  (`IsAlive` gate, `FightSession.Skills.cs:717-721`); no target → refused at
  reach.
- **Presentation** begin: frost crazes outward from the caster's feet. release: a
  flat sheet snaps across. impact: a rime burst and a shiver. settle: a slow
  crystalline drift and the Chilled badge.
- **Tooltip** "Frost damage, and the cold hangs on them for two turns."
- **Content row** existing fields; `appliesStatus: "Chilled"`,
  `statusMagnitude: 25`, `statusDuration: 2`.

### 2.3 Viper's Bite — `vipers_bite`

- **Targeting** one enemy. **Costs** 7 mana `prototype`. **Cooldown** 2
  `prototype`. **Tier** 1 `prototype`.
- **Scaling** one packet `{"type": "Poison", "amount": 3}` `prototype`.
- **Effect order** (1) pay; (2) dodge, once; (3) the packet enters
  `AfterDefences`, whose detonation step (`DamagePipeline.cs:264`) fires because
  the packet is Poison-typed — **every Poison instance the target carries is
  consumed and dealt as one figure, at premium 100**, and all of them are
  removed before anything else;
  (4) `ApplyFinalDamage` for the packet; (5) marks; (6) if the target lives,
  apply fresh Poison, magnitude 3 `prototype`, 3 ticks `prototype`, as a new
  instance (D3).
- **Expiry** `AtTick`: three ticks, the third and the removal simultaneous, per
  instance.
- **Failure** dodge → nothing consumed, nothing applied, no detonation (the
  roll short-circuits `AfterDefences` at `:234`, before `:264`); lethal → no
  fresh Poison; no existing Poison → no detonation, ordinary hit.
- **One detonation, recorded once, however many instances it ate.** The list is
  clear before the fresh Poison is applied, so the fresh instance cannot be
  re-detonated by the same cast — and a target that was carrying three stacks
  is left carrying exactly one.
- **Presentation** the owner's priority visual: begin — coils gather behind the
  caster. release — jaws travel. impact — **jaws close over the target**, the
  authoritative `hitCueSeconds`. settle — the jaws dissolve and green runs down
  the target.
- **Tooltip** exactly: `Detonates existing Poison, then poisons again.`
- **Content row** existing fields; `appliesStatus: "Poison"`,
  `statusMagnitude: 3`, `statusDuration: 3`.

### 2.4 Crownfall — `crownfall`

- **Targeting** one enemy. **Costs** 6 mana `prototype`. **Cooldown** none
  `prototype`. **Tier** 1 `prototype`.
- **Scaling** `{"type": "Arcane", "amount": 7}` `prototype`; against a Marked
  target, `{"type": "Arcane", "amount": 12}` `prototype`.
- **Effect order** (1) pay; (2) dodge, once; (3) **read** `Marks.IsMarked`
  (`Marks.cs:34-37`) — a read, not a consume — and choose which packet list
  resolves; (4) resolve through `AfterDefences`; (5) `ApplyFinalDamage`;
  (6) **on a landed hit only**, `Marks.ConsumeMark` (`Marks.cs:43-52`);
  (7) marks applied by relics, then the skill's own status if any.
- **The mark that is consumed is the general `Marked` status**, never the
  Drowned Lantern's private `_marked` set (1.8).
- **Expiry** none; the spell leaves nothing behind.
- **Failure** dodge → **the mark survives** (consumption is after the miss
  check) and no damage; no mark → the 7-packet resolves, no consumption, no
  refusal; lethal → the mark is consumed on the blow that killed, which is a
  landed hit.
- **Presentation** begin: a circlet of light assembles above the target.
  release: it tips. impact: it shatters down onto them, brighter when a mark was
  there to break. settle: shards fade upward.
- **Tooltip** "Arcane damage. Far heavier against a marked enemy, and spends the
  mark."
- **Content row** two new fields: `consumesStatus: "Marked"` and
  `damageInstancesIfConsumed: [{"type":"Arcane","amount":12}]`.

### 2.5 Ashen Reckoning — `ashen_reckoning`

- **Targeting** one enemy, **which must carry Poison**. **Costs** 10 mana
  `prototype`. **Cooldown** 2 `prototype`. **Tier** 3 `prototype`.
- **Scaling** no packet of its own. Its damage is the consumed Poison at
  **150%** `prototype`, split across Fire and Poison.
- **Effect order** (1) refuse in `CanResolveSkill` if the target carries no
  Poison (`requiresStatus: "Poison"`) — spends nothing; (2) pay; (3) dodge,
  once — a dodged Reckoning consumes nothing; (4) **one** detonation at premium
  150 (1.6): the entry is read, removed, and its worth computed; (5) split the
  total across `["Poison", "Fire"]` with the odd point to the first (1.7), and
  resolve each packet with `resolveDetonation: null`; (6) `ApplyFinalDamage`
  once per packet, one death settlement per target; (7) if the target lives,
  Vulnerable 20% `prototype` for 1 effective turn.
- **No double scaling.** The Poison's `Magnitude` is already a snapshot; the
  premium is the only multiplier, and no caster scaling term is applied at
  step 4.
- **No recursion.** The entry is removed at step 4; the Poison-typed half at
  step 5 passes a null detonation resolver, so it cannot re-enter
  `SpendPoisonIfMatched`. Recorded as one detonation.
- **`SkillEffect.Reclaim`** (D10). It is a member rather than a `DamageSingle`
  variant because its packets do not exist until the consumption has happened:
  `HasFixedDamage` is false, the authored `damageInstances` list is empty, and
  the packets are built at resolution from the consumed total.
- **Expiry** the Vulnerable runs through the target's next completed turn
  (`AtTurnEnd`, `statusDuration: 1`).
- **Failure** no Poison → refused before payment; dodge → paid, nothing
  consumed, no Vulnerable; lethal → no Vulnerable.
- **Presentation** the owner's priority visual: begin — the target's poison
  lifts out of them as green thread. release — the thread twists and takes
  light. impact — **it returns as burning material**, a two-tone Poison/Fire
  burst. settle — ash falls and the Vulnerable badge lands.
- **Tooltip** "Requires Poison. Rips it out and returns it burning, then leaves
  them open."
- **Content row** new `SkillEffect.Reclaim`; new fields
  `requiresStatus: "Poison"`, `detonationPercent: 150`,
  `detonationSplit: ["Poison", "Fire"]`; no `damageInstances`. Existing:
  `appliesStatus: "Vulnerable"`, `statusMagnitude: 20`, `statusDuration: 1`.

### 2.6 Blackglass Spear — `blackglass_spear`

- **Targeting** one enemy. **Costs** 12 mana **plus 5% of the caster's maximum
  health** `prototype`, rounded up. **Cooldown** 3 `prototype`. **Tier** 3
  `prototype`.
- **Scaling** `{"type": "Void", "amount": 13}` `prototype`, scaled the ordinary
  packet way.
- **Effect order** (1) validate mana **and** health together
  (`CurrentHealth - cost >= 1`), refusing both or neither; (2) pay mana, pay
  health (1.2 — direct write, no funnel), begin cooldown; (3) dodge, once;
  (4) resolve the packet with `ignoresDefense: true`, which zeroes broad defense
  (`CombatMath.cs:114-121`) — for a non-physical type that is exactly
  MagicalDefense — and leaves typed resistance, affinity and the ward pool
  intact; (5) `ApplyFinalDamage`; (6) marks.
- **Requires the `ResolveDamageInstances` repair** in 1.3: without it the
  bypass silently does nothing.
- **Expiry** none.
- **Failure** health cost would leave 0 → refused, nothing spent, not even the
  mana; dodge → paid in full, no damage; no target → refused at reach.
- **Presentation** begin: the caster's own blood beads and hardens. release: a
  black-glass shaft crosses. impact: it goes through, with a thin ring of
  nothing. settle: the shaft powders; the caster's bar shows the price.
- **Tooltip** "Costs blood. Ignores magical defence."
- **Content row** new field `healthCostPercent: 5`; existing
  `ignoresDefense: true`.

### 2.7 Borrowed Moment — `borrowed_moment`

- **Targeting** one **other** ally (`SingleAlly`; the caster is excluded, the
  same rule the three Gifts already use — `AllyTargeting.cs:41-43`, which this
  effect joins).
- **Costs** 8 mana `prototype`. **Cooldown** 2 `prototype`. **Tier** 2
  `prototype`.
- **Scaling** none. It deals no damage, and the number of positions it buys does
  not scale with the caster — two positions is two positions, which is what
  keeps it priced against a slot rather than against a stat.
- **Effect order** (1) refuse in `CanResolveSkill` when the target is already at
  forecast position 1, or is not in the order — spends nothing (1.9 rule 5);
  (2) pay; (3) take **one** forecast; (4) `PullForward(target, slots)` until the
  target reaches `max(1, p - advanceSlots)` or a slot changes nothing;
  (5) log where they landed.
- **`advanceSlots: 2`** `prototype`.
- **Expiry** instantaneous; nothing is left on the board.
- **Failure** already next → refused; target dies between telegraph and
  resolution → the ally target is re-validated at cast (1.1 step 2); grants no
  extra action and changes no `Rate` (1.9 rule 3).
- **Presentation** begin: a clock-face of light behind the caster. release: a
  hand sweeps backward. impact: the ally's plate flares and their initiative
  marker slides up the tracker. settle: the light unwinds.
- **Tooltip** "Moves an ally up to two places earlier in the order. Never buys
  them an extra turn."
- **Content row** new `SkillEffect.Hasten`, new field `advanceSlots: 2`,
  `targeting: SingleAlly`.

### 2.8 Gale Scythe — `gale_scythe`

- **Targeting** every living enemy (`AllEnemies`). **Costs** 11 mana
  `prototype`. **Cooldown** 3 `prototype`. **Tier** 2 `prototype`.
- **Scaling** `{"type": "Wind", "amount": 5}` `prototype` per enemy, each
  checked against that enemy's own affinity.
- **Effect order** (1) pay; (2) snapshot the struck list before damage, so a
  target killed by this cast is still drawn taking the blow
  (`FightSession.Skills.cs:740-744`); (3) per enemy: dodge once for that enemy,
  resolve, `ApplyFinalDamage`, marks, status; (4) **after every enemy has
  resolved and every death has settled**, take one forecast; (5) apply
  `PushBack(survivor, 1)` in forecast order, over **landed-hit survivors only**
  — an enemy that dodged is not displaced, and a dead one is skipped (1.9 rule
  6); (6) log.
- **`queuePushSlots: 1`** `prototype`, reusing the existing field; the AOE path
  does not call `ApplyQueuePush` today (`FightSession.Skills.cs:800-826`
  applies status but not the push), so the batch step at (4)-(5) is new code in
  the AOE branch, not a change to the single-target one.
- **Expiry** instantaneous.
- **Failure** every enemy dodges → damage 0, **no displacement at all**; every
  enemy dies → the survivor list is empty and the batch applies nothing; no
  enemies → `AllEnemies` resolves onto nothing and still spends the cooldown
  (`FightSession.Cooldowns.cs:51-54`).
- **Presentation** begin: the air draws in toward the caster. release: a flat
  crescent sweeps the whole enemy rank. impact: one cue, every body, each with
  its own number. settle: dust and the tracker re-ordering.
- **Tooltip** "Wind damage to every enemy, and each one it hits loses a place in
  the order."
- **Content row** existing fields; `effect: DamageAll`, `queuePushSlots: 1`.

### 2.9 Palace Passage — `palace_passage`

- **Targeting** two living allies (`SingleAlly` targeting at pick count 2 —
  1.12). **Costs** 7 mana `prototype`. **Cooldown** 3 `prototype`. **Tier** 2
  `prototype`. **The caster's one free action** (`freeAction: true`).
- **Scaling** none.
- **Effect order** (1) the picker (1.12's state table); (2) on the second pick,
  refuse if either ally is Rooted, if they are the same figure, or if the free
  action is already spent — all before payment; (3) pay mana, begin cooldown;
  (4) `SwapPartySlots`; (5) `NoteDeliberateMove` for both figures with the
  caster named as the acting character; (6) recompute formation legality;
  (7) commit the beat and **do not advance the turn** — set
  `_freeActionTakenBy` (`FightSession.Skills.cs:195-201`).
- **Expiry** the swap is permanent until something else moves them.
- **Failure** cancel at any point spends nothing and leaves the board unchanged
  (atomic); either ally Rooted → refused, with the same sentence `Move` already
  says; a party of one → refused.
- **Enemy intents follow the actor**, not the slot, and are re-validated against
  reach at resolution with no RNG draw
  (`FightSession.Enemies.cs:710-732`).
- **Presentation** the owner's priority visual: begin — **a door opens beside
  each of the two allies**. release — both step through. impact — they arrive in
  each other's places, doors still standing. settle — the doors close and fade.
- **Tooltip** "Swap two allies. Free action. Neither may be rooted."
- **Content row** new `SkillEffect.SwapAllies`, `freeAction: true`,
  `targeting: SingleAlly`. The pick count of 2 is a property of the **effect**,
  not an authored field — one more content number would let a row ask for three
  picks the resolution cannot use.

### 2.10 Velvet Shackles — `velvet_shackles`

- **Targeting** one enemy. **Costs** 9 mana `prototype`. **Cooldown** 3
  `prototype`. **Tier** 2 `prototype`.
- **Scaling** none. It deals no damage and its duration does not scale with the
  caster — a two-turn root is two turns from anyone, which is what keeps it a
  control spell rather than a damage spell in disguise.
- **Effect order** (1) pay; (2) apply Rooted for 2 effective turns `prototype`
  through the `ApplyStatusTo` seam; (3) if the target has a committed intent
  whose ability is now illegal, that intent is **not** rewritten here — it is
  re-validated at resolution by the existing recheck (1.10), which is the one
  place that decision lives.
- **`SkillEffect.Afflict`** — one enemy, this skill's authored status, no
  damage.
- **Expiry** `AtTurnEnd`, two of the target's turns fully restricted, including
  the second one's action.
- **Failure** no target → refused at reach; nothing dodgeable (no damage
  instance, so `AfterDefences` is never entered — an `Afflict` lands or is
  refused).
- **What Rooted now forbids**: every action classified `PhysicalMove` (1.10),
  for both sides, plus `Move` as today. A rooted actor with a legal
  non-physical action takes it; one with none forfeits.
- **Presentation** begin: dark ribbon uncoils from the caster. release: it
  crosses low. impact: it wraps the target's legs and cinches. settle: the
  Rooted badge, and the ribbon holds visibly for its duration.
- **Tooltip** "Binds one enemy for two turns. It cannot strike, charge or move —
  only cast."
- **Content row** new `SkillEffect.Afflict`; existing
  `appliesStatus: "Rooted"`, `statusDuration: 2`.

### 2.11 Censer of Embers — `censer_of_embers`

- **Targeting** one enemy. **Costs** 8 mana `prototype`. **Cooldown** 2
  `prototype`. **Tier** 2 `prototype`. No direct damage.
- **Scaling** the new-DoT rule (1.5): the tick strength is the authored base
  multiplied by the caster's tier and spell scaling, snapshotted at application.
- **No intensity ladder.** `intensitySteps` is **dropped** (owner, 2026-09-20).
  A recast adds a second Burn instance at its own snapshot rather than stoking
  the first, and the target's per-turn burn is the sum. That is the stacking
  model doing the job the ladder was invented to do, with no new field, no new
  content key and no "is the incoming application at least as strong" rule.
  Authored base `4` `prototype`.
- **Effect order** (1) pay; (2) `ApplyDot(target, Burn, base, 3 ticks, caster)`
  through the seam, which adds an instance; (3) log the resulting tick strength,
  which is what the tooltip and the badge show.
- **Expiry** `AtTick`: three ticks `prototype`, the third and the removal
  simultaneous.
- **Tick** Fire-typed, affinity applied against the holder, no flat defense, no
  ward, no variance (1.5). Attribution survives the caster's death.
- **Failure** target already dead at resolution → nothing applied; not
  dodgeable.
- **Presentation** begin: a censer swings up beside the caster. release: it
  arcs over. impact: coals scatter across the target. settle: a held ember glow
  that brightens with each live instance.
- **Tooltip** "Burns for three turns. Cast it again and both fires burn."
- **Content row** new `SkillEffect.Afflict`, existing fields only;
  `appliesStatus: "Burn"`, `statusMagnitude: 4`, `statusDuration: 3`.

### 2.12 Thorn Tithe — `thorn_tithe`

- **Targeting** one enemy. **Costs** 10 mana `prototype`. **Cooldown** 3
  `prototype`. **Tier** 3 `prototype`. No direct damage.
- **Scaling** the new-DoT rule; one authored number serves both halves.
- **Effect order** (1) pay; (2) `ApplyDot(target, Thorned, 5, 3 turns, caster)`
  `prototype`; (3) log.
- **Two damage moments per affected turn**: the `AtTick` tick at the holder's
  turn start, and the post-action hook (1.11) after a **completed physical
  move**. Both deal the same stored snapshot, both Nature-typed with affinity
  and no flat defense, both reported as `TickRow`s and credited
  `KillCredit.Nobody`. One authored number, `statusMagnitude: 5` `prototype`,
  is the owner's "5 + 5".
- **Expiry** active until the end of the final affected turn. Three affected
  turns means **three** retaliation opportunities: the counter moves at the tick
  that opens each turn, and the entry is not removed until the third tick, so
  the third turn's action is still covered. A skipped turn gets the opening tick
  and **no** retaliation, because no action completed.
- **Failure** an interrupted or rejected action triggers nothing (1.11); a miss
  **does** trigger; the retaliation never counts as a new physical move and
  cannot re-trigger itself (1.11's structural guard); a retaliation that kills
  settles the death like any other.
- **Presentation** begin: briars twist up from the ground under the target.
  release: they close. impact: a Nature tick. settle: the briars hold and
  tighten visibly whenever the target acts.
- **Tooltip** "Thorns for three turns: damage at the start of each of its turns,
  and again whenever it strikes or charges."
- **Content row** new `SkillEffect.Afflict`; `appliesStatus: "Thorned"`,
  `statusMagnitude: 5`, `statusDuration: 3`.

### 2.13 Court of Whispers — `court_of_whispers`

- **Targeting** every living enemy, plus the caster. **Costs** 14 mana
  `prototype`. **Cooldown** 4 `prototype`. **Tier** 3 `prototype`.
- **Scaling** none. It deals no damage, and neither the skip nor the delay
  scales with the caster — the cost of the caster's own Vulnerable is what
  prices it, not a stat.
- **Effect order** (1) pay; (2) take **one** forecast; (3) per enemy, in the
  encounter's own order: **boss** → `PushBack(enemy, 2)` `prototype` (via
  `queuePushSlots`, destinations fixed from the one forecast, 1.9);
  **ordinary** → `Fear.Apply(enemy, Fear.DefaultTurns)` — a skipped turn plus
  Vulnerable at `Fear.VulnerablePercent` (25, D8); (4) apply the caster's own
  Vulnerable 20% `prototype` for 1 effective turn; (5) log, naming which enemies
  took which.
- **`SkillEffect.Enthrall`**, and the caster is this effect's status recipient —
  `ApplySkillStatus(skill, recipient: caster, caster)` already takes an
  arbitrary recipient (`FightSession.Skills.cs:1136`).
- **Expiry** the Fear is `AtUse`, spent on one actual skip
  (`StatusEffects.cs:221-232`); the boss delay is instantaneous; both
  Vulnerables are `AtTurnEnd` for one affected turn. The caster's is applied
  during the caster's own turn and is therefore exempt from that turn's end
  tick (D1) — "through their next completed turn", as the owner wrote it.
- **Failure** no living enemies → refused in `CanResolveSkill`, spends nothing;
  an all-boss field → every enemy takes the delay and none takes Fear, which is
  legal and must be visible **in the targeting info before the cast**, not
  discovered after; **the caster's Vulnerable is applied either way** — it is
  the price, not a rider on success.
- **Presentation** the owner's priority visual: begin — figures resolve out of
  the dark **around the whole enemy rank**. release — they lean in and whisper.
  impact — ordinary enemies recoil and freeze; bosses only slow, and the
  tracker shows them sliding back. settle — the court fades; the caster is left
  exposed, with their own Vulnerable badge.
- **Tooltip** "Terrifies ordinary enemies for a turn. Bosses are only delayed.
  You are left open."
- **Content row** new `SkillEffect.Enthrall`; existing `queuePushSlots: 2`,
  `appliesStatus: "Vulnerable"`, `statusMagnitude: 20`, `statusDuration: 1`.

---

## 3. Test matrix

One row per contract clause and per named failure risk. Tests pin literals and
none recomputes a production formula (`docs/CODE_STANDARDS.md` §8). Existing
classes are named where a pin already exists; new methods go into the existing
class for their area wherever one exists, because a test's area is the folder it
sits in and every one of these is `Tests/EditMode/Combat/`.

Tests marked **(dispatcher)** go through `CastSkill`/the session rather than
calling a resolution method directly, per Appendix B.

| Contract / risk | Class → method | Milestone |
|---|---|---|
| **1.1** every refusal spends nothing | `SkillDispatchTests.ACastThatCannotBePaidForIsRefusedAndCostsNoTurn` (exists) | A |
| **1.1** a refused cast starts no cooldown | `SkillCooldownTests.ARefusedCast_ForUnaffordableMana_StartsNoCooldown` (exists) | A |
| **1.1** cooldown counted in the caster's turns | `SkillCooldownTests.ACooldownOfTwoMeansTurnOneThenTurnThree` (exists) | A |
| **1.2** health cost is refused when it would leave 0 HP **(dispatcher)** | `HealthCostTests.ACostThatWouldLeaveZero_IsRefused_AndSpendsNoMana` | B |
| **1.2** percentage rounds **up** | `HealthCostTests.FivePercentOf201MaxHealth_Costs11_Not10` | B |
| **1.2** the cost bypasses wards | `HealthCostTests.AWardedCaster_PaysTheFullCostFromHealth_AndTheWardIsUntouched` | B |
| **1.2** the cost is not incoming damage | `HealthCostTests.PayingHealth_TriggersNoVulnerable_NoWool_NoPhoenixEgg_NoLedgerTookRow` | B |
| **1.2** the cost cannot score a kill | `HealthCostTests.PayingHealth_NeverSettlesADeath_BecauseItCannotReachZero` | B |
| **1.3** pipeline order, dodge first | `DamagePipelineTests`, `CombatMathTests` (exist) | A |
| **1.3** dodge covers every path | `DodgeCoversEveryDamagePathTests` (exists, four cases) | A |
| **1.3** dodge rolled once per multi-packet cast | `DodgeCoversEveryDamagePathTests.FixedPacketSpell_RollsDodgeOnceForTheWholeCast_NotOncePerPacket` (exists) | A |
| **1.3** `ignoresDefense` reaches the fixed-packet path **(dispatcher)** | `SkillDispatchTests.AFixedPacketSpellWithIgnoresDefense_TakesNoMagicalDefenceTerm` | B |
| **1.3** …and still takes typed resistance | `SkillDispatchTests.AFixedPacketSpellWithIgnoresDefense_StillTakesTypedResistance` | B |
| **D1** the three-family table is total | `StatusEffectsTests.EveryStatusTypeAnswersDurationClock` (vacuity-guarded on `Enum.GetValues`) | A |
| **D1** a standing status covers its final affected action | `StatusEffectsTests.AStandingStatusWithTwoTurns_StillApplies_OnTheSecondTurnsAction` | A |
| **D1** the exemption: applied during the bearer's own turn | `StatusEffectsTests.AStatusAppliedOnTheBearersOwnTurn_DoesNotAgeAtThatTurnsEnd` | A |
| **D1** re-authored rows keep today's effective behaviour | `StatusDurationMigrationTests.EveryReAuthoredRow_AffectsTheSameNumberOfTurnsItDidBefore` (one case per table row in §0) | A |
| **D1** Poison/Regen are untouched by the move | `StatusEffectsTests.Tick_Poison_StillDealsExactlyThreeTicksForThree` | A |
| **D1** Fear still skips exactly its authored turns | `FearTests.FearExpiresAfterItsOwnDuration` (exists) | A |
| **1.4** ward clock unchanged | `WardTests.AWardRaisedThisTurn_SurvivesThisTurnsEnd_AndGoesAtTheNextOne` (exists) | A |
| **1.5** the snapshot is taken once and ignores the caster afterwards | `NewDotTests.ABurnTickIsUnchangedWhenTheCastersAttackDoublesAfterApplication` | E |
| **1.5** affinity applies per tick | `NewDotTests.ABurnTickOnAFireWeakHolder_DealsOneAndAHalfTimesItsSnapshot` | E |
| **1.5** no flat defense per tick | `NewDotTests.ABurnTickIgnoresTheHoldersMagicalDefence` | E |
| **1.5** attribution survives the caster's death | `NewDotTests.ABurnAppliedByACasterWhoThenDies_StillTicksAndStillNamesItsSource` | E |
| **1.5** tick death is credited to nobody | `KillCreditTests` + `NewDotTests.ABurnTickThatKills_SettlesWithNobody` | E |
| **D3** the stack-policy table is total | `StatusEffectsTests.EveryStatusTypeAnswersStackPolicy` (vacuity-guarded on `Enum.GetValues`) | A |
| **D3** a second DoT application adds an instance, it does not merge | `StatusEffectsTests.ASecondPoison_StandsBesideTheFirst_WithItsOwnMagnitudeAndClock` | A |
| **D3** the per-turn tick total is the sum of live instances | `StatusEffectsTests.ThreePoisonInstances_TickForTheirSum_InOnePass` | A |
| **D3** instances expire apart, not together | `StatusEffectsTests.TheShortestPoisonInstanceGoesFirst_AndTheOthersKeepTicking` | A |
| **D3** each instance keeps its own source | `StatusEffectsTests.TwoCastersPoisoningOneTarget_EachKeepTheirOwnAttribution` | A |
| **D3** a binary restriction still refreshes | `StatusEffectsTests.ASecondStun_RefreshesRatherThanStacking` | A |
| **D3** an additive debuff sums across instances | `StatusEffectsTests.TwoVulnerables_AddUpInTheDamageMultiplier` | A |
| **D3** a stacked status draws ONE badge carrying the total | `StatusHudCoverageTests.ThreePoisonInstances_DrawOneBadge_WhoseTooltipCarriesTheSum` | A |
| **D3** Chilled's malus is the sum, and survives one instance expiring | `ChilledStatusTests.TwoChills_SlowByTheirSum_AndOneExpiringLeavesTheOtherSlowing` | A |
| **D3** two casters, different strengths, neither is floored by the other | `NewDotTests.TwoCastersDifferentStrengths_BothInstancesTickAtTheirOwnSnapshot` | E |
| **D5** `TickReport` carries two damage types in one tick | `StatusEffectsTests.OneTick_CarryingBurnAndPoison_ReportsBothRowsSeparately` | E |
| **D5** absorbed and health-loss stay apart | `WardTests.APoisonTickAWoolPoolAbsorbsIsStillCountedAndStillSaid` (exists) | E |
| **D6** `appliesStatus: Chilled` actually slows **(dispatcher)** | `ChilledStatusTests.ASkillAuthoringChilled_RegistersTheSpeedMalus_NotJustTheBadge` | A |
| **1.4** the speed malus is revoked by the turn-END expiry report | `ChilledStatusTests.ChilledsSpeedIsGivenBack_OnTheTurnEndThatExpiresIt_NotTheTurnStart` | A |
| **D6** no production path applies a side-effect status outside the seam | `StatusSeamTests.NoProductionCallerAppliesChilledOrADotThroughStatusEffectsApplyDirectly` (reflection over the assembly, vacuity-guarded on a minimum call-site count) | A |
| **1.6** detonation consumes once and is not rescaled | `StatusCombosTests.Detonating_ConsumesThePoisonEntirely` (exists) | A |
| **1.6** detonation eats EVERY instance, in one consumption | `StatusCombosTests.DetonatingThreeStacks_ConsumesAllOfThem_AndLeavesNoneTicking` | A |
| **1.6** the premium is applied to the sum, not per instance | `StatusCombosTests.APremiumOverThreeStacks_RoundsOnceOnTheTotal` | B |
| **Risk** a stacked detonation is recorded once, not once per instance | `VipersBiteTests.ABiteOnATripleStackedTarget_RecordsOneDetonationRow` | A |
| **1.6** a second call does nothing | `StatusCombosTests.DetonatingTwiceInARow_TheSecondCallDoesNothing` (exists) | A |
| **1.6** hit detonates before fresh Poison lands **(dispatcher)** | `SkillDispatchTests.ANatureCastThatAlsoAppliesPoison_DetonatesTheOldPoisonBeforeTheFreshOneLands` (exists) | A |
| **1.6** Viper's Bite: one detonation, one record **(dispatcher)** | `VipersBiteTests.ABiteOnAPoisonedTarget_DetonatesOnce_ThenLeavesExactlyOneFreshPoison` | A |
| **1.6** premium parameter, default 100 | `StatusCombosTests.APremiumOfOneFifty_ReportsHalfAgain_AndStillConsumesOnce` | B |
| **1.7** split: odd point to the first type | `AshenReckoningTests.ASeventeenPointTotalSplitsAsNinePoisonAndEightFire` | B |
| **1.7** the Poison half cannot re-detonate **(dispatcher)** | `AshenReckoningTests.TheReckoningsPoisonHalf_TriggersNoSecondDetonation` | B |
| **1.7** one death settlement across the split | `AshenReckoningTests.AReckoningThatKillsAcrossBothHalves_RecordsOneKillRow` | B |
| **1.8** `TrySpend` reports and removes | `MarksTests` (exists, five cases) + `StatusEffectsTests.TrySpend_ReportsTheEntryItRemoved` | B |
| **1.8** Crownfall reads the general mark, not the Lantern's | `CrownfallTests.ALanternMarkAlone_DoesNotRaiseCrownfallsDamage` | B |
| **1.8** dodge preserves the mark **(dispatcher)** | `CrownfallTests.ADodgedCrownfall_LeavesTheMarkOnTheTarget` | B |
| **1.8** a landed hit consumes it **(dispatcher)** | `CrownfallTests.ALandedCrownfall_SpendsTheMark_AndASecondCastIsBackToSeven` | B |
| **1.9** forecast position 0 is the current actor | `TurnOrderTests.ProjectPutsTheCurrentActorFirst` | C |
| **1.9** advance: the four worked examples | `InitiativeDisplacementTests.EqualSpeedActors_AdvancingByTwo_LandsSecond`, `.AFastActorAppearingTwice_IsMeasuredByItsFirstAppearance`, `.AnActorAlreadyNext_IsRefusedBeforePayment`, `.AMultiTargetDelayAfterADeath_DisplacesEverySurvivorOnce` | C |
| **1.9** no entry duplicated or lost | `InitiativeDisplacementTests.EveryDisplacement_LeavesTheEntryCountAndTheEntrySetUnchanged` | C |
| **1.9** the current actor is never moved | `InitiativeDisplacementTests.NeitherOperationAcceptsTheCurrentActor` | C |
| **1.9** equal-displacement targets keep their order | `InitiativeDisplacementTests.TwoTargetsTiedAtOneCharge_KeepTheirRelativeOrderAfterABatchDelay` | C |
| **1.9** no extra action, no `Rate` change | `InitiativeDisplacementTests.AnAdvanceGrantsNoExtraTurn_AndLeavesRateAlone` | C |
| **1.9** one forecast, taken before any displacement | `InitiativeDisplacementTests.ABatchDelayFixesEveryDestinationBeforeTheFirstOneMoves` | C |
| **1.9** batch after deaths references no dead entry | `GaleScytheTests.ASweepThatKillsTwo_DisplacesOnlyTheSurvivors_AndThrowsNothing` | C |
| **1.9** a dodged target is not displaced **(dispatcher)** | `GaleScytheTests.AnEnemyThatDodgesTheScythe_KeepsItsPlaceInTheOrder` | C |
| **1.9** `ProjectPulled` mutates nothing | `TurnOrderTests.ProjectPulled_LeavesEveryRealChargeUnchanged` | C |
| **1.10** the classification is total for damage rows | `PhysicalMoveAuditTests.EveryDamageSkillInContentStatesPhysicalMoveExplicitly` (vacuity-guarded on the damage-row count) | D |
| **1.10** the audit table is the content | `PhysicalMoveAuditTests.TheClassificationOfEverySkillMatchesTheAuditedTable` (literal id→bool table) | D |
| **1.10** physical skills blocked, non-physical available **(dispatcher)** | `VelvetShacklesTests.ARootedEnemy_CannotDrawAPhysicalAbility_ButStillDrawsACast` | D |
| **1.10** a rooted player is refused a physical cast **(dispatcher)** | `VelvetShacklesTests.ARootedPlayer_IsRefusedAPhysicalSkill_AndSpendsNothing` | D |
| **1.10** a committed intent re-validates when Rooted lands after selection | `EnemyIntentTests` + `.AnIntentCommittedBeforeShacklesLand_IsVoidedAtResolution` | D |
| **1.10** the forfeiture fallback does not regress | `RootedStatusTests.RootedEnemyWithNoLegalSkill_ForfeitsItsTurn_TheSameWayStunDoes` (exists) | D |
| **1.10** the second affected turn is still restricted | `VelvetShacklesTests.TheSecondOfTwoShackledTurns_IsStillRestricted` | D |
| **1.10** the legality query spends no RNG draw | `VelvetShacklesTests.AskingWhetherAnActionIsLegal_ConsumesNoDrawFromTheSeededStream` | D |
| **1.11** a miss still triggers retaliation **(dispatcher)** | `ThornTitheTests.APhysicalSwingThatMisses_StillPaysTheTithe` | E |
| **1.11** an interrupted action does not | `ThornTitheTests.AForfeitedTurn_PaysNoTithe_ButStillTakesTheOpeningTick` | E |
| **1.11** the retaliation is not itself a physical move | `ThornTitheTests.ATitheRetaliation_DoesNotRetriggerItself` | E |
| **1.11** three affected turns, three opportunities | `ThornTitheTests.ThreeAffectedTurns_OfferThreeRetaliations_TheThirdIncluded` | E |
| **1.12** the selection state table | `PalacePassageTests.TheSwapPickerFollowsItsStateTable` (one case per cell) | C |
| **1.12** cancel at pick 1 spends nothing | `PalacePassageTests.CancellingAfterTheFirstPick_LeavesTheBoardUnchanged` | C |
| **1.12** either ally Rooted refuses | `PalacePassageTests.ARootedPartner_RefusesTheSwap_TheSameWayMoveDoes` | C |
| **1.12** health and statuses stay attached to actors | `PalacePassageTests.ASwapMovesSlotsNotStatuses` | C |
| **1.12** intent follows the actor | `PalacePassageTests.AnActorTargetedIntent_FollowsTheSwappedActor` | C |
| **1.12** it is a free action | `PalacePassageTests.ThePassageDoesNotEndTheTurn_ButOnlyOncePerTurn` | C |
| **1.12** both picks are validated at commit, not just at pick time **(dispatcher)** | `PalacePassageTests.APartnerRootedBetweenThePickAndTheCommit_StillRefusesTheCast` | C |
| **1.12** the one-pick overload is unchanged **(dispatcher)** | `SkillDispatchTests.EverySinglePickSkillStillCastsThroughTheOneTargetOverload` | C |
| **1.13** preview consumes no mark, no Poison, no draw | `PreviewPurityTests.APreviewOfEverySpellLeavesTheBoardByteForByte` (one case per new spell, vacuity-guarded on the spell count) | A (frame), extended per milestone |
| **1.13** preview matches resolution for fixed packets | `PreviewPurityTests.APacketPreviewQuotesTheSameFigureResolutionProduces_WithAnElementalRider` | A |
| **1.14** every new source reaches the ledger | `CombatLedgerTests` + `.EveryNewSpellsDamageAppearsInDealtAndTook` | per milestone |
| **1.15** a Fury character cannot learn a new book | `SpellBookEligibilityTests.NoneOfTheThirteenIsOfferedToAFuryCharacter` | F |
| **1.15** a book learned by one character does not cast from another | `SpellBookEligibilityTests.ABookInOneCharactersSlot_IsAbsentFromAnothersKit` | F |
| **1.15** save/reload keeps a learned and equipped new spell | `SpellBookPersistenceTests.AllThirteenSurviveASaveAndReload` (PlayMode) | F |
| **Risk** double scaling at detonation | `StatusCombosTests.Detonating_ConsumesThePoisonEntirely` (exists) + `AshenReckoningTests.ThePremiumIsTheOnlyMultiplier` | B |
| **Risk** double scaling at a DoT tick | `NewDotTests.ABurnTickIsUnchangedWhenTheCastersAttackDoublesAfterApplication` | E |
| **Risk** enum renumbering | `ContentEnumOrdinalTests.SkillEffectAndStatusEffectTypeOrdinalsMatchTheirPinnedList` (literal name→ordinal table; the table gains the appended member in whichever package appends it, and an insertion anywhere fails it) | A, extended in C, D, E, F |
| **Risk** party-wide control loop | `ControlLoopTests.AlternatingCastersCannotDenyEveryEnemyTurnAcrossFiveRounds` | F |

---

## 4. Milestones

Six packages. Each is a complete slice for every spell it names — content,
mechanics, targeting, tooltip, placeholder presentation, tests — and each has an
executable gate. No acceptance criterion in any package depends on a later one.
No effort estimates.

Every package's gate ends with `tools/test.ps1 combat` for iteration and
`tools/run_tests_parallel.ps1 -BuildContent` before commit, because every
package touches `ContentData/skills.json` and therefore
`Resources/Content/content_stamp.json`. `-BuildScenes` is **not** needed unless
a `[SerializeField]` or a screen tree changes; only milestone C's picker can do
that, and its gate says so.

### Milestone A — protection, frost, Poison, and the duration model

> **STATUS: LANDED 2026-09-20**, on `workflow-2026-09-19`.
> `bfe097aa` the plan revision (owner's stacking decision, section 0 and D3/D4),
> `64046f77` the code, content, tests and measurements.
>
> **What landed.** The three-family `DurationClock` and the generalised
> `TickAtTurnEnd` sweep; the nine re-authored rows with
> `StatusDurationMigrationTests` as their pin; instance stacking across
> `StatusEffects`/`StatusCombos`/`RefreshChilledSpeed`/`StatusHud`; the
> `ApplyStatusTo` seam with `StatusSeamTests` as its lint; Gilded Aegis,
> Winter's Rebuke and Viper's Bite as complete slices with placeholder
> `vfx.layers`; the preview-parity repair with `PreviewPurityTests`; and
> `ContentEnumOrdinalTests` over four ordinal-serialised enums.
>
> **What deviated, and why.**
>
> - **`TickReport` is unchanged.** The brief left the choice open between a row
>   per instance and an aggregate per type. It aggregates: `PoisonDamage` and
>   `PoisonAbsorbed` are already sums over the loop, so stacking needed no
>   change there, and D5's typed rows are milestone E's. `Expired` reports one
>   entry per instance REMOVED, and both log sites deduplicate, because three
>   stacks lapsing together is three removals and one thing to tell a player.
> - **Protect and Regen stack**, though the owner's answer named neither. By
>   symmetry with Vulnerable and Poison, which it did name. Stated in D3 as
>   Fable's reading and cheap to reverse: one line in `StackPolicyOf`.
> - **Empowered refreshes.** Also unnamed. It is a single-spend token like
>   Provoked, and stacking it would silently turn one empowered swing into two.
> - **The `Shielded` throw in `StatusEffects.Apply` stays.** Wards are on the
>   stacking side now, so the original argument for the throw is gone, but
>   `ApplyWard` is still the only place that knows a ward of non-positive points
>   is a no-op rather than an entry.
> - **No `physicalMove` on the three rows.** D7's field does not exist yet; it
>   is milestone D's, and the brief said not to author it early.
> - **Winter's Rebuke authors no `reachSlots`.** Section 2.2 says "front-rank
>   `Reach` as authored", which is ambiguous; every existing player book
>   (`mud_burst`, `lightning_bolt`) reaches the whole line, and a book that
>   could not reach the back rank would be a silent nerf against the family it
>   is balanced with.
> - **A `CrossTurnBoundaryForTest` seam was added.** The turn-end exemption set
>   is cleared inside `OpenTurnFor`, which a test driving the two tick seams
>   directly never reaches -- without it every status stays exempt forever and
>   the sweep looks broken while working exactly as written.
> - **Four preview PNGs, not twelve.** `docs/measurements/spell_expansion/
>   milestone_a/` carries the impact and flight frames for both damaging
>   spells; the other eight frames are 2.5 MB each and the gate's own wording
>   asks for the impact frame.
>
> **Gate.** `run_tests_parallel.ps1 -BuildContent`: EditMode 4015/4018 passed,
> 0 failed, 3 skipped; PlayMode 4 failed, every one of them saved-scene wiring
> (`submenuMarks` null in the saved Fight scene, behind both
> `FightSubmenuAffordabilityTests` failures and `ScreenWiringTests`; plus
> `ShopGamepadNavigationTests` on the shop's `systemMenu` reference). No screen
> tree, `SceneBuilder` file or scene is touched by either commit, and the five
> `.unity` files in this tree are mid-edit from another session, so
> `-BuildScenes` was deliberately not run. `test.ps1 combat` re-run after the
> `vfx.layers` retiming: 410/422 PlayMode, the same two book-art failures, 1412
> dotnet passed.
>
> **Open for the owner, from section 5's measurements.** Winter's Rebuke is
> mud_burst's packet for mud_burst's mana one tier higher; and a 25% Chilled
> denies about 6% of an enemy's actions, not 25%, because `SpeedScale`'s curve
> is sub-linear. Neither number was moved.

**Scope.** The duration model (D1) and its nine re-authored rows; **the
stacking model (D3) across `ActiveStatus`/`StatusEffects` and every reader of a
status magnitude**; the `ApplyStatusTo` seam (D6); Gilded Aegis, Winter's
Rebuke, Viper's Bite as complete slices; the preview-parity repair in 1.13; the
enum-ordinal pin.

The stacking model lands here rather than in E because milestone A's own content
already exercises it — Winter's Rebuke stacks Chilled and Viper's Bite detonates
a pile — and because existing content (the Giant Rat's every-hit Poison, the
beetle's Shell Up) changes behaviour the moment the merge rule goes, whichever
milestone the change rides in on.

The duration model lands here, first, alone, and before any spell depends on it
— it is one of the two hardest contracts and it is proven in its own package
with its own migration test (Appendix B).

**Gate.**
- `powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1 combat`
- `powershell -NoProfile -ExecutionPolicy Bypass -File tools/run_tests_parallel.ps1 -BuildContent`
- `tools/preview.ps1 -Spell winters_rebuke` and `-Spell vipers_bite`, then
  **the owner looks at `tools/screenshots/preview/spell_*_impact.png`** and says
  whether the jaws read as jaws. `-Spell gilded_aegis` is **refused** by
  `preview.ps1` — `Ward` is one of the seven effects it will not stand up
  (`docs/TESTING.md`, decision table) — so Gilded Aegis is checked with
  `tools/screenshot.ps1 -Runtime` instead, and the owner looks at that.

**Acceptance, testable and self-contained.**
- A cast's power row shows the same figure resolution produces, including an
  elemental rider (`PreviewPurityTests`).
- A dodged Winter's Rebuke applies no Chill; a dodged Viper's Bite detonates
  nothing.
- Lethal damage applies no fresh status.
- A ward raised on turn N is present for the whole of turn N+1 and gone at its
  end.
- A Viper's Bite on a poisoned target produces exactly one detonation record and
  exactly one fresh Poison entry.
- Every existing content row listed in §0's table affects the same number of
  turns after the change as before it, Lucky Deck excepted and stated.
- `appliesStatus: Chilled` registers a speed malus.
- Three Poison instances tick for their sum, expire apart, draw one badge, and
  are all consumed by one detonation that is recorded once.
- A binary restriction still refreshes rather than stacking.

### Milestone B — consumption and health payment

> **STATUS: LANDED 2026-09-20**, on `workflow-2026-09-19`.
> `26b7fd9e` the mechanics, content and tests; `3573b566` the five scenes and
> the Chakra Petch font, regenerated after a stale sync-back unrelated to this
> milestone (fileID churn only, verified by identical object/component counts
> against HEAD).
>
> **What landed.** `StatusEffects.TrySpend` (1.8), generalising
> `Marks.ConsumeMark`'s own shape; `StatusCombos.SpendPoisonIfMatched`'s
> premium parameter, looped over `TrySpend` to eat a whole stacked pile in one
> consumption; `ConsumedTotalSplit`, a pure function for 1.7's odd-point-first
> split; `HealthCost`, a narrow payment facility validated alongside mana in
> one `SkillResolution.CanAfford` call and paid by a direct write outside the
> damage funnel; the `ResolveDamageInstances` `ignoresDefense` repair (1.3);
> `CanResolveSkill`'s new `requiresStatus` refusal, threaded a `target`
> parameter to ask it; `SkillEffect.Reclaim` (D10, appended after
> `HealSingle`); and Crownfall, Ashen Reckoning and Blackglass Spear as
> complete slices with placeholder `vfx.layers`.
>
> **Two bugs found in review rather than shipped.**
>
> - `ResolvedSkill.RequiresStatus`/`ConsumesStatus` were authored as bare
>   `StatusEffectType?` fields. Unity's serializer has no support for
>   `Nullable<T>`, so both came back silently `null` off every generated
>   ScriptableObject -- invisible to the whole domain test suite, which builds
>   `ResolvedSkill` by hand in C# and never serializes one through Unity.
>   Caught by inspecting the generated `.asset` YAML directly, not by a test.
>   Fixed with the `Type`/`Has*` backing-field pair `AppliesStatus` already
>   uses.
> - `SkillOptionsFor`'s `Affordable` flag did not carry `HealthCostPercent`,
>   so Blackglass Spear read as legal whenever its mana was covered even when
>   the health cost would be refused at cast time. `GreedyAggressivePolicy`
>   scores a fixed-packet skill as positive damage and calls
>   `RecordProgress` on picking it -- the repeat guard built for a
>   zero-scoring skill never engages for a "damaging" cast that is actually
>   always refused, and `BalanceBotSmokeTests` caught the resulting 60-command
>   stall (seed 5, GreedyAggressive) on the first full-gate run. Fixed at the
>   seam `SkillOptionsFor` already is; the gate was re-run afterward and
>   passed clean.
>
> **Deviations from the plan, stated rather than left implicit.**
>
> - `DealsDamage()` (the Gift: Fury spend gate) does **not** include
>   `Reclaim` -- Ashen Reckoning's damage carries no caster-scaling term for
>   Empowered to boost, so spending it on this cast would burn the player's
>   buff for nothing. Not named in the plan either way; recorded as the
>   author's reading.
> - `SkillResolution.Amount`/`PreviewSkillPower` answer `Reclaim` with `0`,
>   matching Provoke/Transform/Summon's own "nothing to preview" shape. No
>   target-aware "would this consume" number was built for the skill-detail
>   card -- `PreviewSkillPower` takes no target at all, extending its
>   signature is a bigger surface change than this milestone's scope, and
>   nothing in the acceptance list names a HUD number for it.
> - `tools/preview.ps1 -Spell ashen_reckoning` is refused outright:
>   `Scripts/Core/PreviewFight.cs` allows only eight `SkillEffect`s and
>   `Reclaim` is not one of them (the same shape Gilded Aegis/`Ward` hit in
>   milestone A). Not staged with `screenshot.ps1 -Runtime` either, for the
>   same reason milestone A gave for a harder case: building a poisoned
>   target through that harness is out of this pass's scope. Crownfall and
>   Blackglass Spear previewed cleanly; see the report for what their impact
>   frames show.
>
> **Gate.** `run_tests_parallel.ps1 -BuildContent -BuildScenes`: EditMode
> 4047/4050 (0 failed, 3 skipped), PlayMode 1276/1328 (0 failed, 52 skipped).
> The four previously-stale-scene failures
> (`FightSubmenuAffordabilityTests` x2, `ScreenWiringTests`,
> `ShopGamepadNavigationTests`) are gone. `dotnet test tools/domain-tests`:
> 4027/4030 (3 pre-existing skips), 0 failed.
>
> **Measured values.** Section 5's own subsection, below.

**Scope.** `StatusEffects.TrySpend` (1.8); the detonation premium parameter and
split (1.6, 1.7); health-cost validation and payment (1.2); the
`ResolveDamageInstances` `ignoresDefense` repair (1.3); Crownfall, Ashen
Reckoning, Blackglass Spear.

**Gate.**
- `tools/test.ps1 combat`; `tools/run_tests_parallel.ps1 -BuildContent`
- `tools/preview.ps1 -Spell crownfall`, `-Spell ashen_reckoning`,
  `-Spell blackglass_spear`; **the owner looks at the three impact frames**, in
  particular whether Ashen Reckoning's extracted poison reads as returning
  burning.

**Acceptance.**
- A preview of any of the three consumes nothing: no mark, no Poison entry, no
  RNG draw, no cast counter.
- Mana and health are validated together; a cast that can pay one but not the
  other spends neither.
- A health cost triggers no ward, no Vulnerable, no damage-received rider, no
  relic mechanic and no kill credit, and appears in no `Took` row.
- A 5% cost on 201 maximum health is 11.
- Splitting Ashen Reckoning's total across two packets produces one death
  settlement and one detonation record.
- Blackglass Spear on a magically-armoured enemy deals its packet with no
  magical-defence term and still takes typed resistance and the ward pool.

### Milestone C — initiative and formation

> **STATUS: LANDED 2026-09-20**, on `workflow-2026-09-19`.
> `2b3356ca` the scheduler, the picker, the content and the tests.
>
> **What landed.** `PullForward`/`ApplyPullForward`/`ProjectPulled` and a
> `ForecastPositionOf`/`ChargeOf` pair on `TurnOrder` (1.9); `PushBackAll`,
> the batch whose destinations are all fixed against ONE pre-pass board, with
> `PushBack` reduced to one arity of it; the four worked examples of 1.9
> written before the operation and landed as `InitiativeDisplacementTests`;
> the AOE batch delay in `ResolveDamageAll` over landed-hit survivors only;
> the two-pick target state on `FightMenuState` with `Back()` dropping one
> pick at a time; `SkillEffect.Hasten` and `SkillEffect.SwapAllies` (D10,
> appended after `Reclaim`); `advanceSlots` end to end; and Borrowed Moment,
> Gale Scythe and Palace Passage as complete slices with placeholder
> `vfx.layers`.
>
> **Four decisions the plan did not make, made here and stated.**
>
> - **`CastSkillOnPicks`, not a `CastSkill` overload.** 1.12 says "`CastSkill`
>   gains one overload taking `IReadOnlyList<CombatantState> targets`". C#
>   cannot resolve `CastSkill(0, null)` between a `CombatantState` parameter
>   and an `IReadOnlyList<CombatantState>` one, and that exact literal appears
>   at some forty call sites across the suite -- every Self and Party cast in
>   the game. Overloading would have turned each into a compile error and
>   every future one into a trap. The requirement 1.12 was actually stating
>   ("there is still exactly one dispatcher") is met: both names reach one
>   body, and the single-target door is a list of one.
> - **The bot SKIPS a two-pick cast, explicitly.** A `FightAction` carries one
>   `Target`; giving it a second field that exactly one skill in the game
>   would ever use is the hardcoded slot `docs/CODE_STANDARDS.md` section 10
>   refuses. `FightAction.LegalActions` therefore drops any effect whose
>   `SkillEffects.PicksRequired` is above one, with the reason written at the
>   line. Skipped rather than offered-and-refused, which is milestone B's
>   lesson: a policy that scores an action positively and never gets to spend
>   it picks it again forever, and `BalanceBotSmokeTests` found that as a
>   60-command stall rather than as a wrong answer. The cost is stated too --
>   Palace Passage is outside the balance bot's reach and its value is
>   measured by the focused harness instead.
> - **The first pick is shown by its plate going INERT**, not by a "chosen"
>   marker. A fourth plate state is a screen-tree change and this milestone
>   did not take one; the prompt carries the count instead ("Choose ally 1 of
>   2"). Named as a limitation rather than left to be noticed.
> - **`EligibleAllies` gained a board-state filter for `Hasten`.**
>   `AllyTargeting` answers what an effect accepts and has no schedule to
>   read; "is there anywhere earlier for this ally to go" is board state, like
>   reach, and belongs to the session. One predicate (`CanAdvanceInOrder`),
>   two readers -- the rack's plates and `CanResolveSkill`'s refusal -- so the
>   menu cannot offer a pick the cast then turns down.
>
> **Three bugs found in review rather than shipped.**
>
> - **An EMPTY pick list was accepted.** `foreach` over an empty list checks
>   nothing, so a Ward handed `new CombatantState[0]` sailed past 1.1 step 2
>   and landed on the caster -- exactly the auto-pick AUDIT #147 removed.
>   Normalised at the top of `CastSkillOnPicks`; caught by
>   `SkillDispatchTests.ACastHandedNoTargetAtAllIsRefused_ThroughEitherDoor`.
> - **The pad could not complete a two-pick cast at all.** Submit with nothing
>   hovered presses `PickableAllyPlates()[0]`, and that list did not exclude
>   the ally pick 1 had already taken -- so the second press landed on the
>   same plate, was refused as "already going", and a whole input method was
>   quietly unable to cast one spell. Fixed at the shared helper, so the
>   cursor, the marker and Submit skip the taken ally together.
> - **Gale Scythe asked for three ground renderers.** Its placeholder dust
>   layer authored `sort: "ground"` on an `AllEnemies` sweep, which needs one
>   per body against `FightHudSpec.SpellGroundRenderers`'s two.
>   `SpellPoolCapacityTests` caught it; the layer moved to `effects`.
>
> **Deviations from the plan, stated rather than left implicit.**
>
> - **1.9's worked example (d) says "E2 is removed from the order". It is
>   not.** Nothing in this game calls `TurnOrder.RemoveCombatant` on a death;
>   `Project` filters the defeated out of what it REPORTS and still simulates
>   them, because they still consume a turn in the real schedule. The
>   arithmetic the example states is unaffected -- E2's 88 is not STRICTLY
>   below E1's 88, so it was never a level either survivor could fall past --
>   and `InitiativeDisplacementTests.AMultiTargetDelayAfterADeath_-
>   DisplacesEverySurvivorOnce` asserts the dead entry's charge is untouched
>   rather than asserting it is gone.
> - **The current-actor guard is new behaviour for `PushBack`.** 1.9 rule 2
>   says the current action is never moved, and nothing enforced it before;
>   `PushBack` would have accepted the acting combatant. No caller ever passed
>   one, so this closes a hole rather than changing a behaviour, but it is a
>   contract change to an existing method and is pinned by
>   `NeitherOperationAcceptsTheCurrentActor`.
> - **`queuePushSlots` is now legal on a `DamageAll` row.** The resolver
>   refused it on anything but `DamageSingle`, and the refusal was honest at
>   the time: the AOE branch applied status but never the push. It does
>   something now, so the content rule follows the code.
> - **`TurnOrder.ChargeOf` and `CombatEncounter.ChargeOf` are new read-only
>   accessors with no production caller.** The displacement contract is
>   written in charges -- every one of 1.9's worked examples states its answer
>   as one -- and at the start of an encounter every charge is seeded from
>   Speed, so one displacement level is a handful of points that reorders
>   nobody yet. A test that could only read the ORDER would pass whether the
>   delay landed on the right enemy, the wrong one, or none. Recorded as a
>   deliberate test-facing read rather than smuggled in.
> - **No picture.** Milestone C's gate asks for `tools/preview.ps1 -Spell
>   gale_scythe` and `tools/screenshot.ps1 -Runtime`, and neither was run: a
>   Unity Editor was open on this project for the whole pass (pid 27916 plus
>   its import workers), which is the exact configuration that cost milestone
>   B its `Resources/Content/` tree. `preview.ps1` routes through an open
>   Editor when it finds one, and a content build through stale assemblies is
>   what wiped that tree. The pictures are deferred; the acceptance they were
>   to carry is covered by tests, and what a still could not have shown -- the
>   tracker sliding -- is pinned numerically instead.
> - **The owner did not confirm the pad path by hand.** It is proved instead
>   by `PalacePassagePadTests`, four PlayMode cases driving the REAL
>   `NavigationInputModule` through a scripted `BaseInput` on
>   `inputOverride` -- the gamepad plan's own section 10 shape, on the seam
>   its section 2 verifies API by API. What that cannot prove is repeat
>   cadence (`Time.unscaledTime`, no `BaseInput` equivalent), which the
>   gamepad plan already records as hardware-acceptance-only.
>
> **Gate.** ``run_tests_parallel.ps1 -BuildContent`: EditMode 4094/4097 (0 failed, 3 skipped), PlayMode 1280/1332 (0 failed, 52 skipped) -- the four new cases in PlayMode are `PalacePassagePadTests`. `-BuildScenes` was NOT needed and not run: no `[SerializeField]` and no screen tree changed. The one new `UiStrings` entry is set at runtime (`RefreshTargetPrompt`); the label `FightScreen` bakes for layout is still `TargetPrompt`, so `UiAudit` has nothing new to solve. `dotnet test tools/domain-tests`: 4067/4072, 3 pre-existing skips, 0 failed.`
>
> **Measured values.** Section 5's own subsection, below, and the headline is
> not about either spell: **on the board a fight opens on, no displacement
> moves anybody** -- not an advance, not a pull to the front, not a delay --
> because charges are seeded from Speed and every actor still owes seventy to
> ninety ticks. Gift: Haste has had this property since it shipped.


**Scope.** `PullForward`/`ApplyPullForward`/`ProjectPulled` (1.9); the
forecast-position definition and the four worked examples as tests; the AOE
batch displacement; the two-pick target state (1.12); Borrowed Moment, Gale
Scythe, Palace Passage.

The four scheduler examples in 1.9 are written **before** the operation, as the
owner's brief requires, and land as tests in this package.

**Gate.**
- `tools/test.ps1 combat`; `tools/run_tests_parallel.ps1 -BuildContent`
- **`-BuildScenes` as well** if the ally picker's tree changed — it is a screen
  tree under `Domain/UiKit/Screens/`, so `UiAudit` re-solves it at build time.
- `tools/preview.ps1 -Spell gale_scythe`. Borrowed Moment and Palace Passage
  are not damage spells and their acceptance is the tracker and the rack, so
  they are checked with `tools/screenshot.ps1 -Runtime`; **the owner looks at
  the tracker sliding and at both doors**.
- Pointer, keyboard and gamepad each drive the two-pick selection once by hand;
  **the owner confirms the pad path**, because the pad is the one input this
  project has repeatedly found unverifiable from tests alone.

**Acceptance.**
- A canceled selection spends nothing: no mana, no cooldown, no free-action
  lock, no swap.
- Initiative entries are never duplicated or lost by any displacement.
- Health and statuses stay attached to actors across a swap, not to slots.
- Keyboard, gamepad and pointer all complete a two-ally swap and all cancel it
  at both pick depths.
- An advance never grants an extra action and never changes `Rate`.
- A multi-target delay after a multi-kill displaces every survivor once and
  references no dead entry.

### Milestone D — physical-action restriction

> **STATUS: LANDED 2026-09-20**, on `workflow-2026-09-19`.
> `2c64a252` the classification, the predicate, the content and the tests.
>
> **What landed.** `physicalMove` end to end (`RawSkillEntry` ->
> `SkillEntryResolver` -> `ResolvedSkill`), authored on all 23 damage rows and
> on the ten spell-expansion rows; the audited classification of every id as a
> literal table in `PhysicalMoveAuditTests`; `CombatActions.IsLegalFor`, the
> one predicate, read at the four sites 1.10 names; the content lint, refusing
> a damage row that states nothing; `SkillEffect.Afflict`; and Velvet Shackles
> as a complete slice with placeholder `vfx.layers`.
>
> **Rooted itself gained the restriction** (2.10), rather than a distinct
> status beside it. **One existing source is affected**, and only one: the
> `Sylvan` item modifier's `RootChancePercent` (10% on a landed hit,
> `FightTuning.RootOnHitTurns` = 1). It is a weapon modifier, so it only ever
> roots an ENEMY. The balance effect, per enemy with an authored kit: `golem`
> (boulder_slam, `attackWeight` 0) and the eleven monsters with no abilities
> at all now forfeit the rooted turn outright where they previously lost only
> a swing they mostly did not have; `forest_warden` is reduced to `roar`,
> `treant` to `spore_cloud`, `beetle` to `shell_up`, and `bog_witch` is
> untouched because its whole kit is a cast. Measured in section 5.
>
> **Four decisions the plan did not make, made here and stated.**
>
> - **"Omitted" is recovered by a PROBE PARSE, not by a sentinel or a
>   scanner.** D7 asks for a `bool physicalMove` whose omission is refused on a
>   damage row, and a bool has no spare value to reserve the way this file's
>   `-1`/`""` sentinels do. `ContentBuilder` therefore deserialises
>   `skills.json` a second time into a probe type whose default is the
>   OPPOSITE, and the two parses agree on exactly the rows that stated a value;
>   the answer is stamped onto `[NonSerialized] RawSkillEntry
>   .physicalMoveOmitted` and the REFUSAL lives in `SkillEntryResolver` beside
>   every other skill refusal. Nothing hand-parses JSON, and the answer comes
>   from the same deserialiser production uses. The stamp defaults to "stated"
>   so an entry built in code -- every resolver test, every fixture -- is never
>   accused of omitting a field it had no file to omit it from.
> - **The vacuity guard is split.** The build-time guard is a SHAPE check (the
>   probe must return the same rows in the same order, or nothing is parsed),
>   because a literal row count in a build refusal would fail the build for
>   deleting a skill. The literal -- 23 damage rows -- is pinned in
>   `PhysicalMoveAuditTests`, which is where `docs/CODE_STANDARDS.md` section 8
>   puts literals: it is a test-rules section.
> - **`SkillEffect.Afflict` APPENDS behind `SwapAllies`.** D10 lists it first
>   of the five; `Reclaim`, `Hasten` and `SwapAllies` landed in milestones B
>   and C while it waited for D, so the plan's ORDER was already spent. The
>   rule the generated assets actually depend on -- "nothing is inserted or
>   reordered" -- is the one kept. Ordinal 18, pinned in
>   `ContentEnumOrdinalTests`.
> - **The forfeiture fallback now answers for the PLAYER too.**
>   `ResolveSkippedTurn`'s Rooted arm was gated on `!IsPlayerSide` because
>   `SourceFor(player)` is null and the query would have read "helpless"
>   unconditionally. Under Shackles a rooted character's plain attack is
>   illegal as well, so "Attack is always there" stopped being what ends the
>   turn, and the gate is replaced by a real player-side answer
>   (`RootedPlayerHasNoLegalAction`: no legal swing, no ready-and-legal skill,
>   no legal Move). It counts READINESS, not only legality, because a
>   legality-only answer would leave a rooted character whose only legal casts
>   are unaffordable staring at a menu of dead rows with no way to end the
>   turn. **Stated limitation:** the satchel is invisible to the session
>   (`UseConsumable` is handed one item, never the stock), so in principle a
>   forfeit could step over a potion. It cannot happen in live content -- every
>   shipped character carries a non-physical skill and nothing authors an
>   enemy-side root -- and if an enemy ever authors one, the satchel has to
>   reach the session first.
>
> **One bug found rather than shipped, and it was not milestone D's.**
> **The turn-end clock only ran on PLAYER turns.**
> `TickStatusesAtTurnEnd` had exactly one caller, inside `AdvanceAfterAction`,
> which only the four player commands reach; a monster's turn and every skipped
> turn on either side advance through `StepToNextTurn`, and an egg's through
> `AutoResolveEggTurns`. So no `AtTurnEnd` status on a monster ever aged, and
> no forfeited turn aged anything. That was invisible while wards were the only
> thing on this clock and became live in **milestone A**, when D1 moved Protect,
> Vulnerable, Chilled, Rooted and Marked onto it: **Winter's Rebuke's two-turn
> Chill was permanent.** Milestone D's root would have been permanent and
> self-sustaining on top of that, because a rooted monster with nothing legal
> forfeits and a forfeited turn aged nothing. Found by
> `VelvetShacklesTests.TheSecondOfTwoShackledTurns_IsStillRestricted` failing on
> its THIRD turn. Fixed by `FightSession.EndTurnStatusesForCurrent`, one seam
> sitting immediately before all three `_encounter.AdvanceTurn()` sites, with
> the extra-action exemption (AUDIT #113) and a corpse guard. `RootedStatusTests`
> pinned the old behaviour at 5 turns remaining after a forfeit; it now pins 4,
> which is the one turn that was actually spent.
>
> **A second content rule had to move, for one authored number.**
> `StatusAuthoring` required a positive `statusMagnitude` from any row naming a
> status, and Rooted has no magnitude -- `grapple` had been satisfying the rule
> with a `statusMagnitude: 1` that nothing reads, and Velvet Shackles would have
> been the second. `StatusEffects.CarriesMagnitude` is the third table beside
> `DurationClock` and `StackPolicyOf`, both halves of the rule read it (required
> where there is one, refused where there is not), and `grapple`'s 1 is gone.
> Not the same line `StackPolicyOf` draws: Empowered and Feared both refresh
> rather than stacking and both carry a real number.
>
> **`EnemyShowcase` needed nothing.** A blocked ability is ZERO-WEIGHTED in
> place rather than removed from the pool, which is the treatment the summon cap
> and the reach gate already get, and `EnemyShowcase.Next` already names and
> skips a zero-weight entry. `tools/preview.ps1 -Enemy forest_warden` was NOT
> run: a Unity Editor is open on this project (pid 27916) and routing the
> preview through an open Editor wiped `Resources/Content/` once before.
>
> **Three AUDIT entries.** #193 records the turn-end clock hole above, struck
> with the same commit. #194 is an owner's call: Velvet Shackles denies a caster
> nothing and hands a beetle a heal. #195 records that Rooted's new meaning
> retuned the Sylvan item modifier without anybody editing it. #84 is unchanged
> but now cheaper -- this milestone settled the "ROOTED" wording it was waiting
> for, on the skill rows rather than on the Move row it names.

**Scope.** The `physicalMove` field, the audited classification of all 36
existing rows plus the plain attack (1.10), the shared legality predicate and
its four callers, the content lint; Velvet Shackles.

**Gate.**
- `tools/test.ps1 combat`; `tools/run_tests_parallel.ps1 -BuildContent`
- `tools/preview.ps1 -Enemy forest_warden` to watch a shackled boss run its kit
  and skip what it may not do; **the owner looks at the per-turn frames**.

**Acceptance.**
- Every physical skill is blocked for a shackled actor and every legal
  non-physical action remains available, on both sides.
- An obsolete committed intent is voided at resolution rather than silently
  resolving.
- The no-legal-action fallback still forfeits the turn, with no Guard mechanic.
- The second of two shackled turns is still restricted.
- Every damage row in content states its classification explicitly, and the
  lint refuses a build where one does not.

### Milestone E — persistent damage

**Scope.** The new-DoT model (1.5), `TickReport` as typed rows (D5, AUDIT #188),
the post-action hook (1.11); Censer of Embers, Thorn
Tithe.

**Gate.**
- `tools/test.ps1 combat`; `tools/run_tests_parallel.ps1 -BuildContent`
- `tools/preview.ps1 -Spell censer_of_embers` and `-Spell thorn_tithe`;
  **the owner looks at the impact and at the held state**, and separately at a
  `tools/screenshot.ps1 -Runtime` capture of a burn tick, because a tick is a
  beat now and a static capture cannot show it.

**Acceptance.**
- Affinity is applied consistently on every tick of a given DoT, and no flat
  defence is subtracted on any of them.
- A third retaliation opportunity exists on the third affected turn.
- A dead actor cannot act and therefore pays no retaliation.
- Attribution survives the caster's death: the status still names its source and
  the tick still lands.
- Tooltips show the actual tick strength, and a recast shows the new one.
- One tick carrying two different damage types reports both, separately.

**STATUS: LANDED, 2026-09-20.** `dotnet test tools/domain-tests`: 4122
passed, 0 failed, 3 skipped (pre-existing, unrelated — two `[Ignore]`d
balance-history assertions and one summoned-body reward case).
`tools/run_tests_parallel.ps1 -BuildContent`: EditMode 4139/4142 passed (3
skipped), PlayMode 1280/1332 passed (52 skipped), 0 failed, run before the
three balance-harness tests below were added (dotnet-verified afterward
against the identical shared source — see §7's own note). `tools/test.ps1
combat`'s Unity half caught one real authoring bug before the full gate: an
early `censer_of_embers` `vfx.layers` entry named `travelSeconds` while
placed `target`, which `SpellVfxRecipeDriftTests`' own resolver refuses
("a projectile leaves the caster") — fixed to `caster`. §1.11 above is
corrected in place rather than superseded, since the plan's own claim about
where the hook lives was simply wrong; see that section for the seam(s) it
actually uses. §5 below records the measured values.

### Milestone F — Court and combined control

**Scope.** `SkillEffect.Enthrall`, the boss/ordinary branch, the caster's
Vulnerable, the targeting info that shows the boss fallback; Court of Whispers;
the party-wide control-loop harness.

**Gate.**
- `tools/test.ps1 combat`; `tools/run_tests_parallel.ps1 -BuildContent`
- `tools/preview.ps1 -Spell court_of_whispers` against an ordinary field and
  against `hollow_choir`; **the owner looks at both**, because the boss fallback
  is the case a player will misread.

**Acceptance.**
- Ordinary enemies skip exactly one turn and take the Fear's Vulnerable; bosses
  take the delay and no Fear.
- The boss fallback is visible in the targeting info before the cast, not after.
- The caster takes Vulnerable whether or not anything landed.
- Alternating casters cannot deny every enemy turn across five rounds of the
  representative late encounter; if they can, costs or cooldowns change, or a
  narrowly specified repeat-control resistance lands, **before** this milestone
  closes.

---

## 5. Balance gate

Run after each milestone. **Investigate, do not auto-tune**: a measurement that
looks wrong is a question for the owner, and the spec is updated with measured
values before final art.

### Parties and encounters

Named from live content (`characters.json`, `enemies.json`, re-read this pass —
16 enemies, 2 of them bosses).

| Band | Party | Encounter |
|---|---|---|
| Early | Shawn alone, level 1, no relics, one book slot filled | `rat` ×2; then `wolf` + `spider` |
| Middle | Shawn + Odette, mid-level, two books each, two relics | `golem` + `imp` + `bog_witch`; then `beetle` + `treant` |
| Late | Shawn + Odette + Bjorn, three books each, full relics | `forest_warden` (boss, with its `roar` summons); then `hollow_choir` (boss) + `ember_hound` ×2 |

`bog_witch` is the one enemy carrying a Poison attack type and is the Poison
band's natural subject; `hollow_choir` and `forest_warden` are the only two
bosses and are therefore the whole of Court of Whispers' boss-fallback sample.
`golem` (speed 3) and `treant` (speed 3) are the slow end; `gloom_moth` (16) and
`crystal_bat` (15) the fast end — the two ends the initiative spells must be
measured against.

### Metrics

Per action and per mana, both, for every comparison:

- damage or protection delivered;
- time to kill (actions, not seconds) and party health preserved;
- **enemy actions allowed during a control rotation** — the metric the control
  loop lives or dies on;
- value against ordinary / elite / boss, reported separately;
- low-investment versus high-investment caster (the same spell at spell-Attack 4
  and at 90, the two anchors `PhaseFourSkillTests` already uses for wards);
- whether the spell earns one of three slots against the four existing books.

### The owner's paired comparisons

| Pair | Where the numbers come from |
|---|---|
| Gilded Aegis vs existing protection (`fleece_ward` 50 flat, `prism_ward` 20 + spell attack, `placeholder_brawler_ward` 20% max HP, Bull's Horn 50% for 2) | **focused harness.** `tools/bot.ps1` reports damage and survival but not points-of-shield-per-mana; a short EditMode harness over `SkillResolution.Amount` at the two anchor casters is the honest instrument |
| Winter's Rebuke vs direct damage and vs queue control | **`tools/bot.ps1`** — damage per mana and time to kill are in `runs.jsonl`/`summary.json` (`docs/BOT_SUMMARY_SCHEMA.md`); the queue-control half needs the enemy-actions-allowed counter, which is the harness |
| Ashen Reckoning vs Bite recasting and vs ordinary detonation | **focused harness.** Three scripted lines — Bite/Bite/Bite, Bite/wait/Reckoning, Bite/Nature-hit — over the same target, comparing total damage and mana. Too specific for the bot's policy to produce reliably |
| Blackglass Spear vs Lightning Bolt, on a defended and on a fragile enemy | **`tools/bot.ps1`** for the aggregate, plus two scripted casts against `rust_knight` (high defence) and `gloom_moth` (fragile, fast) for the literal figures |
| Thorn Tithe vs direct damage, on fast physical enemies and on slow casters | **focused harness.** The whole point is action *frequency*, so the pair is `crystal_bat`/`gloom_moth` (fast, physical) against `treant`/`golem` (slow); the bot's aggregate hides exactly this |
| Court of Whispers + Borrowed Moment + Gale Scythe + Chill + Rooted, alternating casters | **focused harness**, and it is the `ControlLoopTests` fixture from §3 run long. Count enemy actions allowed per round over five rounds with two and three casters |

### Measured values — milestone A, 2026-09-20

Instrument: `Assets/_Project/Scripts/Tests/EditMode/Combat/SpellExpansionBalanceTests.cs`,
a focused EditMode harness. `tools/bot.ps1` was **not** used for these two
pairs and the reason is the one §5 already gives: it reports damage and
survival, not points of shield per mana, and it cannot script "the same spell
at spell-Attack 4 and at 90". The figures below are pinned as literals in that
file, so moving one costs a visible edit here and there.

**Gilded Aegis vs existing protection.** Ward points, at the two anchor casters
`PhaseFourSkillTests` already uses:

| caster | Gilded Aegis (7 mana) | `prism_ward` (8 mana) | `fleece_ward` (2 wool) |
|---|---|---|---|
| spell attack 4 | **22** (3.1 / mana) | 24 (3.0 / mana) | 50 |
| spell attack 90 | **108** (15.4 / mana) | 110 (13.8 / mana) | 50 |

Verdict: **neither dominated nor dominating, number stands at `prototype` 18.**
It is a shade under `prism_ward` at both ends for one mana less, and it carries
a two-turn cooldown `prism_ward` does not — marginally better per point of
mana, strictly worse per cast, which is the trade the cooldown is there to buy.
Against `fleece_ward`'s flat 50 it is worse early and twice as good late, so it
crosses rather than replaces.

**Winter's Rebuke vs direct damage.** One cast against an undefended target,
variance off:

| spell | packet | mana | landed | per mana |
|---|---|---|---|---|
| Winter's Rebuke | Frost 6 | 8 | **6** | 0.75 |
| `mud_burst` | Earth 6 | 8 | 6 | 0.75 |
| `lightning_bolt` | Lightning 10 | 11 | 10 | 0.91 |

Both anchors read the same, because a fixed packet does not scale with the
caster's attack — existing behaviour of every `damageInstances` book, not
something this spell introduces.

**Finding, reported rather than tuned:** Winter's Rebuke is *exactly*
`mud_burst`'s packet for *exactly* `mud_burst`'s mana, and it is authored a
tier higher. The damage half of it earns nothing over a cheaper book. It is not
strictly dominated — it carries a two-turn Chilled where mud_burst carries a
one-turn Vulnerable — so under this section's own rule the number was left
alone. **Owner's call:** raise the packet, drop the tier to 1, or accept that
the Chill is what the tier buys.

**Winter's Rebuke vs queue control.** Enemy actions allowed, read off
`CombatEncounter.UpcomingTurns` over a 30-turn forecast. Hero speed 10, one
enemy at speed 20:

| chill on the enemy | enemy turns in 30 | actions denied |
|---|---|---|
| none | 17 | — |
| one at 25% | 16 | 1 |
| two at 25% | 15 | 2 |
| three at 25% | 12 | 5 |

**Finding, and the one worth carrying to the owner: a 25% Chilled does not deny
25% of an enemy's actions — it denies about 6% of them.** The badge says -25%
Speed and that is true; `SpeedScale`'s charge curve is deliberately sub-linear
with a hard ceiling (its own header records why), so a quarter off the Speed
number is far less than a quarter off how often the actor acts. The effect only
starts to bite at the third stack. That is an argument *for* the stacking model
rather than against the percent, and it means the "Chilled 25%" line in every
spell entry below promises more than it delivers to a player reading it as an
action tax. Not tuned; 25% is the owner's `prototype` figure and the
measurement says it is weak, not wrong.

**Viper's Bite** has no §5 pair of its own — its comparison (against Ashen
Reckoning and against ordinary detonation) is milestone B's, because two of the
three scripted lines need spells B introduces. Its milestone A behaviour is
pinned by `VipersBiteTests` instead.

### Measured values — milestone B, 2026-09-20

Instrument: `SpellExpansionBalanceTests.cs`, extended with B's two pairs.

**Ashen Reckoning vs a Bite recast vs an ordinary Nature detonation.** One
Bite cast (packet 3, leaves Poison 3/3), then one of three second casts
against exactly that stack, undefended target, variance off:

| line | 2nd cast damage | total (2 casts) | mana (2 casts) |
|---|---|---|---|
| Bite / Bite | 12 (packet 3 + detonation 9) | 15 | 14 |
| Bite / Ashen Reckoning | 14 (150% of 9, rounded up) | 17 | 17 |
| Bite / ordinary Nature hit | 19 (packet 10 + ordinary 100% detonation 9) | 22 | 13 |

**Finding, reported rather than tuned.** On a single 3/3 stack, Reckoning
beats a plain Bite recast (14 vs 12) for three more mana, but a fixed Nature
packet that happens to detonate the same pile deals more still (19) for
*less* mana than Reckoning — because that packet's own 10 is bigger than
either Poison-shaped number. The 150% premium is Reckoning's only lever over
an ordinary 100% detonation, and half of a single 3/3 stack's worth (9) is
4.5, which does not close a ten-point packet gap. This is not evidence
against the spell: its case is the pile a single Bite cannot reach in one
turn — three Bites deposit three independent instances, and one Reckoning
detonates their SUM at 150% in one hit, plus the Vulnerable it leaves behind
— neither of which a single-stack comparison exercises. Left at the
prototype 150; the owner's call is whether the premium should rise to make a
one-stack Reckoning competitive with a bigger fixed packet on its own terms,
or whether its case is deliberately the multi-stack one.

**Blackglass Spear vs Lightning Bolt**, against the owner's own two named
enemies' authored broad defense (`enemies.json`), undefended-elsewise,
variance off:

| target | phys def | mag def | Blackglass (Void 13) | Lightning Bolt (Ltng 10) |
|---|---|---|---|---|
| `rust_knight` (defended) | 35 | 5 | 13 | 9 |
| `gloom_moth` (fragile) | 5 | 10 | 13 | 9 |

**Finding, reported rather than tuned.** `rust_knight`'s own "high defence"
is *physical* (35); its MagicalDefense is a modest 5, and both compared
spells are already non-physical, so `ignoresDefense` buys Blackglass Spear a
small edge here (13 vs 9) rather than a dramatic one — the bypass matters
most against a magically-armoured body, which neither of the owner's two
named enemies actually is. The two targets reading identically for Lightning
Bolt (9 and 9) is a rounding coincidence of `CombatMath.AfterResistance`'s
integer division at this packet size (10×100/105=9.52 and 10×100/110=9.09
both floor to 9), not a claim that the two bodies resist alike. What
"fragile" actually changes — `gloom_moth`'s low 45 max health and its speed —
is a time-to-kill question this harness does not model; that half of the
pair is `tools/bot.ps1`'s. If the owner's intent for "a defended enemy" is a
body with real *magical* defense, none in the shipped roster clears single
digits except `ember_hound`/the beetle's low tens, and the comparison would
read very differently against one of those.

### Measured values — milestone C, 2026-09-20

Instrument: `SpellExpansionBalanceTests.cs`, extended with C's two pairs. The
queue-control half reuses milestone A's own instrument and milestone A's own
board — enemy actions allowed inside a 30-turn `UpcomingTurns` forecast, hero
speed 10 against one enemy at speed 20 — so Gale Scythe's delay and Winter's
Rebuke's Chill are two answers to one question rather than two numbers that
happen to sit near each other. `tools/bot.ps1` was not used, for the reason §5
already gives: it reports damage and survival, not actions denied, and it
cannot script "the same board with and without one cast".

**Gale Scythe vs queue control.** Enemy actions allowed in 30 turns:

| board | no cast | 1 cast | 2 casts | 3 casts |
|---|---|---|---|---|
| hero 10 vs one enemy at 20 (milestone A's board) | 17 | **17** | 17 | 16 |
| party of three at 10 vs two enemies at 10 | 12 | **10** | — | — |
| *for comparison, Chilled 25% on the first board* | 17 | 16 | 15 | 12 |

**Finding, reported rather than tuned: a delay and a Chill are complements,
and the difference is what each one moves.** A Chill lowers the *rate*, so it
keeps paying for its whole duration and pays most against a fast actor. A
delay is a one-off charge nudge, so a faster enemy earns it straight back —
three Gale Scythes against a speed-20 enemy deny a single action, where one
Winter's Rebuke denies one. What Gale Scythe has instead is **breadth**: on an
evenly matched field it denies about one action per enemy it hits, and it hits
everybody, so two enemies is two actions denied for one 11-mana cast and three
would be three. The owner's call is whether the card should say so. As
authored it reads as a tempo tool ("each one loses a place") and measures as a
crowd tool; the honest one-line description of what it buys is *one action off
each enemy you are keeping pace with*.

**Borrowed Moment vs Gift: Haste.** The ally's forecast position and its turn
*count* inside a 30-turn window, measured on a mid-fight charge landscape
(charges 80 / 80 / 70, ally at 55, caster mid-action, every rate 1.0):

| treatment | ally's position | ally's turns in 30 |
|---|---|---|
| nothing | 4 | 6 |
| Borrowed Moment, 1 slot | 3 | 6 |
| Borrowed Moment, 2 slots (as authored) | **1** | **6** |
| Gift: Haste | 1 | 6 |

**Actions gained per mana is 0.00, and that is the contract holding rather
than the spell failing** (1.9 rule 3): an advance buys a position, never a
turn. Six turns before, six after, at 8 mana a cast. The honest unit is
**0.375 positions per mana**, which is what the tooltip already promises. The
second slot is what reaches position 1: slot 1 clears the 70, slot 2 clears
the 80 level, which holds two combatants and is therefore worth two positions
at once.

**Finding, reported rather than tuned: Borrowed Moment lands in exactly the
same place as Gift: Haste here, and Gift: Haste costs no mana.** What the 8
mana buys is *access* — Gift: Haste is a Fragile Lamb talent paid for in wool,
on one character, one strand deep, while Borrowed Moment is a book any caster
can hold. On a board with three or more charge levels above the target it buys
strictly less movement than the talent does, because `PullToFront` clears every
level in one step. Left at the prototype 8 mana / 2 slots; the owner's call is
whether a book that matches a talent's effect at a mana price is the trade
intended, or whether the slot count should rise.

**The finding that matters more than either table, and it is about the
scheduler rather than about milestone C.** On the board a fight *opens* on,
**no displacement moves anybody at all** — not an advance, not a pull to the
front, not a delay. Charges are seeded from Speed (`TurnOrder.Start`), so the
whole field sits between 0 and 40 against a threshold of 100 and every actor
still owes seventy to ninety ticks; a few points of charge is nothing against
that. Measured: an ally fourth in the opening forecast is still fourth after a
two-slot advance *and* after a `PullToFront`. This is pinned as
`SpellExpansionBalanceTests.AtTheOpeningOfAFightNoDisplacementMovesAnybody`,
with Gift: Haste included in the row precisely so it cannot be read as
something this milestone introduced — Gift: Haste has had this property since
it shipped, and what makes it feel immediate in play is its
`GiftAppliesImmediateTurn` talent rather than the pull. **Consequence for every
initiative spell in this plan:** they are turn-two-onwards tools, and a player
who opens with one will see nothing happen. Not tuned, and not fixable inside
a spell — the lever, if the owner wants one, is the seed itself.

### Measured values — milestone D, 2026-09-20

Instrument: `SpellExpansionBalanceTests.cs`, extended with the metric section 5
calls "enemy actions allowed during a control rotation" read from the other end
— actions DENIED. A root is a different question from a delay or a Chill and
needs a different instrument: those change WHEN an enemy acts and are measured
against a forecast window, while a root changes WHETHER it acts, so its turns
are run and the ones that resolved into nothing are counted. `tools/bot.ps1` was
not used, for section 5's own reason.

The kits mirror `enemies.json` in the only two things this measures — which
abilities are in the draw, at which weights, and whether each is a physical
move. `bog_mud_burst`'s authored `reachSlots: [2,3]` is deliberately omitted:
against the harness's one-hero party it would zero-weight the cast for having
nothing in reach, which would measure the front-rank rule instead of the root.

**Enemy turns that resolved into an action, out of six:**

| enemy | kit | no root | shackled |
|---|---|---|---|
| `crystal_bat` (speed 15) | plain swing only | 6 | **0** |
| `golem` (speed 3) | `boulder_slam` only, `attackWeight` 0 | 6 | **0** |
| `bog_witch` (speed 8) | swing (w3) + `bog_mud_burst` (w2) | 6 | **6** |

As authored — two effective turns for 9 mana — that is **2 actions denied**
against a physical-only enemy and **0** against a caster.

**Finding, reported rather than tuned: speed is not the axis, kit composition
is.** The plan's own parties section names the fast end (`gloom_moth` 16,
`crystal_bat` 15) and the slow end (`golem` 3, `treant` 3) as the ends an
initiative spell must be measured against, and for a root they turn out not to
be the relevant ends at all — a turn denied is a turn denied whether it came
quickly or slowly. What decides Velvet Shackles' value is whether the target has
anything non-physical to fall back on:

| enemy | what a shackled turn becomes |
|---|---|
| eleven rows with no `abilities` list | forfeit |
| `golem` | forfeit (`attackWeight` 0, and `boulder_slam` is physical) |
| `forest_warden` (boss) | `roar` only — it has no plain swing either (`attackWeight` 0) |
| `treant` | `spore_cloud` only |
| `beetle` | `shell_up` only |
| `bog_witch` | unchanged — its whole kit is a cast |

So against thirteen of sixteen rows the spell removes a turn, and against three
it converts one. **The owner's call is whether converting is enough.** Two of
the three conversions are a downgrade for the monster (`forest_warden` trades a
40-attack slam for a summon it may not have room for; `treant` trades a lunge
for an AOE), but `beetle`'s is arguably an upgrade: `shell_up` is a heal, and a
shackled beetle spends its two turns healing rather than rolling. Left alone;
the levers if the owner wants one are the mana cost, the duration, or making the
restriction cover a named non-physical ability class as well — and the third is
a model change, not a number.

**A second finding, about the existing source rather than the new spell.**
Rooted gaining "no physical moves" retunes the `Sylvan` modifier without anyone
touching it: a 10% on-hit root for 1 turn used to cost a monster a plain swing,
which the five monsters with authored kits mostly were not going to take anyway
(`golem` and `forest_warden` author `attackWeight: 0`). It now costs `golem` and
`forest_warden` their whole turn and their best ability respectively. That is a
buff to one item modifier, delivered by a rule change, and it is recorded here
rather than in a tuning pass because nothing in `modifiers.json` changed.

---

### Measured values — milestone E, 2026-09-20

Instrument: `SpellExpansionBalanceTests.cs`, three new EditMode harnesses
(`CensersThreeCastStack_OutpacesThreeLightningBoltsPerMana`,
`ThornTitheVsDirectDamage_OnFastAndSlowPhysicalTargets`,
`ThornTitheAgainstAPureCaster_EarnsOnlyTheOpeningTicks`). `tools/bot.ps1` was
not used, for the same reason section 5 gives for every scripted-line pair:
these need exact figures over a fixed sequence of casts, which a policy
cannot reliably reproduce.

**Censer of Embers' three-cast stack vs three Lightning Bolts, undefended
target, variance off:**

| spell | mana | total damage | per mana |
|---|---|---|---|
| Censer x3 (stacked) | 24 | 36 | 1.5 |
| `lightning_bolt` x3 | 33 | 30 | 0.91 |

Three stacked Burns beat three Lightning Bolts by a wide margin per mana
**and** in absolute total, for less mana spent. Cooldown 2 means "turn one
then turn three" (`SkillCooldownTests`' own words), so three casts actually
span five of the caster's own turns — the casts do not overlap freely, and it
does not matter: each instance still ticks three times at its own
4-magnitude snapshot regardless of how the other two are timed (D3's
independence), so the total is exactly `3 instances x 4 magnitude x 3 ticks =
36` no matter what cadence the cooldown imposes. **Reported rather than
tuned.** A DoT that stacks without a ladder or a cap earns exactly the
property D3 itself names — "an applier adding one instance per turn
plateaus... it does not run away" — but three instances across five turns is
well short of that plateau, and the comparison this pair asks for is a
snapshot at three, not the asymptote. The lever, if the owner wants one, is
the cooldown: raising it to 3 would space the casts to match the DoT's own
3-tick lifetime and remove the overlap entirely.

**Thorn Tithe vs direct damage, fast and slow physical targets, one 10-mana
cast run to its natural end (three affected turns):**

| target | speed | total damage |
|---|---|---|
| fast (`crystal_bat`'s own speed) | 15 | 30 |
| slow (`golem`/`treant`'s own speed) | 3 | 30 |

**Finding, reported rather than tuned: the total is identical at both ends**
— three opening ticks plus three retaliations, five each, six events of five,
not a function of speed at all in an all-physical fixture where every one of
the target's own turns both opens with a tick and resolves a physical swing.
One 10-mana cast therefore already clears a single Blackglass Spear packet
(9–14 depending on the defended enemy, `BlackglassSpearVsLightningBolt`) at
either end, for a spell that also denies nothing and costs no further action
after the first. This is the same axis milestone D already found for Velvet
Shackles: **speed is not the axis, kit composition is.**

**The real axis, measured separately: against a target with nothing physical
in its kit, the retaliation never fires at all.**

| target | total damage |
|---|---|
| physical (always swings) | 30 |
| pure caster (always casts) | 15 |

A caster-type target pays only the three opening ticks — a third less value
for the identical 10 mana. The lever this suggests, if the owner wants one,
is the same shape D9's "kit composition, not speed, decides a root's value"
finding already offered for Velvet Shackles: nothing to tune here without a
model change, since the classification (physical or not) is deliberately
binary and the retaliation is doing exactly what 2.12 asks of it.

---

**Where `tools/bot.ps1` covers it, use it** — it plays whole runs and its
numbers are a floor, not a verdict. Where the comparison needs a scripted line
rather than a policy, a focused EditMode harness is the instrument, and its
output goes into this document's spell entries as measured values replacing the
`prototype` marks.

---

## 6. Presentation and book integration, and the release gate

### Stage 5 checklist

Per spell, all thirteen:

- [ ] An authored begin / release / impact / settle sequence, as `vfx.layers`
      with `layerFormat: 1` and an explicit `hitCueSeconds`
      (`docs/ART_PIPELINE.md` §5b — the cue is authored in seconds and nothing
      derives it, which is the point of the layered format).
- [ ] Mechanics, damage popup, sound and target reaction all sync at
      `hitCueSeconds`.
- [ ] The priority visual where the owner named one: Viper's Bite's closing
      jaws, Ashen Reckoning's poison returning as burning material, Palace
      Passage's paired doors, Court of Whispers' surrounding court.
- [ ] Sheet cut with `tools/slice_spell_sheet.py`, previewed with `--preview`,
      `impactX`/`impactY` stated rather than guessed.
- [ ] Book icon under `Art/Items/SpellBooks/<Name>/`, wired through `iconPath`.
- [ ] Status icon for each new status (Burn, Thorned), following `Marks.IconKey`'s
      convention — register the key, let `LoadSprite` degrade to a blank slot
      with a warning until the art lands.
- [ ] Player-facing `description`, and the tooltip text from §2 verbatim
      (Viper's Bite's line is exact).
- [ ] `bookTier` set, and the shop offers it at that tier.
- [ ] Content validation green: `ContentBuilder` regeneration, the
      `physicalMove` lint, `ContentSchemaTests` byte-for-byte after
      `tools/content_schema.ps1`, `ContentFreshnessTests` with the regenerated
      `content_stamp.json` committed alongside.
- [ ] `Art/` diffed after the build — `LoadSprite` flips importer settings and
      writes fresh `.meta` files.
- [ ] Every new file's `.meta` committed in the same pass.

Then, once: an eligible character can learn, replace, equip, save, reload and
cast **all thirteen**; an ineligible character is offered none of them, in all
five gates `SpellBooks.CanHold` feeds.

### Stage 6 release-gate handover

1. This document, with every `prototype` mark replaced by a measured value, and
   the tuned catalogue.
2. The implementation and its focused regression tests (§3), with each milestone's
   gate output.
3. Evidence that previews match resolution with no side effects —
   `PreviewPurityTests`, including the elemental-rider parity case.
4. Recorded checks for: queue movement (the four worked examples), formation
   swaps, status expiry under the new duration model, health payment, damage
   attribution.
5. Balance results including the party control-loop test, with the
   enemy-actions-allowed figures.
6. Visual captures at every supported battle speed
   (`Domain/Combat/Session/BattleSpeed.cs`'s preset table), via
   `tools/screenshot.ps1 -Runtime` — a static Edit Mode capture cannot show
   motion.
7. Remaining limitations stated, starting with the three already known: Lucky
   Deck's chill gains a turn (D1); a delay after a death may leave a survivor at
   the same forecast index (1.9 rule 8); Poison's own mitigation is deliberately
   not migrated and still differs from every new DoT.

---

## 7. Repo claims verified

Every `path:line` this plan relies on, opened by the author of this document
during this pass. Line numbers are from the tree at `a016e0ba`.

| Claim | Verified at | What was checked |
|---|---|---|
| `CastSkill` refusal order, then payment | `Domain/Combat/Session/FightSession.Skills.cs:45-218` | the six refusals at `:59`, `:85`, `:99`, `:110`, `:121`, `:130`; payment at `:149`, `:154`, `:155`, `:164`, `:173`; resolution at `:175` |
| Cooldown is refused inside `CanResolveSkill`, first | `Domain/Combat/Session/FightSession.Talents.cs:389-418` | the cooldown branch precedes every board-state question |
| Cooldown starts at cast, per caster turns | `Domain/Combat/Session/FightSession.Cooldowns.cs:41-91` | `BeginCooldown` `:55-66`, `TickCooldowns` at turn start `:74-91` |
| `AfterDefences` composition order | `Domain/Combat/Session/DamagePipeline.cs:221-288` | dodge `:234`, effectiveness `:242`, status+defense `:245`, detonation `:264`, variance `:270`, flat physical `:275`, ward `:285` |
| `rng: null` means never dodge; preview convention | `Domain/Combat/Session/DamagePipeline.cs:155-176` | header states it; `RollDodge` returns false on a null rng |
| Multi-packet spells roll dodge once | `Domain/Combat/Session/FightSession.Skills.cs:971-1002` | `:974` rolls, `:989` passes `dodgeAlreadyResolved: true` |
| **`ResolveDamageInstances` does not pass `ignoresDefense`** | `Domain/Combat/Session/FightSession.Skills.cs:983-991` | the argument list has no `ignoresDefense`; the scaled path at `:685` does |
| `ApplySkillStatus` is gated on `IsAlive` and applies directly | `Domain/Combat/Session/FightSession.Skills.cs:717-721`, `:1136-1147` | `StatusEffects.Apply` called with no seam |
| **`ApplyChilled` is the only path that registers the speed malus** | `Domain/Combat/Session/FightSession.SpeedBuffs.cs:347-379` | its own header says every caller must go through it |
| **`PreviewSkillPower`'s packet branch omits `ElementalDamagePercent`** | `Domain/Combat/Session/FightSession.Skills.cs:1036-1051` vs `:981` | resolution multiplies by it; the preview does not |
| `PreviewSkillPower` takes no RNG and restores its stash | `Domain/Combat/Session/FightSession.Skills.cs:1036-1075` | signature and the `finally` |
| Detonation: consume once, no rescale, entry removed first | `Domain/Combat/StatusCombos.cs:43-59`; `Domain/Combat/Session/FightSession.cs:909-918` | `bonus = Magnitude * TurnsRemaining`; `Remove` precedes the return; `DealDamage` typed Poison, credit by side |
| `StatusEffects.Apply`: max/max, source re-point, Shielded throws | `Domain/Combat/StatusEffects.cs:65-96` | all three |
| `Tick` decrements at turn start; `IsSpentByTheTurn` exempts three | `Domain/Combat/StatusEffects.cs:795-874` | Shielded also exempted at `:861`; expiry sweep at `:868-871` |
| `TickReport` names Poison by field | `Domain/Combat/StatusEffects.cs:761-792` | `PoisonDamage`, `PoisonAbsorbed`, `RegenHealed`, `Expired`, `IsEmpty` |
| `ElementOf` is the generic element table | `Domain/Combat/StatusEffects.cs:745-759` | Poison only; header requires new damaging members to be added |
| `ConsumeStun` counts Fear down once per skip | `Domain/Combat/StatusEffects.cs:221-232` | Stun removed outright, Fear decremented |
| Wards age at turn end | `Domain/Combat/StatusEffects.cs:622`; `Session/FightSession.Talents.cs:129-134`; `Session/FightSession.Riders.cs:110-114` | the one existing turn-end clock, skipped on an extra action |
| `ActiveStatus` fields; turns floored at 1 | `Domain/Combat/StatusEffect.cs:184-215` | `Magnitude`, `TurnsRemaining`, `Source`, `Math.Max(1, turns)` |
| `StatusEffectType` members and order | `Domain/Combat/StatusEffect.cs:24-181` | Poison, Regen, Protect, Vulnerable, Stun, Shielded, Provoked, Empowered, Chilled, Rooted, Marked, Feared |
| `Marks`: 99 turns, `IsMarked`, `ConsumeMark` returns what it removed | `Domain/Combat/Marks.cs:26,28-52` | and its header naming the Lantern's private set as separate |
| `Fear`: one authored percent, 25 | `Domain/Combat/Fear.cs:19,21-25` | header argues against per-caller values |
| `TurnOrder` charge model, threshold 100, tie-break | `Domain/Combat/TurnOrder.cs:12-29`, `:376-426` | `Charge`/`Rate`; most overcharged, then initiative, then insertion order |
| **`Project` puts the current actor at index 0** | `Domain/Combat/TurnOrder.cs:444-459`, `:511-524` | `SimulateForward` adds `simCurrent` before the loop |
| `ApplyPushBack`: one slot per charge level | `Domain/Combat/TurnOrder.cs:303-323` | "greatest strictly below" minus 1, else a full threshold |
| `PullToFront` lands above the highest, never over the line | `Domain/Combat/TurnOrder.cs:345-375` | header states the "turn and a half" reasoning |
| `ProjectPushed` runs the real rule on a snapshot | `Domain/Combat/TurnOrder.cs:470-508` | `Snapshot()` is a fresh copy |
| `GrantExtraTurn` / `SetSpeed` are separate operations | `Domain/Combat/TurnOrder.cs:58-65`, `:246-265`, `:161-192` | displacement touches neither |
| Rate curve and clamps | `Domain/Combat/SpeedScale.cs:34-61` | baseline 10, exponent 0.5, max 2.5, min 0.35 |
| `Move`/`CanMove` read Rooted off both sides | `Domain/Combat/Session/FightSession.cs:466-516` | and `NoteDeliberateMove` fired for both figures |
| `NoteDeliberateMove` pays two different relics | `Domain/Combat/Session/FightSession.RelicMechanics.cs:359-367` | mover vs acting character |
| `EffectivePoolFor` zero-weights the plain swing when Rooted | `Domain/Combat/Session/FightSession.Enemies.cs:122-171` | and `EnemyAbility.IsPlainSwing` at `Session/EnemyAbility.cs:75` |
| `RootedEnemyHasNoLegalAction` spends no draw | `Domain/Combat/Session/FightSession.Enemies.cs:266-271` | `Pick(pool, 0f)`, a literal, not `_rng` |
| Telegraph and resolution ask the same question | `Domain/Combat/Session/FightSession.Enemies.cs:294-326`, `:614-631` | both call it fresh |
| Committed intents re-validate against reach, no RNG | `Domain/Combat/Session/FightSession.Enemies.cs:710-732`; `:1019` | re-pick is first-in-list-order |
| `MenuDepth`, `TargetSide`, `Back()` | `Domain/Combat/Session/FightMenuState.cs:6-41`, `:234-289` | Root/Sub/Element/Target; Back retraces the way in |
| `AllyTargeting` excludes the caster for Gifts only | `Domain/Combat/AllyTargeting.cs:32-53` | Ward takes the `default` arm |
| `AdvanceAfterAction` is the one post-action funnel | `Domain/Combat/Session/FightSession.Riders.cs:43-58` | free-action lock released there |
| `TickStatuses` then `TickCooldowns` at turn start | `Domain/Combat/Session/FightSession.Riders.cs:302-303`, `:457-605` | the tick is a beat; `RecordUnattributedDamage` at `:554`; `SettleDeath(Nobody)` at `:565` |
| `DealDamage` measures alive-across-the-call | `Domain/Combat/Session/FightSession.Ledger.cs:51-69` | one settlement per body |
| `ApplyDamage` drains the signature pool and caps spikes | `Domain/Combat/CombatMath.cs:514-517`, `:528+` | why a health cost must not use it |
| `ignoresDefense` zeroes broad defense only | `Domain/Combat/CombatMath.cs:114-121`, `:171-176` | typed resistance is never ignored |
| `DamageScale` is 5 and unused on the skill path | `Domain/Combat/CombatMath.cs:61`, `:309-312` | `ComputeSkillDamage` has no scale term |
| Weakness 1.5, resistance 0.5 | `Domain/Combat/CombatMath.cs:328-329` | |
| `DamageType` has eleven members and no `Frost` | `Domain/Stats/DamageType.cs:8-28` | append-only note at `:17-22` |
| `"Frost"` is a skills.json alias for `Ice` | `Domain/Content/SkillEntryResolver.cs:1059-1071`; `Domain/Content/RawSkillEntry.cs:497` | |
| `SkillEffect`/`SkillTargeting` are ordinal in generated assets | `Domain/Combat/SkillEffect.cs:88-96`, `:136-141` | append-only, never insert |
| `ResolvedSkill` is serialized by name | `Domain/Content/ResolvedSkill.cs:1-36` | a `[Serializable]` class of public fields |
| `RawSkillEntry`'s field surface | `Domain/Content/RawSkillEntry.cs:15-508` | 44 fields; `wardTurns` `:117`, `queuePushSlots` `:254`, `bookOnly` `:348`, `bookTier` `:358`, `meleeReach` `:373`, `reachSlots` `:392` |
| Book eligibility: pool-gated, five agreeing gates | `Domain/Content/SpellBooks.cs:12,14-34` | `MaxSpellSlots = 3`; null pool reads as yes |
| `AvailableSkillsFor`'s learned-book route | `Core/Content/ContentDatabase.cs:306-425`, `:379-381` | requires `BookOnly && canHoldBooks && learnedSpells` names both |
| `ReconcileLearnedSpells` and `IsBookEligible` | `Data/SaveData.cs:1047-1074` | `BookTier > 0`; a displaced book returns to the unplaced pile |
| `bookTier` and `bookOnly` agree by coincidence | `Data/SaveData.cs:1019-1029` | the comment says so outright |
| `ContentBuilder` resolves skills through `SkillEntryResolver` | `Editor/ContentBuilder.cs:508-513` | |
| `skills.json`'s `_readme` is stale on scale and defence | `ContentData/skills.json:2` | says `x10` and defence-in-raw; neither is true |
| The four existing books | `ContentData/skills.json:43-183` | `mud_burst` T1 8/2 Earth 6; `frost_flare` T2 7/– Fire 4 + Frost 4; `cinderfault` T2 15/3 Fire 5 + Nature 5 `DamageAll`; `lightning_bolt` T3 11/– Lightning 10 |
| The four live standing-status content rows | `ContentData/skills.json:43,282,410,466` | `mud_burst`/`bog_mud_burst` Vulnerable 25/2; `wail`/`shell_up` Protect 50/2 |
| The five live standing-status code sites | `Session/FightSession.Talents.cs:509,521-522`; `Session/FightSession.RelicMechanics.cs:256-257`; `Session/FightSession.cs:851,872`; `Session/FightSession.Relics.cs:359` | Shatter 30/2, Bull's Horn 50/2, Frosty 20/2, Sylvan 2, Lucky Deck 30/1 |
| Tuning constants | `Session/FightTuning.cs:61-62,68,98,109,117,164-165,175,202-203` | as quoted throughout |
| The enemy roster and its authored abilities | `ContentData/enemies.json` | 16 enemies; bosses `forest_warden` and `hollow_choir`; nine authored abilities across five enemies |
| `preview.ps1 -Spell` refuses seven effects | `docs/TESTING.md`, decision table | `Ward`, `Shatter`, three Gifts, `Provoke`, `RestorePartyMana` |
| AUDIT #188 names the `TickReport` problem | `AUDIT.md:2556-2572` | and proposes the element-keyed shape |
| `docs/CODE_STANDARDS.md` §8 and §10 | `docs/CODE_STANDARDS.md:355-395`, `:426-470` | literal pins, vacuity guards, two-use validation |
| Globality test for a spell | `docs/SPELL_DESIGN_STANDARD.md:73-99`, `:219-226` | all thirteen pass it: none needs Wool, a body part, a companion or a form |

### Where the baseline is wrong or silent

Reported here; `docs/SPELL_EXPANSION_BASELINE.md` is **not** edited.

0. **Correct when written, SUPERSEDED on 2026-09-20.** Baseline §5's row
   "Re-applying Poison **refreshes, does not stack**" and §9's reading of
   `StatusEffects.Apply` describe the code as it stood at stage 1a. The owner's
   stacking decision replaces that rule; the baseline stays as the record of
   what the change was made against, and anyone reading it for present-tense
   behaviour should read §0's owner-decision block and D3 instead. The two
   `StatusEffectsTests` the baseline names as pins for the merge
   (`Apply_SameTypeAgain_TakesTheStrongerMagnitudeAndLongerDuration`,
   `.Apply_AWeakerReapplication_NeverWeakensTheExistingOne`) are re-aimed at a
   refresh-policy status rather than deleted, so the merge branch keeps its
   coverage for the statuses that still use it.
1. **Silent, and it changes a contract.** Baseline §1 does not record that
   `ResolveDamageInstances` omits `ignoresDefense`. A fixed-packet spell's
   `ignoresDefense` is inert today (`FightSession.Skills.cs:983-991`). Blackglass
   Spear depends on the repair; milestone B carries it.
2. **Silent, and it changes a contract.** Baseline §7 records that Chilled works
   through `FightSession.SpeedBuffs` but not that `ApplySkillStatus` bypasses
   that path entirely. `appliesStatus: Chilled` on a content row would land an
   inert badge. Winter's Rebuke is the first row that would hit it; D6 closes it.
3. **Silent, and it affects the release gate.** Baseline §2 and §12 item 6 treat
   preview purity as structurally guaranteed, which it is, but neither records
   that `PreviewSkillPower`'s fixed-packet branch omits `ElementalDamagePercent`
   while `ResolveDamageInstances` applies it. Preview and resolution already
   disagree for a caster with an elemental rider.
4. **Silent, and it is load-bearing for milestone C.** Baseline §10 does not
   state that `Project`'s index 0 is the current actor. Every use of "forecast
   position" in the owner's brief turns on it.
5. **Silent.** Baseline §9's table does not distinguish *why* Poison and Regen
   decrement at turn start (their effect IS the tick) from why Protect and
   friends do (an accident of one shared loop). The distinction is what makes
   D1's three families a model rather than a reshuffle.
6. **Line drift only, claims correct.** Baseline §2 cites `PreviewSkillPower` at
   `:1005-1036`; it is `:1036-1075`. §1 cites `ResolveDamageInstances` at
   `:938-947`/`:974-995`; it is `:971-1002`. §5 cites detonation at
   `FightSession.cs:887-918`; `ResolveDetonation` is `:909-918`.
7. **Correct and worth restating**, because it looks like an error: baseline
   §11's table gives `frost_flare` as "Fire 4 + Frost 4". That is what the row
   authors, and `Frost` is a real skills.json alias for `DamageType.Ice`
   (`SkillEntryResolver.cs:1059-1071`); there is no `Frost` enum member.

### One contradiction in the owner's brief, resolved rather than carried

Court of Whispers asks for Fear "with 20% increased damage taken".
`Fear.VulnerablePercent` is **25** and is deliberately a single authored constant
(`Fear.cs:11-19`). D8 takes the constant; the alternative and its consequence
are stated there.

---

## Appendix A — the owner's brief, verbatim

> Implement the thirteen spells in six stages, using the existing combat flow and adding shared operations only where the spells require them. Treat numerical values as prototype tuning; the behavioral rules below are the implementation contract.
>
> ### 1. Establish the baseline and write the specification
>
> Create `docs/PLAN_SPELL_EXPANSION.md` with one entry per spell: targeting, costs, cooldown, tier, scaling, effect order, expiry, failure behavior, and presentation.
>
> Before changing combat, capture existing behavior for:
> - Direct spell scaling, defense, dodge, wards, and elemental affinity.
> - Poison application, ticking, detonation, and kill attribution.
> - Marked versus the separate Drowned Lantern mark.
> - Fear, Rooted, formation movement, and initiative manipulation.
> - Spellbook eligibility and save/load.
>
> Preserve existing character skills, relic behavior, and book eligibility. Fury characters remain unable to learn books.
>
> ### 2. Lock these shared rules
>
> | Area | Implementation rule |
> |---|---|
> | Casting | Validate targets and all costs before paying anything. Invalid or canceled casts spend nothing. |
> | Health costs | Require full payment while leaving at least 1 HP. Round percentage costs upward. Costs bypass wards and do not count as incoming damage. |
> | Direct damage | Use the existing spell scaling, dodge, defense, affinity, and damage-recording flow. |
> | New DoTs | Capture caster potency when applied. Store the resulting tick strength. Apply elemental affinity when ticking, without subtracting flat defense again each tick. |
> | Existing Poison | Preserve its current behavior in this batch. Any migration to different mitigation or scaling is separate work. |
> | Detonation | Consume stored remaining Poison value once. Do not apply caster spell scaling a second time. Preserve existing detonation semantics unless the spell explicitly modifies them. |
> | Duration | Define duration as effective affected turns. Restrictions remain through the final affected action; post-action curses expire after their final retaliation opportunity. |
> | Preview | Never mutate statuses, resources, initiative, RNG, or cast counters. Show ranges where damage varies. |
> | Damage recording | Every new damage source reports absorption, health loss, death, and attribution through the established session flow. |
> | Marked | Preserve its existing meaning. Crownfall consumes the actual Marked status, not the independent Lantern mark. |
>
> For new DoTs, store source attribution independently of whether the caster remains alive. Document refresh behavior when two casters apply different strengths.
>
> ### 3. Implement in dependency order
>
> **Milestone A — protection, frost, and Poison.** Ship as complete slices: content, mechanics, targeting, tooltip, placeholder presentation, tests.
>
> | Spell | Prototype specification | Required work |
> |---|---|---|
> | Gilded Aegis | Tier 1; 7 mana; cooldown 2. Ward one ally for two wearer turns. | Reuse ward application and expiry. Author an explicit base-plus-Wisdom formula after measuring existing ward strengths. |
> | Winter's Rebuke | Tier 2; 8 mana; cooldown 2. Low–medium Frost damage, then Chilled 25% for two effective target turns. | Reuse damage and Chill; verify its final affected turn. Start with damage packet 6. |
> | Viper's Bite | Tier 1; 7 mana; cooldown 2. Poison hit, then Poison for three ticks. | Start with packet 3 and tick strength 3. Preserve the existing interaction: the hit detonates old Poison before fresh Poison is applied. |
>
> Viper's Bite must explicitly advertise "Detonates existing Poison, then poisons again." Its animation is a snake bite closing over the target.
>
> Acceptance: scaling visible in previews, dodge follows existing application rules, lethal damage does not apply a fresh status, wards expire correctly, Poison detonation recorded once.
>
> **Milestone B — consumption and health payment.** Add narrow shared operations for inspecting/consuming statuses and validating/paying health costs.
>
> | Spell | Prototype specification | Required work |
> |---|---|---|
> | Crownfall | Tier 1; 6 mana; no cooldown. Arcane packet 7, increased to 12 against Marked; consumes the mark on a landed hit. | Read and consume the existing Marked status. Preserve it on dodge. |
> | Ashen Reckoning | Tier 3; 10 mana; cooldown 2. Requires Poison. Consumes it for 150% of remaining stored damage, then applies Vulnerable 20% through the target's next completed turn. | Concrete premium over ordinary detonation. Split damage into Poison/Fire, odd point to Poison. Prevent recursive detonation and double scaling. |
> | Blackglass Spear | Tier 3; 12 mana plus 5% maximum HP; cooldown 3. Void packet 13, bypassing magical defense. | Connect defense bypass to fixed-packet resolution. Preserve Void affinity and normal dodge. |
>
> Ashen Reckoning's 150% is a prototype balance decision: compare with Bite recasting and ordinary Nature/Poison follow-ups; revise price or premium before release if unjustified.
>
> Acceptance: previews never consume setup; all payments validated together; health costs cannot trigger damage-received benefits; packet splitting cannot cause duplicate reactions or kills.
>
> **Milestone C — initiative and formation.** First write concrete scheduler examples: equal-speed actors, repeated appearances of fast actors, actors already next, multi-target delay. Implement queue changes against one pre-effect forecast. Preserve the current action and the relative order of equally displaced targets. Reuse existing charge-based scheduling operations.
>
> | Spell | Prototype specification | Required work |
> |---|---|---|
> | Borrowed Moment | Tier 2; 8 mana; cooldown 2. Advance another ally's next action by up to two forecast positions. | Clip at the next available action; never grant an extra action or change Speed. |
> | Gale Scythe | Tier 2; 11 mana; cooldown 3. Wind packet 5 to all enemies; delay each landed-hit survivor by one forecast position. | Resolve movement as a batch after damage and deaths. |
> | Palace Passage | Tier 2; 7 mana; cooldown 3. Swap two allies as the caster's one free action. | Extend existing swapping with two-target selection and atomic cancellation. |
>
> Palace Passage follows current Rooted restrictions: neither ally may be Rooted. Actor-targeted enemy intent follows the actor after a swap; formation legality recalculated with existing rules.
>
> Acceptance: canceled selection spends nothing; initiative entries never duplicated or lost; health/statuses stay attached to actors; keyboard, gamepad, pointer selection work.
>
> **Milestone D — physical-action restriction.** Add one explicit `PhysicalMove` classification; no unused action categories. Audit basic attacks and every selectable character/enemy ability. Classification is independent of damage element (flaming sword strike is physical; magical rock projection need not be).
>
> | Velvet Shackles | Tier 2; 9 mana; cooldown 3. Root one enemy for two effective turns, preventing physical moves. |
>
> Shared legality check for menus, AI selection, committed-intent validation, execution. Keep the no-legal-action fallback (turn forfeiture); no undefined Guard mechanic.
>
> Acceptance: all physical skills blocked, legal nonphysical actions available, obsolete intents update, second affected action still restricted.
>
> **Milestone E — persistent damage.** Extend status tick reporting to represent multiple damage types without a field per DoT. Add a focused post-action hook for Thorn Tithe; follow-ups must not recursively count as new physical actions.
>
> | Censer of Embers | Tier 2; 8 mana; cooldown 2. Burn for three ticks. Base intensity 4 → 6 → 8 on recast, refreshing duration. Scale intensity by the new DoT rule. |
> | Thorn Tithe | Tier 3; 10 mana; cooldown 3. Three affected turns: Nature damage at turn start and again after a completed physical move. Start base 5 + 5, tune against real health pools. |
>
> Burn: separate intensity level from scaled damage; on refresh keep the stronger potency snapshot. Thorn Tithe: active until end of final affected turn; skipped turns get the opening tick, no retaliation; a completed physical action triggers retaliation even on a miss; interrupted/rejected actions do not.
>
> Acceptance: affinity consistent, third retaliation opportunity exists, dead actors cannot act, attribution survives caster death, tooltips show actual tick strength and refresh results.
>
> **Milestone F — Court and combined control.**
>
> | Court of Whispers | Tier 3; 14 mana; cooldown 4. Ordinary enemies: Fear for one skipped turn with 20% increased damage taken. Bosses: two-position delay instead. Caster: Vulnerable 20% through their next completed turn. |
>
> Vulnerable is the explicit drawback; do not change Marked globally. Show boss fallback in targeting info. Test Court with Borrowed Moment, Gale Scythe, Chill, Rooted, existing character control. If alternating casters can sustain denial, adjust costs/cooldowns or add a narrowly specified repeat-control resistance before shipping. Per-caster cooldowns do not solve party-wide loops.
>
> ### 4. Balance gate after each milestone
>
> Representative early/middle/late parties and encounters. Compare damage or protection per action and per mana; time to kill and health preserved; enemy actions allowed during control rotations; value vs ordinary/elite/boss; low- vs high-investment casters; whether the spell earns one of three slots. Explicit: Aegis vs existing protection; Winter's Rebuke vs direct damage and queue control; Ashen Reckoning vs Bite recasting and normal detonation; Blackglass vs Lightning Bolt on defended and fragile enemies; Thorn Tithe vs direct damage on fast physical enemies and slow casters. Update the spec with measured values before final art.
>
> ### 5. Presentation and book integration
>
> Every spell gets an authored begin, release, impact, settle sequence; mechanics, popups, sound, reactions sync at impact. Priority visuals: closing snake jaws (Viper's Bite), extracted Poison returning as burning material (Ashen Reckoning), paired doors (Passage), surrounding whispering court (Court). Book icons, status icons, descriptions, shop tiers, content validation. Verify eligible characters can learn, replace, equip, save, reload, cast all thirteen; ineligible remain excluded.
>
> ### 6. Release gate
>
> Hand over: final spec and tuned catalogue; implementation and focused regression tests; evidence previews match resolution without side effects; recorded checks for queue movement, formation swaps, status expiry, health payment, damage attribution; balance results incl. party control-loop tests; visual captures at supported battle speeds; remaining limitations stated.

---

## Appendix B — reviewer self-check

| Requirement | Where this plan satisfies it |
|---|---|
| Contracts before files | Section 1 is thirteen behavioural contracts; the first file path appears in section 1.1's citations and the first content row in section 2. Section 7 is the only inventory and it is last. |
| Explicit precedence order per contract | 1.1 (six refusals, numbered, then payment in order); 1.3 (the eight pipeline steps); 1.4 (turn start vs turn end); 1.6 (five numbered steps); 1.7 (split then resolve); 1.9 (rules 1-8); 1.10 (legality checked after authoring, before targeting); 1.12 (the state table's row order). |
| Empty case per contract | Named in every subsection of section 1: 1.1 refusal spends nothing; 1.2 zero cost; 1.3 floor of 1; 1.4 no statuses; 1.5 snapshot floors at 1, dead holders never tick; 1.6 no Poison returns 0; 1.7 total of 0; 1.8 no entry returns false; 1.9 empty survivor list; 1.10 no legal action forfeits; 1.11 no curse runs nothing; 1.12 a party of one; 1.13 zero; 1.15 three empty slots. |
| RNG draw policy where a roll exists | 1.1 (no refusal draws); 1.3 (one dodge, one variance per packet); 1.5 (a tick draws nothing); 1.6 (detonation draws nothing); 1.8 (consumption is deterministic); 1.9 (neither operation nor `Project` draws); 1.10 (`Pick(pool, 0f)` is a literal, not a draw); 1.12 (none); 1.13 (never). |
| One convention per type per entry point | D6 (one `ApplyStatusTo` seam, no status applied outside it); 1.8 (one `TrySpend`, two existing callers folded into it); 1.6 (one detonation operation with a premium parameter, not two paths); 1.10 (one legality predicate, four callers); 1.9 (`ApplyPullForward` is the exact mirror of `ApplyPushBack`, shared by the real op and the forecast). |
| Static layout vs runtime eligibility for the swap picker | 1.12: a Rooted ally is **shown** (static layout) and **refused on Submit** with the reason (runtime eligibility), the same split `AllyTargeting.Accepts` already draws. |
| State table for the multi-state interaction | 1.12's four-row table: state × Submit-legal × Submit-illegal × Cancel, with pointer, keyboard and gamepad differing only in what produces Submit and Cancel. |
| Tests through the production dispatcher | Section 3 marks **(dispatcher)** on fifteen rows, including every consumption, every payment, every dodge-interaction and every Rooted-legality case; they go through `CastSkill`/the session rather than a resolution method. |
| Hardest contracts proven in their own milestone before rollout | The duration model (D1) lands in milestone A **alone and first**, with `StatusDurationMigrationTests` covering all nine re-authored rows, before Winter's Rebuke or Velvet Shackles depends on it. Initiative displacement (1.9) lands in milestone C with the four worked examples written as tests before the operation exists; milestone F's boss delay is a *use* of the operation C already proved, adds no rule to it, and has its own acceptance criterion that does not restate C's. |
| No cross-phase acceptance dependency | Each milestone's acceptance list in section 4 names only behaviour that package ships. Milestone A's criteria never mention a DoT; C's never mention `PhysicalMove`; E's never mention Court. Section 3's milestone column assigns every test to the package that lands its contract. |
| No effort estimates | Section 4 contains none, and no section states a duration, a point value or a day count. |
