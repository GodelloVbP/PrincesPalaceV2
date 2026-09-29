using System;
using PrincesPalace.Domain.Stats;


namespace PrincesPalace.Domain.Combat
{
    // Pure combat math: no state of its own, only functions over
    // CombatantState. Kept separate from CombatEncounter so every formula is
    // trivially unit-testable without needing a running fight.
    public static class CombatMath
    {
        // A plain swing is deliberately useful but remains behind authored
        // skills. Skills carry their own Power/tier multipliers; basics get
        // this one modest coefficient and no mana/resource rider.
        public const float BasicAttackPowerMultiplier = 1.2f;

        // INTERNAL rather than private -- the attributes-panel effect text
        // (Domain/Stats/AbilityEffectDescriptions.cs) names this exact
        // fallback when a character's weapon slot is neutral, so the panel's
        // "no weapon: unarmed STR-B" line reads the same grade this method
        // actually swings with rather than a second, hand-typed "B".
        internal static readonly ScalingSet UnarmedStrengthScaling =
            ScalingProfile.None.With(AbilityScore.Strength, ScalingGrade.B);
        // Fallback only — used when no per-character spell tier applies
        // (a non-player CombatantState with no Character behind it; enemies
        // never use Skill today, but FightController.SkillManaCostFor keeps
        // this as an honest fallback rather than assuming that). Real
        // player casters use ContentDatabase.GetSpellTierForLevel's
        // manaCost instead — see Assets/_Project/ContentData/spells.json.
        // Set equal to that file's level-1 manaCost so this constant can't
        // silently drift from what a level-1 character actually pays.
        public const int SkillManaCost = 10;

        // Health pools moved to a x10 scale (a Stone Golem holds 350, not
        // 35). Stat-derived damage has to move with them or an ordinary
        // attack stops meaning anything — 200 attacks could not finish a
        // fight, which is exactly how the regression was caught.
        //
        // This is a multiplier on the FINAL figure rather than a x10 on the
        // authored Attack and Defense numbers, and the difference matters in
        // two places. It keeps a single point of Strength worth a visible
        // amount of damage instead of rounding away, and it scales the
        // max(1, ...) floor along with everything else — x10'ing the stats
        // instead would leave a well-armoured target taking literally 1.
        //
        // THAT FLOOR IS NO LONGER load-BEARING against armour, because armour
        // no longer subtracts -- see DamagePipeline.AfterDefences, the canonical
        // mitigation equation. It survives as what it says on the tin: a hit is
        // never zero.
        //
        // Fixed damage authored directly on a skill (a spell's
        // damageInstances) is already written on the new scale and is
        // deliberately NOT routed through here.
        //
        // TAKEN FROM 10 TO 5, on request, to make fights -- Giant Rat in
        // particular -- take more than one hit. Halving this halves EVERY
        // (attack-defense)-derived damage number in the game uniformly,
        // player and enemy alike, which is what "all damage just less"
        // actually needs: one constant, not a pass over every enemy and
        // skill's own stats. It does NOT reach damageInstances (frost_flare,
        // lightning_bolt) or a flat statusMagnitude (the rat's own Poison) --
        // both are already-final numbers by design, per the comment above --
        // so those were halved by hand alongside this, in skills.json and
        // enemies.json, to keep them in the same proportion to plain attacks
        // they were authored in. SignatureAbsorbPerPoint (ContentDatabase.
        // Effective.cs) was halved too, for the reason its own comment
        // already documents: it is deliberately NOT coupled to this constant,
        // calibrated instead against the SIZE of a typical hit -- halve hits
        // and leave it standing, and two points of Wool absorbs what used to
        // take four, which is the exact "quietly catastrophic" failure that
        // comment records happening once already, just approached from the
        // other direction this time.
        public const int DamageScale = 5;

