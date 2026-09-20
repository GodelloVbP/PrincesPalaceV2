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
    // ASHEN RECKONING (plan 2.5, SkillEffect.Reclaim): one detonation at a
    // 150% premium, split across Poison and Fire with the odd point to
    // Poison, no caster scaling, one death settlement across the split.
    // Driven through CastSkill throughout, per Appendix B -- the ordering
    // under test (refuse with no Poison, dodge before consuming, one
    // settlement across several packets) is the dispatcher's own.
    public class AshenReckoningTests
    {
        private static ResolvedSkill Reckoning() =>
            new ResolvedSkill("ashen_reckoning", "Ashen Reckoning", "test fixture", "hero", 1,
                SkillEffect.Reclaim, SkillTargeting.SingleEnemy, 0, 0, false, 0, 0, false,
                null, SpellPresentation.None, 0,
                appliesStatus: StatusEffectType.Vulnerable, statusMagnitude: 20, statusDuration: 1,
                requiresStatus: StatusEffectType.Poison, detonationPercent: 150,
                detonationSplit: new[] { DamageType.Poison, DamageType.Fire });

        private static (FightSession session, CombatantState hero, CombatantState foe) Fight(
            CombatantState foe = null, params ModifierEffect[] foeEffects)
        {
            var hero = new CombatantState("Hero", true, 500, 50, 40, 10);
            foe = foe ?? new CombatantState("Foe", false, 1000, 10, 5, 1);
            if (foeEffects.Length > 0) foe.ModifierEffects = new ModifierEffectSet(foeEffects);

            var kit = new PlayerKit("hero", CharacterRole.Tank, new[] { Reckoning() }, null, null);
            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foe }),
                new List<PlayerKit> { kit }, null, new SeededRandom(3))
            {
                DamageVarianceRange = 0f,
            };
            return (session, hero, foe);
        }

        // A PURE UNIT TEST of the split arithmetic (1.7) -- no session, no
        // combatant, because ConsumedTotalSplit needs neither.
        [Test]
        public void ASeventeenPointTotalSplitsAsNinePoisonAndEightFire()
        {
            var split = ConsumedTotalSplit.Split(17, new[] { DamageType.Poison, DamageType.Fire });

            Assert.AreEqual(2, split.Count);
            Assert.AreEqual(DamageType.Poison, split[0].type);
            Assert.AreEqual(9, split[0].amount, "the odd point goes to the FIRST type in the list");
            Assert.AreEqual(DamageType.Fire, split[1].type);
            Assert.AreEqual(8, split[1].amount);
        }

        [Test]
        public void NoPoisonOnTheTarget_IsRefused_BeforeAnythingIsPaid()
        {
            var (session, hero, foe) = Fight();
            int manaBefore = hero.PrimaryPool.Current;

            bool cast = session.CastSkill(0, foe);

            Assert.IsFalse(cast);
            Assert.AreEqual(manaBefore, hero.PrimaryPool.Current, "a board-state refusal spends nothing");
        }

        [Test]
        public void TheReckoningsPoisonHalf_TriggersNoSecondDetonation()
        {
            var (session, _, foe) = Fight();
            // worth = 10 x 2 = 20; premium 150% = 30, split 15 Poison / 15 Fire.
            foe.Statuses.Add(new ActiveStatus(StatusEffectType.Poison, 10, 2));
            int before = foe.CurrentHealth;

            session.CastSkill(0, foe);

            Assert.AreEqual(before - 30, foe.CurrentHealth,
                "exactly the consumed-and-marked-up total -- a re-detonation of the Poison-typed " +
                "half would add more than this");
            Assert.IsFalse(foe.Statuses.Any(s => s.Type == StatusEffectType.Poison),
                "the whole pile was consumed in one sweep; there is nothing left for either " +
                "split packet to re-enter SpendPoisonIfMatched against");
        }

        [Test]
        public void AReckoningThatKillsAcrossBothHalves_RecordsOneKillRow()
        {
            var foe = new CombatantState("Foe", false, 10, 10, 5, 1);
            var (session, hero, _) = Fight(foe);
            // worth = 10 x 2 = 20; premium 150% = 30, comfortably past 10 HP.
            foe.Statuses.Add(new ActiveStatus(StatusEffectType.Poison, 10, 2));

            session.CastSkill(0, foe);

            Assert.IsFalse(foe.IsAlive);
            // Keyed by the PLAYER KIT's id ("hero"), not the combatant's
            // display name -- LedgerIdOf prefers KitFor(combatant).Id when
            // one exists.
            Assert.AreEqual(1, session.Ledger.For("hero").Kills,
                "one death settlement across the whole split, not one per packet");
        }

        [Test]
        public void ThePremiumIsTheOnlyMultiplier()
        {
            var (session, hero, foe) = Fight();
            foe.Statuses.Add(new ActiveStatus(StatusEffectType.Poison, 10, 2)); // worth 20
            hero.Attack = 999; // wildly boosted; must change nothing below
            int before = foe.CurrentHealth;

            session.CastSkill(0, foe);

            Assert.AreEqual(before - 30, foe.CurrentHealth,
                "150% of the consumed worth and nothing else -- the snapshot was taken when the " +
                "Poison was applied, and no caster scaling term runs at detonation");
        }

        [Test]
        public void ADodgedReckoning_ConsumesNothing()
        {
            var (session, _, foe) = Fight(null, new ModifierEffect(ModifierEffectType.DodgeRating, 100_000));
            foe.Statuses.Add(new ActiveStatus(StatusEffectType.Poison, 10, 2));
            int before = foe.CurrentHealth;

            session.CastSkill(0, foe);

            Assert.AreEqual(before, foe.CurrentHealth, "a dodged Reckoning deals nothing");
            var poison = foe.Statuses.Single(s => s.Type == StatusEffectType.Poison);
            Assert.AreEqual(10, poison.Magnitude, "a dodge must leave the Poison untouched, not merely undealt");
            Assert.AreEqual(2, poison.TurnsRemaining);
        }
    }
}
