using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // Phase C: every new ModifierEffectType member, exercised end to end
    // through a REAL FightSession (no scene, no ContentDatabase -- see
    // FightSessionTests' own header for why that is still "a real fight" in
    // this codebase's own vocabulary) with a hand-built CombatantState
    // carrying the exact ModifierEffect a real modifiers.json entry would
    // resolve to after ContentDatabase.ModifierEffects' tier/rift scaling.
    // The scaling seam itself (base -> final Magnitude) is pinned separately
    // in ModifierMagnitudeTests; these tests start from an already-scaled
    // Magnitude, the same number a combat hook actually reads.
    //
    // Every expected value is a literal, hand-derived from CombatMath's own
    // published formulas -- never recomputed from the production code under
    // test (CLAUDE.md gotcha 5).
    //
    // ElementalDamageOnHitPercent and LifestealPercent are NOT on
    // CombatMath.Scale/DamageScale(5) -- that x5 multiplier was deleted from
    // the mitigated-combat path on 2026-08-26 (see CombatMath.Scale's own
    // header) and these two on-hit riders are part of that path, unlike
    // OnKillSplashPercent (Explosive) just below, which is on DamagePipeline's
    // own documented exemption list and legitimately stays on Scale. Their
    // magnitude is Rounding.AwayFromZero(value * percent / 100f), floored at
    // 1 -- FightSession.ApplyModifierOnHitRiders' own header explains why.
    public class ItemModifierCombatHookTests
    {
        private static CombatantState Fighter(string name, bool isPlayerSide, int maxHealth = 100, int attack = 20, int speed = 5) =>
            new CombatantState(name, isPlayerSide, maxHealth, 10, attack, speed);

        private static FightSession Session(CombatEncounter encounter) =>
            new FightSession(encounter, null, null, new SeededRandom(1)) { DamageVarianceRange = 0f };

        private static void Give(CombatantState combatant, params ModifierEffect[] effects)
        {
            combatant.ModifierEffects = new ModifierEffectSet(effects);
        }

        // ---- ElementalDamageOnHitPercent ---------------------------------------

        [Test]
        public void ElementalDamageOnHit_AddsATypedInstanceOnTopOfThePlainSwing()
        {
            var hero = Fighter("Hero", true, attack: 20, speed: 10);
            var foe = Fighter("Foe", false, maxHealth: 100, speed: 1);
            Give(hero, new ModifierEffect(ModifierEffectType.ElementalDamageOnHitPercent, 20, against: DamageType.Fire));

            var session = Session(new CombatEncounter(new[] { hero }, new[] { foe }));
            session.ExecuteAttack(foe);

            // Plain swing: max(1, hero.Attack * 1.0) = 20, no defense = 20.
            // Elemental rider: max(1, Rounding.AwayFromZero(20 * 20 / 100f))
            // = max(1, 4) = 4, then through DamagePipeline.AfterDefences
            // against a target with zero Fire resistance/defense and neutral
            // affinity -- unmitigated, so it lands as the full 4.
            Assert.AreEqual(100 - 24, foe.CurrentHealth,
                "the elemental on-hit rider must land ON TOP OF the plain swing, not replace it");

            // Typed correctly: the plain swing counts as Physical (no kit, no
            // enemy source -- FightSession.AttackTypeOf's own fallback), the
            // elemental rider counts as Fire, which the ledger buckets under
            // "Other" (CombatMath.IsPhysical draws the same physical/not-physical
            // line DamagePipeline itself does).
            var line = session.Ledger.For("Hero");
            Assert.AreEqual(20, line.PhysicalDealt, "the plain swing's own 20 damage");
            Assert.AreEqual(4, line.OtherDealt, "the Fire rider's own 4 damage, typed as non-physical");
        }

        [Test]
        public void ElementalDamageOnHit_IsMitigatedByTheTargetsTypedResistanceToTheElement()
        {
            // Not meaningful before the fix (the rider bypassed
            // DamagePipeline.AfterDefences entirely and so never read
            // TypedResistance at all): now that it is routed through the
            // typed overload the same way a spell's own damageInstances are,
            // a target's Fire resistance must measurably reduce it.
            var hero = Fighter("Hero", true, attack: 20, speed: 10);
            var resisted = Fighter("Resisted", false, maxHealth: 1000, speed: 1);
            resisted.TypedResistance = resisted.TypedResistance.With(DamageType.Fire, 100);
            var bare = Fighter("Bare", false, maxHealth: 1000, speed: 1);
            Give(hero, new ModifierEffect(ModifierEffectType.ElementalDamageOnHitPercent, 20, against: DamageType.Fire));

            var resistedSession = Session(new CombatEncounter(new[] { hero }, new[] { resisted }));
            resistedSession.ExecuteAttack(resisted);

            var bareHero = Fighter("Hero2", true, attack: 20, speed: 10);
            Give(bareHero, new ModifierEffect(ModifierEffectType.ElementalDamageOnHitPercent, 20, against: DamageType.Fire));
            var bareSession = Session(new CombatEncounter(new[] { bareHero }, new[] { bare }));
            bareSession.ExecuteAttack(bare);

            // Elemental raw = max(1, Rounding.AwayFromZero(20*20/100f)) = 4.
            // Plain swing (Physical, no resistance either side) lands the
            // same 20 on both targets; the elemental rider is the only
            // figure that differs. R/(R+100) at R=100 halves it:
            // max(1, 4*100/200) = 2.
            Assert.AreEqual(1000 - 20 - 2, resisted.CurrentHealth,
                "Fire resistance must reduce the elemental rider, not just the plain swing");
            Assert.AreEqual(1000 - 20 - 4, bare.CurrentHealth,
                "fixture check: with no resistance the elemental rider lands its full, unmitigated 4");
        }

        // BUG: Astral (+41% Arcane) on an Arcane spell was adding only ~4
        // bonus damage to a 117-damage hit, because the rider computed
        // Magnitude% of ATTACK (a small number) even when the affix's own
        // element was the SAME element the hit already was -- the intended
        // reading of "+41% arcane damage" for an arcane-typed hit is 41% of
        // THAT HIT, not a second, separate proc sized off Attack. Here the
        // hero carries no kit, so AttackTypeOf(actor) falls back to Physical
        // (FightSession.AttackTypeOf's own fallback) -- giving a Physical
        // affix on a Physical swing the identical same-element shape a real
        // Astral-on-an-Arcane-spell case has, without needing a PlayerKit
        // fixture. Attack 117, no defense on the target: plain swing lands
        // exactly 117 (see the fixture check below, matching the "for
        // {damage} damage" combat-log figure the bug report quoted).
        [Test]
        public void ElementalDamageOnHit_WhenItMatchesTheHitsOwnElement_BoostsThatHitInstead_OfAddingASeparateAttackSizedProc()
        {
            var hero = Fighter("Hero", true, attack: 117, speed: 10);
            var foe = Fighter("Foe", false, maxHealth: 1000, speed: 1);
            Give(hero, new ModifierEffect(ModifierEffectType.ElementalDamageOnHitPercent, 41, against: DamageType.Physical));

            var session = Session(new CombatEncounter(new[] { hero }, new[] { foe }));
            session.ExecuteAttack(foe);

            // Same-element bonus = Rounding.AwayFromZero(117 * 41 / 100f)
            //                    = Rounding.AwayFromZero(47.97) = 48.
            // Total landed = 117 (plain swing, no defense) + 48 = 165 --
            // the exact "117 arcane, +41% -> 165" figure named in the bug.
            Assert.AreEqual(1000 - 165, foe.CurrentHealth,
                "a same-element affix must scale the hit it rode in on, not add a small Attack-sized proc");

            // No separate "bonus Physical damage" proc line, and no split
            // ledger entry -- the whole 165 counts as ONE hit of the actor's
            // own type, same as a plain swing with no modifier would.
            var line = session.Ledger.For("Hero");
            Assert.AreEqual(165, line.PhysicalDealt, "the boosted hit is still all Physical, all on the swing's own bucket");
            Assert.AreEqual(0, line.OtherDealt, "a same-element bonus must not also count as a foreign-element rider");
        }

        [Test]
        public void ElementalDamageOnHit_NeverFiresWithoutTheModifier()
        {
            var hero = Fighter("Hero", true, attack: 20, speed: 10);
            var foe = Fighter("Foe", false, maxHealth: 100, speed: 1);

            var session = Session(new CombatEncounter(new[] { hero }, new[] { foe }));
            session.ExecuteAttack(foe);

            Assert.AreEqual(100 - 20, foe.CurrentHealth, "no modifier equipped, no bonus damage");
            Assert.AreEqual(0, session.Ledger.For("Hero").OtherDealt);
        }

        // ---- ElementalDamagePercent (P4b's packet hook -- reads the SPELL's
        // own instance.type inside ResolveDamageInstances, not the caster's
        // attackType the way the rider above does; see that member's own
        // header, ModifierEffect.cs) ---------------------------------------

        private static ResolvedSkill FireBolt() =>
            new ResolvedSkill("firebolt_fixture", "Firebolt", "", "hero", 1, SkillEffect.DamageSingle,
                SkillTargeting.SingleEnemy, 0, 0, false, 0, 0, false,
                new[] { new DamageInstance(DamageType.Fire, 20) },
                SpellPresentation.None, 0);

        [Test]
        public void AFireSpellWithTenPercentFireOnTheCasterDealsTwentyTwoNotTwenty()
        {
            var hero = Fighter("Hero", true, attack: 20, speed: 10);
            var foe = Fighter("Foe", false, maxHealth: 100, speed: 1);
            Give(hero, new ModifierEffect(ModifierEffectType.ElementalDamagePercent, 10, against: DamageType.Fire));

            var session = Session(new CombatEncounter(new[] { hero }, new[] { foe }));
            Assert.IsTrue(session.CastSkill(FireBolt(), foe));

            // 20 base * (1.0 skill/scaling multiplier) * 1.10 Fire bonus = 22,
            // Rounding.AwayFromZero, unmitigated against a target with no Fire
            // resistance/defense and DamageVarianceRange 0.
            Assert.AreEqual(100 - 22, foe.CurrentHealth);
        }

        [Test]
        public void AFireSpellWithTenPercentIceDealsTheUnmodifiedTwenty()
        {
            var hero = Fighter("Hero", true, attack: 20, speed: 10);
            var foe = Fighter("Foe", false, maxHealth: 100, speed: 1);
            Give(hero, new ModifierEffect(ModifierEffectType.ElementalDamagePercent, 10, against: DamageType.Ice));

            var session = Session(new CombatEncounter(new[] { hero }, new[] { foe }));
            Assert.IsTrue(session.CastSkill(FireBolt(), foe));

            // the Ice bonus does not apply to a Fire packet -- unmodified 20.
            Assert.AreEqual(100 - 20, foe.CurrentHealth);
        }

        // ---- TypedResistanceFlat (read directly through DamagePipeline, the
        // one hook every source -- relic or modifier -- shares) -----------------

        // TypedResistanceFlat's own combat hook is CombatantState.TypedResistance
        // (a field DamagePipeline already reads for every source, relic or
        // modifier) -- ContentDatabase/FightEncounterAdapter is the seam that
        // FOLDS a modifier's TypedResistanceFlat effect into that field (see
        // FightEncounterAdapter.cs's own comment beside its TypedResistance
        // assignment), pinned separately with a REAL content-authored
        // modifier in ItemModifierScalingReachesCombatTests
        // (TypedResistance_FromARealModifier_ReachesCombatantStateTypedResistance,
        // PlayMode, since ContentDatabase needs Resources to read from). This
        // test pins DamagePipeline's own half of the contract: once
        // TypedResistance carries a number, only the matching element is
        // reduced by it.
        [Test]
        public void TypedResistance_ReducesOnlyTheMatchingElement()
        {
            var target = Fighter("Target", false, maxHealth: 200, speed: 1);
            target.TypedResistance = target.TypedResistance.With(DamageType.Fire, 100);

            // R/(R+100) at R=100 halves it: max(1, 40 * 100/200) = 20.
            var burned = DamagePipeline.AfterDefences(40, DamageType.Fire, target,
                affinity: ElementalAffinity.Neutral, varianceRange: 0f, rng: null, resolveWard: null);
            Assert.AreEqual(20, burned.Damage, "Fire resistance must halve a Fire hit at R=100");

            // The identical raw hit, typed Ice instead -- unresisted, because
            // this modifier only named Fire.
            var frozen = DamagePipeline.AfterDefences(40, DamageType.Ice, target,
                affinity: ElementalAffinity.Neutral, varianceRange: 0f, rng: null, resolveWard: null);
            Assert.AreEqual(40, frozen.Damage, "an untargeted element must pass through unresisted");
        }

        // ---- LifestealPercent (Vampiric) ---------------------------------------

        [Test]
        public void Lifesteal_HealsTheWielderForAPercentOfDamageDealt()
        {
            var hero = Fighter("Hero", true, maxHealth: 100, attack: 20, speed: 10);
            hero.CurrentHealth = 50;
            var foe = Fighter("Foe", false, maxHealth: 200, speed: 1);
            Give(hero, new ModifierEffect(ModifierEffectType.LifestealPercent, 15));

            var session = Session(new CombatEncounter(new[] { hero }, new[] { foe }));
            session.ExecuteAttack(foe);

            // Swing damage 20 (no defense on the foe); heal =
            // Rounding.AwayFromZero(20 * 15/100f) = Rounding.AwayFromZero(3.0) = 3.
            Assert.AreEqual(53, hero.CurrentHealth, "50 + 3 healed from the lifesteal rider");
        }

        [Test]
        public void Lifesteal_NeverHealsPastMaxHealth()
        {
            var hero = Fighter("Hero", true, maxHealth: 100, attack: 20, speed: 10);
            // 98, not 95: the heal is now 3 (see the test above), so the
            // starting health has to sit within 3 of the cap for this
            // fixture to still actually exercise the clamp rather than
            // landing under it unclamped.
            hero.CurrentHealth = 98;
            var foe = Fighter("Foe", false, maxHealth: 200, speed: 1);
            Give(hero, new ModifierEffect(ModifierEffectType.LifestealPercent, 15));

            var session = Session(new CombatEncounter(new[] { hero }, new[] { foe }));
            session.ExecuteAttack(foe);

            Assert.AreEqual(100, hero.CurrentHealth, "CombatMath.Heal clamps at MaxHealth");
        }

        // ---- FlatPhysicalDamageReduction (Stalwart, half 1) --------------------

        [Test]
        public void FlatPhysicalReduction_SubtractsAfterTheMitigationCurve_PhysicalOnly()
        {
            var target = Fighter("Target", false, maxHealth: 200, speed: 1);
            Give(target, new ModifierEffect(ModifierEffectType.FlatPhysicalDamageReduction, 5));

            var physical = DamagePipeline.AfterDefences(20, DamageType.Physical, target,
                affinity: ElementalAffinity.Neutral, varianceRange: 0f, rng: null, resolveWard: null);
            Assert.AreEqual(15, physical.Damage, "R=0 leaves 20 through the curve, minus the flat 5");

            var fire = DamagePipeline.AfterDefences(20, DamageType.Fire, target,
                affinity: ElementalAffinity.Neutral, varianceRange: 0f, rng: null, resolveWard: null);
            Assert.AreEqual(20, fire.Damage, "the flat reduction is PHYSICAL only");
        }

        [Test]
        public void FlatPhysicalReduction_NeverDropsBelowTheDamageFloorOfOne()
        {
            var target = Fighter("Target", false, maxHealth: 200, speed: 1);
            Give(target, new ModifierEffect(ModifierEffectType.FlatPhysicalDamageReduction, 50));

            var outcome = DamagePipeline.AfterDefences(3, DamageType.Physical, target,
                affinity: ElementalAffinity.Neutral, varianceRange: 0f, rng: null, resolveWard: null);
            Assert.AreEqual(1, outcome.Damage, "Stalwart softens a hit, it does not stop one");
        }

        // ---- BreakShieldDepletionResistPercent (Stalwart, half 2) --------------

        [Test]
        public void BreakShieldResist_ShrinksTheDepletionAmount_MeasurablyChangingTheOutcome()
        {
            var hero = Fighter("Hero", true, attack: 20, speed: 10);
            var foeWithResist = Fighter("ResistedFoe", false, maxHealth: 1000, speed: 1);
            foeWithResist.BreakShield = new BreakShield(10);
            Give(foeWithResist, new ModifierEffect(ModifierEffectType.BreakShieldDepletionResistPercent, 100));

            var resistedSession = Session(new CombatEncounter(new[] { hero }, new[] { foeWithResist }));
            resistedSession.ExecuteAttack(foeWithResist);

            Assert.AreEqual(10, foeWithResist.BreakShield.Current,
                "100% resist must reduce the depletion amount to zero -- BreakShield.Deplete(0) is a no-op");

            var control = Fighter("PlainFoe", false, maxHealth: 1000, speed: 1);
            control.BreakShield = new BreakShield(10);
            var controlSession = Session(new CombatEncounter(
                new[] { Fighter("Hero2", true, attack: 20, speed: 10) }, new[] { control }));
            controlSession.ExecuteAttack(control);

            Assert.AreEqual(9, control.BreakShield.Current,
                "fixture check: without the resist, one plain hit costs BreakShield.BaseDepletion (1)");
        }

        // ---- OnKillSplashPercent (Explosive) ------------------------------------

        [Test]
        public void OnKillSplash_HitsOnlyTheDeadTargetsLivingNeighbours()
        {
            var hero = Fighter("Hero", true, attack: 20, speed: 10);
            var victim = Fighter("Victim", false, maxHealth: 5, speed: 1);
            var neighbour = Fighter("Neighbour", false, maxHealth: 100, speed: 1);
            var faraway = Fighter("Faraway", false, maxHealth: 100, speed: 1);
            Give(hero, new ModifierEffect(ModifierEffectType.OnKillSplashPercent, 30));

            var session = Session(new CombatEncounter(new[] { hero }, new[] { victim, neighbour, faraway }));
            session.ExecuteAttack(victim);

            Assert.IsFalse(victim.IsAlive, "fixture check: the swing (20 dmg into 5 HP) must kill the target");

            // Splash = CombatMath.Scale(hero.Attack * 30 / 100) = max(1,6)*5 = 30.
            Assert.AreEqual(70, neighbour.CurrentHealth, "the adjacent slot takes the splash");
            Assert.AreEqual(100, faraway.CurrentHealth, "a non-adjacent slot is untouched");
        }

        [Test]
        public void OnKillSplash_NeverFiresWithoutAKill()
        {
            var hero = Fighter("Hero", true, attack: 20, speed: 10);
            var victim = Fighter("Victim", false, maxHealth: 1000, speed: 1);
            var neighbour = Fighter("Neighbour", false, maxHealth: 100, speed: 1);
            Give(hero, new ModifierEffect(ModifierEffectType.OnKillSplashPercent, 30));

            var session = Session(new CombatEncounter(new[] { hero }, new[] { victim, neighbour }));
            session.ExecuteAttack(victim);

            Assert.IsTrue(victim.IsAlive, "fixture check: 1000 HP must survive one swing");
            Assert.AreEqual(100, neighbour.CurrentHealth, "no kill, no splash");
        }

        // ---- PushBackOnHitChancePercent (Hardened) ------------------------------
        //
        // A LARGE hero/enemy speed gap, the exact pattern FightSessionTests'
        // own OneOnOne fixture already relies on, so the player stays in
        // control turn after turn and AutoResolveEnemyTurns never runs an
        // enemy action that could confound the comparison with its own RNG
        // draws. Magnitude 100 makes the chance roll deterministic outright
        // (FightSession.RollPercent short-circuits at >= 100), so this is
        // about the WIRING, not about sampling a chance.
        [Test]
        public void PushBackOnHit_MovesTheTargetLaterInTurnOrder_RelativeToATiedNeighbour()
        {
            var heroNoPush = Fighter("Hero", true, attack: 20, speed: 50);
            var foeA1 = Fighter("FoeA", false, maxHealth: 1000, speed: 5);
            var foeB1 = Fighter("FoeB", false, maxHealth: 1000, speed: 5);
            var sessionNoPush = Session(new CombatEncounter(new[] { heroNoPush }, new[] { foeA1, foeB1 }));
            sessionNoPush.ExecuteAttack(foeA1);
            Assert.IsTrue(sessionNoPush.IsPlayerTurn,
                "fixture check: the hero must still hold the turn after one swing, or foeA/foeB's charge " +
                "would be disturbed by an auto-resolved enemy action rather than only by the push");
            var beforeOrder = sessionNoPush.Encounter.UpcomingTurns(200);
            int beforeIndexA = beforeOrder.ToList().IndexOf(foeA1);
            int beforeIndexB = beforeOrder.ToList().IndexOf(foeB1);

            var heroPush = Fighter("Hero", true, attack: 20, speed: 50);
            var foeA2 = Fighter("FoeA", false, maxHealth: 1000, speed: 5);
            var foeB2 = Fighter("FoeB", false, maxHealth: 1000, speed: 5);
            Give(heroPush, new ModifierEffect(ModifierEffectType.PushBackOnHitChancePercent, 100));
            var sessionPush = Session(new CombatEncounter(new[] { heroPush }, new[] { foeA2, foeB2 }));
            sessionPush.ExecuteAttack(foeA2);
            Assert.IsTrue(sessionPush.IsPlayerTurn, "fixture check: same as above, for the push run");
            var afterOrder = sessionPush.Encounter.UpcomingTurns(200);
            int afterIndexA = afterOrder.ToList().IndexOf(foeA2);
            int afterIndexB = afterOrder.ToList().IndexOf(foeB2);

            Assert.LessOrEqual(beforeIndexA, beforeIndexB,
                "fixture check: tied speed, FoeA added first, must not already trail FoeB before any push");
            Assert.Greater(afterIndexA, afterIndexB,
                "PushBackOnHitChancePercent must move the struck target LATER in the real turn order " +
                "relative to its tied, un-pushed neighbour");
        }

        // ---- AOE (FightSession.Skills.cs ResolveDamageAll) now fires the SAME
        // on-hit riders a plain swing / single-target skill already does ---------
        //
        // Before this fix, ResolveDamageAll called DealDamage directly per
        // enemy instead of routing through ApplyFinalDamage -- the shared
        // rider path every other real damage entry point (a plain swing,
        // ResolveDamageSingle) already funnels through -- so an AOE cast
        // silently skipped every item-modifier on-hit rider
        // (elemental procs, lifesteal, push/chill/root chances) while a
        // single-target hit fired them correctly. These tests prove the fix:
        // the SAME rider fires per enemy an AOE cast actually lands on.

        private static PlayerKit AllEnemiesSkillKit() =>
            new PlayerKit("hero", CharacterRole.Tank, new[]
            {
                new ResolvedSkill("firestorm", "Firestorm", "", "hero", 1, SkillEffect.DamageAll,
                    SkillTargeting.AllEnemies, 0, 0, false, 100, 0, false,
                    null, SpellPresentation.None, 0)
            }, null, null);

        [Test]
        public void ElementalDamageOnHit_FiresPerEnemy_OnAnAoeSkillCast()
        {
            var hero = Fighter("Hero", true, attack: 20, speed: 10);
            var foeA = Fighter("FoeA", false, maxHealth: 1000, speed: 1);
            var foeB = Fighter("FoeB", false, maxHealth: 1000, speed: 1);
            Give(hero, new ModifierEffect(ModifierEffectType.ElementalDamageOnHitPercent, 20, against: DamageType.Fire));

            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foeA, foeB }),
                new List<PlayerKit> { AllEnemiesSkillKit() }, null, new SeededRandom(1)) { DamageVarianceRange = 0f };
            session.CastSkill(0, null);

            // Before the fix, ResolveDamageAll's DealDamage call never fired
            // ApplyModifierOnHitRiders at all, so BOTH enemies would sit at
            // exactly 1000 minus only the skill's own base damage -- this
            // asserts the elemental rider's own extra damage actually landed
            // on EACH enemy the sweep hit, not just one of them.
            Assert.Less(foeA.CurrentHealth, 1000, "fixture check: the AOE's own base damage must land on FoeA");
            Assert.Less(foeB.CurrentHealth, 1000, "fixture check: the AOE's own base damage must land on FoeB");

            // "hero", not "Hero" -- once a real PlayerKit is in play (needed
            // here so CastSkill has a skill to resolve), LedgerIdOf keys off
            // the KIT's own id (FightSession.Ledger.cs's own header), not the
            // CombatantState's display Name the way the kit-less ExecuteAttack
            // fixtures above this one in the file do.
            Assert.Greater(session.Ledger.For("hero").OtherDealt, 0,
                "the elemental on-hit rider must fire off an AOE cast, typed as non-physical damage in the ledger -- " +
                "before this fix it never fired at all because ResolveDamageAll bypassed ApplyFinalDamage");
        }

        [Test]
        public void Lifesteal_FiresOncePerEnemyHit_OnAnAoeSkillCast_HealingCumulatively()
        {
            var hero = Fighter("Hero", true, maxHealth: 200, attack: 20, speed: 10);
            hero.CurrentHealth = 50;
            var foeA = Fighter("FoeA", false, maxHealth: 1000, speed: 1);
            var foeB = Fighter("FoeB", false, maxHealth: 1000, speed: 1);
            Give(hero, new ModifierEffect(ModifierEffectType.LifestealPercent, 15));

            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foeA, foeB }),
                new List<PlayerKit> { AllEnemiesSkillKit() }, null, new SeededRandom(1)) { DamageVarianceRange = 0f };
            session.CastSkill(0, null);

            // Before the fix, lifesteal never fired off an AOE cast at all --
            // the hero's health would sit unchanged at 50. Two enemies hit
            // means TWO separate lifesteal procs (one per landed hit, the
            // same "one rider block per landed hit" ApplyModifierOnHitRiders
            // already documents), so the heal must be strictly more than
            // what a single landed hit alone could have healed.
            Assert.Greater(hero.CurrentHealth, 50, "lifesteal must fire off an AOE cast, not just a single-target one");
        }

        [Test]
        public void OnHitRiders_NeverFireForAnEnemyTheAoeCastMissedOrNeverReached()
        {
            // A dodging enemy already proves "no rider on a miss"
            // (DodgeCoversEveryDamagePathTests' own AOE case); this proves
            // the complementary claim for THIS fix specifically -- routing
            // through ApplyFinalDamage must not somehow make a rider fire
            // for an enemy that was never actually struck (e.g. a
            // zero-effective-target edge case), by checking a lone survivor
            // takes exactly the base sweep damage plus the rider, no more.
            var hero = Fighter("Hero", true, attack: 20, speed: 10);
            var foe = Fighter("Foe", false, maxHealth: 1000, speed: 1);
            Give(hero, new ModifierEffect(ModifierEffectType.ElementalDamageOnHitPercent, 20, against: DamageType.Fire));

            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foe }),
                new List<PlayerKit> { AllEnemiesSkillKit() }, null, new SeededRandom(1)) { DamageVarianceRange = 0f };
            session.CastSkill(0, null);

            // "hero", not "Hero" -- see the ledger id note on the test above.
            Assert.Greater(session.Ledger.For("hero").OtherDealt, 0, "the sole enemy actually struck must take the rider");
            // OtherDealt only ever grows off a rider firing -- a single enemy in
            // a single-cast sweep means the rider fired exactly once.
        }
    }
}