        // Basic attack: the raw, unmitigated swing. Mitigation is no longer
        // applied here — see DamagePipeline.AfterDefences, the single place
        // any defense term is ever subtracted, for the canonical equation.
        //
        // The weapon's SCALING is folded into the attack side, exactly as
        // before; only the subsequent Mitigate step has moved. `target` is
        // kept as a parameter for call-site stability (every production and
        // test call site already hands one in) even though nothing here
        // reads it any more — the target only ever mattered for the defense
        // term that used to live in this function.
        //
        // NO LONGER SCALED. D1 says so explicitly ("DamageScale x5 ... are
        // deleted") and a balance-validation pass (2026-08-26) proved why:
        // §P's own worked example was hand-derived by feeding WP x M straight
        // into the mitigation equation with NO x5 — every one of its pinned
        // "after DEF" figures only reproduces without the multiplier. Two
        // prior implementation passes left the x5 live here regardless (one
        // called it out of scope, the other built around it), which meant
        // every real fight resolved in 2-5 actions instead of the designed
        // 10-20 for a boss. Floored at 1 rather than run through Scale(),
        // which still exists and still serves callers OUTSIDE the mitigated-
        // combat path — see its own header.
        public static int ComputeAttackDamage(CombatantState attacker, CombatantState target)
        {
            // Enemy attacks retain their authored balance. The basic-action
            // coefficient and STR fallback are player-side rules.
            if (!attacker.IsPlayerSide)
            {
                return Math.Max(1, ScaledAttack(attacker, attacker.WeaponScaling, 1f));
            }

            // A real weapon owns its scaling completely. The fallback exists
            // only for an unarmed/neutral profile with real authored scores,
            // so STR never double-scales a sword and legacy/default fixtures
            // with an all-zero score block do not acquire a hidden penalty.
            var scaling = attacker.WeaponScaling.IsNeutral && attacker.AbilityScores.strength > 0
                ? UnarmedStrengthScaling
                : attacker.WeaponScaling;
            return Math.Max(1, ScaledAttack(attacker, scaling, BasicAttackPowerMultiplier));
        }

        // How much of a target's broad Defense (PhysicalDefense or
        // MagicalDefense, picked by damage type) actually stands between an
        // attack and its target right now — the D_broad term of
        // DamagePipeline's canonical mitigation equation.
        //
        // `ignoresDefense` (a skill flag) skips this entirely, returning 0 —
        // Sharp Horns' penetration and Last Stand's bonus never get a look
        // in, because there is nothing left for either to act on. Typed
        // Resistance (target.TypedResistance, summed in TotalDefense below)
        // is a SEPARATE term that is never skipped this way — "ignores
        // defense" means the broad stat, not a suit of elemental wards.
        //
        // BreakShield overrides everything else: for one turn's window after
        // it breaks, this returns 0 regardless of Last Stand or penetration,
        // which is the single place that rule lives so every damage path
        // reads through it rather than each deciding for itself whether to
        // check BreakShield (AUDIT.md #16 is three different rounding
        // conventions in one pipeline because no one function owned a
        // decision like this one).
        //
        // Order matters and is exactly the plan's: Last Stand's bonus first
        // (it is a property of the target alone), then BreakShield's zero
        // (which wins outright over any bonus just computed), then the
        // attacker's penetration last, against whatever survived the first
        // two steps.
        public static int BroadDefense(CombatantState target, DamageType type, CombatantState attacker, bool ignoresDefense)
        {
            if (ignoresDefense || target == null)
            {
                return 0;
            }

            int broad = IsPhysical(type) ? target.PhysicalDefense : target.MagicalDefense;

            // Last Stand T1: armour that only exists once he is hurt.
            int bonusPercent = target.Talents.BestBelowHealth(
                TalentEffectType.DefenseBonusPercentBelowHealth, target);
            if (bonusPercent > 0)
            {
                broad += broad * bonusPercent / 100;
            }

            if (target.BreakShield != null && target.BreakShield.IsBroken)
            {
                broad = 0;
            }

            // Sharp Horns' armour penetration, against whatever is left.
            if (attacker != null && broad > 0)
            {
                // The cast's own share (a Slam at full Fury) rides the same
                // percent, so the two cannot disagree about what "ignores"
                // means; capped so both together never read past all of it.
                int ignorePercent = Math.Min(100,
                    attacker.Talents.Best(TalentEffectType.IgnoreDefensePercent) + attacker.CastIgnoreDefensePercent);
                if (ignorePercent > 0)
                {
                    broad -= broad * ignorePercent / 100;
                }
            }

            // Mechanic (e): flat armour penetration, LAST -- against
            // whatever survived Last Stand's bonus, BreakShield's zero and
            // Sharp Horns' percent above, same ordering rule ("penetration
            // acts on whatever is left"). PHYSICAL ONLY: this stat is
            // authored as "melee attacks ignore armour", and every melee
            // swing in this game carries DamageType.Physical, so gating on
            // IsPhysical(type) rather than on how the blow was thrown is
            // what keeps a caster's own Physical-typed nuke sharing the
            // same rule a plain attack does, without penetration leaking
            // onto a Magical or Nature cast that has nothing to do with a
            // melee weapon.
            if (attacker != null && attacker.ArmorPenetration > 0 && IsPhysical(type))
            {
                broad -= attacker.ArmorPenetration;
            }

            return Math.Max(0, broad);
        }

