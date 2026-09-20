using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // GALE SCYTHE (plan 2.8): Wind damage to every enemy, and each one it
    // HITS loses a place in the order.
    //
    // THE THREE WORDS THAT CARRY THE WHOLE SPELL are "each one it hits". A
    // sweep that delayed everything it was aimed at would delay a dodger and
    // would reach for a corpse; the batch therefore runs after every enemy has
    // resolved and every death has settled, over landed-hit survivors only
    // (plan 1.9 rule 6). Every test here is about that list rather than about
    // the damage, which is an ordinary fixed-packet AOE already pinned by
    // CinderfaultSpellTests.
    //
    // DRIVEN THROUGH CastSkill (dispatcher) throughout, per the plan's
    // Appendix B: "which survivors were displaced" is a claim about the
    // sweep's own resolution order, and calling ResolveDamageAll directly
    // would skip the payment and death paths the claim depends on.
    //
    // TWO FIXTURE CHOICES THAT ARE LOAD-BEARING, both stated here rather than
    // repeated at five call sites.
    //
    // (1) ASSERTED IN CHARGES, NOT IN FORECAST POSITIONS. At the opening of an
    //     encounter every charge is seeded from Speed, so the whole field sits
    //     between 0 and 40 against a threshold of 100 and one displacement
    //     LEVEL is a handful of points -- a real move that crosses nobody yet.
    //     An order-based assertion would pass whether the delay landed on the
    //     right enemy, the wrong one, or nobody at all, which is the failure
    //     mode CODE_STANDARDS section 8 calls a test that covers nothing.
    //
    // (2) THE TEST'S SWEEP IS A FREE ACTION. An ordinary cast ends the turn,
    //     and AdvanceAfterAction then ticks every charge forward until
    //     somebody is ready -- which buries the displacement under a much
    //     larger number before anything can be read. freeAction stops the
    //     clock at exactly the moment the batch has run. The batch itself is
    //     the same code either way (ResolveDamageAll), and the shipped
    //     gale_scythe row authors no freeAction.
    public class GaleScytheTests
    {
        // Speeds ARE the charges at the opening of an encounter
        // (TurnOrder.Start seeds from initiative, and CombatEncounter passes
        // Speed as initiative), so the board below is a charge landscape of
        // Hero -60 (it has taken its turn), Front 14, Middle 9, Rear 5 -- far
        // enough apart that each one is its own level for a delay to fall
        // past.
        private const float HeroCharge = -60f; // SeedCharge(40) - TurnThreshold
        private const float FrontCharge = 14f;
        private const float MiddleCharge = 9f;
        private const float RearCharge = 5f;

        private static ResolvedSkill Scythe(int packet = 5, int pushSlots = 1) =>
            new ResolvedSkill("gale_scythe", "Gale Scythe", "", "sheep", 1,
                SkillEffect.DamageAll, SkillTargeting.AllEnemies, 0, 0, false, 0, 0, false,
                new[] { new DamageInstance(DamageType.Wind, packet) }, SpellPresentation.None, 0,
                queuePushSlots: pushSlots, freeAction: true);

        private static (FightSession session, CombatantState hero, List<CombatantState> foes) Sweep(
            ResolvedSkill skill, int foeHealth = 1000)
        {
            var hero = new CombatantState("Hero", true, 500, 50, 40, 40);
            var foes = new List<CombatantState>
            {
                new CombatantState("Front", false, foeHealth, 10, 5, 14),
                new CombatantState("Middle", false, foeHealth, 10, 5, 9),
                new CombatantState("Rear", false, foeHealth, 10, 5, 5),
            };

            var kit = new PlayerKit("sheep", CharacterRole.Support, new[] { skill }, null, DamageType.Wind);
            var session = new FightSession(new CombatEncounter(new[] { hero }, foes),
                new List<PlayerKit> { kit }, null, new SeededRandom(11))
            {
                DamageVarianceRange = 0f,
            };

            Assert.AreSame(hero, session.Current, "fixture: the caster has to be the one acting");
            Assert.AreEqual(HeroCharge, session.Encounter.ChargeOf(hero), "fixture: the charge landscape");
            Assert.AreEqual(FrontCharge, session.Encounter.ChargeOf(foes[0]));
            Assert.AreEqual(MiddleCharge, session.Encounter.ChargeOf(foes[1]));
            Assert.AreEqual(RearCharge, session.Encounter.ChargeOf(foes[2]));
            return (session, hero, foes);
        }

        [Test]
        public void ASweepDelaysEverySurvivorItHit()
        {
            var (session, _, foes) = Sweep(Scythe());

            Assert.IsTrue(session.CastSkill(0, foes[0]), "the cast was refused");
            foreach (var foe in foes) Assert.IsTrue(foe.IsAlive, "fixture: nobody was meant to die");

            // EVERY DESTINATION OFF THE SAME PRE-PASS BOARD (1.9 rule 1).
            // Front falls past Middle's ORIGINAL 9, not past the 4 Middle is
            // about to be given; Middle falls past Rear's original 5; Rear has
            // only the caster beneath it and lands one under that.
            Assert.AreEqual(8f, session.Encounter.ChargeOf(foes[0]));
            Assert.AreEqual(4f, session.Encounter.ChargeOf(foes[1]));
            Assert.AreEqual(HeroCharge - 1f, session.Encounter.ChargeOf(foes[2]));
        }

        [Test]
        public void ASweepThatKillsTwo_DisplacesOnlyTheSurvivors_AndThrowsNothing()
        {
            // 1.9 rule 6, and the risk the plan names by name: a batch that
            // reached for a dead entry. Two enemies die to the packet and one
            // survives, so the survivor list is one short of the list the
            // sweep was aimed at.
            //
            // THE DEAD STAY IN THE ORDER -- nothing in this game calls
            // TurnOrder.RemoveCombatant on a death -- so their charges are
            // still part of the landscape the survivor measures itself
            // against. That is not an oversight in the batch: it is
            // ApplyPushBack's existing rule, reused unchanged as 1.9 says to.
            var (session, _, foes) = Sweep(Scythe(packet: 40));
            foes[0].CurrentHealth = 5;
            foes[1].CurrentHealth = 5;

            Assert.DoesNotThrow(() => session.CastSkill(0, foes[0]),
                "the batch reached for an entry the sweep had just killed");

            Assert.IsFalse(foes[0].IsAlive, "fixture: the first two were meant to die");
            Assert.IsFalse(foes[1].IsAlive);
            Assert.IsTrue(foes[2].IsAlive, "fixture: one was meant to survive");

            Assert.AreEqual(FrontCharge, session.Encounter.ChargeOf(foes[0]),
                "a dead entry was displaced");
            Assert.AreEqual(MiddleCharge, session.Encounter.ChargeOf(foes[1]),
                "a dead entry was displaced");
            Assert.AreEqual(HeroCharge - 1f, session.Encounter.ChargeOf(foes[2]),
                "the one survivor fell past the only charge below its own, which is the caster's");
        }

        [Test]
        public void ASweepThatKillsEveryone_AppliesNothingAndThrowsNothing()
        {
            // 1.9's empty case, stated in the plan and easy to leave untested
            // because it looks like it cannot happen. It can: a late-run
            // sweep against three weakened enemies is the ordinary way a
            // fight ends.
            var (session, hero, foes) = Sweep(Scythe(packet: 40));
            foreach (var foe in foes) foe.CurrentHealth = 5;

            Assert.DoesNotThrow(() => session.CastSkill(0, foes[0]));

            foreach (var foe in foes) Assert.IsFalse(foe.IsAlive);
            Assert.AreEqual(FrontCharge, session.Encounter.ChargeOf(foes[0]));
            Assert.AreEqual(MiddleCharge, session.Encounter.ChargeOf(foes[1]));
            Assert.AreEqual(RearCharge, session.Encounter.ChargeOf(foes[2]));
            Assert.AreEqual(HeroCharge, session.Encounter.ChargeOf(hero));
        }

        [Test]
        public void AnEnemyThatDodgesTheScythe_KeepsItsPlaceInTheOrder()
        {
            // "EACH ONE IT HITS", and the dodger is the case that tells the
            // two readings apart.
            //
            // 100_000 IS NOT A GUARANTEE, it is 99% after the dodge curve's
            // integer floor (CombatMath.DodgePercentFrom -- no finite rating
            // reaches 100). This borrows DodgeCoversEveryDamagePathTests' own
            // AlwaysDodge constant and its habit of pairing it with one fixed
            // seed on an otherwise-empty fixture. The assertion that the
            // dodger took NO damage is what keeps the test honest if the
            // stream ever stops co-operating: it then fails as a fixture
            // problem by name rather than passing for the wrong reason.
            var (session, _, foes) = Sweep(Scythe());
            foes[1].ModifierEffects = new ModifierEffectSet(
                new[] { new ModifierEffect(ModifierEffectType.DodgeRating, 100_000) });

            Assert.IsTrue(session.CastSkill(0, foes[0]), "the cast was refused");

            Assert.AreEqual(foes[1].MaxHealth, foes[1].CurrentHealth, "fixture: the dodger took damage");
            Assert.AreEqual(MiddleCharge, session.Encounter.ChargeOf(foes[1]),
                "an enemy that dodged the sweep was displaced anyway");

            // AND THE TWO THAT WERE HIT STILL FALL PAST THE DODGER'S LEVEL.
            // The dodger is not removed from the landscape, only from the
            // target list -- it is still a living entry at charge 9, and
            // Front's destination is measured against it exactly as it would
            // be against an enemy that took the hit.
            Assert.AreEqual(8f, session.Encounter.ChargeOf(foes[0]),
                "the enemies that were hit have to move, or the assertion above proves nothing");
            Assert.AreEqual(HeroCharge - 1f, session.Encounter.ChargeOf(foes[2]));
        }

        [Test]
        public void ASweepWithNoQueuePush_TouchesTheOrderNotAtAll()
        {
            // THE VACUITY GUARD FOR THE WHOLE FILE. Every test above asserts
            // that charges moved, and a bug that shuffled the schedule for
            // some unrelated reason would satisfy all of them. Cinderfault is
            // the shipped AOE that authors no push, and its sweep must leave
            // every charge exactly where it found it.
            var (session, hero, foes) = Sweep(Scythe(pushSlots: 0));

            Assert.IsTrue(session.CastSkill(0, foes[0]), "the cast was refused");

            Assert.AreEqual(HeroCharge, session.Encounter.ChargeOf(hero));
            Assert.AreEqual(FrontCharge, session.Encounter.ChargeOf(foes[0]),
                "an AOE authoring no queuePushSlots moved the order");
            Assert.AreEqual(MiddleCharge, session.Encounter.ChargeOf(foes[1]));
            Assert.AreEqual(RearCharge, session.Encounter.ChargeOf(foes[2]));
        }
    }
}
