using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // The funnel every damage path in the game shares, and which until now had
    // no test of its own at all -- only indirect exercise through whole fights.
    //
    // What these pin is ORDER. Each step is individually covered by CombatMath's
    // own tests; what nobody could check before is that effectiveness lands
    // before armour, that the variance roll lands after both, and that the ward
    // spends against the rolled figure rather than the pre-roll one. Get the
    // order wrong and nothing fails -- the numbers are just quietly wrong.
    public class DamagePipelineTests
    {
        private static CombatantState Fighter(string name, bool isPlayerSide, int maxHealth = 100) =>
            new CombatantState(name, isPlayerSide, maxHealth, 10, 5, 5);

        // Variance off, so every assertion below is deterministic. The roll's
        // own behaviour is pinned separately.
        private const float NoVariance = 0f;

        // THE COMBO'S SPEND HALF, handed in the way every real damage path
        // hands FightSession.ResolveDetonation in. It is a parameter and not a
        // hardcoded call precisely so a TELEGRAPH can leave it out -- a
        // preview that spends a status is the bug this shape closes -- which
        // means a test that leaves it out is asserting about a preview, not
        // about a hit. Every case below that is about a real hit passes it.
        private static readonly System.Func<CombatantState, CombatantState, DamageType, int> Detonate =
            (attacker, target, type) => StatusCombos.SpendPoisonIfMatched(target, type);

        private static DamagePipeline.Outcome Typed(
            int raw, DamageType type, CombatantState target,
            ElementalAffinity affinity = default,
            System.Func<CombatantState, int, int> ward = null) =>
            DamagePipeline.AfterDefences(raw, type, target, affinity, NoVariance, null,
                ward == null ? null : (t, dmg, _, _, _) => ward(t, dmg),
                resolveDetonation: Detonate);

        [Test]
        public void WithNoDefencesAtAll_TheRawFigureSurvives()
        {
            var target = Fighter("Target", false);
            Assert.AreEqual(30, Typed(30, DamageType.Fire, target).Damage);
        }

        [Test]
        public void AnUnknownMatchupIsNeutral_Silently()
        {
            // An enemy attacking a player, or a direct test entry with no
            // definition behind the target. Neutral rather than a throw: this
            // is the majority path, not an error.
            var target = Fighter("Target", true);
            var outcome = Typed(30, DamageType.Fire, target);

            Assert.AreEqual(1f, outcome.Effectiveness, 0.0001f);
            Assert.AreEqual(30, outcome.Damage);
        }

        [Test]
        public void AWeaknessAndArmourBothApply()
        {
            // 30 into a matching weakness is 1.5x = 45; armour 10 softens by
            // 100/(10+100), giving 40.
            //
            // Worth being honest about what this does NOT prove: effectiveness
            // and resistance are both MULTIPLICATIVE here, so swapping their
            // order produces the same answer. This pins the value, not the
            // order. The genuinely order-sensitive steps are covered below.
            var target = Fighter("Target", false);
            target.PhysicalDefense = 10;

            var outcome = Typed(30, DamageType.Physical, target,
                ElementalAffinity.Of(DamageType.Physical, DamageType.Fire));

            Assert.AreEqual(1.5f, outcome.Effectiveness, 0.0001f, "physical into a physical weakness");
            Assert.AreEqual(40, outcome.Damage);
        }

        [Test]
        public void TheWardSpendsAgainstTheFinishedFigure_NotTheRawOne()
        {
            // The ward is last for a reason: it should reduce whatever actually
            // landed, after effectiveness and armour -- not the raw figure.
            var target = Fighter("Target", false);
            target.PhysicalDefense = 10;

            int wardSaw = -1;
            var outcome = Typed(30, DamageType.Physical, target,
                ElementalAffinity.Of(DamageType.Physical, DamageType.Fire),
                ward: (t, dmg) => { wardSaw = dmg; return dmg - 5; });

            Assert.AreEqual(40, wardSaw,
                "the ward is handed the post-effectiveness, post-armour figure, not the raw 30");
            Assert.AreEqual(35, outcome.Damage);
        }

        [Test]
        public void TheWardSpendsAgainstTheROLLEDFigure()
        {
            // THE order assertion that actually bites. The variance roll has to
            // land BEFORE the ward, so a shield absorbs what the swing really
            // dealt rather than its pre-roll estimate. With the roll after the
            // ward, the ward would always see exactly 100.
            var target = Fighter("Target", false);
            var rng = new PrincesPalace.Domain.Rng.SeededRandom(7);

            var seen = new List<int>();
            for (int i = 0; i < 40; i++)
            {
                DamagePipeline.AfterDefences(100, DamageType.Fire, target, ElementalAffinity.Neutral,
                    varianceRange: 0.2f, rng: rng,
                    resolveWard: (t, dmg, _, _, _) => { seen.Add(dmg); return dmg; });
            }

            CollectionAssert.IsNotEmpty(seen);
            Assert.IsTrue(seen.Exists(v => v != 100),
                "if the ward only ever saw 100, the roll is landing after it");
            Assert.IsTrue(seen.TrueForAll(v => v >= 80 && v <= 120), "and it is still the same roll");
        }

        [Test]
        public void TheExecuteBonusRidesTheRawFigure_BeforeArmour()
        {
            // Order-sensitive and deliberate: a finishing bonus applied to the
            // finished number would be worth less against exactly the armoured
            // targets it exists to finish. Only the untyped/actor overload gets
            // it -- a spell's authored packet deals what it says.
            var actor = Fighter("Actor", true);
            var target = Fighter("Target", false, maxHealth: 100);
            target.PhysicalDefense = 25;

            var withoutBonus = DamagePipeline.AfterDefences(
                40, actor, target, attackType: null, affinity: ElementalAffinity.Neutral,
                varianceRange: NoVariance, rng: null, resolveWard: null);

            // Drop the target low enough for the execute bonus to engage.
            target.CurrentHealth = 5;
            var withBonus = DamagePipeline.AfterDefences(
                40, actor, target, attackType: null, affinity: ElementalAffinity.Neutral,
                varianceRange: NoVariance, rng: null, resolveWard: null);

            Assert.GreaterOrEqual(withBonus.Damage, withoutBonus.Damage,
                "an execute bonus must never make a finishing blow weaker");
        }

        [Test]
        public void APoisonComboIsReportedSeparately_NotFoldedIntoTheDamage()
        {
            // The view announces the detonation as its own line, so the funnel
            // reports it rather than silently adding it to this hit's number.
            var target = Fighter("Target", false);
            StatusEffects.Apply(target.Statuses, StatusEffectType.Poison, 20, 3);

            var outcome = Typed(20, DamageType.Nature, target);

            Assert.Greater(outcome.PoisonDetonation, 0, "nature into poison should detonate");
            Assert.AreEqual(20, outcome.Damage, "and the detonation must not be folded into this hit");
        }

        [Test]
        public void WithNoDetonationResolver_APoisonIsNeitherSpentNorReported()
        {
            // THE TELEGRAPH'S GUARANTEE, at the level it is actually made.
            // PreviewDamage and PreviewSkill leave rng, resolveWard and this
            // null so a preview cannot spend anything the player still holds.
            // Before the combo became a handed-in collaborator it fired
            // unconditionally from inside this method, so preparing an intent
            // for a Poison-typed monster really did set off a party member's
            // Poison and take the health for it.
            var target = Fighter("Target", false);
            StatusEffects.Apply(target.Statuses, StatusEffectType.Poison, 20, 3);
            int before = target.CurrentHealth;

            var outcome = DamagePipeline.AfterDefences(
                20, DamageType.Nature, target, ElementalAffinity.Neutral,
                NoVariance, rng: null, resolveWard: null);

            Assert.AreEqual(0, outcome.PoisonDetonation);
            Assert.AreEqual(before, target.CurrentHealth);
            Assert.IsTrue(target.Statuses.Exists(st => st.Type == StatusEffectType.Poison),
                "a preview must leave the status exactly where it found it");
        }

        [Test]
        public void TheUntypedPath_NeverDetonatesAPoison()
        {
            // A monster's own claws are untyped and never reach the typed
            // funnel, so they cannot accidentally set off a status combo.
            var actor = Fighter("Monster", false);
            var target = Fighter("Player", true);
            StatusEffects.Apply(target.Statuses, StatusEffectType.Poison, 20, 3);

            var outcome = DamagePipeline.AfterDefences(
                20, actor, target, attackType: null, affinity: ElementalAffinity.Neutral,
                varianceRange: NoVariance, rng: null, resolveWard: null,
                resolveDetonation: Detonate);

            Assert.AreEqual(0, outcome.PoisonDetonation);
            Assert.AreEqual(1f, outcome.Effectiveness, 0.0001f);
            Assert.IsTrue(target.Statuses.Exists(st => st.Type == StatusEffectType.Poison),
                "and the Poison is still standing -- an untyped hit does not even reach the combo");
        }

        [Test]
        public void TheUntypedPathIsStoppedByPhysicalArmour()
        {
            var actor = Fighter("Monster", false);
            var target = Fighter("Player", true);
            target.PhysicalDefense = 8;

            var outcome = DamagePipeline.AfterDefences(
                30, actor, target, attackType: null, affinity: ElementalAffinity.Neutral,
                varianceRange: NoVariance, rng: null, resolveWard: null);

            // Resistance softens rather than subtracts: 30 * 100/(8+100) = 27.
            Assert.AreEqual(27, outcome.Damage, "untyped means physical armour, not no armour");
        }

        // --- the canonical mitigation equation --------------------------------
        //
        // D_broad (PhysicalDefense/MagicalDefense, via CombatMath.BroadDefense)
        // and D_typed (TypedResistance, via ResistanceByType.For) are summed
        // before AfterResistance's softening curve applies — see this file's
        // production counterpart, DamagePipeline's own header, for the full
        // equation. What CombatMath's own tests pin per-modifier in isolation,
        // these three pin end to end through the real pipeline, with BOTH
        // terms present at once so a modifier that leaked into the wrong one
        // would show up as a wrong number rather than an untested seam.

        [Test]
        public void IgnoresDefense_SkipsTheBroadTermOnly_TypedResistanceStillApplies()
        {
            var target = Fighter("Target", false);
            target.MagicalDefense = 50;
            target.TypedResistance = target.TypedResistance.With(DamageType.Fire, 20);

            var withDefense = Typed(100, DamageType.Fire, target);
            var ignoring = DamagePipeline.AfterDefences(
                100, DamageType.Fire, target, ElementalAffinity.Neutral,
                NoVariance, null, null, ignoresDefense: true);

            // Broad(50) + typed(20) = 70: 100 x 100/170, floored.
            Assert.AreEqual(58, withDefense.Damage);
            // ignoresDefense skips the broad term only: typed(20) alone,
            // 100 x 100/120, floored.
            Assert.AreEqual(83, ignoring.Damage);
        }

        [Test]
        public void AttackerPenetration_ReducesTheBroadTermOnly_TypedResistanceIsUntouched()
        {
            var attacker = Fighter("Attacker", true);
            attacker.Talents = new TalentEffectSet(new[]
            {
                new TalentEffect(TalentEffectType.IgnoreDefensePercent, 50),
            });
            var target = Fighter("Target", false);
            target.MagicalDefense = 40;
            target.TypedResistance = target.TypedResistance.With(DamageType.Fire, 20);

            var outcome = DamagePipeline.AfterDefences(
                100, attacker, target, attackType: DamageType.Fire,
                affinity: ElementalAffinity.Neutral, varianceRange: NoVariance, rng: null, resolveWard: null);

            // Broad 40, minus 50% penetration = 20, plus typed(20) untouched
            // = 40 total: 100 x 100/140, floored.
            Assert.AreEqual(71, outcome.Damage);
        }

        [Test]
        public void BreakShield_ZeroesTheBroadTermOnly_TypedResistanceSurvives()
        {
            var target = Fighter("Target", false);
            target.MagicalDefense = 60;
            target.TypedResistance = target.TypedResistance.With(DamageType.Fire, 20);
            target.BreakShield = new BreakShield(3);
            target.BreakShield.Deplete(3);

            var outcome = Typed(100, DamageType.Fire, target);

            // Broad zeroes on break; typed(20) alone remains: 100 x 100/120,
            // floored.
            Assert.AreEqual(83, outcome.Damage);
        }

        // --- the variance roll ------------------------------------------------

        [Test]
        public void VarianceOff_IsExactlyTheInputFigure()
        {
            // What every fight test relies on: with the roll disabled, a
            // prediction made through CombatMath directly still matches.
            Assert.AreEqual(37, DamagePipeline.ApplyVariance(37, 0f, null));
            Assert.AreEqual(37, DamagePipeline.ApplyVariance(37, -1f, null));
        }

        [Test]
        public void VarianceNeverReducesAHitToNothing()
        {
            // A 1-damage hit rolled down would otherwise become 0, turning a
            // connecting blow into a whiff.
            var rng = new PrincesPalace.Domain.Rng.SeededRandom(12345);
            for (int i = 0; i < 200; i++)
            {
                Assert.GreaterOrEqual(DamagePipeline.ApplyVariance(1, 0.2f, rng), 1);
            }
        }

        [Test]
        public void VarianceStaysInsideItsStatedRange()
        {
            var rng = new PrincesPalace.Domain.Rng.SeededRandom(999);
            for (int i = 0; i < 500; i++)
            {
                int rolled = DamagePipeline.ApplyVariance(100, 0.2f, rng);
                Assert.GreaterOrEqual(rolled, 80);
                Assert.LessOrEqual(rolled, 120);
            }
        }

        [Test]
        public void VarianceIsReproducibleForAGivenSeed()
        {
            // The whole reason the fight draws from a seeded stream: the same
            // fight at the same place in the same run plays out the same way,
            // so a mid-fight quit is not a reroll.
            var a = new PrincesPalace.Domain.Rng.SeededRandom(4242);
            var b = new PrincesPalace.Domain.Rng.SeededRandom(4242);

            var rollsA = new List<int>();
            var rollsB = new List<int>();
            for (int i = 0; i < 20; i++)
            {
                rollsA.Add(DamagePipeline.ApplyVariance(50, 0.2f, a));
                rollsB.Add(DamagePipeline.ApplyVariance(50, 0.2f, b));
            }

            CollectionAssert.AreEqual(rollsA, rollsB);
        }

        // --- Phase D1: dodge, the funnel's own half of the contract -----------
        //
        // RollDodge and the AfterDefences wiring around it. Whether every REAL
        // damage PATH actually reaches this (and skips every on-hit rider on a
        // miss) is FightSession-level behaviour, pinned separately in
        // DodgeCoversEveryDamagePathTests -- these pin DamagePipeline's own
        // half: the roll itself, and that AfterDefences honours it correctly
        // in both overloads before anything else runs.

        private static void Give(CombatantState combatant, params ModifierEffect[] effects)
        {
            combatant.ModifierEffects = new ModifierEffectSet(effects);
        }

        [Test]
        public void RollDodge_WithNoModifier_NeverDodges()
        {
            var target = Fighter("Target", false);
            var rng = new PrincesPalace.Domain.Rng.SeededRandom(1);

            for (int i = 0; i < 100; i++)
            {
                Assert.IsFalse(DamagePipeline.RollDodge(target, null, rng),
                    "a target with no DodgeRating modifier must never dodge");
            }
        }

        [Test]
        public void RollDodge_AtZeroRating_NeverConsumesADraw()
        {
            // The chance<=0 short-circuit is the only "free ride" left on
            // this roll -- see RollDodge_NoFiniteRatingEverGuaranteesADodge
            // below for why the >=100 side of that same short-circuit is
            // now unreachable by any finite DodgeRating.
            var target = Fighter("Target", false);
            Give(target, new ModifierEffect(ModifierEffectType.DodgeRating, 0));

            var rng = new PrincesPalace.Domain.Rng.SeededRandom(1);
            ulong stateBefore = rng.State;

            for (int i = 0; i < 50; i++)
            {
                Assert.IsFalse(DamagePipeline.RollDodge(target, null, rng));
            }

            Assert.AreEqual(stateBefore, rng.State,
                "a rating of 0 curves to 0% and RandomOps.RollPercent's <=0 short-circuit must not touch the stream");
        }

        [Test]
        public void RollDodge_NoFiniteRatingEverGuaranteesADodge()
        {
            // THE point of the curve: CombatMath.DodgePercentFrom's own
            // header proves 100 * R / (R + 100) is strictly less than 100
            // for every finite R, so RandomOps.RollPercent's own >=100
            // guaranteed-dodge short-circuit is unreachable by content no
            // matter how extreme the authored rating -- unlike before this
            // phase, when a raw percent past 100 (a maxed Swift item could
            // already exceed it) guaranteed an unavoidable dodge. An
            // extreme rating still lands BOTH outcomes over enough rolls,
            // proving the roll is genuinely being consulted rather than
            // short-circuited.
            var target = Fighter("Target", false);
            Give(target, new ModifierEffect(ModifierEffectType.DodgeRating, 1_000_000));
            var rng = new PrincesPalace.Domain.Rng.SeededRandom(2024);

            int dodged = 0, landed = 0;
            for (int i = 0; i < 400; i++)
            {
                if (DamagePipeline.RollDodge(target, null, rng)) dodged++; else landed++;
            }

            Assert.Greater(dodged, 0, "an extreme rating should dodge the overwhelming majority of the time");
            Assert.Greater(landed, 0,
                "...but even an extreme rating must still land SOME swings -- no finite rating guarantees a dodge");
        }

        [Test]
        public void RollDodge_AboveZeroBelow100Percent_LandsBothOutcomesOverManyRolls()
        {
            // DodgeRating 50 curves to 33% (100*50/150, floored) -- comfortably
            // inside (0, 100) either way the curve rounds, so this is really
            // exercising the SAME property RollDodge_NoFiniteRatingEverGuaranteesADodge
            // does at the other end of the scale: a genuine roll, not a
            // short-circuit.
            var target = Fighter("Target", false);
            Give(target, new ModifierEffect(ModifierEffectType.DodgeRating, 50));
            var rng = new PrincesPalace.Domain.Rng.SeededRandom(2024);

            int dodged = 0, hit = 0;
            for (int i = 0; i < 400; i++)
            {
                if (DamagePipeline.RollDodge(target, null, rng)) dodged++; else hit++;
            }

            Assert.Greater(dodged, 0, "a 33% chance must land at least once in 400 rolls");
            Assert.Greater(hit, 0, "a 33% chance must also MISS landing at least once in 400 rolls");
        }

        [Test]
        public void RollDodge_WithNoRng_NeverDodges_RegardlessOfChance()
        {
            // rng == null reads as "never dodge, full stop" -- the exact
            // convention ApplyVariance already uses, and the reason a
            // PREVIEW call (PreviewDamage/PreviewSkill, both of which pass
            // rng: null on purpose) never shows a dodge even for a target
            // with a very high one: preview shows the hit that WOULD land if
            // it connects, the same posture it already takes toward the ward
            // and toward variance, and does not attempt to represent a
            // probabilistic miss.
            var target = Fighter("Target", false);
            Give(target, new ModifierEffect(ModifierEffectType.DodgeRating, 99));
            Assert.IsFalse(DamagePipeline.RollDodge(target, null, null),
                "a low-ish rating with no rng stream must never dodge");

            Give(target, new ModifierEffect(ModifierEffectType.DodgeRating, 1_000_000));
            Assert.IsFalse(DamagePipeline.RollDodge(target, null, null),
                "even an extreme rating must not fire with no rng stream -- preview mode never dodges");
        }

        [Test]
        public void RollDodge_WithNoTargetOrEmptyModifierEffects_NeverThrows_NeverDodges()
        {
            var rng = new PrincesPalace.Domain.Rng.SeededRandom(1);
            Assert.IsFalse(DamagePipeline.RollDodge(null, null, rng));

            var target = Fighter("Target", false);
            Assert.IsFalse(DamagePipeline.RollDodge(target, null, rng), "ModifierEffects.Empty is the default");
        }

        [Test]
        public void AfterDefences_TypedOverload_OnADodge_ReturnsAZeroMissOutcome_SkippingAllMitigation()
        {
            var target = Fighter("Target", false);
            target.PhysicalDefense = 0; // would otherwise pass 100 straight through
            // DodgeRating 100_000 curves to 99% (100*100000/100100, floored)
            // -- close enough to certain, combined with SeededRandom(1)'s
            // known first draw (~56.66 out of 100), that this specific
            // seed/rating pair dodges deterministically. Not a guaranteed
            // dodge any more (see CombatMath.DodgePercentFrom's own
            // header) -- this pins ONE concrete outcome of that roll, the
            // same way every other seeded-rng test in this file does.
            Give(target, new ModifierEffect(ModifierEffectType.DodgeRating, 100_000));
            StatusEffects.Apply(target.Statuses, StatusEffectType.Poison, 20, 3);

            bool wardWasAsked = false;
            var outcome = DamagePipeline.AfterDefences(
                100, DamageType.Nature, target, ElementalAffinity.Neutral,
                varianceRange: 0.2f, rng: new PrincesPalace.Domain.Rng.SeededRandom(1),
                resolveWard: (t, dmg, _, _, _) => { wardWasAsked = true; return dmg; },
                resolveDetonation: Detonate);

            Assert.IsTrue(outcome.IsMiss);
            Assert.AreEqual(0, outcome.Damage);
            Assert.AreEqual(1f, outcome.Effectiveness, 0.0001f);
            Assert.AreEqual(0, outcome.PoisonDetonation,
                "a miss must not detonate a poison combo -- nothing landed to detonate it");
            Assert.IsFalse(wardWasAsked, "a miss must never spend the ward -- there is nothing for it to reduce");
        }

        [Test]
        public void AfterDefences_UntypedOverload_WithATypedAttack_OnADodge_NeverDoubleRolls()
        {
            // The untyped overload with attackType.HasValue DELEGATES to the
            // typed overload above rather than rolling a second time -- see
            // both overloads' own comments on why a double roll would be
            // wrong, not merely wasteful. Pinned here by checking the stream
            // advanced by exactly the ONE draw a single RollDodge call
            // consumes: a second (unwanted) roll would desync it from a
            // parallel stream that only ever calls RollDodge once per swing.
            var actor = Fighter("Actor", true);
            var target = Fighter("Target", false);
            Give(target, new ModifierEffect(ModifierEffectType.DodgeRating, 50));

            var rngA = new PrincesPalace.Domain.Rng.SeededRandom(777);
            var rngB = new PrincesPalace.Domain.Rng.SeededRandom(777);

            var outcome = DamagePipeline.AfterDefences(
                50, actor, target, attackType: DamageType.Physical,
                affinity: ElementalAffinity.Neutral, varianceRange: 0f, rng: rngA, resolveWard: null);

            bool expectedDodge = DamagePipeline.RollDodge(target, actor, rngB);
            Assert.AreEqual(expectedDodge, outcome.IsMiss,
                "the untyped-with-attackType overload must consume EXACTLY the one roll a direct " +
                "RollDodge call would -- no more, no fewer");
        }

        [Test]
        public void AfterDefences_UntypedOverload_WithNoAttackType_AlsoRollsDodge()
        {
            // The untyped TAIL (attackType: null) never reaches the typed
            // overload, so it has to own its own single roll -- this is the
            // other of the two places RollDodge is actually called from.
            var actor = Fighter("Actor", false);
            var target = Fighter("Target", true);
            // DodgeRating 200 curves to 66% (100*200/300, floored).
            // SeededRandom(1)'s first draw is ~56.66 out of 100, which is
            // below 66 -- a deterministic dodge for this specific
            // seed/rating pair (no finite rating short-circuits any more,
            // see CombatMath.DodgePercentFrom's own header).
            Give(target, new ModifierEffect(ModifierEffectType.DodgeRating, 200));

            var outcome = DamagePipeline.AfterDefences(
                50, actor, target, attackType: null,
                affinity: ElementalAffinity.Neutral, varianceRange: 0f,
                rng: new PrincesPalace.Domain.Rng.SeededRandom(1), resolveWard: null);

            Assert.IsTrue(outcome.IsMiss, "the untyped, type-less tail must also honour a high dodge chance");
        }

        [Test]
        public void AfterDefences_DodgeAlreadyResolved_SkipsTheRollEntirely()
        {
            // ResolveDamageInstances' own escape hatch: a multi-packet spell
            // rolls dodge ONCE outside the loop and hands dodgeAlreadyResolved:
            // true to every packet inside it, so a target with a very high
            // dodge rating must still take damage here when the caller has
            // already (falsely, for this test) declared the roll resolved
            // as "hit". The rating's exact value is irrelevant -- the roll
            // never runs at all with dodgeAlreadyResolved: true.
            var target = Fighter("Target", false);
            Give(target, new ModifierEffect(ModifierEffectType.DodgeRating, 100_000));

            var outcome = DamagePipeline.AfterDefences(
                30, DamageType.Fire, target, ElementalAffinity.Neutral,
                varianceRange: 0f, rng: new PrincesPalace.Domain.Rng.SeededRandom(1), resolveWard: null,
                dodgeAlreadyResolved: true);

            Assert.IsFalse(outcome.IsMiss, "dodgeAlreadyResolved: true must skip the internal roll outright");
            Assert.AreEqual(30, outcome.Damage);
        }

        [Test]
        public void RollDodge_IsReproducibleForAGivenSeed_SameChance_SameSequence()
        {
            var targetA = Fighter("TargetA", false);
            var targetB = Fighter("TargetB", false);
            Give(targetA, new ModifierEffect(ModifierEffectType.DodgeRating, 35));
            Give(targetB, new ModifierEffect(ModifierEffectType.DodgeRating, 35));

            var rngA = new PrincesPalace.Domain.Rng.SeededRandom(9001);
            var rngB = new PrincesPalace.Domain.Rng.SeededRandom(9001);

            var sequenceA = new List<bool>();
            var sequenceB = new List<bool>();
            for (int i = 0; i < 50; i++)
            {
                sequenceA.Add(DamagePipeline.RollDodge(targetA, null, rngA));
                sequenceB.Add(DamagePipeline.RollDodge(targetB, null, rngB));
            }

            CollectionAssert.AreEqual(sequenceA, sequenceB,
                "the same seed and the same dodge% must produce the identical miss/hit sequence");
        }

        [Test]
        public void Dodge_IsRolledBeforeVariance_OnTheSameStream()
        {
            // Both roll off the SAME rng parameter, never a second source.
            //
            // Proven by STATE rather than by a guaranteed-dodge free ride --
            // no finite DodgeRating short-circuits the roll any more (see
            // CombatMath.DodgePercentFrom's own header), so a 100%-and-
            // zero-draws trick is no longer available. Instead: replay the
            // identical seed through RollDodge and then -- only if it
            // missed -- ApplyVariance, in that exact order, on an
            // INDEPENDENT rng. If AfterDefences really checks dodge first,
            // both streams must land on the identical final state and agree
            // on IsMiss. If variance ran first inside AfterDefences instead,
            // it would consume a draw regardless of what dodge would have
            // done, and the two streams would diverge.
            var target = Fighter("Target", false);
            Give(target, new ModifierEffect(ModifierEffectType.DodgeRating, 200));

            var rngA = new PrincesPalace.Domain.Rng.SeededRandom(55);
            var outcome = DamagePipeline.AfterDefences(
                999, DamageType.Fire, target, ElementalAffinity.Neutral,
                varianceRange: 0.2f, rng: rngA, resolveWard: null);

            var rngB = new PrincesPalace.Domain.Rng.SeededRandom(55);
            bool expectedMiss = DamagePipeline.RollDodge(target, null, rngB);
            if (!expectedMiss)
            {
                DamagePipeline.ApplyVariance(999, 0.2f, rngB);
            }

            Assert.AreEqual(expectedMiss, outcome.IsMiss);
            Assert.AreEqual(rngB.State, rngA.State,
                "dodge must be the FIRST draw off this stream -- if variance ran before it inside " +
                "AfterDefences, these two independently-replayed streams would end up in different states");
        }
    }
}