        // D_broad + D_typed — the full defense figure DamagePipeline's
        // canonical equation subtracts a swing's way through. Typed
        // Resistance is summed on top and is NEVER broken or penetrated —
        // BroadDefense above is where BreakShield and penetration act, and
        // neither reaches the typed term added here.
        public static int TotalDefense(CombatantState target, DamageType type, CombatantState attacker, bool ignoresDefense)
        {
            int broad = BroadDefense(target, type, attacker, ignoresDefense);
            int typed = target?.TypedResistance.For(type) ?? 0;
            return broad + typed;
        }

        // Trample T1: more damage against a target already on its way down.
        //
        // Applied to a FINISHED damage figure rather than to the attack side,
        // unlike weapon scaling — this is a bonus against a weakened target,
        // not a property of the swing, so it should not be blunted by the
        // armour it is being aimed past. That is the same place the Assassin
        // role's own execute bonus already sits.
        public static int ApplyExecuteBonus(int damage, CombatantState attacker, CombatantState target)
        {
            if (damage <= 0 || attacker == null || target == null)
            {
                return damage;
            }

            int bonusPercent = attacker.Talents.BestBelowHealth(
                TalentEffectType.ExecuteDamageBonusPercent, target);

            return bonusPercent <= 0 ? damage : damage + damage * bonusPercent / 100;
        }

        // Last Stand T2: while badly wounded, no single hit may take more
        // than a stated slice of the target's maximum health.
        //
        // Stands in for the handoff's "cannot be critically hit". Crits exist
        // now (CritRules), but this node still uses its spike cap rather than
        // a crit immunity -- see TalentEffectType.DamageCapPercentBelowHealth'
        // own comment for the full reasoning. The intent survives intact: the
        // spike that ends the run is the thing removed, crit or not.
        public static int CapSpikeDamage(int damage, CombatantState target)
        {
            if (damage <= 0 || target == null || target.MaxHealth <= 0)
            {
                return damage;
            }

            int capPercent = target.Talents.BestBelowHealth(
                TalentEffectType.DamageCapPercentBelowHealth, target);
            if (capPercent <= 0)
            {
                return damage;
            }

            // Floored at 1 for the same reason every other reducer in this
            // file is: a cap is not immunity, and a very small max health
            // must not round the cap down to zero.
            return Math.Min(damage, Math.Max(1, target.MaxHealth * capPercent / 100));
        }

