# Spell expansion baseline

Stage 1a of the thirteen-spell expansion brief (`spell-expansion-brief-2026-09-19`
memory). Captures CURRENT combat behaviour, before any combat code changes, so
`docs/PLAN_SPELL_EXPANSION.md` (stage 1b) has facts instead of assumptions to
build on. No production code was touched to produce this document.

Format per line: **behaviour** -> `path:line` -> pin -> literal values where a
number exists. `DIVERGES:` marks a place today's code already does something
the brief's shared rules (section 2) will require differently — not fixed
here, just named.

---

## 1. Direct spell damage path

| Behaviour | Location | Pin |
|---|---|---|
| A skill's raw damage is `Math.Max(1, ScaledAttack(attacker, SkillScaling, powerMultiplier))` — no defense term, no `x5`/`x10` scale folded in any more | `CombatMath.cs:309` (`ComputeSkillDamage`), `:235` (`ScaledAttack`) | `CombatMathTests` |
| `DamageScale` is **5**, not 10, and as of 2026-08-26 neither `ComputeAttackDamage` nor `ComputeSkillDamage` calls `Scale()` at all any more — mitigation moved entirely into `DamagePipeline` | `CombatMath.cs:61` (const), `:85-88`, `:258-291` (`Scale`'s own header explains the removal) | `CombatMathTests` |
| `ScalingAxis.Auto` derives Weapon-vs-Spell from the damage type (`IsPhysical(type) ? Weapon : Spell`); `Weapon`/`Spell` are explicit per-skill overrides, `None` rides neither | `ScalingAxis.cs:16-30` | — (pure data, no dedicated test file; exercised via `CombatMathTests`/`SkillDispatchTests`) |
| A spell's `damageInstances` are fixed, already-final-scale packets, one `DamageInstance{type, amount}` each, NOT run through `ScaledAttack`/`DamageScale` | `DamageInstance.cs:1-30` | `SkillDispatchTests.AnAuthoredPacketSpellReportsItsSplit` |
| Fixed packets DO scale by the caster's own `SkillPowerMultiplier` (tier) and `SkillScaling` (INT/WIS-graded, per D3) and by `ElementalDamagePercent`, multiplicatively, THEN floor at 1 per packet | `FightSession.Skills.cs:971-1003` (`ResolveDamageInstances`) | `PrismaticOrbTests`, `CinderfaultSpellTests` (existing files, not re-read here — cite by name only) |
| **Composition order inside `DamagePipeline.AfterDefences` (the ONE funnel every real damage path shares), typed overload**: (1) dodge roll first — short-circuits everything below; (2) `EffectivenessMultiplier` (weakness/resistance, +Jo-Sun bonus); (3) `ApplyStatusEffects` (Protect/Vulnerable, one multiplier in the same slot); (4) `TotalDefense` = `BroadDefense` (Last Stand bonus -> BreakShield zero -> Sharp Horns %-penetration -> flat ArmorPenetration, physical only) + `TypedResistance` (never broken/penetrated), then `AfterResistance` = `max(1, dmg*100/(100+D))`; (5) poison-combo detonation (`resolveDetonation`, mutating, null on preview); (6) variance roll (±20% default, `DamageVarianceRange`); (7) Stalwart's flat physical reduction (physical only, floor 1); (8) the ward pool (`resolveWard`, last, can legitimately return 0) | `DamagePipeline.cs:100-288` (header states the order explicitly and argues each position) | `CombatMathTests`, `DamagePipelineTests` |
| Result recorded through `FightSession.ApplyFinalDamage` -> `DealDamage` (`FightSession.Ledger.cs:51`) -> `ApplyAndCountDamage` (pools, Phoenix Egg, Cursed Idol, ledger `Dealt`/`Took` rows, clamped-to-what-was-there `booked` figure) -> `SettleDeath` (kill row + rider flag, gated by `KillCredit`) | `FightSession.Ledger.cs:17-363`; `KillCredit.cs` (whole file, 2 members: `Attacker`/`Nobody`) | `CombatLedgerTests`, `KillCreditTests` |
| **Dodge is rolled AFTER cost payment.** `CastSkill` pays mana/resource/cooldown at `FightSession.Skills.cs:149-168`, before `ResolveCharacterSkill` (`:175`) is ever called; `RollDodge` only fires inside `DamagePipeline.AfterDefences`, reached from deep inside that resolution. A cast that goes on to miss has already paid in full. | `FightSession.Skills.cs:110-175` (payment) vs `DamagePipeline.cs:158-188`, `:234` (roll site) | **was UNPINNED** -> added `DodgeCoversEveryDamagePathTests.ACastThatDodges_HasAlreadyPaidItsManaBeforeTheRollHappens` |
| **A dodged cast applies no status rider.** `outcome.IsMiss` returns before `ApplyFinalDamage`/`ApplySkillStatus`/`ApplyMark` are ever reached, for both the scaled path (`ResolveDamageSingle:693-698`) and the fixed-packet path (`ResolveDamageInstances` rolls once, `ResolveDamageSingle:655-660` returns on `dodgedInstances`) | `FightSession.Skills.cs:619-722` | `DodgeCoversEveryDamagePathTests.CharacterSkill_SingleTarget_AgainstAVeryHighDodgeChance_DealsNoDamage_AndAppliesNoStatus`, `.FixedPacketSpell_AgainstAVeryHighDodgeChance_NoPacketLands` |
| A multi-packet spell rolls dodge **once** for the whole cast (`ResolveDamageInstances` rolls before its packet loop, passes `dodgeAlreadyResolved: true` into every packet's `AfterDefences` call) — a spell is not half-dodged | `FightSession.Skills.cs:938-947`, `:974-995` | `DodgeCoversEveryDamagePathTests.FixedPacketSpell_RollsDodgeOnceForTheWholeCast_NotOncePerPacket` |
| `damageInstances` packets each check affinity **individually** against the target, exactly as `skills.json`'s readme claims | `FightSession.Skills.cs:983-1000` (per-instance `AffinityOf(target)` inside the loop) | `PrismaticOrbTests` (frost_flare-style split), area 4 below |

DIVERGES: `Assets/_Project/ContentData/skills.json:2` (`_readme`) still describes
the damage formula as `... floored at 1) x10` and "minus the target's Defense
unless `ignoresDefense`" happening inside the raw figure. Neither is true any
more: the scale constant is 5 (not 10), it is not applied to the mitigated
combat path at all since 2026-08-26, and defense is subtracted exactly once,
later, inside `DamagePipeline`. This stale readme is very likely where this
brief's own phrase "x10 DamageScale" came from — see Anomalies.

---

## 2. Cast validation and payment order

`CastSkill` (`FightSession.Skills.cs:45-218`) refuses, in this exact order, before spending anything:

1. Reach (`SingleEnemy` only) — `:59-63`
2. Ally-target validity (`SingleAlly` only; null target with live candidates is a refusal, not an auto-pick, AUDIT #147) — `:83-93`
3. Element choice mismatch (three ways, `TryTakeElementChoice`) — `:95-102`, `:259-286`
4. `SkillResolution.CanAfford` (mana + resource) — `:110-114`
5. `CanResolveSkill` (board-state refusal: Shatter with no wards, a Gift with nobody to give it to) — `:116-125`
6. The once-per-turn free-action lock — `:127-135`

Only after all six pass does payment happen: `ChargeSkillMana` (`:149`), `BeginCooldown` (`:154`), the signature-resource spend (`:155`), the pool-tier spend (`:164-168`), then `RefreshAttackBonus`/Gift-Fury spend (`:173`) — all before `ResolveCharacterSkill` (`:175`) runs.

| Behaviour | Pin |
|---|---|
| An unaffordable cast spends no mana, ends no turn | `SkillDispatchTests.ACastThatCannotBePaidForIsRefusedAndCostsNoTurn` |
| A board-state refusal (Shatter, empty-target Gift) spends nothing | `SkillDispatchTests.ShatterWithNoWardsOutRefusesBeforeAnythingIsPaid`, `.AGiftWithNobodyToGiveItToRefuses` |
| **A refused cast starts no cooldown** (the cooldown half of the same rule — not previously pinned) | **was UNPINNED** -> added `SkillCooldownTests.ARefusedCast_ForUnaffordableMana_StartsNoCooldown` |
| Cooldown counted from the turn cast, in the caster's own turns (`cast turn N, cooldown 2 -> back on N+2`) | `FightSession.Cooldowns.cs:14-28` | `SkillCooldownTests.ACooldownOfTwoMeansTurnOneThenTurnThree` |
| Cooldown starts (`BeginCooldown`) at cast commitment, NOT at resolution — a cast that resolves onto nothing (every target died first) still spends it | `FightSession.Cooldowns.cs:51-66` | (comment states the rule; no dedicated "resolves onto nothing" pin found — UNPINNED, low priority, no content path currently produces this for a player cast) |
| `CooldownRemaining(actor, skillId)` is the public read hook — 0 for "no entry" and "0 remaining" alike | `FightSession.Cooldowns.cs:41-49` | used directly by the new cooldown-refusal pin above |
| **Preview never mutates.** `PreviewSkillPower` never calls `DamagePipeline.AfterDefences` at all (no target passed), restores `BonusAttackPercent` in a `finally`, spends no resource, advances no `PotencyFor` tally, touches no RNG | `FightSession.Skills.cs:1005-1036` (header states the contract at length) | no dedicated preview-purity test found by name — UNPINNED at the integration level (the contract is enforced by construction: the method signature has no `SeededRandom` parameter to spend) |

---

## 3. Wards

Model: a ward is `StatusEffectType.Shielded`, a shield-point POOL on `Magnitude`
(not a percentage, since AUDIT #152, 2026-09-16). Wards **stack** — every cast
is a new entry, never a merge/refusal (`StatusEffects.Apply` throws outright if
handed `Shielded`, forcing every caller through `ApplyWard`). Damage drains
soonest-expiring entry first, ties by age (`WardsInDrainOrder`,
`StatusEffects.cs:361-377`).

| Behaviour | Location | Pin |
|---|---|---|
| Duration counts the **wearer's** own turns, ticked at the **end** of the wearer's turn (`TickWardsAtTurnEnd`), the only status duration in the game that ages at turn end rather than turn start — the turn it went up is exempt | `StatusEffects.cs:280-299` | `WardTests.TheTurnStartTickDoesNotTouchAWard`, `.AWardRaisedThisTurn_SurvivesThisTurnsEnd_AndGoesAtTheNextOne`, `.ALongerWardOutlastsTheTurnsItWasGiven` |
| Default duration (any skill not authoring `wardTurns`) is **1** wearer turn, not 2 — changed from 2 on 2026-09-16 (owner) once wards started stacking | `FightSession.Talents.cs:85-86`; `FightTuning.cs:90-98` | none of the five ward skills author `wardTurns`, so this is the live default |
| Absorption order at the hit: dodge -> effectiveness/Protect/Vulnerable/resistance -> variance -> Stalwart's flat plating -> **the ward pools** -> the signature pool (Wool) -> health | `StatusEffects.cs:327-340` | `WardTests` (whole file) |
| Ward strength formula (`SkillResolution.Amount`, `SkillEffect.Ward`): `AuthoredAttackTerm(axis) + flatAmount + power*resourceSpent + PercentOfCasterMaxHealth`, floored at 1, then `x(1 + WardReductionPercent/100)` | `SkillResolution.cs:108-141` | `PhaseFourSkillTests.PrismWardIsTwentyPlusSpellAttackForEightMana`, `.FleeceWardIsFiftyShieldPointsForTwoWool`, `.BraceIsAFifthOfTheCastersOwnHealth`, `.TheWardTalentAddsItsPercentToWhateverTheSkillAuthored` |
| **Measured literal ward strengths** (existing pins, at two different caster figures): `fleece_ward` (flat, no scaling) = **50** points always. `prism_ward` (`scalingAxis: Spell`, `20 + spell attack`) = **24** at spell-Attack 4 (a fresh Owl), **110** at spell-Attack 90 (late build) — the "spell attack" term is `CombatMath.ScaledAttack(actor, SkillScaling, 1f)`, i.e. the caster's Attack stat scaled by their Wisdom/Intelligence-graded `SkillScaling` multiplier, so a higher Wisdom raises it via that multiplier on top of raw Attack. `placeholder_brawler_ward` (`Brace`, `20% of caster max HP`) = **52** at 260 HP, **100** at 500 HP | `Assets/_Project/ContentData/skills.json:259-269` (fleece_ward), `:1097-1107` (prism_ward) | `PhaseFourSkillTests` (all four cases cited above) |
| A talent (`WardReductionPercent`) is a MULTIPLIER on the authored pool now, not a rival number — 40% talent takes fleece_ward's 50 to 70, Brace's 52 to 72 | `SkillResolution.cs:127-140` | `PhaseFourSkillTests.TheWardTalentAddsItsPercentToWhateverTheSkillAuthored` |
| Every existing source: `fleece_ward`/`tuck_in` (Shawn, Wool-costed), `prism_ward` (Owl, mana), `placeholder_brawler_ward`/`bulwark` (Bjorn, health%/mana), plus two relic wards (Magical Shield and one other) at `PermanentWardTurns` (99, "for the rest of the fight") | `Assets/_Project/ContentData/skills.json` (grep `"effect": "Ward"`); `FightTuning.cs:100-103` | `WardTests.TheGoldenFleece_*`, `PhaseFourSkillTests` |

---

## 4. Elemental affinity

| Behaviour | Location | Pin |
|---|---|---|
| Affinity lives on the target's own definition (`ElementalAffinity`), passed into `DamagePipeline` rather than looked up live; a target with no definition is `Neutral` | `DamagePipeline.cs:190-207` | `CombatMathTests` |
| Multipliers: weakness **1.5x** (`WeaknessMultiplier`), resistance **0.5x** (`ResistanceMultiplier`) — symmetric since a 2026 rebalance from 2x/0.5x; weakness checked first so a content mistake (same type in both sets) resolves as weakness | `CombatMath.cs:314-329`, `:376-396` | `CombatMathTests` |
| Jo-Sun's Book of Anatomy adds `weaknessBonusPercent` MULTIPLICATIVELY onto the weakness branch only (1.5 x 1.2 = 1.8 at +20%); relic states "x2.0-2.2" in flavour text but the actual base is 1.5, not 2.0 | `CombatMath.cs:368-396` | `CombatMathTests` |
| **Affinity is NOT applied to a Poison DoT tick.** `StatusEffects.Tick` calls `CombatMath.ApplyDamage(combatant, status.Magnitude)` directly — a two-argument call with no `DamageType`/affinity parameter to pass. The tick never enters `DamagePipeline` at all | `StatusEffects.cs:800-827` | **was UNPINNED for the "ignores mitigation" claim specifically** -> added `StatusEffectsTests.Tick_Poison_IgnoresTheTargetsDefense_UnlikeAnOrdinaryHit` (proves defense; affinity absence is structural — `ApplyDamage`'s signature has no type/affinity parameter to carry one) |
| Affinity IS applied to each `damageInstances` packet **individually** (skills.json readme's claim, and it is true) — see area 1's own row | `FightSession.Skills.cs:983-1000` | `PrismaticOrbTests` |

---

## 5. Poison

| Behaviour | Location | Pin |
|---|---|---|
| Application stores an `ActiveStatus(Poison, Magnitude, TurnsRemaining)` — Magnitude is the flat per-tick damage, TurnsRemaining is ticks left, NOT a running total | `StatusEffect.cs:184-216` | `StatusEffectsTests` |
| Re-applying Poison **refreshes, does not stack**: `StatusEffects.Apply` takes `max(existing.Magnitude, new)` and `max(existing.TurnsRemaining, new)` — the STRONGER of the two on both axes, source re-pointed to whoever most recently applied it (never cleared to null) | `StatusEffects.cs:49-96` | `StatusEffectsTests.Apply_SameTypeAgain_TakesTheStrongerMagnitudeAndLongerDuration`, `.Apply_AWeakerReapplication_NeverWeakensTheExistingOne` |
| Ticking: at the holder's own turn START, `CombatMath.ApplyDamage(target, Magnitude)` — no defense subtraction, no affinity, no `x5`/`x10` scale (already final) | `StatusEffects.cs:800-827` | `StatusEffectsTests.Tick_Poison_DealsItsMagnitudeAsDamage`, `.Tick_Poison_IgnoresTheTargetsDefense_UnlikeAnOrdinaryHit` (new) |
| Poison's own duration decrements at the SAME tick it deals damage (not exempted by `IsSpentByTheTurn`), removed same-tick if it reaches 0 | `StatusEffects.cs:792-874` | `StatusEffectsTests.Tick_DecrementsDuration`, `.Tick_ExpiresAndRemovesAStatusThatReachesZeroDuration` |
| A signature-pool absorption on a tick is reported separately from health loss (`PoisonAbsorbed` vs `PoisonDamage`) and still counted for pool/ledger purposes via `RecordUnattributedDamage` | `FightSession.Ledger.cs:247-270`; `StatusEffects.cs:761-790` (`TickReport`) | `WardTests.APoisonTickAWoolPoolAbsorbsIsStillCountedAndStillSaid` (name from that file's own list, StatusEffects side) |
| **Detonation**: `ResolveDetonation` (session) calls `StatusCombos.SpendPoisonIfMatched(target, incomingType)` — fires on a **Nature or Poison** typed hit, `bonus = Magnitude * TurnsRemaining`, then **removes** the Poison entry outright. Runs INSIDE `DamagePipeline.AfterDefences`, so every preview (`resolveDetonation: null`) automatically cannot trigger it | `FightSession.cs:887-918`; `StatusCombos.cs:22-61` | `StatusCombosTests.Poisoned_NatureHit_Detonates`, `.Poisoned_PoisonTypedHit_AlsoDetonates`, `.Detonating_ConsumesThePoisonEntirely`, `.DetonatingTwiceInARow_TheSecondCallDoesNothing` |
| Detonation damage is dealt through the same `DealDamage`/ledger funnel, typed as Poison regardless of the triggering hit's own type, credited to the attacker if player-side, to Nobody otherwise (AUDIT #63) | `FightSession.cs:900-918` | (same StatusCombosTests + `KillCreditTests` for the credit split) |
| **Caster spell scaling is NOT re-applied at detonation** — the bonus is `Magnitude * TurnsRemaining` only, no multiplier term at all | `StatusCombos.cs:43-59` | `StatusCombosTests.Detonating_ConsumesThePoisonEntirely` (asserts the raw product) |
| **Verified: "the hit detonates old Poison before fresh Poison is applied" is TRUE today**, end to end. `ResolveDetonation` runs inside `AfterDefences`, called from `ResolveDamageSingle`/`ResolveDamageAll`/`ResolveDamageInstances` BEFORE `ApplyFinalDamage`; `ApplySkillStatus` (which applies a skill's own authored Poison) only runs afterward, gated on `target.IsAlive`, at `FightSession.Skills.cs:717-721`/`:820-823`. Since the old entry is REMOVED by the detonation before the fresh `StatusEffects.Apply` call ever runs, the two never merge | `FightSession.Skills.cs:682-721` (single-target path cited; AOE path is the same shape at `:798-826`) | **was UNPINNED at the integration level** (only pinned at `StatusCombos`'s own unit level) -> added `SkillDispatchTests.ANatureCastThatAlsoAppliesPoison_DetonatesTheOldPoisonBeforeTheFreshOneLands` |
| Kill attribution for a tick death: settled via `SettleDeath` inside `StatusEffects.Tick`'s caller with `KillCredit.Nobody` explicitly (a tick's applier may be dead or absent) — for a detonation death: `Attacker` if the detonating hit was player-side, `Nobody` otherwise | `FightSession.Ledger.cs:303-354`; `FightSession.cs:901-918` | `KillCreditTests` |

DIVERGES: the brief's shared rule for **new** DoTs says "apply elemental
affinity when ticking, without subtracting flat defense again each tick" —
i.e. affinity yes, defense no. Existing Poison does **neither** (flat
Magnitude, no affinity, no defense) and the brief explicitly says to preserve
existing Poison's behaviour unchanged in this batch — the two are meant to
diverge on purpose; flagged here so the plan does not accidentally unify them.

---

## 6. Marked versus the Drowned Lantern mark

**Two independent systems**, both currently applied on every landing spell hit, both reachable from the same call sites:

| | The general `Marked` status (`Marks.cs`) | The Drowned Lantern's own mark |
|---|---|---|
| Storage | `StatusEffectType.Marked` on `target.Statuses`, `Magnitude` unused (0), `TurnsRemaining` = 99 (`MarkDurationTurns`) | A private `HashSet<CombatantState>` (`FightSession.Relics.cs:229`, field `_marked`) — NOT a status at all |
| Applied by | `Marks.Apply(target, source)` — currently only the Magic Marker relic (`MagicMarkerApplyMark`) and Jar of Bear Urine | Only a wearer of the Drowned Lantern relic (`ApplyMark`, gated `HasRelic(actor, RelicEffect.DrownedLantern)`) |
| Consumed by | `Marks.ConsumeMark` — Magic Marker's own attack-rider (`MagicMarkerConsumeOnAttack`), refunds 20% missing primary resource | `MarkBonus`, consumed by an ATTACK specifically, adds `FightTuning.MarkBonusPercent` (50%) bonus damage |
| Survives a dodge? | Yes — consumption is called from `RelicsAfterSwing`, only reached after a landed hit (`ResolveAttackSwing` returns on `outcome.IsMiss` first) | Yes — `MarkBonus` is called from `ResolveAttackSwing` after the same miss-check |
| How a reader tells them apart | `Marks.IsMarked(target)` (general status) vs `FightSession.IsMarked(target)` (an instance method reading the private `_marked` set) — two entirely different queries, a target can carry both, neither, or one of either | — |

Citations: `Marks.cs` (whole file, 54 lines); `FightSession.Relics.cs:220-259`
(Lantern); `FightSession.RelicMechanics.cs:58-93` (Magic Marker, using the
shared `Marks` facility). Pin: `MarksTests` (general facility, all 5 cases);
the Lantern's own private mark is explicitly "untested here" per
`MarksTests.cs:6-8`'s own header — **UNPINNED**, and stated as a known gap by
the file itself rather than an oversight found here.

The brief's rule ("Crownfall consumes the actual Marked status, not the
independent Lantern mark") maps directly onto this table: Crownfall should
read/consume through `Marks.IsMarked`/`Marks.ConsumeMark`, exactly the way
Magic Marker already does, and must not touch `FightSession`'s private
`_marked` set.

---

## 7. Chilled and Vulnerable

| Behaviour | Location | Pin |
|---|---|---|
| Vulnerable: `DamageTakenMultiplier` adds `Magnitude/100` to a running total (additive with Protect, floored at `MinimumDamageTakenMultiplier` = 0.1) | `StatusEffects.cs:240-270` | `StatusEffectsTests.DamageTakenMultiplier_Vulnerable25_IncreasesByTwentyFivePercent`, `.DamageTakenMultiplier_ProtectAndVulnerableTogether_AreAdditive` |
| Vulnerable decays by ordinary turn-count (not `IsSpentByTheTurn`) — decrements at the bearer's turn START, same slot as Protect/Chilled/Rooted | `StatusEffects.cs:792-798` | **was UNPINNED specifically for Vulnerable** (only the generic `Tick_DecrementsDuration`, which uses Poison, existed) -> added `StatusEffectsTests.Tick_VulnerableAndMarked_DecrementAtTurnStart_LikeAnyStandingStatus` |
| Chilled reduces Speed by `Magnitude` percent of the TRUE base, via `FightSession.SpeedBuffs`'s existing relic-speed-buff bookkeeping (`GrantSpeedMalusPercent`/`RevokeSpeedBuff`) rather than a new hook in `SpeedScale`/`TurnOrder` | `StatusEffects.cs:17-46`; `FightSession.SpeedBuffs.cs:316-420` | `ChilledStatusTests` (whole file) |
| Chilled's only LIVE authored magnitude today is Frosty's item-modifier proc, **20%** for **2** turns (`ChilledOnHitSpeedPercent`/`ChilledOnHitTurns`) — NOT the 25% the brief's Winter's Rebuke spec names; the two are independent authored numbers on the same mechanism, not a conflict | `FightTuning.cs:148-165` | `ChilledStatusTests.ChilledOnHit_LandsTheStatus_WhenTheChanceRollSucceeds` |
| Chilled refreshes (never stacks), takes the stronger of old/new magnitude and duration, same `StatusEffects.Apply` rule as everything else | `StatusEffects.cs:65-96` | `ChilledStatusTests.Chilled_ASecondStrongerApplication_RefreshesRatherThanStacking`, `.Chilled_AWeakerReapplication_NeverWeakensTheExistingOne` |
| Chilled composes additively with other speed-percent sources against the same true base (e.g. the Necklace ramp) | `FightSession.SpeedBuffs.cs` | `ChilledStatusTests.Chilled_ComposesAdditivelyWithTheNecklaceRamp_AgainstTheSameTrueBase` |

---

## 8. Fear, Rooted, formation movement

| Behaviour | Location | Pin |
|---|---|---|
| Fear = Stunned (`HasStun` also matches `Feared`) + Vulnerable `VulnerablePercent` (**25**, one authored constant) for `durationTurns` of the HOLDER's own turns | `Fear.cs` (whole file, 32 lines) | `FearTests.FearedReadsAsStunned`, `.FearedIsAlsoVulnerable` |
| Fear is NOT spent like Stun on a single skip — it decays by turn count, so it keeps skipping every turn until its duration runs out; its own countdown happens inside `ConsumeStun` (which also handles Feared), NOT inside the generic `Tick`, specifically so it is not accidentally decremented a turn early | `StatusEffects.cs:209-230`, `:835-857` (the exemption's own reasoning) | `FearTests.FearDoesNotConsumeOnASingleSkippedTurn`, `.FearExpiresAfterItsOwnDuration`, `.FearSurvivesTheTurnStartTickThatOpensTheTurnItSkips` |
| **Today, `Rooted` forbids only `Move` for a PLAYER** — the front-rank swap (`CanMove`/`Move`, `FightSession.cs:466-519`) checks `HasRooted` on both the mover and the swap partner and refuses either way. Nothing in `CastSkill`/`ResolveAttackSwing` checks Rooted at all — a rooted player can still cast and swing freely today | `FightSession.cs:463-519` | `RootedStatusTests.ARootedPlayer_StillTakesAnOrdinaryTurn_NeverForfeits` |
| **For an ENEMY**, the SAME status instead excludes the plain-melee-attack option from `EffectivePoolFor`, forcing a skill or a forfeit — a completely different mechanical consumer of the identical `StatusEffectType.Rooted` | `FightSession.Enemies.cs:100-171` (`EffectivePoolFor`), `:253-321` (`RootedEnemyHasNoLegalAction`) | `RootedStatusTests.RootedEnemyWithACommittedPlainSwing_VoidsTheAttack_EvenWithALegalSkillAvailable`, `.RootedEnemyWithNoLegalSkill_ForfeitsItsTurn_TheSameWayStunDoes` |
| Rooted is applied to either side by "Sylvan's root" (`RootChancePercentOnHit`, an item modifier), landing on whichever side the wielder's on-hit riders fire against — but the codebase's own comment states nothing currently AUTHORS an enemy-side item carrying this modifier, so a player being Rooted today is theoretical, not live content | `FightSession.cs:860-875`; `StatusEffect.cs:113-136` (comment) | `RootedStatusTests.RootChancePercentOnHit_LandsRooted_ForExactlyTwoTurns` (**2** turns, `FightTuning.RootOnHitTurns`) |
| Rooted decays by ordinary turn count (generic `Tick`), unlike Stun | `StatusEffects.cs:792-798` | `RootedStatusTests.Rooted_SurvivesATickWithTurnsRemaining_UnlikeStunWhichIsSpentOutright`, `.Rooted_ExpiresAfterItsDuration_LikeAnyOtherTurnCountedStatus` |
| **Enemy intents that target an actor after that actor moves**: the committed target is re-validated at resolution against BOTH "still alive" and "still reachable by the committed ability's own reach mask" (`CanReachWithAbility`). If a `Move` broke reach, the enemy RE-PICKS the first eligible target in list order — **no RNG draw**, so a re-pick cannot change the seeded stream's frequency — and forfeits the turn outright if nobody is left in reach (never silently widens to an illegal target) | `FightSession.Enemies.cs:659-732` | `EnemyIntentTests` (existing file, not re-read in full here — cited by name) |
| Front-rank rule / `Reach`: `ReachKind` (`Any`/`Melee`/`ExplicitRanks`) is load-bearing as a KIND, not just a mask — a relic that lifts the front-rank rule must not also unlock an authored aim restriction that happens to carry the same mask | `docs/CODE_MAP.md:512` (already documents this; not re-derived here) | `ReachTests`, `EnemyReachTests` |

---

## 9. Status duration semantics (generic table)

Every `StatusEffectType`, when its counter decrements, and whether the
restriction still applies on the turn it reaches zero:

| Type | Decrements | On the turn it hits zero |
|---|---|---|
| Poison | `Tick`, turn START, same tick it deals damage | Removed that same tick — its LAST hit and its removal are simultaneous |
| Regen | `Tick`, turn START | Same as Poison |
| Protect | `Tick`, turn START | Removed before that turn's own actions — a status with `turns=N` protects for the bearer's next `N-1` turns' worth of incoming hits plus the remainder of the turn it was applied on, not a full `N` |
| Vulnerable | `Tick`, turn START | Same shape as Protect (see above) |
| Chilled | `Tick`, turn START (speed reverted via `RevokeSpeedBuff` when it expires) | Same shape as Protect |
| Rooted | `Tick`, turn START | Same shape as Protect |
| Marked | `Tick`, turn START — but authored at 99 turns specifically so ordinary decay can never reach zero before `ConsumeMark` does | Same shape as Protect, in principle; never observed in practice |
| Stun | **NOT** `Tick` — consumed and removed by `ConsumeStun`, called from `ResolveSkippedTurn` AFTER the skip has actually happened | The skip it promised DOES happen — this is the one type where "effective affected turns" already equals the authored number, because consumption happens at use, not on a clock |
| Feared | **NOT** `Tick` for its OWN countdown (exempted, see area 8) — decremented inside `ConsumeStun` too, once per actual skip | Same as Stun: the promised skip happens before the countdown moves |
| Provoked | **NOT** `Tick` — consumed by `ConsumeProvoke`, called after the taunted swing resolves | The redirect DOES apply to the swing that spends it |
| Shielded (ward) | **NOT** `Tick` at all — aged at the wearer's turn END via `TickWardsAtTurnEnd`, exempting the turn it went up | Stands through the whole of its last counted turn, gone at the end of it — this is the one type whose spelling already matches "N turns = N full turns of coverage" |
| Empowered | Spent on the next attack (`ConsumeEmpowerment`), not decremented by `Tick` at all in practice (duration authored generously high so ordinary ticking can't reach it first) | The spend always happens, same shape as Stun/Provoked |

Source: `StatusEffects.cs:792-874` (`Tick`, `IsSpentByTheTurn`), `:209-238`
(`ConsumeStun`/`ConsumeProvoke`), `:622-654` (`TickWardsAtTurnEnd`),
`:694-716` (`ConsumeEmpowerment`).

**This is what the brief's "effective affected turns" will be checked
against.** DIVERGES: the brief wants "restrictions remain through the final
affected action" for every new duration. Today that is already true for the
**consumed-on-use** family (Stun, Feared's skip-half, Provoked, Shielded,
Empowered) but is **not** true for the **decremented-at-turn-start** family
(Protect, Vulnerable, Chilled, Rooted, Marked) — those lose their last
counted turn's worth of protection because the counter reaches zero and the
entry is removed before that turn's action happens. Milestone A's Winter's
Rebuke ("Chilled 25% for two effective target turns... verify its final
affected turn") and Velvet Shackles (Rooted) will need to either accept this
existing shape or move onto the consumed-on-use pattern; worth a design
decision rather than an assumption either way.

---

## 10. Initiative

Charge-based scheduler (`TurnOrder<TActor>`, `TurnOrder.cs`): every entry has
`Charge`/`Rate`; `ChargeUntilNextReady` adds `Rate` to every entry each tick
until one crosses 100 (`TurnThreshold`), subtracting rather than resetting so
overflow carries forward.

| Behaviour | Location | Pin |
|---|---|---|
| Forecast (`Project`/`ProjectPushed`) SIMULATES the schedule forward on a throwaway snapshot (`Snapshot`) rather than reading list order, because with per-rate charging the same actor can legitimately act twice before a slower one acts once | `TurnOrder.cs:432-504` | `TurnOrderTests` |
| **An actor CAN appear twice (or more) in one forecast window** — already exercised, not merely claimed | `TurnOrder.cs:432-442` (comment states it) | `UpcomingTurnsTests.WrapsIntoFollowingRoundsWhenAskedForMoreTurnsThanCombatants` (`["Hero","Rat","Hero","Rat","Hero"]` for a 2:1 speed ratio) |
| **Tie-break**: "most overcharged goes first; initiative breaks EXACT ties" — `ready == null \|\| e.Charge > ready.Charge \|\| (e.Charge == ready.Charge && e.Initiative > ready.Initiative)`, so an exact charge-and-initiative tie falls through to `_entries`' own (stable) list order | `TurnOrder.cs:376-428` (`ChargeUntilNextReady`) | `TurnOrderTests.EqualInitiative_PreservesInsertionOrder`, `.Start_OrdersByInitiativeDescending` (charge-tie broken by initiative on the opening turn, since `Start()` seeds different initiatives to different charges) |
| Operations that move an actor in the queue: `PullToFront` (Gift: Haste — lands just above the current highest charge, never crossing the threshold outright, so it buys exactly the position and not an extra fraction of a turn), `PushBack(actor, slots)` (Black Ram's Headbutt — each slot drops the actor to just under whoever is charged immediately below), `GrantExtraTurn`/`PendingExtraTurns` (stacks; spent one at a time inside `SimulateForward`/the real advance), `SetSpeed` (changes `Rate`, i.e. how OFTEN someone acts, not just their position) | `TurnOrder.cs:58-65` (`SetSpeed`), `:246-368` (`GrantExtraTurn`/`PushBack`/`PullToFront`) | `WardTests.cs`'s `GiftHasteTests.PullToFront_HandsTheNextTurnToTheHurriedActor`/`.BeingHurried_DoesNotAlsoBuyTheTurnAfterIt`/`.HurryingSomethingNotInTheOrder_IsRefused` (a nested class sharing the file, not `TurnOrderTests`); `TurnOrderTests.GrantExtraTurn_MakesTheSameActorGoAgainBeforeTheQueueMovesOn`, `.GrantExtraTurn_Stacks` |
| **Stun/Feared do NOT touch `TurnOrder` at all.** The turn is granted normally by the scheduler; `ResolveSkippedTurn` intercepts it afterward and forfeits it. Only `PushBack`/`PullToFront`/`GrantExtraTurn`/`SetSpeed` actually move the schedule itself | `FightSession.Enemies.cs:586-631` (skip path) vs `TurnOrder.cs` (no Stun/Feared reference anywhere in the class) | inferred from the absence — no direct pin needed, structural |
| A reinforcement joining mid-fight is seeded by its own initiative (`SeedCharge`), not zero, so it does not act dead last purely for arriving late | `TurnOrder.cs:67-92` | `TurnOrderTests.AddCombatant_MidEncounter_ReinforcementJoinsAtCorrectInitiativeSlot` |

---

## 11. Spellbook eligibility and persistence

| Behaviour | Location | Pin |
|---|---|---|
| A skill is available to a character by: (1) level ladder (1 or 999 only — anything between is refused at content build), (2) `unlockedSkillIds` (event-taught, currently unused by any live content), (3) a talent's `grantsSkillId` (Provoke, Headbutt, Black Ram Mode), (4) `run.learnedSpells` (bought this run), (5) a reward-track `UnlockSkill` node | `ContentDatabase.cs:306-346` (`AvailableSkillsFor`) | (Core/PlayMode boundary — no EditMode pin; PlayMode coverage not re-verified here) |
| **Fury-cannot-learn-books is enforced through the POOL, not the character**: `ResolvedPool.AllowsSpellBooks` -> `SpellBooks.CanHold(pool)` (null pool reads as "yes" — graceful degradation) -> `PoolOwnership.BookRefusers` computes the refusing character-id set once, fed into FIVE independent gates that must all agree: `RunOrchestrator.CanLearn`, the shop's book offer, the dossier's slot display, `ContentDatabase.LearnedThisRun` (a stale save's book stops casting), and `SkillEntryResolver` (refused at content build time) | `SpellBooks.cs:14-35`; `PoolOwnership.cs:31-72` | Content-build-time gate: `SkillEntryResolver`'s own tests (existing file, not re-read here) |
| Three spell slots per character per run, fixed, not authored per-character | `SpellBooks.cs:9-12` (`MaxSpellSlots = 3`) | (Data-layer/Core; not an EditMode-pinnable Domain fact by itself) |
| `run.learnedSpells` (`List<LearnedSpellEntry>{characterId, slot, skillId}`) and `run.unassignedSpellBooks` (`List<string>`) are the persisted state; reconciled on load by `ReconcileLearnedSpells`, which drops an entry whose skill no longer exists, whose owner no longer exists, or whose owner's pool STOPPED reading books between save and load (returning the book to `unassignedSpellBooks` rather than discarding it) | `Assets/_Project/Scripts/Data/SaveData.cs:1047-1074` | (Data-layer; PlayMode/save-load test territory, not re-verified in this pass) |
| `bookTier` gates the shop, independent of `bookOnly` (which gates the level ladder) — the two happen to agree for all four live books today, but nothing enforces that they must | `Assets/_Project/Scripts/Data/SaveData.cs:1019-1029` (comment states the coincidence explicitly); `RawSkillEntry.cs:340-358` | `ShopStock.cs` (own tests not re-read here) |
| **The four existing books, literal table** (`Assets/_Project/ContentData/skills.json`, all `characterId: "sheep"`, `bookOnly: true`): | | |

| id | bookTier | manaCost | cooldownTurns | damageInstances |
|---|---|---|---|---|
| `mud_burst` | 1 | 8 | 2 | Earth 6 |
| `frost_flare` | 2 | 7 | 0 (none authored) | Fire 4 + Frost 4 |
| `cinderfault` | 2 | 15 | 3 | Fire 5 (+ more; `DamageAll`) |
| `lightning_bolt` | 3 | 11 | 0 (none authored) | Lightning 10 |

Citations: `skills.json:43-70` (mud_burst), `:73-99` (frost_flare), `:102-158`
(cinderfault), `:161-183` (lightning_bolt).

---

## 12. Anomalies and open questions

1. **`skills.json`'s own `_readme` (line 2) is stale.** It describes the
   mitigated-damage formula as ending "floored at 1) x10" and describes
   defense as subtracted inside the raw figure. Neither matches
   `CombatMath`/`DamagePipeline` today: `DamageScale` is 5, and as of
   2026-08-26 it is not applied to `ComputeAttackDamage`/`ComputeSkillDamage`
   at all (`CombatMath.cs:258-291`'s own header explains the removal and the
   bug it fixed). This almost certainly explains why this very assignment's
   brief said "x10 DamageScale" — the number was inherited from the readme,
   not from the code. Recommend updating the readme in a follow-up (out of
   this baseline's edit surface).

2. **`docs/SPELL_EXPANSION_BASELINE.md`'s own source brief** (the
   `spell-expansion-brief-2026-09-19` memory file) states the existing books
   as "prismatic_orb, lightning_bolt, cinderfault, one more" — `prismatic_orb`
   is NOT a book (it is Owl's own `unlockLevel: 1` starting skill, no
   `bookOnly`/`bookTier`); the actual fourth book is `mud_burst`. This
   baseline's own area 11 table is correct; flagging so the memory note gets
   corrected rather than propagated into the plan doc.

3. **`FightTuning.DefaultWardTurns`'s neighbour comment is stale by one
   value.** `StatusEffect.cs:49` still says wards last "two of the wearer's
   own turns by default" — the constant was dropped from 2 to 1 on
   2026-09-16 (`FightTuning.cs:90-98`) once wards started stacking. Not
   fixed here (out of edit surface); worth a one-line comment correction in
   a follow-up.

4. **Cooldown-starts-at-cast-not-at-resolution** (`FightSession.Cooldowns.cs`'s
   own header claims a cast "that resolves into nothing... still spends the
   cooldown") has no dedicated pin proving it for a real cast that resolves
   onto nothing — today's content has no skill effect that can legitimately
   resolve onto zero targets after passing `CanResolveSkill`'s refusal gate,
   so this is UNPINNED but also currently unreachable by any live skill.
   Left unpinned rather than forcing an artificial fixture; worth revisiting
   if Milestone B/C's consumption-based spells create a real "resolves onto
   nothing" path.

5. **The Drowned Lantern's own private mark is explicitly untested**
   (`MarksTests.cs:6-8`'s own header says so) — this is a pre-existing,
   self-documented gap, not one found by this pass. Not pinned here either,
   since doing so is outside this baseline's "no production code changes,
   minimal new tests" scope and the brief's Crownfall spec reads the
   general `Marks` facility, not the Lantern's private set.

6. **Preview purity** (`PreviewSkillPower` never mutating RNG/resources/
   counters) is enforced structurally (the method takes no `SeededRandom`
   and never reaches `DamagePipeline.AfterDefences`) but has no integration
   test asserting "board state before == board state after a preview call."
   Left UNPINNED — the structural guarantee is strong (no RNG parameter to
   spend), and constructing a meaningful mutation-guard test without a
   richer preview surface (tooltips, `FightHudModel`) felt more like new
   scaffolding than a characterization pin; flagged for the plan writer to
   decide whether Milestone B's "previews never consume setup" acceptance
   criterion needs one.

7. **Ward strength was measured via Attack, not directly via a Wisdom score.**
   The existing `PhaseFourSkillTests` pins scale ward size against the
   caster's raw `Attack` field at two values (4 and 90), which is the
   correct lever (`prism_ward`'s `scalingAxis: Spell` reads
   `CombatMath.ScaledAttack`, itself `Attack x SkillScaling.MultiplierFor
   (AbilityScores)` — Wisdom/Intelligence enters through that multiplier,
   not as a separate additive term). No existing test isolates "same Attack,
   different Wisdom" to show the multiplier moving on its own; not added
   here since `ScalingSet.MultiplierFor` is exhaustively covered by
   `CombatMathTests`/`ScalingProfile`-area tests already (not re-read in this
   pass) and re-deriving it would be redundant with existing coverage.