        // Attack after the wielder's scaling and any action multiplier.
        //
        // One function so a plain swing and a cast round identically —
        // AUDIT.md #16 is about three rounding conventions already coexisting
        // in this pipeline and this is where a fourth would have appeared.
        // Internal rather than private: SkillResolution.Damage reuses this
        // exact rounding for its own scaled-Attack term (Phase 4 of the
        // stat-scaling plan) rather than adding a fourth convention of
        // its own.
        internal static int ScaledAttack(CombatantState attacker, ScalingSet scaling, float actionMultiplier)
        {
            float scaled = scaling.MultiplierFor(attacker.AbilityScores) * actionMultiplier;

            // Everything CombatMath cannot work out for itself — the Lamb's
            // Weight of Wool, and Gift: Fury — arrives pre-summed on the
            // combatant. See CombatantState.BonusAttackPercent for who is
            // allowed to write it.
            //
            // Folded in HERE, on the attack side before defense is
            // subtracted, for exactly the reason ComputeAttackDamage's own
            // header gives about weapon scaling: multiplying the finished
            // figure means a 4x multiplier doubles the damage that GOT
            // THROUGH, so heavy armour would be worth a quarter as much
            // against precisely the swings it most needs to blunt.
            if (attacker.BonusAttackPercent != 0)
            {
                scaled *= 1f + attacker.BonusAttackPercent / 100f;
            }

            return Rounding.AwayFromZero(attacker.Attack * scaled);
        }

        // NOT part of the mitigated-combat path any more. ComputeAttackDamage
        // and ComputeSkillDamage — the two entry points every real player and
        // enemy swing/cast reaches DamagePipeline through — stopped calling
        // this on 2026-08-26 (see their own headers): D1 always said the x5
        // was deleted, but two prior implementation passes left it live here
        // regardless, which meant every fight in the game resolved in 2-5
        // actions instead of the designed 10-20 for a boss. Confirmed root
        // cause by a dedicated balance-validation pass (2026-08-26): feeding
        // §P's own WP x M straight into the mitigation equation with no x5
        // reproduces every one of its pinned "after DEF" figures exactly.
        //
        // KEPT rather than deleted outright, because it still legitimately
        // serves callers that are NOT on the mitigated-combat path and were
        // never part of the bug: FightHudModel's POWER/PreviewBasicSpellPower
        // readouts read ComputeAttackDamage/ComputeSkillDamage directly now
        // instead, but FightSession.Talents.cs's two unmitigated splash
        // sites (Shatter, Trample's kill splash) still call this directly —
        // a percent of raw Attack, never mitigated, deliberately left on the
        // old x5 scale by the same designer call that fixed the two entry
        // points above (see the session report for why that is worth a
        // second look before the next playtest: those two abilities now hit
        // for roughly 5x a plain swing of the same nominal size, which used
        // to not be true).
        //
        // NO LONGER SUBTRACTIVE-SHAPED in the sense its name once implied —
        // "raw difference" is history from when this took (attack - defense)
        // directly. Mitigation — HOW MUCH OF A SWING GETS PAST ARMOUR — now
        // lives entirely in DamagePipeline.AfterDefences, via TotalDefense/
        // BroadDefense above and AfterResistance below, which is what
        // replaced this file's own Mitigate/ArmourSoftening (deleted).
        public static int Scale(int rawDifference)
        {
            return Math.Max(1, rawDifference) * DamageScale;
        }

        // Skill: costs mana, hits harder than a plain Attack by
        // powerMultiplier. The multiplier is a required parameter, not a
        // hardcoded constant, specifically so it can scale with the
        // caster's level — see ContentDatabase.GetSpellTierForLevel and
        // FightController.SpellTierFor, which is where a real player
        // caster's multiplier actually comes from. This function itself
        // stays level-agnostic; Domain has no concept of Character or
        // level at all.
        //
        // The caster's SKILL scaling multiplies alongside the tier — a spell
        // rides its own stats (Intelligence for arcane, Wisdom for nature)
        // rather than the sword in the caster's other hand, which is why the
        // two profiles are separate fields on CombatantState.
        // NO LONGER SCALED — see ComputeAttackDamage's own header for why.
        // Floored at 1 the same way, so a skill and a plain swing can never
        // end up on different conventions for a zero-or-negative input.
        public static int ComputeSkillDamage(CombatantState attacker, CombatantState target, float powerMultiplier)
        {
            return Math.Max(1, ScaledAttack(attacker, attacker.SkillScaling, powerMultiplier));
        }

        // Rudimentary weakness/resistance: an attack matching one of the
        // target's weaknesses deals half again, matching one of its
        // resistances deals half, anything else is unaffected. Weakness is
        // checked first, so an enemy authored with the same type in both sets
        // (a content mistake, not a valid design -- see ElementalAffinity)
        // resolves as a weakness rather than silently cancelling out.
        // Symmetric: +50% into a weakness, -50% into a resistance.
        //
        // Weakness was 2x (a full +100%) and is now 1.5x, so the pair finally
        // mirrors. At 2x/0.5x a single matched element swung damage by a
        // factor of four end to end, which is a lot of weight to hang on one
        // authored field — and it matters more now that a spell can carry
        // several elements at once and land on both sides of the chart in the
        // same cast.
        private const float WeaknessMultiplier = 1.5f;
        public const float ResistanceMultiplier = 0.5f;

        // Applies one effectiveness multiplier to one packet of fixed damage.
        //
        // Lives here rather than in the caller so a spell's typed instances
        // and every other scaled figure round the SAME way — see
        // Rounding.AwayFromZero's own header for why away-from-zero rather
        // than UnityEngine.Mathf.RoundToInt's to-even is the rule everywhere
        // now (AUDIT.md #16). The difference is not cosmetic: a 25-point
        // packet into a resistance is exactly 12.5, the tie case.
        //
        // Floored at 1 for the usual reason: resisted is not immune.
        public static int ApplyEffectiveness(int baseDamage, float multiplier)
        {
            return Math.Max(1, Rounding.AwayFromZero(baseDamage * multiplier));
        }

        // Protect/Vulnerable, applied at the same point EffectivenessMultiplier
        // is — one more multiplicative step in the same funnel, not a second
        // one a caller has to remember to add. Floored the same way
        // ApplyEffectiveness is, for the same reason: a buff can reduce
        // damage a great deal, never to zero.
        public static int ApplyStatusEffects(int damage, CombatantState target)
        {
            if (target == null || target.Statuses.Count == 0)
            {
                return damage;
            }

            return Math.Max(1, Rounding.AwayFromZero(damage * StatusEffects.DamageTakenMultiplier(target.Statuses)));
        }

        // ONE MATCH IS THE WHOLE ANSWER, however many elements the affinity
        // names. Deliberately not additive: two weaknesses matched by one
        // typed packet is impossible (a packet carries a single DamageType),
        // and stacking multipliers per element would make "weak to four
        // things" a different KIND of vulnerability rather than a broader one.
        // A troll that burns and freezes takes 1.5x from fire and 1.5x from
        // ice, not 2.25x from either.
        // `weaknessBonusPercent` is Jo-Sun's Book of Anatomy's own reading --
        // the relic reports "x2.0 to x2.2" in the game's voice, but this
        // codebase's own weakness multiplier is 1.5, not 2.0 (see
        // WeaknessMultiplier's own comment), so the relic is authored as
        // +20% MULTIPLICATIVE against WHATEVER the base already is: 1.5 x
        // 1.2 = 1.8. Applied to the weakness branch only -- a resistance is
        // a different number entirely and "anatomy" has nothing to say
        // about it. Zero (every attacker without the relic) changes nothing.
        public static float EffectivenessMultiplier(DamageType attackType, ElementalAffinity affinity,
            int weaknessBonusPercent = 0)
        {
            if (affinity.IsWeakTo(attackType))
            {
                float multiplier = WeaknessMultiplier;
                if (weaknessBonusPercent > 0)
                {
                    multiplier *= 1f + weaknessBonusPercent / 100f;
                }

                return multiplier;
            }

            if (affinity.Resists(attackType))
            {
                return ResistanceMultiplier;
            }

            return 1f;
        }

        // How many points of resistance halve incoming damage.
        //
        // Resistance is deliberately NOT a percentage. Equipment sets stack
        // five pieces, each worth up to ~17 points at the top plus level, so
        // a full set reaches ~85 — as flat percent that is either immunity or
        // a hard cap that makes the last three pieces worthless, and both are
        // bad answers. On this curve 85 points is a 46% reduction, and no
        // amount of stacking ever reaches 100%: every point helps, later
        // points help less, and the number can grow as far as the game wants
        // it to without anyone having to revisit this function.
        //
        // reduction = R / (R + Softener), so R == Softener is exactly half.
        public const int ResistanceSoftener = 100;

        // The R/(R+Softener) shape itself, factored out once both
        // AfterResistance and DodgePercentFrom below turned out to be the
        // IDENTICAL curve read two different ways. Written as `scale *
        // numerator / (numerator + denominatorAddend)` rather than a plain
        // two-argument "rating, softener" pair because the two callers put
        // DIFFERENT halves of the curve in the numerator: AfterResistance
        // reads the curve's COMPLEMENT (Softener is the numerator -- "how
        // much of a hit gets through"), DodgePercentFrom reads the curve
        // DIRECTLY (the rating is the numerator, scaled to a percent).
        // Long arithmetic and integer division throughout, matching what
        // both call sites already did -- this only moves the one division
        // that appeared twice, it does not change how either rounds.
        private static long DiminishingReturns(long scale, long numerator, long denominatorAddend)
        {
            return scale * numerator / (numerator + denominatorAddend);
        }

        // Applies a resistance value to a damage figure.
        //
        // Integer arithmetic throughout, and floored at 1 for the same reason
        // every other damage path is: no amount of armour makes a combatant
        // unhittable.
        public static int AfterResistance(int damage, int resistance)
        {
            if (damage <= 0)
            {
                return 0;
            }

            if (resistance <= 0)
            {
                return damage;
            }

            long reduced = DiminishingReturns(damage, ResistanceSoftener, resistance);
            return (int)Math.Max(1, reduced);
        }

        // Swift's dodge curve — the IDENTICAL R/(R+Softener) shape
        // ResistanceSoftener/AfterResistance above already uses, just read
        // as a probability instead of a damage multiplier. Designer's own
        // spec, verbatim: "100 dodge = 50% chance to dodge". Same argument
        // as ResistanceSoftener's own comment for why this shape and not a
        // direct percent: every point of dodge rating helps, later points
        // help less, and no amount of stacking ever lets a modifier's raw
        // magnitude alone reach a guaranteed dodge — see
        // DamagePipeline.RollDodge's own header for why a direct-percent
        // reading of DodgeRating let a maxed Swift item exceed 100 outright
        // (an unavoidable, guaranteed dodge) before this curve existed.
        //
        // A SEPARATE named constant from ResistanceSoftener even though
        // today's value is numerically identical — "how much dodge rating
        // buys 50%" is a different tuning knob than "how much defense buys
        // 50% mitigation", and a designer retuning one must never
        // accidentally retune the other by sharing a constant.
        public const int DodgeSoftener = 100;

        // Applies the dodge curve to a raw dodge rating (Swift's own scaled
        // ModifierEffect.Magnitude, NOT a direct percent — see
        // ModifierEffectType.DodgeRating's own header), returning the
        // percent chance DamagePipeline.RollDodge actually rolls against.
        //
        // Integer division, floored — matching AfterResistance's own
        // rounding convention immediately above rather than inventing a
        // second one: this is the same shape of number (a percent read off
        // an R/(R+Softener) curve), and there is no reason for dodge to
        // round any differently than mitigation already does. Floored
        // rather than rounded also means the returned percent can NEVER
        // read as 100 for any finite rating (100 * R / (R + 100) is
        // strictly less than 100 for every finite R >= 0), which is exactly
        // the point of this curve existing: RandomOps.RollPercent's
        // `chance >= 100` guaranteed-dodge short-circuit is now reachable
        // only by an infinite rating, i.e. never by content.
        public static int DodgePercentFrom(int dodgeRating)
        {
            if (dodgeRating <= 0)
            {
                return 0;
            }

            return (int)DiminishingReturns(100, dodgeRating, DodgeSoftener);
        }

        // Which of a target's two resistances answers a given damage type.
        // Physical is its own thing; everything else — fire, ice, nature,
        // poison, arcane — is magic. One function rather than a flag on
        // DamageType so adding an element cannot forget to say which side it
        // falls on.
        public static bool IsPhysical(DamageType type)
        {
            return type == DamageType.Physical;
        }

        // Returns how much a signature resource soaked before health was
        // touched, so the caller can say so. Zero for every combatant
        // without one, which is the overwhelming majority.
        //
        // Absorption lives HERE, in the single funnel every damage source
        // already goes through, rather than at the call sites — there are
        // several (player attack, player skill, enemy turn, AOE) and one of
        // them forgetting would make the resource silently stop being armour
        // depending on who hit you.
        public static int ApplyDamage(CombatantState target, int amount)
        {
            return ApplyDamageDetailed(target, amount).Absorbed;
        }

        // What actually happened to a combatant when damage landed.
        //
        // The int-returning overload above is kept as the shape almost every
        // call site wants, and every existing one keeps compiling. This one
        // exists because Last Stand's death save is not visible in the health
        // number afterwards — a combatant left at 1 HP looks exactly like a
        // combatant that was only ever going to be left at 1 HP, so a caller
        // that wants to SAY "he refuses to fall" has no way to know from the
        // outside that anything happened.
        public readonly struct DamageResult
        {
            public readonly int Absorbed;
            public readonly bool CheatedDeath;

            // The whole packet was turned aside before any arithmetic ran
            // (Kinship, FightSession.Kinship.cs). Nothing reached health AND
            // nothing was absorbed -- Absorbed stays 0 on purpose, because a
            // caller that reads Absorbed > 0 prints "soaks N of it", and no
            // pool or ward soaked anything. A caller that shows the blow
            // (a damage number, a recoil) reads this to show nothing instead.
            public readonly bool Cancelled;

            public DamageResult(int absorbed, bool cheatedDeath, bool cancelled = false)
            {
                Absorbed = absorbed;
                CheatedDeath = cheatedDeath;
                Cancelled = cancelled;
            }

            public static DamageResult CancelledPacket => new DamageResult(0, false, cancelled: true);
        }

        public static DamageResult ApplyDamageDetailed(CombatantState target, int amount)
        {
            if (amount <= 0)
            {
                return new DamageResult(0, false);
            }

            // Last Stand T2's spike cap lands here rather than in
            // FightController.AfterDefences, even though that is where every
            // other reduction lives. This is the tighter funnel of the two:
            // AfterDefences has two overloads and is bypassed entirely by a
            // status tick, and a "no hit may exceed X" rule that a poison
            // tick can walk around is not the rule it claims to be.
            amount = CapSpikeDamage(amount, target);

            int absorbed = target.SignaturePool != null ? target.SignaturePool.Absorb(amount) : 0;
            int toHealth = amount - absorbed;

            // Last Stand T3. Checked BEFORE health is written rather than by
            // resurrecting afterwards, so nothing downstream ever observes a
            // dead combatant that is about to be undead — IsAlive is read all
            // over this codebase and a one-statement window where it lies is
            // a bug waiting for a reordering.
            bool cheatedDeath = false;
            if (toHealth >= target.CurrentHealth
                && target.CurrentHealth > 0
                && !target.CheatDeathSpent
                && target.Talents.Has(TalentEffectType.CheatDeathOncePerFight))
            {
                target.CheatDeathSpent = true;
                cheatedDeath = true;
                target.CurrentHealth = 1;

                // BLOOD PRICE T3's second half: the save also fills the primary
                // pool. Here, in the one branch that decides the save, so a
                // future path into this funnel cannot fire the save and skip
                // the Fury.
                if (target.PrimaryPool != null && target.Talents.Has(TalentEffectType.CheatDeathFillsPrimary))
                {
                    target.PrimaryPool.Gain(target.PrimaryPool.Max - target.PrimaryPool.Current);
                }

                return new DamageResult(absorbed, true);
            }

            target.CurrentHealth = Math.Max(0, target.CurrentHealth - toHealth);
            return new DamageResult(absorbed, cheatedDeath);
        }

        public static void Heal(CombatantState target, int amount)
        {
            target.CurrentHealth = Math.Min(target.MaxHealth, target.CurrentHealth + amount);
        }

        // WHAT WISDOM IS WORTH TO A HEALING SKILL, on top of its authored
        // flat and resource parts (SkillResolution.Amount).
        //
        // Half a percent of the CASTER'S OWN max health per point of Wisdom
        // above neutral. A percentage rather than a flat rate because that is
        // the only shape that answers both ends of the brief at once: a heal
        // worth about 50 on a fresh Shawn and about 200 -- a fifth of the bar
        // -- on a late build that actually bought Wisdom. A flat rate through
        // zero at 10 cannot do both, because Shawn's authored WIS 14 is 4
        // points above neutral and a level-60 Wisdom build is roughly 30, and
        // 4:30 is nowhere near 10:160.
        //
        // THE TWO ANCHORS, pinned literally in WoolgatheringHealTests:
        //   fresh Shawn  -- WIS 14, 400 max health -> 8  (Woolgathering 56)
        //   late  Shawn  -- WIS 40, 1000 max health -> 150 (Woolgathering 198)
        //
        // Why it reads the caster's max health and not the target's: it is a
        // measure of how good this caster is at mending, and a party heal that
        // grew on the frailest member would be worth more the worse off the
        // party was for reasons nobody chose.
        //
        // FLOORED AT NEUTRAL rather than signed, which is the one place this
        // departs from AbilityDerivation's house convention. Everything with
        // no authored ability scores derives a Wisdom of 0 -- an enemy built
        // from a definition with no scores block, most of the roster -- and a
        // signed rate would turn Shell Up's authored heal of 2 into a heal of
        // nothing at all. A below-neutral caster healing at their authored
        // rate is a far smaller lie than a monster whose heal silently stopped
        // working.
        public const int HealPermillePerWisdomPoint = 5;

        public static int WisdomHealBonus(CombatantState actor)
        {
            if (actor == null) return 0;

            int above = actor.AbilityScores.wisdom - AbilityDerivation.NeutralScore;
            if (above <= 0) return 0;

            return actor.MaxHealth * above * HealPermillePerWisdomPoint / 1000;
        }

        // SPENDS FROM THE PRIMARY POOL -- whatever this combatant's skills
        // actually pay in, which is mana for everyone shipped today. Clamped
        // rather than all-or-nothing: affordability was already checked by
        // SkillResolution.CanAfford before anything reaches here, and a
        // silent refusal at this depth would fire the ability for free.
        public static void SpendMana(CombatantState combatant, int amount)
        {
            combatant?.PrimaryPool?.SpendUpTo(amount);
        }

        // POURS INTO THE PRIMARY POOL, and REFUSES on a pool that does not
        // take mana effects -- returning 0, which is what makes the refusal
        // visible instead of silent. Every caller here is something the
        // player understands as "mana comes back": a mana potion
        // (FightSession.UseConsumable), RestorePartyMana, Lucky Deck's red
        // card, the relic that tops up a fraction of what is missing.
        //
        // WITHOUT THE REFUSAL A MANA POTION IS A RAGE POTION. A pool a
        // character earns by fighting must not be refillable from the
        // satchel, and the honest place to say so is here rather than at
        // each of the five call sites -- see ResourcePool.RestoredByManaEffects
        // and the plan's attack point 2.
        //
        // Returns how much actually landed, so a caller can say the true
        // number (or say nothing) rather than announcing an amount it hoped
        // for. Was void; every existing caller ignores the result and still
        // compiles.
        public static int RestoreMana(CombatantState combatant, int amount)
        {
            return CanRestoreMana(combatant) ? combatant.PrimaryPool.Gain(amount) : 0;
        }

        // WHETHER A MANA EFFECT WOULD DO ANYTHING FOR THIS COMBATANT, asked
        // separately from doing it because two other things need the answer
        // BEFORE the effect resolves: the bot's missing-mana accounting
        // (Lookahead2Policy), which would otherwise value a party mana heal
        // by how empty a bar it cannot touch is, and the affordance text a
        // potion shows. One predicate, so the answer cannot differ between
        // "what the card says" and "what happens".
        public static bool CanRestoreMana(CombatantState combatant) =>
            combatant?.PrimaryPool != null && combatant.PrimaryPool.RestoredByManaEffects;
    }
}
